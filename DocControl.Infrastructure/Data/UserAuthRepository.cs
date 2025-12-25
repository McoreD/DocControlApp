using System.Text.Json;
using DocControl.Core.Security;
using Microsoft.Extensions.Configuration;
using DocControl.Core.Models;
using Npgsql;

namespace DocControl.Infrastructure.Data;

public sealed class UserAuthRepository
{
    private readonly DbConnectionFactory factory;
    private readonly AesGcmSecretProtector? mfaProtector;

    public UserAuthRepository(DbConnectionFactory factory, IConfiguration configuration)
    {
        this.factory = factory;
        var key = configuration["MFA_SECRET_KEY"];
        if (!string.IsNullOrWhiteSpace(key))
        {
            mfaProtector = new AesGcmSecretProtector(key);
        }
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
            var json = reader.IsDBNull(2) ? null : reader.GetString(2);
            json = TryDecryptJson(json);
            return new UserAuthRecord
            {
                UserId = reader.GetInt64(0),
                MfaEnabled = reader.GetBoolean(1),
                MfaMethodsJson = json
            };
        }

        return null;
    }

    public async Task SaveTotpAsync(long userId, string secret, bool verified, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        var protectedSecret = ProtectSecret(secret);
        var state = new TotpState(protectedSecret, DateTime.UtcNow, verified ? DateTime.UtcNow : null);
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

    private string ProtectSecret(string secret)
    {
        if (mfaProtector is null || string.IsNullOrWhiteSpace(secret))
        {
            return secret;
        }

        if (secret.StartsWith("v1:", StringComparison.Ordinal))
        {
            return secret;
        }

        return mfaProtector.Encrypt(secret);
    }

    private string? TryDecryptJson(string? json)
    {
        if (mfaProtector is null || string.IsNullOrWhiteSpace(json))
        {
            return json;
        }

        try
        {
            var state = JsonSerializer.Deserialize<TotpState>(json);
            if (state is null || string.IsNullOrWhiteSpace(state.Secret))
            {
                return json;
            }

            if (!state.Secret.StartsWith("v1:", StringComparison.Ordinal))
            {
                return json;
            }

            var decrypted = mfaProtector.Decrypt(state.Secret);
            if (string.IsNullOrWhiteSpace(decrypted))
            {
                return json;
            }

            var updated = new TotpState(decrypted, state.CreatedAtUtc, state.VerifiedAtUtc);
            return JsonSerializer.Serialize(updated);
        }
        catch (JsonException)
        {
            return json;
        }
    }
}

internal sealed record TotpState(string Secret, DateTime CreatedAtUtc, DateTime? VerifiedAtUtc);
