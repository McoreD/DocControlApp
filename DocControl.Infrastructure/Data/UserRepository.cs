using DocControl.Core.Models;
using Npgsql;

namespace DocControl.Infrastructure.Data;

public sealed class UserRepository
{
    private readonly DbConnectionFactory factory;

    public UserRepository(DbConnectionFactory factory)
    {
        this.factory = factory;
    }

    public async Task<UserRecord> RegisterAsync(string email, string displayName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        if (string.IsNullOrWhiteSpace(displayName)) displayName = email;

        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
            INSERT INTO Users (Email, DisplayName, CreatedAtUtc)
            VALUES (@email, @name, now() at time zone 'utc')
            ON CONFLICT (Email) DO NOTHING
            RETURNING Id, Email, DisplayName, CreatedAtUtc;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@email", email);
        cmd.Parameters.AddWithValue("@name", displayName);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new UserRecord
            {
                Id = reader.GetInt64(0),
                Email = reader.GetString(1),
                DisplayName = reader.GetString(2),
                CreatedAtUtc = reader.GetDateTime(3)
            };
        }

        // Existing user: fetch without mutating existing display name.
        await reader.DisposeAsync().ConfigureAwait(false);
        return await GetByEmailAsync(email, cancellationToken).ConfigureAwait(false)
               ?? throw new InvalidOperationException("Failed to register user.");
    }

    public async Task<long> GetOrCreateAsync(string email, string displayName, CancellationToken cancellationToken = default)
    {
        var user = await RegisterAsync(email, displayName, cancellationToken).ConfigureAwait(false);
        return user.Id;
    }

    public async Task<UserRecord?> GetByIdAsync(long userId, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        const string sql = "SELECT Id, Email, DisplayName, CreatedAtUtc FROM Users WHERE Id = @id;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", userId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new UserRecord
            {
                Id = reader.GetInt64(0),
                Email = reader.GetString(1),
                DisplayName = reader.GetString(2),
                CreatedAtUtc = reader.GetDateTime(3)
            };
        }
        return null;
    }

    public async Task<UserRecord?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        const string sql = "SELECT Id, Email, DisplayName, CreatedAtUtc FROM Users WHERE Email = @email;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@email", email);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new UserRecord
            {
                Id = reader.GetInt64(0),
                Email = reader.GetString(1),
                DisplayName = reader.GetString(2),
                CreatedAtUtc = reader.GetDateTime(3)
            };
        }
        return null;
    }

    public async Task<UserPasswordRecord?> GetPasswordAuthByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        const string sql = @"
            SELECT Id, Email, DisplayName, PasswordHash, PasswordSalt, KeySalt, OpenAiKeyEncrypted, GeminiKeyEncrypted
            FROM Users
            WHERE Email = @email;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@email", email);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new UserPasswordRecord
            {
                Id = reader.GetInt64(0),
                Email = reader.GetString(1),
                DisplayName = reader.GetString(2),
                PasswordHash = reader.IsDBNull(3) ? null : reader.GetString(3),
                PasswordSalt = reader.IsDBNull(4) ? null : reader.GetString(4),
                KeySalt = reader.IsDBNull(5) ? null : reader.GetString(5),
                OpenAiKeyEncrypted = reader.IsDBNull(6) ? null : reader.GetString(6),
                GeminiKeyEncrypted = reader.IsDBNull(7) ? null : reader.GetString(7)
            };
        }
        return null;
    }

    public async Task<UserPasswordRecord?> GetPasswordAuthByIdAsync(long userId, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        const string sql = @"
            SELECT Id, Email, DisplayName, PasswordHash, PasswordSalt, KeySalt, OpenAiKeyEncrypted, GeminiKeyEncrypted
            FROM Users
            WHERE Id = @id;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", userId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new UserPasswordRecord
            {
                Id = reader.GetInt64(0),
                Email = reader.GetString(1),
                DisplayName = reader.GetString(2),
                PasswordHash = reader.IsDBNull(3) ? null : reader.GetString(3),
                PasswordSalt = reader.IsDBNull(4) ? null : reader.GetString(4),
                KeySalt = reader.IsDBNull(5) ? null : reader.GetString(5),
                OpenAiKeyEncrypted = reader.IsDBNull(6) ? null : reader.GetString(6),
                GeminiKeyEncrypted = reader.IsDBNull(7) ? null : reader.GetString(7)
            };
        }
        return null;
    }

    public async Task SetPasswordAsync(long userId, string passwordHash, string passwordSalt, string keySalt, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        const string sql = @"
            UPDATE Users
            SET PasswordHash = @hash, PasswordSalt = @salt, KeySalt = @keySalt
            WHERE Id = @id;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", userId);
        cmd.Parameters.AddWithValue("@hash", passwordHash);
        cmd.Parameters.AddWithValue("@salt", passwordSalt);
        cmd.Parameters.AddWithValue("@keySalt", keySalt);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<(string? openAiEncrypted, string? geminiEncrypted)> GetAiKeysEncryptedAsync(long userId, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        const string sql = "SELECT OpenAiKeyEncrypted, GeminiKeyEncrypted FROM Users WHERE Id = @id;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", userId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var openAi = reader.IsDBNull(0) ? null : reader.GetString(0);
            var gemini = reader.IsDBNull(1) ? null : reader.GetString(1);
            return (openAi, gemini);
        }
        return (null, null);
    }

    public async Task SaveAiKeysEncryptedAsync(
        long userId,
        string? openAiEncrypted,
        string? geminiEncrypted,
        bool clearOpenAi,
        bool clearGemini,
        CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        const string sql = @"
            UPDATE Users
            SET OpenAiKeyEncrypted = CASE
                    WHEN @clearOpenAi THEN NULL
                    WHEN @openAi IS NOT NULL THEN @openAi
                    ELSE OpenAiKeyEncrypted
                END,
                GeminiKeyEncrypted = CASE
                    WHEN @clearGemini THEN NULL
                    WHEN @gemini IS NOT NULL THEN @gemini
                    ELSE GeminiKeyEncrypted
                END
            WHERE Id = @id;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", userId);
        cmd.Parameters.AddWithValue("@clearOpenAi", clearOpenAi);
        cmd.Parameters.AddWithValue("@clearGemini", clearGemini);
        cmd.Parameters.AddWithValue("@openAi", (object?)openAiEncrypted ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@gemini", (object?)geminiEncrypted ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateDisplayNameAsync(long userId, string displayName, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        const string sql = @"
            UPDATE Users
            SET DisplayName = @name
            WHERE Id = @id;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", userId);
        cmd.Parameters.AddWithValue("@name", displayName);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> LinkAccountAsync(
        long currentUserId,
        long legacyUserId,
        string newEmail,
        string newDisplayName,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == legacyUserId)
        {
            await UpdateProfileAsync(legacyUserId, newEmail, newDisplayName, cancellationToken).ConfigureAwait(false);
            return true;
        }

        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        const string hasRefsSql = @"
            SELECT
                EXISTS (SELECT 1 FROM ProjectMembers WHERE UserId = @id)
                OR EXISTS (SELECT 1 FROM Projects WHERE CreatedByUserId = @id)
                OR EXISTS (SELECT 1 FROM Documents WHERE CreatedByUserId = @id)
                OR EXISTS (SELECT 1 FROM Audit WHERE CreatedByUserId = @id)
                OR EXISTS (SELECT 1 FROM ProjectInvites WHERE CreatedByUserId = @id OR AcceptedByUserId = @id);";
        await using (var refsCmd = new NpgsqlCommand(hasRefsSql, conn, (NpgsqlTransaction)tx))
        {
            refsCmd.Parameters.AddWithValue("@id", currentUserId);
            var hasRefs = (bool)(await refsCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? false);
            if (hasRefs)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }
        }

        const string deleteAuthSql = "DELETE FROM UserAuth WHERE UserId = @id;";
        await using (var deleteAuth = new NpgsqlCommand(deleteAuthSql, conn, (NpgsqlTransaction)tx))
        {
            deleteAuth.Parameters.AddWithValue("@id", currentUserId);
            await deleteAuth.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        const string deleteUserSql = "DELETE FROM Users WHERE Id = @id;";
        await using (var deleteUser = new NpgsqlCommand(deleteUserSql, conn, (NpgsqlTransaction)tx))
        {
            deleteUser.Parameters.AddWithValue("@id", currentUserId);
            await deleteUser.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        const string updateSql = @"
            UPDATE Users
            SET Email = @email,
                DisplayName = @name
            WHERE Id = @id;";
        await using (var updateCmd = new NpgsqlCommand(updateSql, conn, (NpgsqlTransaction)tx))
        {
            updateCmd.Parameters.AddWithValue("@id", legacyUserId);
            updateCmd.Parameters.AddWithValue("@email", newEmail);
            updateCmd.Parameters.AddWithValue("@name", newDisplayName);
            await updateCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task UpdateProfileAsync(long userId, string email, string displayName, CancellationToken cancellationToken)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        const string sql = @"
            UPDATE Users
            SET Email = @email,
                DisplayName = @name
            WHERE Id = @id;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", userId);
        cmd.Parameters.AddWithValue("@email", email);
        cmd.Parameters.AddWithValue("@name", displayName);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class UserPasswordRecord
{
    public long Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? PasswordSalt { get; set; }
    public string? KeySalt { get; set; }
    public string? OpenAiKeyEncrypted { get; set; }
    public string? GeminiKeyEncrypted { get; set; }
}
