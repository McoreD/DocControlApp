using DocControl.Core.Models;
using Npgsql;

namespace DocControl.Infrastructure.Data;

public sealed class CodeCatalogRepository
{
    private readonly DbConnectionFactory factory;

    public CodeCatalogRepository(DbConnectionFactory factory)
    {
        this.factory = factory;
    }

    public async Task<long> UpsertAsync(CodeSeriesKey key, string? description, CancellationToken cancellationToken = default)
    {
        var level4 = DbValue.NormalizeLevel(key.Level4);
        var level5 = DbValue.NormalizeLevel(key.Level5);
        var level6 = DbValue.NormalizeLevel(key.Level6);
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
            INSERT INTO CodeCatalog (ProjectId, Level1, Level2, Level3, Level4, Level5, Level6, Description)
            VALUES (@ProjectId, @Level1, @Level2, @Level3, @Level4, @Level5, @Level6, @Description)
            ON CONFLICT(ProjectId, Level1, Level2, Level3, Level4, Level5, Level6)
            DO UPDATE SET
                Description = CASE WHEN EXCLUDED.Description IS NULL OR EXCLUDED.Description = '' THEN CodeCatalog.Description ELSE EXCLUDED.Description END
            RETURNING Id;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ProjectId", key.ProjectId);
        cmd.Parameters.AddWithValue("@Level1", key.Level1);
        cmd.Parameters.AddWithValue("@Level2", key.Level2);
        cmd.Parameters.AddWithValue("@Level3", key.Level3);
        cmd.Parameters.AddWithValue("@Level4", level4);
        cmd.Parameters.AddWithValue("@Level5", level5);
        cmd.Parameters.AddWithValue("@Level6", level6);
        cmd.Parameters.AddWithValue("@Description", (object?)description ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result ?? throw new InvalidOperationException("Failed to upsert code catalog."));
    }

    public async Task<IReadOnlyList<CodeCatalogRecord>> ListAsync(long projectId, CancellationToken cancellationToken = default)
    {
        var list = new List<CodeCatalogRecord>();
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"SELECT Id, ProjectId, Level1, Level2, Level3, Level4, Level5, Level6, Description FROM CodeCatalog WHERE ProjectId = @ProjectId ORDER BY Level1, Level2, Level3, Level4, Level5, Level6;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ProjectId", projectId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var key = new CodeSeriesKey
            {
                ProjectId = reader.GetInt64(1),
                Level1 = reader.GetString(2),
                Level2 = reader.GetString(3),
                Level3 = reader.GetString(4),
                Level4 = DbValue.NormalizeRead(reader.GetString(5)),
                Level5 = DbValue.NormalizeRead(reader.GetString(6)),
                Level6 = DbValue.NormalizeRead(reader.GetString(7))
            };
            list.Add(new CodeCatalogRecord
            {
                Id = reader.GetInt64(0),
                Key = key,
                Description = reader.IsDBNull(8) ? null : reader.GetString(8)
            });
        }

        return list;
    }

    public async Task DeleteAsync(long projectId, long catalogId, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string deleteSql = "DELETE FROM CodeCatalog WHERE ProjectId = @ProjectId AND Id = @Id;";
        await using var deleteCmd = new NpgsqlCommand(deleteSql, conn);
        deleteCmd.Parameters.AddWithValue("@ProjectId", projectId);
        deleteCmd.Parameters.AddWithValue("@Id", catalogId);
        await deleteCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> PurgeAsync(long projectId, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"DELETE FROM CodeCatalog WHERE ProjectId = @ProjectId;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ProjectId", projectId);
        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected;
    }
}
