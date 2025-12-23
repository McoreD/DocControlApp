using System.Text.Json;
using DocControl.Core.Models;
using Npgsql;

namespace DocControl.Infrastructure.Data;

public sealed class UserAuthRepository
{
    private readonly DbConnectionFactory factory;

    public UserAuthRepository(DbConnectionFactory factory)
    {
        this.factory = factory;
    }

    public async Task EnsureExistsAsync(long userId, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        const string sql = @"
            INSERT INTO UserAuth (UserId, ProviderSubject, ProviderName, MfaEnabled, MfaMethodsJson)
            VALUES (@userId, '', '', FALSE, NULL)
            ON CONFLICT (UserId) DO NOTHING;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@userId", userId);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<UserAuthRecord?> GetAsync(long userId, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = "SELECT UserId, MfaEnabled, MfaMethodsJson FROM UserAuth WHERE UserId = @userId;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@userId", userId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new UserAuthRecord
            {
                UserId = reader.GetInt64(0),
                MfaEnabled = reader.GetBoolean(1),
                MfaMethodsJson = reader.IsDBNull(2) ? null : reader.GetString(2)
            };
        }

        return null;
    }

    public async Task SaveTotpAsync(long userId, string secret, bool verified, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        var state = new TotpState(secret, DateTime.UtcNow, verified ? DateTime.UtcNow : null);
        var json = JsonSerializer.Serialize(state);

        const string sql = @"
            INSERT INTO UserAuth (UserId, ProviderSubject, ProviderName, MfaEnabled, MfaMethodsJson)
            VALUES (@userId, '', '', @enabled, @json)
            ON CONFLICT (UserId) DO UPDATE SET
                MfaEnabled = EXCLUDED.MfaEnabled,
                MfaMethodsJson = EXCLUDED.MfaMethodsJson;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@enabled", verified);
        cmd.Parameters.AddWithValue("@json", (object?)json ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal sealed record TotpState(string Secret, DateTime CreatedAtUtc, DateTime? VerifiedAtUtc);
