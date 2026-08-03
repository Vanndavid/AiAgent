using System.Text.RegularExpressions;
using Npgsql;

namespace JobAssistant.Api.Data;

/// <summary>
/// Applies ordered SQL files from Data/Migrations and records them in schema_migrations.
/// </summary>
public sealed class MigrationRunner
{
    private static readonly Regex MigrationName = new(
        @"^(?<version>\d{3,})_(?<name>[a-z0-9_]+)\.sql$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly string _connectionString;
    private readonly string _migrationsDirectory;

    public MigrationRunner(string connectionString, string? migrationsDirectory = null)
    {
        _connectionString = connectionString;
        _migrationsDirectory = migrationsDirectory ?? ResolveDefaultMigrationsDirectory();
    }

    public string MigrationsDirectory => _migrationsDirectory;

    public static IReadOnlyList<(string Version, string Path)> DiscoverMigrations(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return Array.Empty<(string, string)>();
        }

        var found = new List<(string Version, string Path, string FileName)>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.sql"))
        {
            var fileName = Path.GetFileName(path);
            var match = MigrationName.Match(fileName);
            if (!match.Success)
            {
                continue;
            }

            found.Add((match.Groups["version"].Value, path, fileName));
        }

        return found
            .OrderBy(x => x.Version, StringComparer.Ordinal)
            .ThenBy(x => x.FileName, StringComparer.Ordinal)
            .Select(x => (x.Version, x.Path))
            .ToList();
    }

    public async Task<int> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var migrations = DiscoverMigrations(_migrationsDirectory);
        if (migrations.Count == 0)
        {
            throw new InvalidOperationException(
                $"No migrations found in '{_migrationsDirectory}'. Expected files like 001_name.sql.");
        }

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using (var ensure = new NpgsqlCommand(
                         """
                         CREATE TABLE IF NOT EXISTS schema_migrations (
                             version TEXT PRIMARY KEY,
                             applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
                         );
                         """,
                         conn))
        {
            await ensure.ExecuteNonQueryAsync(cancellationToken);
        }

        var applied = 0;
        foreach (var (version, path) in migrations)
        {
            await using var check = new NpgsqlCommand(
                "SELECT 1 FROM schema_migrations WHERE version = @version;",
                conn);
            check.Parameters.AddWithValue("version", version);
            var exists = await check.ExecuteScalarAsync(cancellationToken);
            if (exists is not null)
            {
                continue;
            }

            var sql = await File.ReadAllTextAsync(path, cancellationToken);
            await using var tx = await conn.BeginTransactionAsync(cancellationToken);
            try
            {
                await using (var migrate = new NpgsqlCommand(sql, conn, tx))
                {
                    await migrate.ExecuteNonQueryAsync(cancellationToken);
                }

                await using (var record = new NpgsqlCommand(
                                 "INSERT INTO schema_migrations (version) VALUES (@version);",
                                 conn,
                                 tx))
                {
                    record.Parameters.AddWithValue("version", version);
                    await record.ExecuteNonQueryAsync(cancellationToken);
                }

                await tx.CommitAsync(cancellationToken);
                applied++;
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        }

        return applied;
    }

    private static string ResolveDefaultMigrationsDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Data", "Migrations"),
            Path.Combine(Directory.GetCurrentDirectory(), "Data", "Migrations"),
            Path.Combine(Directory.GetCurrentDirectory(), "backend", "JobAssistant.Api", "Data", "Migrations"),
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return candidates[0];
    }
}
