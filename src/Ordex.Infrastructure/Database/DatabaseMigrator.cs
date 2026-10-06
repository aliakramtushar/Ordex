using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ordex.Infrastructure.Data;

namespace Ordex.Infrastructure.Database;

/// <summary>
/// Creates the database (if missing) and runs every *.sql file in the scripts
/// folder in name order. Each script is tracked in dbo.SchemaVersion by its
/// SHA-256 hash: unchanged scripts are skipped, edited scripts run again
/// (all scripts are written to be re-runnable). Each script runs in a transaction.
/// </summary>
public sealed partial class DatabaseMigrator(
    IOptions<DatabaseOptions> options,
    ILogger<DatabaseMigrator> logger)
{
    private readonly DatabaseOptions _options = options.Value;

    /// <summary>
    /// Startup uses a fail-fast copy of the connection string (15 s, no retries),
    /// so a wrong server/password is reported in seconds instead of minutes.
    /// </summary>
    private string StartupConnectionString(string? database = null)
    {
        var builder = new SqlConnectionStringBuilder(_options.ConnectionString)
        {
            ConnectTimeout = 15,
            ConnectRetryCount = 0
        };
        if (database is not null)
            builder.InitialCatalog = database;
        return builder.ConnectionString;
    }

    public async Task MigrateAsync(string scriptsFolder, CancellationToken ct = default)
    {
        if (!Directory.Exists(scriptsFolder))
        {
            logger.LogWarning("Database scripts folder not found: {Folder}", scriptsFolder);
            return;
        }

        var target = new SqlConnectionStringBuilder(_options.ConnectionString);
        logger.LogInformation("Connecting to SQL Server {Server}, database {Database}…", target.DataSource, target.InitialCatalog);

        await EnsureDatabaseExistsAsync(ct);

        await using var connection = new SqlConnection(StartupConnectionString());
        await connection.OpenAsync(ct);
        logger.LogInformation("Connected. Checking database scripts…");

        await connection.ExecuteAsync("""
            IF OBJECT_ID(N'dbo.SchemaVersion', N'U') IS NULL
                CREATE TABLE dbo.SchemaVersion
                (
                    ScriptName NVARCHAR(200) NOT NULL CONSTRAINT PK_SchemaVersion PRIMARY KEY,
                    ScriptHash CHAR(64)      NOT NULL,
                    AppliedAt  DATETIME2(0)  NOT NULL CONSTRAINT DF_SchemaVersion_AppliedAt DEFAULT (SYSDATETIME())
                );
            """);

        foreach (var file in Directory.GetFiles(scriptsFolder, "*.sql").Order(StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileName(file);
            var script = await File.ReadAllTextAsync(file, ct);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(script)));

            var appliedHash = await connection.ExecuteScalarAsync<string?>(
                "SELECT ScriptHash FROM dbo.SchemaVersion WHERE ScriptName = @Name;", new { Name = name });

            if (string.Equals(appliedHash, hash, StringComparison.OrdinalIgnoreCase))
                continue;

            logger.LogInformation("Applying database script {Script}", name);

            await using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                foreach (var batch in GoSeparator().Split(script).Where(b => !string.IsNullOrWhiteSpace(b)))
                    await connection.ExecuteAsync(new CommandDefinition(batch, transaction: transaction, commandTimeout: 300, cancellationToken: ct));

                await connection.ExecuteAsync("""
                    MERGE dbo.SchemaVersion AS t
                    USING (SELECT @Name AS ScriptName, @Hash AS ScriptHash) AS s ON t.ScriptName = s.ScriptName
                    WHEN MATCHED THEN UPDATE SET ScriptHash = s.ScriptHash, AppliedAt = SYSDATETIME()
                    WHEN NOT MATCHED THEN INSERT (ScriptName, ScriptHash) VALUES (s.ScriptName, s.ScriptHash);
                    """, new { Name = name, Hash = hash }, transaction);

                await transaction.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                logger.LogError(ex, "Database script {Script} failed and was rolled back", name);
                throw;
            }
        }
    }

    private async Task EnsureDatabaseExistsAsync(CancellationToken ct)
    {
        var databaseName = new SqlConnectionStringBuilder(_options.ConnectionString).InitialCatalog;
        if (string.IsNullOrWhiteSpace(databaseName))
            return;

        // If the database already exists we don't need master at all
        // (hosted SQL logins often have no access to master).
        try
        {
            await using var direct = new SqlConnection(StartupConnectionString());
            await direct.OpenAsync(ct);
            return;
        }
        catch (SqlException ex) when (ex.Number == 4060) // "Cannot open database" = it doesn't exist (or no access)
        {
            logger.LogInformation("Database {Database} not found – trying to create it.", databaseName);
        }

        await using var master = new SqlConnection(StartupConnectionString("master"));
        await master.OpenAsync(ct);

        // QUOTENAME protects the dynamic SQL.
        await master.ExecuteAsync("""
            IF DB_ID(@Name) IS NULL
            BEGIN
                DECLARE @Sql NVARCHAR(400) = N'CREATE DATABASE ' + QUOTENAME(@Name);
                EXEC (@Sql);
            END
            """, new { Name = databaseName });
    }

    [GeneratedRegex(@"^\s*GO\s*;?\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoSeparator();
}
