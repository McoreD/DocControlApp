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
        var level4 = DbValue.NormalizeLevel(key.Level4);
        var level5 = DbValue.NormalizeLevel(key.Level5);
        var level6 = DbValue.NormalizeLevel(key.Level6);
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
            INSERT INTO CodeSeries (ProjectId, Level1, Level2, Level3, Level4, Level5, Level6, Description, NextNumber)
            VALUES (@ProjectId, @Level1, @Level2, @Level3, @Level4, @Level5, @Level6, @Description, COALESCE(@NextNumber, 1))
            ON CONFLICT(ProjectId, Level1, Level2, Level3, Level4, Level5, Level6)
            DO UPDATE SET
                Description = CASE WHEN EXCLUDED.Description IS NULL OR EXCLUDED.Description = '' THEN CodeSeries.Description ELSE EXCLUDED.Description END,
                NextNumber = CASE
                    WHEN @NextNumber IS NULL THEN CodeSeries.NextNumber
                    WHEN CodeSeries.NextNumber >= @NextNumber THEN CodeSeries.NextNumber
                    ELSE @NextNumber
                END
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
            var level4 = DbValue.NormalizeLevel(key.Level4);
            var level5 = DbValue.NormalizeLevel(key.Level5);
            var level6 = DbValue.NormalizeLevel(key.Level6);
            const string upsert = @"
                INSERT INTO CodeSeries (ProjectId, Level1, Level2, Level3, Level4, Level5, Level6, Description, NextNumber)
                VALUES (@ProjectId, @Level1, @Level2, @Level3, @Level4, @Level5, @Level6, @Description, @NextNumber)
                ON CONFLICT(ProjectId, Level1, Level2, Level3, Level4, Level5, Level6)
                DO UPDATE SET 
                    Description = CASE WHEN EXCLUDED.Description IS NULL OR EXCLUDED.Description = '' THEN CodeSeries.Description ELSE EXCLUDED.Description END,
                    NextNumber = CASE WHEN EXCLUDED.NextNumber > CodeSeries.NextNumber THEN EXCLUDED.NextNumber ELSE CodeSeries.NextNumber END;
            ";
            await using var cmd = new NpgsqlCommand(upsert, conn, (NpgsqlTransaction)tx);
            cmd.Parameters.AddWithValue("@ProjectId", key.ProjectId);
            cmd.Parameters.AddWithValue("@Level1", key.Level1);
            cmd.Parameters.AddWithValue("@Level2", key.Level2);
            cmd.Parameters.AddWithValue("@Level3", key.Level3);
            cmd.Parameters.AddWithValue("@Level4", level4);
            cmd.Parameters.AddWithValue("@Level5", level5);
            cmd.Parameters.AddWithValue("@Level6", level6);
            cmd.Parameters.AddWithValue("@Description", (object?)description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@NextNumber", max + 1);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CodeSeriesRecord>> ListAsync(long projectId, CancellationToken cancellationToken = default)
    {
        var list = new List<CodeSeriesRecord>();
        var dedup = new Dictionary<string, CodeSeriesRecord>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"SELECT Id, ProjectId, Level1, Level2, Level3, Level4, Level5, Level6, Description, NextNumber FROM CodeSeries WHERE ProjectId = @ProjectId ORDER BY Level1, Level2, Level3, Level4, Level5, Level6;";
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
                Level4 = reader.IsDBNull(5) ? null : DbValue.NormalizeRead(reader.GetString(5)),
                Level5 = reader.IsDBNull(6) ? null : DbValue.NormalizeRead(reader.GetString(6)),
                Level6 = reader.IsDBNull(7) ? null : DbValue.NormalizeRead(reader.GetString(7))
            };
            list.Add(new CodeSeriesRecord
            {
                Id = reader.GetInt64(0),
                Key = key,
                Description = reader.IsDBNull(8) ? null : reader.GetString(8),
                NextNumber = reader.GetInt32(9)
            });
        }

        foreach (var item in list)
        {
            var dedupKey = $"{item.Key.Level1}|{item.Key.Level2}|{item.Key.Level3}|{item.Key.Level4 ?? string.Empty}|{item.Key.Level5 ?? string.Empty}|{item.Key.Level6 ?? string.Empty}";
            if (!dedup.TryGetValue(dedupKey, out var existing))
            {
                dedup[dedupKey] = item;
                order.Add(dedupKey);
                continue;
            }

            // Merge duplicates: keep earliest, but preserve max next number and any description.
            var mergedDescription = !string.IsNullOrWhiteSpace(existing.Description)
                ? existing.Description
                : item.Description;
            if (string.IsNullOrWhiteSpace(mergedDescription))
            {
                mergedDescription = !string.IsNullOrWhiteSpace(item.Description) ? item.Description : existing.Description;
            }

            var merged = new CodeSeriesRecord
            {
                Id = existing.Id,
                Key = existing.Key,
                Description = mergedDescription,
                NextNumber = Math.Max(existing.NextNumber, item.NextNumber)
            };
            dedup[dedupKey] = merged;
        }

        return order.Select(k => dedup[k]).ToList();
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

    public async Task<int> PurgeAsync(long projectId, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"DELETE FROM CodeSeries WHERE ProjectId = @ProjectId;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ProjectId", projectId);
        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected;
    }

    public async Task<int> CountDistinctAsync(long projectId, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
            SELECT COUNT(DISTINCT (Level1, Level2, Level3, Level4, Level5, Level6))
            FROM CodeSeries
            WHERE ProjectId = @ProjectId;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ProjectId", projectId);
        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(result ?? 0);
    }
}
