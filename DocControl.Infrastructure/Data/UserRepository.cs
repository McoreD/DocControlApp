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

    public async Task<long> GetOrCreateAsync(string email, string displayName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        if (string.IsNullOrWhiteSpace(displayName)) displayName = email;

        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
            INSERT INTO Users (Email, DisplayName, CreatedAtUtc)
            VALUES (@email, @name, now() at time zone 'utc')
            ON CONFLICT (Email) DO UPDATE SET DisplayName = EXCLUDED.DisplayName
            RETURNING Id;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@email", email);
        cmd.Parameters.AddWithValue("@name", displayName);
        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result ?? throw new InvalidOperationException("Failed to upsert user."));
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
}
