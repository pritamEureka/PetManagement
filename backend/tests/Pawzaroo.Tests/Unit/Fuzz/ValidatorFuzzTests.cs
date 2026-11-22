using System.Collections;
using System.Reflection;
using FluentAssertions;
using FluentValidation;
using Pawzaroo.Application.Modules.Pets.Validators;
using Xunit;

namespace Pawzaroo.Tests.Unit.Fuzz;

/// <summary>
/// Deterministic, seeded property tests (no FsCheck dependency). For every parameterless FluentValidation
/// validator in the Application assembly we build random-but-structurally-valid instances of the validated
/// DTO (including nasty strings, extreme numbers and nulls) and assert the validators:
///   * never throw,
///   * are deterministic (same input twice => same verdict and same error messages),
///   * report IsValid consistently with the error list.
/// </summary>
public class ValidatorFuzzTests
{
    private const int Iterations = 1000;
    private const int Seed = 20240611;

    private static readonly string[] NastyStrings =
    {
        "", " ", "\t\r\n", "a", new string('x', 10_000), "<script>alert(1)</script>", "'; DROP TABLE users;--",
        "\u0000", "🐶 dog", "é", "‮evil", "../../etc/passwd", "%s%n%x", "null", "0", "-1",
        "user@example.com", "not-an-email", "+8801712345678", "99999999999999999999", "  padded  ", "ÀÉÎÕÜ"
    };

    private static string RandomString(Random r)
    {
        if (r.Next(3) == 0) return NastyStrings[r.Next(NastyStrings.Length)];
        var len = r.Next(0, 200);
        var chars = new char[len];
        for (var i = 0; i < len; i++) chars[i] = (char)r.Next(0x20, r.Next(2) == 0 ? 0x7F : 0xFFFF);
        return new string(chars);
    }

    private static object? RandomValue(Type type, Random r, int depth)
    {
        var nullable = Nullable.GetUnderlyingType(type);
        if (nullable is not null) return r.Next(4) == 0 ? null : RandomValue(nullable, r, depth);

        if (type == typeof(string)) return r.Next(10) == 0 ? null : RandomString(r);
        if (type == typeof(Guid)) return r.Next(5) == 0 ? Guid.Empty : new Guid(Enumerable.Range(0, 16).Select(_ => (byte)r.Next(256)).ToArray());
        if (type == typeof(bool)) return r.Next(2) == 0;
        if (type == typeof(int)) return Pick(r, 0, 1, -1, int.MaxValue, int.MinValue, r.Next(-1000, 1000));
        if (type == typeof(long)) return Pick(r, 0L, -1L, long.MaxValue, long.MinValue, (long)r.Next());
        if (type == typeof(short)) return (short)r.Next(short.MinValue, short.MaxValue);
        if (type == typeof(byte)) return (byte)r.Next(256);
        if (type == typeof(double)) return Pick(r, 0d, -1d, double.MaxValue, double.NaN, double.PositiveInfinity, r.NextDouble() * 400 - 200);
        if (type == typeof(float)) return Pick(r, 0f, -1f, float.NaN, (float)(r.NextDouble() * 400 - 200));
        if (type == typeof(decimal)) return Pick(r, 0m, -1m, decimal.MaxValue, decimal.MinValue, (decimal)(r.NextDouble() * 10_000 - 100));
        if (type == typeof(DateTime))
            return Pick(r, DateTime.MinValue, DateTime.MaxValue, DateTime.UtcNow, DateTime.UtcNow.AddDays(r.Next(-5000, 5000)));
        if (type == typeof(DateTimeOffset)) return DateTimeOffset.UtcNow.AddDays(r.Next(-5000, 5000));
        if (type == typeof(DateOnly)) return DateOnly.FromDateTime(DateTime.UtcNow.AddDays(r.Next(-5000, 5000)));
        if (type == typeof(TimeOnly)) return TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(r.Next(0, 1440)));
        if (type == typeof(TimeSpan)) return TimeSpan.FromMinutes(r.Next(-100, 5000));
        if (type.IsEnum)
        {
            var values = Enum.GetValues(type);
            // Mostly defined values, occasionally an undefined numeric value.
            return r.Next(8) == 0 ? Enum.ToObject(type, r.Next(-5, 500)) : values.GetValue(r.Next(values.Length));
        }
        if (type.IsArray)
        {
            var el = type.GetElementType()!;
            var arr = Array.CreateInstance(el, r.Next(0, 4));
            for (var i = 0; i < arr.Length; i++) arr.SetValue(RandomValue(el, r, depth + 1), i);
            return arr;
        }
        if (type.IsGenericType && type.GetGenericArguments().Length == 1
            && (typeof(IEnumerable).IsAssignableFrom(type) && !type.IsClass || type.GetGenericTypeDefinition() == typeof(List<>)))
        {
            var el = type.GetGenericArguments()[0];
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(el))!;
            for (var i = 0; i < r.Next(0, 4); i++) list.Add(RandomValue(el, r, depth + 1));
            return type.IsAssignableFrom(list.GetType()) ? list : null;
        }
        if (type.IsClass && depth < 3)
        {
            var ctor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
            if (ctor is null) return null;
            try
            {
                return ctor.Invoke(ctor.GetParameters().Select(p => RandomValue(p.ParameterType, r, depth + 1)).ToArray());
            }
            catch (TargetInvocationException)
            {
                return null;   // DTO constructor guards; skip this value
            }
        }
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    private static T Pick<T>(Random r, params T[] options) => options[r.Next(options.Length)];

    private static IEnumerable<(Type Validator, Type Target)> DiscoverValidators() =>
        typeof(Pawzaroo.Application.Modules.Pets.Validators.UpsertPetInputValidator).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.GetConstructor(Type.EmptyTypes) is not null)
            .Select(t => (Validator: t, Base: BaseValidatorType(t)))
            .Where(x => x.Base is not null)
            .Select(x => (x.Validator, Target: x.Base!.GetGenericArguments()[0]));

    private static Type? BaseValidatorType(Type t)
    {
        for (var b = t.BaseType; b is not null; b = b.BaseType)
            if (b.IsGenericType && b.GetGenericTypeDefinition() == typeof(AbstractValidator<>)) return b;
        return null;
    }

    [Fact]
    public void validators_are_discoverable()
    {
        DiscoverValidators().Count().Should().BeGreaterThan(10);
    }

    [Fact]
    public void validators_never_throw_and_are_deterministic_for_random_inputs()
    {
        var rnd = new Random(Seed);
        var validators = DiscoverValidators().ToList();
        var failures = new List<string>();
        var exercised = 0;

        foreach (var (validatorType, target) in validators)
        {
            IValidator validator;
            try { validator = (IValidator)Activator.CreateInstance(validatorType)!; }
            catch { continue; }   // validators needing DI are out of scope

            for (var i = 0; i < Iterations; i++)
            {
                object? instance;
                try { instance = RandomValue(target, rnd, 0); }
                catch { continue; }
                if (instance is null) continue;

                try
                {
                    var ctx = new ValidationContext<object>(instance);
                    var first = validator.Validate(ctx);
                    var second = validator.Validate(new ValidationContext<object>(instance));
                    exercised++;

                    if (first.IsValid != second.IsValid
                        || !first.Errors.Select(e => $"{e.PropertyName}:{e.ErrorMessage}").SequenceEqual(second.Errors.Select(e => $"{e.PropertyName}:{e.ErrorMessage}")))
                        failures.Add($"{validatorType.Name}: non-deterministic result (iteration {i})");
                    if (first.IsValid != (first.Errors.Count == 0))
                        failures.Add($"{validatorType.Name}: IsValid disagrees with Errors (iteration {i})");
                }
                catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
                {
                    failures.Add($"{validatorType.Name} threw {ex.GetType().Name}: {ex.Message} (iteration {i}, seed {Seed})");
                    break;   // one report per validator is enough
                }
            }
        }

        exercised.Should().BeGreaterThan(1000, "the fuzz loop must actually run validators");
        failures.Should().BeEmpty(string.Join("\n", failures.Distinct().Take(20)));
    }

    [Fact]
    public void fuzz_generation_is_reproducible_for_a_fixed_seed()
    {
        var a = new Random(Seed);
        var b = new Random(Seed);
        for (var i = 0; i < 100; i++) RandomString(a).Should().Be(RandomString(b));
    }
}
