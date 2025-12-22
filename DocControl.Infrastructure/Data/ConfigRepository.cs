using Npgsql;

namespace DocControl.Infrastructure.Data;

public sealed class ConfigRepository
{
    private readonly DbConnectionFactory factory;

    public ConfigRepository(DbConnectionFactory factory)
    {
        this.factory = factory;
    }

    public async Task SetAsync(string scopeType, long? projectId, string key, string value, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
            INSERT INTO Config (ScopeType, ProjectId, Key, Value)
            VALUES (@scope, @projectId, @key, @value)
            ON CONFLICT(ScopeType, ProjectId, Key) DO UPDATE SET Value = EXCLUDED.Value;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@scope", scopeType);
        cmd.Parameters.AddWithValue("@projectId", (object?)projectId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> GetAsync(string scopeType, long? projectId, string key, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = "SELECT Value FROM Config WHERE ScopeType = @scope AND (ProjectId IS NOT DISTINCT FROM @projectId) AND Key = @key;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@scope", scopeType);
        cmd.Parameters.AddWithValue("@projectId", (object?)projectId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@key", key);

        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result?.ToString();
    }
}
