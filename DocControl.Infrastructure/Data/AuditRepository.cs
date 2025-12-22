using DocControl.Core.Models;
using Npgsql;

namespace DocControl.Infrastructure.Data;

public sealed class AuditRepository
{
    private readonly DbConnectionFactory factory;

    public AuditRepository(DbConnectionFactory factory)
    {
        this.factory = factory;
    }

    public async Task InsertAsync(long projectId, string action, string? payload, long createdByUserId, DateTime createdAtUtc, long? documentId = null, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
            INSERT INTO Audit (ProjectId, Action, Payload, CreatedByUserId, CreatedAtUtc, DocumentId)
            VALUES (@ProjectId, @Action, @Payload, @By, @At, @DocId);";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ProjectId", projectId);
        cmd.Parameters.AddWithValue("@Action", action);
        cmd.Parameters.AddWithValue("@Payload", (object?)payload ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@By", createdByUserId);
        cmd.Parameters.AddWithValue("@At", createdAtUtc);
        cmd.Parameters.AddWithValue("@DocId", (object?)documentId ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<AuditEntry>> GetRecentAsync(long projectId, int take = 50, CancellationToken cancellationToken = default) =>
        GetPagedAsync(projectId, take, 0, null, null, cancellationToken);

    public Task<IReadOnlyList<AuditEntry>> GetRecentAsync(long projectId, int take, string? containsAction, string? containsUser, CancellationToken cancellationToken = default) =>
        GetPagedAsync(projectId, take, 0, containsAction, containsUser, cancellationToken);

    public async Task<IReadOnlyList<AuditEntry>> GetPagedAsync(long projectId, int take = 50, int skip = 0, string? containsAction = null, string? containsUser = null, CancellationToken cancellationToken = default)
    {
        var list = new List<AuditEntry>();
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        var where = new List<string> { "ProjectId = @ProjectId" };
        await using var cmd = new NpgsqlCommand();
        cmd.Connection = conn;
        cmd.Parameters.AddWithValue("@ProjectId", projectId);

        if (!string.IsNullOrWhiteSpace(containsAction))
        {
            where.Add("Action ILIKE @action");
            cmd.Parameters.AddWithValue("@action", $"%{containsAction}%");
        }
        if (!string.IsNullOrWhiteSpace(containsUser))
        {
            where.Add("CreatedByUserId::text ILIKE @user");
            cmd.Parameters.AddWithValue("@user", $"%{containsUser}%");
        }

        cmd.CommandText = $@"SELECT Id, ProjectId, Action, Payload, CreatedByUserId, CreatedAtUtc, DocumentId FROM Audit
                             WHERE {string.Join(" AND ", where)}
                             ORDER BY Id DESC LIMIT @take OFFSET @skip;";
        cmd.Parameters.AddWithValue("@take", take);
        cmd.Parameters.AddWithValue("@skip", skip);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new AuditEntry
            {
                Id = reader.GetInt64(0),
                ProjectId = reader.GetInt64(1),
                Action = reader.GetString(2),
                Payload = reader.IsDBNull(3) ? null : reader.GetString(3),
                CreatedByUserId = reader.GetInt64(4),
                CreatedAtUtc = reader.GetDateTime(5),
                DocumentId = reader.IsDBNull(6) ? null : reader.GetInt64(6)
            });
        }
        return list;
    }

    public async Task<int> GetCountAsync(long projectId, string? containsAction = null, string? containsUser = null, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        var where = new List<string> { "ProjectId = @ProjectId" };
        await using var cmd = new NpgsqlCommand();
        cmd.Connection = conn;
        cmd.Parameters.AddWithValue("@ProjectId", projectId);

        if (!string.IsNullOrWhiteSpace(containsAction))
        {
            where.Add("Action ILIKE @action");
            cmd.Parameters.AddWithValue("@action", $"%{containsAction}%");
        }
        if (!string.IsNullOrWhiteSpace(containsUser))
        {
            where.Add("CreatedByUserId::text ILIKE @user");
            cmd.Parameters.AddWithValue("@user", $"%{containsUser}%");
        }

        cmd.CommandText = $"SELECT COUNT(1) FROM Audit WHERE {string.Join(" AND ", where)};";
        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(result);
    }
}
