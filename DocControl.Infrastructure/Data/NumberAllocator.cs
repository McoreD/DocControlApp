using DocControl.Core.Models;
using Npgsql;
using System.Data;

namespace DocControl.Infrastructure.Data;

public sealed class NumberAllocator
{
    private readonly DbConnectionFactory factory;

    public NumberAllocator(DbConnectionFactory factory)
    {
        this.factory = factory;
    }

    public async Task<AllocatedNumber> AllocateAsync(CodeSeriesKey key, CancellationToken cancellationToken = default)
    {
        var level4 = DbValue.NormalizeLevel(key.Level4);
        var level5 = DbValue.NormalizeLevel(key.Level5);
        var level6 = DbValue.NormalizeLevel(key.Level6);
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var tx = await conn.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);

        // Ensure series exists for this project/key
        const string ensureSql = @"
            INSERT INTO CodeSeries (ProjectId, Level1, Level2, Level3, Level4, Level5, Level6, NextNumber)
            VALUES (@ProjectId, @Level1, @Level2, @Level3, @Level4, @Level5, @Level6, 1)
            ON CONFLICT(ProjectId, Level1, Level2, Level3, Level4, Level5, Level6) DO NOTHING;";
        await using (var ensureCmd = new NpgsqlCommand(ensureSql, conn, (NpgsqlTransaction)tx))
        {
            ensureCmd.Parameters.AddWithValue("@ProjectId", key.ProjectId);
            ensureCmd.Parameters.AddWithValue("@Level1", key.Level1);
            ensureCmd.Parameters.AddWithValue("@Level2", key.Level2);
            ensureCmd.Parameters.AddWithValue("@Level3", key.Level3);
            ensureCmd.Parameters.AddWithValue("@Level4", level4);
            ensureCmd.Parameters.AddWithValue("@Level5", level5);
            ensureCmd.Parameters.AddWithValue("@Level6", level6);
            await ensureCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // Check max number already persisted
        const string maxSql = @"
            SELECT MAX(Number) FROM Documents 
            WHERE ProjectId = @ProjectId AND Level1 = @Level1 AND Level2 = @Level2 AND Level3 = @Level3 AND (Level4 IS NOT DISTINCT FROM @Level4) AND (Level5 IS NOT DISTINCT FROM @Level5) AND (Level6 IS NOT DISTINCT FROM @Level6);";
        var maxDocNumber = 0;
        await using (var maxCmd = new NpgsqlCommand(maxSql, conn, (NpgsqlTransaction)tx))
        {
            maxCmd.Parameters.AddWithValue("@ProjectId", key.ProjectId);
            maxCmd.Parameters.AddWithValue("@Level1", key.Level1);
            maxCmd.Parameters.AddWithValue("@Level2", key.Level2);
            maxCmd.Parameters.AddWithValue("@Level3", key.Level3);
            maxCmd.Parameters.AddWithValue("@Level4", level4);
            maxCmd.Parameters.AddWithValue("@Level5", level5);
            maxCmd.Parameters.AddWithValue("@Level6", level6);
            var maxDocResult = await maxCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (maxDocResult is not DBNull and not null)
            {
                maxDocNumber = Convert.ToInt32(maxDocResult);
            }
        }

        // Lock the series row and read NextNumber
        const string selectSql = @"
            SELECT Id, NextNumber FROM CodeSeries
            WHERE ProjectId = @ProjectId AND Level1 = @Level1 AND Level2 = @Level2 AND Level3 = @Level3 AND (Level4 IS NOT DISTINCT FROM @Level4) AND (Level5 IS NOT DISTINCT FROM @Level5) AND (Level6 IS NOT DISTINCT FROM @Level6)
            FOR UPDATE;";

        long seriesId;
        int nextNumber;
        await using (var selectCmd = new NpgsqlCommand(selectSql, conn, (NpgsqlTransaction)tx))
        {
            selectCmd.Parameters.AddWithValue("@ProjectId", key.ProjectId);
            selectCmd.Parameters.AddWithValue("@Level1", key.Level1);
            selectCmd.Parameters.AddWithValue("@Level2", key.Level2);
            selectCmd.Parameters.AddWithValue("@Level3", key.Level3);
            selectCmd.Parameters.AddWithValue("@Level4", level4);
            selectCmd.Parameters.AddWithValue("@Level5", level5);
            selectCmd.Parameters.AddWithValue("@Level6", level6);

            await using var reader = await selectCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("Failed to load code series after insert.");
            }
            seriesId = reader.GetInt64(0);
            nextNumber = reader.GetInt32(1);
        }

        var actualNextNumber = Math.Max(nextNumber, maxDocNumber + 1);

        const string updateSql = @"UPDATE CodeSeries SET NextNumber = @newNext WHERE Id = @id;";
        await using (var updateCmd = new NpgsqlCommand(updateSql, conn, (NpgsqlTransaction)tx))
        {
            updateCmd.Parameters.AddWithValue("@id", seriesId);
            updateCmd.Parameters.AddWithValue("@newNext", actualNextNumber + 1);
            await updateCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

        return new AllocatedNumber(seriesId, actualNextNumber);
    }

    public async Task<int> PeekNextAsync(CodeSeriesKey key, CancellationToken cancellationToken = default)
    {
        var level4 = DbValue.NormalizeLevel(key.Level4);
        var level5 = DbValue.NormalizeLevel(key.Level5);
        var level6 = DbValue.NormalizeLevel(key.Level6);
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await conn.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);

        // Ensure series exists
        const string ensureSql = @"
            INSERT INTO CodeSeries (ProjectId, Level1, Level2, Level3, Level4, Level5, Level6, NextNumber)
            VALUES (@ProjectId, @Level1, @Level2, @Level3, @Level4, @Level5, @Level6, 1)
            ON CONFLICT(ProjectId, Level1, Level2, Level3, Level4, Level5, Level6) DO NOTHING;";
        await using (var ensureCmd = new NpgsqlCommand(ensureSql, conn, (NpgsqlTransaction)tx))
        {
            ensureCmd.Parameters.AddWithValue("@ProjectId", key.ProjectId);
            ensureCmd.Parameters.AddWithValue("@Level1", key.Level1);
            ensureCmd.Parameters.AddWithValue("@Level2", key.Level2);
            ensureCmd.Parameters.AddWithValue("@Level3", key.Level3);
            ensureCmd.Parameters.AddWithValue("@Level4", level4);
            ensureCmd.Parameters.AddWithValue("@Level5", level5);
            ensureCmd.Parameters.AddWithValue("@Level6", level6);
            await ensureCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // Check max number already persisted
        const string maxSql = @"
            SELECT MAX(Number) FROM Documents 
            WHERE ProjectId = @ProjectId AND Level1 = @Level1 AND Level2 = @Level2 AND Level3 = @Level3 AND (Level4 IS NOT DISTINCT FROM @Level4) AND (Level5 IS NOT DISTINCT FROM @Level5) AND (Level6 IS NOT DISTINCT FROM @Level6);";
        var maxDocNumber = 0;
        await using (var maxCmd = new NpgsqlCommand(maxSql, conn, (NpgsqlTransaction)tx))
        {
            maxCmd.Parameters.AddWithValue("@ProjectId", key.ProjectId);
            maxCmd.Parameters.AddWithValue("@Level1", key.Level1);
            maxCmd.Parameters.AddWithValue("@Level2", key.Level2);
            maxCmd.Parameters.AddWithValue("@Level3", key.Level3);
            maxCmd.Parameters.AddWithValue("@Level4", level4);
            maxCmd.Parameters.AddWithValue("@Level5", level5);
            maxCmd.Parameters.AddWithValue("@Level6", level6);
            var maxDocResult = await maxCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (maxDocResult is not DBNull and not null)
            {
                maxDocNumber = Convert.ToInt32(maxDocResult);
            }
        }

        // Lock the series row and read NextNumber
        const string selectSql = @"
            SELECT Id, NextNumber FROM CodeSeries
            WHERE ProjectId = @ProjectId AND Level1 = @Level1 AND Level2 = @Level2 AND Level3 = @Level3 AND (Level4 IS NOT DISTINCT FROM @Level4) AND (Level5 IS NOT DISTINCT FROM @Level5) AND (Level6 IS NOT DISTINCT FROM @Level6)
            FOR UPDATE;";

        int nextNumber;
        await using (var selectCmd = new NpgsqlCommand(selectSql, conn, (NpgsqlTransaction)tx))
        {
            selectCmd.Parameters.AddWithValue("@ProjectId", key.ProjectId);
            selectCmd.Parameters.AddWithValue("@Level1", key.Level1);
            selectCmd.Parameters.AddWithValue("@Level2", key.Level2);
            selectCmd.Parameters.AddWithValue("@Level3", key.Level3);
            selectCmd.Parameters.AddWithValue("@Level4", level4);
            selectCmd.Parameters.AddWithValue("@Level5", level5);
            selectCmd.Parameters.AddWithValue("@Level6", level6);

            await using var reader = await selectCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("Failed to load code series after insert.");
            }
            nextNumber = reader.GetInt32(1);
        }

        var actualNextNumber = Math.Max(nextNumber, maxDocNumber + 1);

        // Do not update NextNumber; this is a peek.
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return actualNextNumber;
    }
}
