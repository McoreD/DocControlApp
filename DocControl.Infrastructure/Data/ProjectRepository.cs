using DocControl.Core.Models;
using Npgsql;

namespace DocControl.Infrastructure.Data;

public sealed class ProjectRepository
{
    private readonly DbConnectionFactory factory;

    public ProjectRepository(DbConnectionFactory factory)
    {
        this.factory = factory;
    }

    public async Task<long> CreateAsync(string name, string description, long createdByUserId, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        const string insertProject = @"
            INSERT INTO Projects (Name, Description, CreatedByUserId, CreatedAtUtc, IsArchived)
            VALUES (@name, @description, @createdBy, now() at time zone 'utc', FALSE)
            RETURNING Id;";
        long projectId;
        await using (var cmd = new NpgsqlCommand(insertProject, conn, (NpgsqlTransaction)tx))
        {
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@description", description);
            cmd.Parameters.AddWithValue("@createdBy", createdByUserId);
            var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            projectId = Convert.ToInt64(result ?? throw new InvalidOperationException("Failed to create project"));
        }

        const string insertMember = @"
            INSERT INTO ProjectMembers (ProjectId, UserId, Role, AddedByUserId, AddedAtUtc)
            VALUES (@projectId, @userId, @role, @addedBy, now() at time zone 'utc');";
        await using (var memberCmd = new NpgsqlCommand(insertMember, conn, (NpgsqlTransaction)tx))
        {
            memberCmd.Parameters.AddWithValue("@projectId", projectId);
            memberCmd.Parameters.AddWithValue("@userId", createdByUserId);
            memberCmd.Parameters.AddWithValue("@role", "Owner");
            memberCmd.Parameters.AddWithValue("@addedBy", createdByUserId);
            await memberCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return projectId;
    }

    public async Task<IReadOnlyList<ProjectRecord>> ListForUserAsync(long userId, CancellationToken cancellationToken = default)
    {
        var list = new List<ProjectRecord>();
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
            SELECT p.Id, p.Name, p.Description, p.CreatedByUserId, p.CreatedAtUtc, p.IsArchived, pm.IsDefault
            FROM Projects p
            JOIN ProjectMembers pm ON pm.ProjectId = p.Id
            WHERE pm.UserId = @userId AND p.IsArchived = FALSE
            ORDER BY p.CreatedAtUtc DESC;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@userId", userId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ReadProject(reader));
        }
        return list;
    }

    public async Task<ProjectRecord?> GetAsync(long projectId, long userId, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
            SELECT p.Id, p.Name, p.Description, p.CreatedByUserId, p.CreatedAtUtc, p.IsArchived, pm.IsDefault
            FROM Projects p
            JOIN ProjectMembers pm ON pm.ProjectId = p.Id
            WHERE p.Id = @projectId AND pm.UserId = @userId;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@projectId", projectId);
        cmd.Parameters.AddWithValue("@userId", userId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return ReadProject(reader);
        }
        return null;
    }

    public async Task<bool> IsMemberAsync(long projectId, long userId, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        const string sql = "SELECT 1 FROM ProjectMembers WHERE ProjectId = @projectId AND UserId = @userId LIMIT 1;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@projectId", projectId);
        cmd.Parameters.AddWithValue("@userId", userId);
        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is not null;
    }

    private static ProjectRecord ReadProject(NpgsqlDataReader reader) =>
        new()
        {
            Id = reader.GetInt64(0),
            Name = reader.GetString(1),
            Description = reader.GetString(2),
            CreatedByUserId = reader.GetInt64(3),
            CreatedAtUtc = reader.GetDateTime(4),
            IsArchived = reader.GetBoolean(5),
            IsDefault = reader.GetBoolean(6)
        };
}
