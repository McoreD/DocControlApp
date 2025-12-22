using System.Security.Cryptography;
using System.Text;
using DocControl.Core.Models;
using Npgsql;

namespace DocControl.Infrastructure.Data;

public sealed class ProjectMemberRepository
{
    private readonly DbConnectionFactory factory;

    public ProjectMemberRepository(DbConnectionFactory factory)
    {
        this.factory = factory;
    }

    public async Task<string?> GetRoleAsync(long projectId, long userId, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        const string sql = "SELECT Role FROM ProjectMembers WHERE ProjectId = @projectId AND UserId = @userId;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@projectId", projectId);
        cmd.Parameters.AddWithValue("@userId", userId);
        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result?.ToString();
    }

    public async Task<IReadOnlyList<ProjectMemberRecord>> ListAsync(long projectId, CancellationToken cancellationToken = default)
    {
        var list = new List<ProjectMemberRecord>();
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        const string sql = @"
            SELECT ProjectId, UserId, Role, AddedByUserId, AddedAtUtc
            FROM ProjectMembers
            WHERE ProjectId = @projectId
            ORDER BY AddedAtUtc DESC;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@projectId", projectId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new ProjectMemberRecord
            {
                ProjectId = reader.GetInt64(0),
                UserId = reader.GetInt64(1),
                Role = reader.GetString(2),
                AddedByUserId = reader.GetInt64(3),
                AddedAtUtc = reader.GetDateTime(4)
            });
        }
        return list;
    }

    public async Task AddOrUpdateMemberAsync(long projectId, long userId, string role, long addedByUserId, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        const string sql = @"
            INSERT INTO ProjectMembers (ProjectId, UserId, Role, AddedByUserId, AddedAtUtc)
            VALUES (@projectId, @userId, @role, @addedBy, now() at time zone 'utc')
            ON CONFLICT (ProjectId, UserId)
            DO UPDATE SET Role = EXCLUDED.Role;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@projectId", projectId);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@role", role);
        cmd.Parameters.AddWithValue("@addedBy", addedByUserId);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveAsync(long projectId, long userId, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        const string sql = "DELETE FROM ProjectMembers WHERE ProjectId = @projectId AND UserId = @userId;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@projectId", projectId);
        cmd.Parameters.AddWithValue("@userId", userId);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class ProjectInviteRepository
{
    private readonly DbConnectionFactory factory;

    public ProjectInviteRepository(DbConnectionFactory factory)
    {
        this.factory = factory;
    }

    public async Task<(string token, long inviteId)> CreateAsync(long projectId, string email, string role, long createdByUserId, DateTime expiresAtUtc, CancellationToken cancellationToken = default)
    {
        var token = Guid.NewGuid().ToString("N");
        var hash = Hash(token);

        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
            INSERT INTO ProjectInvites (ProjectId, InvitedEmail, Role, InviteTokenHash, ExpiresAtUtc, CreatedByUserId, CreatedAtUtc)
            VALUES (@projectId, @email, @role, @hash, @expires, @createdBy, now() at time zone 'utc')
            RETURNING Id;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@projectId", projectId);
        cmd.Parameters.AddWithValue("@email", email);
        cmd.Parameters.AddWithValue("@role", role);
        cmd.Parameters.AddWithValue("@hash", hash);
        cmd.Parameters.AddWithValue("@expires", expiresAtUtc);
        cmd.Parameters.AddWithValue("@createdBy", createdByUserId);

        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return (token, Convert.ToInt64(result ?? 0));
    }

    public async Task<(long projectId, string email, string role)?> AcceptAsync(string token, long acceptingUserId, CancellationToken cancellationToken = default)
    {
        var hash = Hash(token);
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        const string selectSql = @"
            SELECT Id, ProjectId, InvitedEmail, Role, ExpiresAtUtc, AcceptedByUserId
            FROM ProjectInvites
            WHERE InviteTokenHash = @hash
            FOR UPDATE;";

        long inviteId;
        long projectId;
        string email;
        string role;
        DateTime expires;
        long? acceptedBy;

        await using (var cmd = new NpgsqlCommand(selectSql, conn, (NpgsqlTransaction)tx))
        {
            cmd.Parameters.AddWithValue("@hash", hash);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            inviteId = reader.GetInt64(0);
            projectId = reader.GetInt64(1);
            email = reader.GetString(2);
            role = reader.GetString(3);
            expires = reader.GetDateTime(4);
            acceptedBy = reader.IsDBNull(5) ? null : reader.GetInt64(5);
        }

        if (acceptedBy.HasValue)
        {
            return null;
        }

        if (expires < DateTime.UtcNow)
        {
            return null;
        }

        const string updateSql = @"
            UPDATE ProjectInvites
            SET AcceptedByUserId = @userId, AcceptedAtUtc = now() at time zone 'utc'
            WHERE Id = @id;";

        await using (var cmd = new NpgsqlCommand(updateSql, conn, (NpgsqlTransaction)tx))
        {
            cmd.Parameters.AddWithValue("@userId", acceptingUserId);
            cmd.Parameters.AddWithValue("@id", inviteId);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return (projectId, email, role);
    }

    private static string Hash(string token)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
