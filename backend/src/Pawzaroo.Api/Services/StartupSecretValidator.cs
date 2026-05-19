namespace Pawzaroo.Api.Services;

internal static class StartupSecretValidator
{
    private static readonly string[] WeakSentinels =
    {
        "REPLACE_ME",
        "CHANGE_ME",
        "minioadmin",
        "pawzaroo:pawzaroo",
        "Admin@12345",
    };

    public static void Validate(IConfiguration config, IHostEnvironment env)
    {
        var errors = new List<string>();

        Require(config, "ConnectionStrings:Postgres", errors);
        Require(config, "ConnectionStrings:Redis", errors);
        Require(config, "Jwt:SigningKey", errors);
        Require(config, "Kafka:BootstrapServers", errors);
        Require(config, "Storage:Endpoint", errors);
        Require(config, "Storage:AccessKey", errors);
        Require(config, "Storage:SecretKey", errors);

        var signingKey = config["Jwt:SigningKey"];
        if (!string.IsNullOrEmpty(signingKey) && signingKey.Length < 32)
            errors.Add("Jwt:SigningKey must be at least 32 characters (256 bits).");

        if (!env.IsDevelopment())
        {
            foreach (var key in new[]
            {
                "ConnectionStrings:Postgres",
                "Jwt:SigningKey",
                "Storage:AccessKey",
                "Storage:SecretKey",
            })
            {
                var v = config[key] ?? string.Empty;
                foreach (var sentinel in WeakSentinels)
                {
                    if (v.Contains(sentinel, StringComparison.OrdinalIgnoreCase))
                        errors.Add($"{key} contains a known weak/placeholder value '{sentinel}'. " +
                                   "Replace via environment variables or a secrets manager before deploying.");
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Startup configuration validation failed:" + Environment.NewLine +
                string.Join(Environment.NewLine, errors.Select(e => " - " + e)));
        }
    }

    private static void Require(IConfiguration config, string key, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(config[key]))
            errors.Add($"Configuration value '{key}' is required. Set it via environment variable " +
                       $"(e.g. {key.Replace(':', '_')}) or a secrets manager.");
    }
}
