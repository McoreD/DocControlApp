using Npgsql;

namespace DocControl.Infrastructure.Data;

public sealed class DbConnectionFactory
{
    private readonly string connectionString;

    public DbConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException("Connection string required", nameof(connectionString));
        this.connectionString = connectionString;
    }

    public NpgsqlConnection Create() => new(connectionString);
}
