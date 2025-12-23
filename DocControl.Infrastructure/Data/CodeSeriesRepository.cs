using DocControl.Core.Models;
using Npgsql;

namespace DocControl.Infrastructure.Data;

public sealed class CodeSeriesRepository
{
    private readonly DbConnectionFactory factory;

    public CodeSeriesRepository(DbConnectionFactory factory)
    {
        this.factory = factory;
    }

    public async Task<long> UpsertAsync(CodeSeriesKey key, string? description, int? nextNumber = null, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
            INSERT INTO CodeSeries (ProjectId, Level1, Level2, Level3, Level4, Description, NextNumber)
            VALUES (@ProjectId, @Level1, @Level2, @Level3, @Level4, @Description, COALESCE(@NextNumber, 1))
            ON CONFLICT(ProjectId, Level1, Level2, Level3, Level4)
            DO UPDATE SET
                Description = COALESCE(EXCLUDED.Description, CodeSeries.Description),
                NextNumber = COALESCE(@NextNumber, CodeSeries.NextNumber)
            RETURNING Id;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ProjectId", key.ProjectId);
        cmd.Parameters.AddWithValue("@Level1", key.Level1);
        cmd.Parameters.AddWithValue("@Level2", key.Level2);
        cmd.Parameters.AddWithValue("@Level3", key.Level3);
        cmd.Parameters.AddWithValue("@Level4", (object?)key.Level4 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Description", (object?)description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@NextNumber", (object?)nextNumber ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result ?? throw new InvalidOperationException("Failed to upsert code series."));
    }

    public async Task SeedNextNumbersAsync(IEnumerable<(CodeSeriesKey key, string description, int maxNumber)> seriesMax, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        foreach (var (key, description, max) in seriesMax)
        {
            const string upsert = @"
                INSERT INTO CodeSeries (ProjectId, Level1, Level2, Level3, Level4, Description, NextNumber)
                VALUES (@ProjectId, @Level1, @Level2, @Level3, @Level4, @Description, @NextNumber)
                ON CONFLICT(ProjectId, Level1, Level2, Level3, Level4)
                DO UPDATE SET 
                    Description = COALESCE(EXCLUDED.Description, CodeSeries.Description),
                    NextNumber = CASE WHEN EXCLUDED.NextNumber > CodeSeries.NextNumber THEN EXCLUDED.NextNumber ELSE CodeSeries.NextNumber END;
            ";
            await using var cmd = new NpgsqlCommand(upsert, conn, (NpgsqlTransaction)tx);
            cmd.Parameters.AddWithValue("@ProjectId", key.ProjectId);
            cmd.Parameters.AddWithValue("@Level1", key.Level1);
            cmd.Parameters.AddWithValue("@Level2", key.Level2);
            cmd.Parameters.AddWithValue("@Level3", key.Level3);
            cmd.Parameters.AddWithValue("@Level4", (object?)key.Level4 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Description", (object?)description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@NextNumber", max + 1);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CodeSeriesRecord>> ListAsync(long projectId, CancellationToken cancellationToken = default)
    {
        var list = new List<CodeSeriesRecord>();
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"SELECT Id, ProjectId, Level1, Level2, Level3, Level4, Description, NextNumber FROM CodeSeries WHERE ProjectId = @ProjectId ORDER BY Level1, Level2, Level3, Level4;";
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
                Level4 = reader.IsDBNull(5) ? null : reader.GetString(5)
            };
            list.Add(new CodeSeriesRecord
            {
                Id = reader.GetInt64(0),
                Key = key,
                Description = reader.IsDBNull(6) ? null : reader.GetString(6),
                NextNumber = reader.GetInt32(7)
            });
        }
        return list;
    }

    public async Task DeleteAsync(long projectId, long codeSeriesId, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string checkSql = "SELECT COUNT(*) FROM Documents WHERE ProjectId = @ProjectId AND CodeSeriesId = @SeriesId;";
        await using (var checkCmd = new NpgsqlCommand(checkSql, conn))
        {
            checkCmd.Parameters.AddWithValue("@ProjectId", projectId);
            checkCmd.Parameters.AddWithValue("@SeriesId", codeSeriesId);
            var docCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
            if (docCount > 0)
            {
                throw new InvalidOperationException($"Cannot delete code series because {docCount} document(s) reference it.");
            }
        }

        const string deleteSql = "DELETE FROM CodeSeries WHERE ProjectId = @ProjectId AND Id = @SeriesId;";
        await using var deleteCmd = new NpgsqlCommand(deleteSql, conn);
        deleteCmd.Parameters.AddWithValue("@ProjectId", projectId);
        deleteCmd.Parameters.AddWithValue("@SeriesId", codeSeriesId);
        await deleteCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
