using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Pawzaroo.Api.Authorization;
using Xunit;

namespace Pawzaroo.Tests.Unit.Security;

/// <summary>
/// Reflection audit of every MVC action in Pawzaroo.Api. Makes the authorization surface reviewable:
///  1. each action is authorized ([Authorize]/[Permission], class or action level) or is an approved anonymous endpoint,
///  2. anonymous endpoints must be approved in AuthorizationAllowlist.txt ("anon Controller.Action"),
///  3. controllers not listed as "ownership" must use [Permission] on every authorized action.
/// A failure message tells the reviewer exactly which line to add (after reviewing the endpoint).
/// </summary>
public class ControllerAuthorizationTests
{
    private sealed record ActionInfo(Type Controller, MethodInfo Method, bool Anonymous, bool HasPermission)
    {
        public string Key => $"{Controller.Name}.{Method.Name}";
    }

    private static readonly Assembly ApiAssembly = typeof(PermissionAttribute).Assembly;

    private static (HashSet<string> Anon, HashSet<string> Ownership) LoadAllowlist()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Unit", "Security", "AuthorizationAllowlist.txt");
        File.Exists(path).Should().BeTrue($"the allowlist must be copied to the output directory ({path})");
        var anon = new HashSet<string>(StringComparer.Ordinal);
        var own = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var parts = line.Split(' ', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2) continue;
            if (parts[0] == "anon") anon.Add(parts[1]);
            else if (parts[0] == "ownership") own.Add(parts[1]);
        }
        return (anon, own);
    }

    private static IEnumerable<ActionInfo> DiscoverActions()
    {
        var controllers = ApiAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true } && typeof(ControllerBase).IsAssignableFrom(t));

        foreach (var c in controllers)
        {
            var classAuth = c.GetCustomAttributes(inherit: true).OfType<IAuthorizeData>().ToList();
            var classAnon = c.GetCustomAttributes(inherit: true).OfType<IAllowAnonymous>().Any();

            foreach (var m in c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (m.IsSpecialName || m.GetCustomAttribute<NonActionAttribute>() is not null) continue;
                // An action is a public method carrying an HTTP verb attribute.
                if (!m.GetCustomAttributes(inherit: true).OfType<IActionHttpMethodProvider>().Any()) continue;

                var methodAuth = m.GetCustomAttributes(inherit: true).OfType<IAuthorizeData>().ToList();
                var methodAnon = m.GetCustomAttributes(inherit: true).OfType<IAllowAnonymous>().Any();
                var anonymous = classAnon || methodAnon || (classAuth.Count == 0 && methodAuth.Count == 0);
                var hasPermission = m.GetCustomAttributes(inherit: true).OfType<PermissionAttribute>().Any()
                                    || c.GetCustomAttributes(inherit: true).OfType<PermissionAttribute>().Any();
                yield return new ActionInfo(c, m, anonymous, hasPermission);
            }
        }
    }

    [Fact]
    public void discovery_finds_a_sane_number_of_actions()
    {
        // Guards against the audit silently checking nothing (e.g. an assembly/attribute refactor).
        DiscoverActions().Count().Should().BeGreaterThan(100);
    }

    [Fact]
    public void every_anonymous_action_is_explicitly_approved()
    {
        var (anon, _) = LoadAllowlist();
        var unknown = DiscoverActions().Where(a => a.Anonymous && !anon.Contains(a.Key))
            .Select(a => a.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();

        unknown.Should().BeEmpty(
            "anonymous endpoints must be reviewed and approved. Add these lines to " +
            "tests/Pawzaroo.Tests/Unit/Security/AuthorizationAllowlist.txt if intentional, otherwise add [Authorize]/[Permission]:\n" +
            string.Join("\n", unknown.Select(k => "anon " + k)));
    }

    [Fact]
    public void allowlisted_anonymous_actions_still_exist_and_are_still_anonymous()
    {
        var (anon, _) = LoadAllowlist();
        var actual = DiscoverActions().Where(a => a.Anonymous).Select(a => a.Key).ToHashSet(StringComparer.Ordinal);
        var stale = anon.Where(k => !actual.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();

        stale.Should().BeEmpty("stale allowlist entries hide future regressions; remove:\n" +
                               string.Join("\n", stale.Select(k => "anon " + k)));
    }

    [Fact]
    public void controllers_outside_the_ownership_list_guard_every_authorized_action_with_permission()
    {
        var (_, ownership) = LoadAllowlist();
        var offenders = DiscoverActions()
            .Where(a => !a.Anonymous && !a.HasPermission && !ownership.Contains(a.Controller.Name))
            .Select(a => a.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();

        offenders.Should().BeEmpty(
            "new controllers must use [Permission(...)] on each action, or be listed as 'ownership <Controller>' " +
            "in AuthorizationAllowlist.txt after confirming object-level (ownership) checks exist in the service:\n" +
            string.Join("\n", offenders));
    }
}
