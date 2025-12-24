using Npgsql;

namespace DocControl.Infrastructure.Data;

public sealed class DatabaseInitializer
{
    private readonly DbConnectionFactory factory;

    public DatabaseInitializer(DbConnectionFactory factory)
    {
        this.factory = factory;
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Basic schema aligned to Neon/PostgreSQL. Serial types are fine for serverless.
        var sql = @"
        CREATE TABLE IF NOT EXISTS Users (
            Id BIGSERIAL PRIMARY KEY,
            Email TEXT NOT NULL UNIQUE,
            DisplayName TEXT NOT NULL,
            CreatedAtUtc TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE TABLE IF NOT EXISTS UserAuth (
            UserId BIGINT PRIMARY KEY REFERENCES Users(Id),
            ProviderSubject TEXT NOT NULL,
            ProviderName TEXT NOT NULL,
            MfaEnabled BOOLEAN NOT NULL DEFAULT FALSE,
            MfaMethodsJson TEXT,
            LastLoginUtc TIMESTAMPTZ
        );

        CREATE TABLE IF NOT EXISTS Projects (
            Id BIGSERIAL PRIMARY KEY,
            Name TEXT NOT NULL,
            Description TEXT NOT NULL,
            CreatedByUserId BIGINT NOT NULL REFERENCES Users(Id),
            CreatedAtUtc TIMESTAMPTZ NOT NULL DEFAULT now(),
            IsArchived BOOLEAN NOT NULL DEFAULT FALSE
        );

        CREATE TABLE IF NOT EXISTS ProjectMembers (
            Id BIGSERIAL PRIMARY KEY,
            ProjectId BIGINT NOT NULL REFERENCES Projects(Id) ON DELETE CASCADE,
            UserId BIGINT NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
            Role TEXT NOT NULL,
            IsDefault BOOLEAN NOT NULL DEFAULT FALSE,
            AddedByUserId BIGINT NOT NULL REFERENCES Users(Id),
            AddedAtUtc TIMESTAMPTZ NOT NULL DEFAULT now(),
            UNIQUE(ProjectId, UserId)
        );

        CREATE TABLE IF NOT EXISTS ProjectInvites (
            Id BIGSERIAL PRIMARY KEY,
            ProjectId BIGINT NOT NULL REFERENCES Projects(Id) ON DELETE CASCADE,
            InvitedEmail TEXT NOT NULL,
            Role TEXT NOT NULL,
            InviteTokenHash TEXT NOT NULL,
            InviteToken TEXT,
            ExpiresAtUtc TIMESTAMPTZ NOT NULL,
            CreatedByUserId BIGINT NOT NULL REFERENCES Users(Id),
            CreatedAtUtc TIMESTAMPTZ NOT NULL DEFAULT now(),
            AcceptedByUserId BIGINT,
            AcceptedAtUtc TIMESTAMPTZ,
            UNIQUE(ProjectId, InvitedEmail, Role)
        );

        -- Ensure plaintext token column exists for owner-side retrieval (hash still used for validation)
        ALTER TABLE ProjectInvites ADD COLUMN IF NOT EXISTS InviteToken TEXT;
        ALTER TABLE ProjectMembers ADD COLUMN IF NOT EXISTS IsDefault BOOLEAN NOT NULL DEFAULT FALSE;

        CREATE UNIQUE INDEX IF NOT EXISTS IX_ProjectMembers_DefaultProject
            ON ProjectMembers(UserId)
            WHERE IsDefault = TRUE;

        CREATE TABLE IF NOT EXISTS Config (
            Id BIGSERIAL PRIMARY KEY,
            ScopeType TEXT NOT NULL, -- Global|Project
            ProjectId BIGINT,
            Key TEXT NOT NULL,
            Value TEXT NOT NULL,
            UNIQUE(ScopeType, ProjectId, Key)
        );

        CREATE TABLE IF NOT EXISTS CodeSeries (
            Id BIGSERIAL PRIMARY KEY,
            ProjectId BIGINT NOT NULL REFERENCES Projects(Id) ON DELETE CASCADE,
            Level1 TEXT NOT NULL,
            Level2 TEXT NOT NULL,
            Level3 TEXT NOT NULL,
            Level4 TEXT,
            Description TEXT,
            NextNumber INTEGER NOT NULL DEFAULT 1,
            UNIQUE(ProjectId, Level1, Level2, Level3, Level4)
        );

        CREATE TABLE IF NOT EXISTS Documents (
            Id BIGSERIAL PRIMARY KEY,
            ProjectId BIGINT NOT NULL REFERENCES Projects(Id) ON DELETE CASCADE,
            Level1 TEXT NOT NULL,
            Level2 TEXT NOT NULL,
            Level3 TEXT NOT NULL,
            Level4 TEXT,
            Number INTEGER NOT NULL,
            FreeText TEXT,
            FileName TEXT NOT NULL,
            CreatedByUserId BIGINT NOT NULL REFERENCES Users(Id),
            CreatedAtUtc TIMESTAMPTZ NOT NULL DEFAULT now(),
            OriginalQuery TEXT,
            CodeSeriesId BIGINT NOT NULL REFERENCES CodeSeries(Id),
            UNIQUE(ProjectId, Level1, Level2, Level3, Level4, Number)
        );

        CREATE TABLE IF NOT EXISTS Audit (
            Id BIGSERIAL PRIMARY KEY,
            ProjectId BIGINT NOT NULL REFERENCES Projects(Id) ON DELETE CASCADE,
            Action TEXT NOT NULL,
            Payload TEXT,
            CreatedByUserId BIGINT NOT NULL REFERENCES Users(Id),
            CreatedAtUtc TIMESTAMPTZ NOT NULL DEFAULT now(),
            DocumentId BIGINT REFERENCES Documents(Id)
        );

        CREATE INDEX IF NOT EXISTS IX_Documents_ProjectId ON Documents(ProjectId);
        CREATE INDEX IF NOT EXISTS IX_Documents_Search ON Documents(ProjectId, Level1, Level2, Level3, Level4, Number);
        CREATE INDEX IF NOT EXISTS IX_Audit_ProjectId ON Audit(ProjectId, CreatedAtUtc DESC);
        ";

        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
