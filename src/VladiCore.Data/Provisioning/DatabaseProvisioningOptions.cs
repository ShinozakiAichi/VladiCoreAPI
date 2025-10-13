namespace VladiCore.Data.Provisioning;

public sealed class DatabaseProvisioningOptions
{
    public bool Enabled { get; init; } = true;

    public bool ApplySeeds { get; init; }
        = false;

    public bool CreateDbIfMissing { get; init; }
        = true;

    public string DatabaseName { get; init; } = "vladicore";

    public string MigrationsPath { get; init; } = "db/migrations/mysql";

    public string SeedsPath { get; init; } = "db/seed";

    public int TimeoutSeconds { get; init; } = 120;
}
