using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Pawzaroo.Application.Common.Messaging;
using Xunit;

namespace Pawzaroo.Tests.Unit.Contracts;

/// <summary>
/// Kafka event contract snapshot. Every record in Application/Common/Messaging and in each module's
/// Events namespace is flattened to { FullName -> { Property -> type } } and compared with the checked-in
/// contracts/integration-events.json. Removing/renaming/retyping a property (a breaking change for
/// consumers) fails the test; so does adding an event without reviewing the snapshot.
///
/// To accept an intentional change regenerate the snapshot:
///     UPDATE_CONTRACTS=1 dotnet test --filter EventContractTests      (writes the file under the source tree)
/// or  node scripts/gen-event-contracts.mjs                            (parses the sources, no build needed)
/// and commit the diff so it is reviewed.
/// </summary>
public class EventContractTests
{
    private static readonly Assembly AppAssembly = typeof(IIntegrationEvent).Assembly;

    private static bool IsEventNamespace(string? ns) =>
        ns == "Pawzaroo.Application.Common.Messaging"
        || (ns is not null && ns.StartsWith("Pawzaroo.Application.Modules.", StringComparison.Ordinal)
                           && ns.EndsWith(".Events", StringComparison.Ordinal));

    private static bool IsRecord(Type t) =>
        t.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) is not null;

    private static string Friendly(Type t, NullabilityInfo? info)
    {
        var underlying = Nullable.GetUnderlyingType(t);
        if (underlying is not null) return Friendly(underlying, null) + "?";

        string name;
        if (t == typeof(string)) name = "string";
        else if (t == typeof(int)) name = "int";
        else if (t == typeof(long)) name = "long";
        else if (t == typeof(bool)) name = "bool";
        else if (t == typeof(decimal)) name = "decimal";
        else if (t == typeof(double)) name = "double";
        else if (t == typeof(float)) name = "float";
        else if (t == typeof(object)) name = "object";
        else if (t.IsArray) name = Friendly(t.GetElementType()!, null) + "[]";
        else if (t.IsGenericType)
        {
            var baseName = t.Name[..t.Name.IndexOf('`')];
            name = $"{baseName}<{string.Join(", ", t.GetGenericArguments().Select(a => Friendly(a, null)))}>";
        }
        else name = t.Name;

        if (!t.IsValueType && info?.ReadState == NullabilityState.Nullable) name += "?";
        return name;
    }

    private static SortedDictionary<string, SortedDictionary<string, string>> Compute()
    {
        var ctx = new NullabilityInfoContext();
        var result = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var t in AppAssembly.GetTypes()
                     .Where(t => t is { IsClass: true, IsGenericTypeDefinition: false, IsNested: false } && IsEventNamespace(t.Namespace) && IsRecord(t)))
        {
            var props = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                props[p.Name] = Friendly(p.PropertyType, ctx.Create(p));
            result[t.FullName!] = props;
        }
        return result;
    }

    private static string? FindSourceSnapshot()
    {
        // bin/Debug/net9.0 -> walk up until the test project directory (contains the csproj).
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Pawzaroo.Tests.csproj"))) dir = dir.Parent;
        return dir is null ? null : Path.Combine(dir.FullName, "contracts", "integration-events.json");
    }

    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    [Fact]
    public void integration_event_shapes_match_the_checked_in_contract()
    {
        var actual = Compute();
        actual.Count.Should().BeGreaterThan(50, "event discovery must find the Kafka event records");

        if (Environment.GetEnvironmentVariable("UPDATE_CONTRACTS") == "1")
        {
            var target = FindSourceSnapshot();
            target.Should().NotBeNull("the source tree must be reachable to regenerate the snapshot");
            File.WriteAllText(target!, JsonSerializer.Serialize(actual, Pretty) + Environment.NewLine);
            return;
        }

        var path = Path.Combine(AppContext.BaseDirectory, "contracts", "integration-events.json");
        File.Exists(path).Should().BeTrue($"the snapshot must be copied to the output directory ({path})");
        var expected = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(path))!;

        var removed = expected.Keys.Except(actual.Keys).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var added = actual.Keys.Except(expected.Keys).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var changed = new List<string>();
        foreach (var key in expected.Keys.Intersect(actual.Keys).OrderBy(k => k, StringComparer.Ordinal))
        {
            var e = expected[key];
            var a = actual[key];
            foreach (var p in e.Keys.Except(a.Keys)) changed.Add($"{key}: property '{p}' REMOVED (breaking)");
            foreach (var p in a.Keys.Except(e.Keys)) changed.Add($"{key}: property '{p}' added ({a[p]})");
            foreach (var p in e.Keys.Intersect(a.Keys).Where(p => e[p] != a[p]))
                changed.Add($"{key}: property '{p}' type {e[p]} -> {a[p]} (breaking)");
        }

        var diff = new List<string>();
        diff.AddRange(removed.Select(r => $"event REMOVED: {r} (breaking)"));
        diff.AddRange(added.Select(r => $"event added:   {r}"));
        diff.AddRange(changed);

        diff.Should().BeEmpty(
            "event contracts changed. Review for consumer compatibility, then regenerate with " +
            "UPDATE_CONTRACTS=1 dotnet test --filter EventContractTests (or node scripts/gen-event-contracts.mjs):\n" +
            string.Join("\n", diff));
    }

    [Fact]
    public void every_integration_event_implements_the_marker_and_carries_id_and_timestamp()
    {
        var events = AppAssembly.GetTypes()
            .Where(t => t.Namespace == "Pawzaroo.Application.Common.Messaging" && IsRecord(t) && !t.IsGenericTypeDefinition)
            .ToList();

        events.Should().NotBeEmpty();
        foreach (var t in events)
        {
            typeof(IIntegrationEvent).IsAssignableFrom(t).Should().BeTrue($"{t.Name} must implement IIntegrationEvent");
            t.GetProperty("EventId")?.PropertyType.Should().Be(typeof(Guid), $"{t.Name}.EventId");
            t.GetProperty("OccurredAt")?.PropertyType.Should().Be(typeof(DateTime), $"{t.Name}.OccurredAt");
        }
    }

    [Fact]
    public void envelope_serializes_with_the_agreed_wire_property_names()
    {
        var env = new EventEnvelope<UserRegistered>(Guid.NewGuid(), "user.registered", "1", DateTime.UtcNow, "corr", "u1",
            new UserRegistered(Guid.NewGuid(), Guid.NewGuid(), "a@b.c", "A", DateTime.UtcNow));

        var json = JsonSerializer.Serialize(env, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            "eventId", "eventType", "version", "occurredAt", "correlationId", "userId", "data");
    }
}
