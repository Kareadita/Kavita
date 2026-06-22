using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Kavita.Integration.Tests.Fixtures;

/// <summary>
/// Resolves KavitaPlus API URL and license key for integration tests.
///
/// API URL (priority):
///   1. KAVITAPLUS_API_URL env var
///   2. Default: http://localhost:5020
///
/// License key (priority):
///   1. KAVITAPLUS_LICENSE_KEY env var (raw/encrypted key string)
///   2. KAVITA_DB_PATH env var → SELECT Value FROM ServerSetting WHERE Key=23
///
/// If no license key is found, LicenseKey is null and all tests skip.
/// </summary>
/// <remarks>Can run manually via dotnet test Kavita.Integration.Tests --filter "Category=Integration"</remarks>
public sealed class KavitaPlusFixture : IAsyncLifetime
{
    private const int LicenseKeyDbId = 23; // ServerSettingKey.LicenseKey

    public string ApiUrl { get; private set; } = "http://localhost:5020";
    public string? LicenseKey { get; private set; }
    public string? SkipReason { get; private set; }

    // Environment.GetEnvironmentVariable(name) only checks Process scope.
    // On Windows, variables set via System Properties or setx live in User/Machine scope.
    // This helper falls through all three scopes so both work.
    private static string? GetEnv(string name) =>
        Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process)
        ?? Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User)
        ?? Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine);

    public async Task InitializeAsync()
    {
        ApiUrl = GetEnv("KAVITAPLUS_API_URL")?.Trim() ?? "http://localhost:5020";

        var envKey = GetEnv("KAVITAPLUS_LICENSE_KEY");
        if (!string.IsNullOrWhiteSpace(envKey))
        {
            LicenseKey = envKey.Trim();
            return;
        }

        var dbPath = GetEnv("KAVITA_DB_PATH");
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            SkipReason = "Set KAVITAPLUS_LICENSE_KEY (raw key) or KAVITA_DB_PATH (path to kavita.db) to run these tests.";
            return;
        }

        try
        {
            var connStr = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            await using var conn = new SqliteConnection(connStr);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Value FROM ServerSetting WHERE Key = $key LIMIT 1";
            cmd.Parameters.AddWithValue("$key", LicenseKeyDbId);

            var raw = (await cmd.ExecuteScalarAsync())?.ToString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                SkipReason = $"ServerSetting Key=23 (LicenseKey) is empty in DB at '{dbPath}'.";
                return;
            }

            LicenseKey = raw;
        }
        catch (Exception ex)
        {
            SkipReason = $"Could not read license from DB at '{dbPath}': {ex.Message}";
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition("KavitaPlus")]
public sealed class KavitaPlusCollection : ICollectionFixture<KavitaPlusFixture> { }
