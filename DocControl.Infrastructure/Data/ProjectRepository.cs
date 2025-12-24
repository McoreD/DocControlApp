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

    public async Task<long> CreateAsync(
        string name,
        string description,
        string separator,
        int paddingLength,
        int levelCount,
        string level1Label,
        string level2Label,
        string level3Label,
        string level4Label,
        string level5Label,
        string level6Label,
        int level1Length,
        int level2Length,
        int level3Length,
        int level4Length,
        int level5Length,
        int level6Length,
        long createdByUserId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        const string insertProject = @"
            INSERT INTO Projects (
                Name,
                Description,
                Separator,
                PaddingLength,
                LevelCount,
                Level1Label,
                Level2Label,
                Level3Label,
                Level4Label,
                Level5Label,
                Level6Label,
                Level1Length,
                Level2Length,
                Level3Length,
                Level4Length,
                Level5Length,
                Level6Length,
                CreatedByUserId,
                CreatedAtUtc,
                IsArchived
            )
            VALUES (
                @name,
                @description,
                @separator,
                @padding,
                @levelCount,
                @level1Label,
                @level2Label,
                @level3Label,
                @level4Label,
                @level5Label,
                @level6Label,
                @level1Length,
                @level2Length,
                @level3Length,
                @level4Length,
                @level5Length,
                @level6Length,
                @createdBy,
                now() at time zone 'utc',
                FALSE
            )
            RETURNING Id;";
        long projectId;
        await using (var cmd = new NpgsqlCommand(insertProject, conn, (NpgsqlTransaction)tx))
        {
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@description", description);
            cmd.Parameters.AddWithValue("@separator", string.IsNullOrWhiteSpace(separator) ? "-" : separator);
            cmd.Parameters.AddWithValue("@padding", paddingLength <= 0 ? 3 : paddingLength);
            cmd.Parameters.AddWithValue("@levelCount", levelCount < 1 ? 3 : levelCount);
            cmd.Parameters.AddWithValue("@level1Label", string.IsNullOrWhiteSpace(level1Label) ? "Level1" : level1Label);
            cmd.Parameters.AddWithValue("@level2Label", string.IsNullOrWhiteSpace(level2Label) ? "Level2" : level2Label);
            cmd.Parameters.AddWithValue("@level3Label", string.IsNullOrWhiteSpace(level3Label) ? "Level3" : level3Label);
            cmd.Parameters.AddWithValue("@level4Label", string.IsNullOrWhiteSpace(level4Label) ? "Level4" : level4Label);
            cmd.Parameters.AddWithValue("@level5Label", string.IsNullOrWhiteSpace(level5Label) ? "Level5" : level5Label);
            cmd.Parameters.AddWithValue("@level6Label", string.IsNullOrWhiteSpace(level6Label) ? "Level6" : level6Label);
            cmd.Parameters.AddWithValue("@level1Length", level1Length <= 0 ? 3 : level1Length);
            cmd.Parameters.AddWithValue("@level2Length", level2Length <= 0 ? 3 : level2Length);
            cmd.Parameters.AddWithValue("@level3Length", level3Length <= 0 ? 3 : level3Length);
            cmd.Parameters.AddWithValue("@level4Length", level4Length <= 0 ? 3 : level4Length);
            cmd.Parameters.AddWithValue("@level5Length", level5Length <= 0 ? 3 : level5Length);
            cmd.Parameters.AddWithValue("@level6Length", level6Length <= 0 ? 3 : level6Length);
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
            SELECT p.Id, p.Name, p.Description, p.Separator, p.PaddingLength, p.LevelCount, p.Level1Label, p.Level2Label, p.Level3Label, p.Level4Label, p.Level5Label, p.Level6Label,
                   p.Level1Length, p.Level2Length, p.Level3Length, p.Level4Length, p.Level5Length, p.Level6Length,
                   p.CreatedByUserId, p.CreatedAtUtc, p.IsArchived, pm.IsDefault
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
            SELECT p.Id, p.Name, p.Description, p.Separator, p.PaddingLength, p.LevelCount, p.Level1Label, p.Level2Label, p.Level3Label, p.Level4Label, p.Level5Label, p.Level6Label,
                   p.Level1Length, p.Level2Length, p.Level3Length, p.Level4Length, p.Level5Length, p.Level6Length,
                   p.CreatedByUserId, p.CreatedAtUtc, p.IsArchived, pm.IsDefault
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

    public async Task UpdateAsync(long projectId, string name, string description, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
            UPDATE Projects
            SET Name = @name,
                Description = @description
            WHERE Id = @projectId;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@projectId", projectId);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@description", description);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
            Separator = reader.GetString(3),
            PaddingLength = reader.GetInt32(4),
            LevelCount = reader.GetInt32(5),
            Level1Label = reader.GetString(6),
            Level2Label = reader.GetString(7),
            Level3Label = reader.GetString(8),
            Level4Label = reader.GetString(9),
            Level5Label = reader.GetString(10),
            Level6Label = reader.GetString(11),
            Level1Length = reader.GetInt32(12),
            Level2Length = reader.GetInt32(13),
            Level3Length = reader.GetInt32(14),
            Level4Length = reader.GetInt32(15),
            Level5Length = reader.GetInt32(16),
            Level6Length = reader.GetInt32(17),
            CreatedByUserId = reader.GetInt64(18),
            CreatedAtUtc = reader.GetDateTime(19),
            IsArchived = reader.GetBoolean(20),
            IsDefault = reader.GetBoolean(21)
        };
}
