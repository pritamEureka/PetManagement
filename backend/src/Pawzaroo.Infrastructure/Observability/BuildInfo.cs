using System.Reflection;

namespace Pawzaroo.Infrastructure.Observability;

/// <summary>
/// Build identity: semantic version (AssemblyInformationalVersion, minus any "+metadata") and the
/// Git SHA injected by CI as the GIT_SHA environment variable ("dev" when unset).
/// </summary>
public static class BuildInfo
{
    public const string DefaultGitSha = "dev";

    public static string Version { get; } = ResolveVersion(
        typeof(BuildInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
        Environment.GetEnvironmentVariable("BUILD_VERSION"));

    public static string GitSha { get; } = ResolveGitSha(Environment.GetEnvironmentVariable("GIT_SHA"));

    /// <summary>BUILD_VERSION env wins; otherwise the informational version without "+sourcerevision".</summary>
    public static string ResolveVersion(string? informationalVersion, string? envVersion)
    {
        if (!string.IsNullOrWhiteSpace(envVersion)) return envVersion.Trim();
        if (string.IsNullOrWhiteSpace(informationalVersion)) return "0.0.0";
        var plus = informationalVersion.IndexOf('+');
        return (plus >= 0 ? informationalVersion[..plus] : informationalVersion).Trim();
    }

    public static string ResolveGitSha(string? env)
        => string.IsNullOrWhiteSpace(env) ? DefaultGitSha : env.Trim();

    /// <summary>Short (7 char) form for log lines.</summary>
    public static string ShortSha => GitSha.Length > 7 ? GitSha[..7] : GitSha;

    /// <summary>
    /// Serilog enrichment: <c>cfg.Enrich.WithProperty("version", BuildInfo.Version).Enrich.WithProperty("gitSha", BuildInfo.GitSha)</c>.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Properties { get; } = new Dictionary<string, string>
    {
        ["version"] = Version,
        ["gitSha"] = GitSha
    };
}
