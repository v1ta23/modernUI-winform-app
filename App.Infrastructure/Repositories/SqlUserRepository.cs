using App.Core.Interfaces;
using App.Infrastructure.Config;
using Microsoft.Data.SqlClient;

namespace App.Infrastructure.Repositories;

public sealed class SqlUserRepository : IUserRepository
{
    private readonly string _connectionString;

    public SqlUserRepository(SqlServerOptions options)
    {
        _connectionString = options.ConnectionString;
        SqlServerSchemaInitializer.EnsureInitialized(_connectionString);
    }

    public bool ValidateCredentials(string account, string password)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        const string sql = """
                           SELECT password
                           FROM Users
                           WHERE account = @account
                           """;

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@account", account);

        var storedPassword = command.ExecuteScalar() as string;
        if (string.IsNullOrWhiteSpace(storedPassword))
        {
            return false;
        }

        if (PasswordHasher.IsHash(storedPassword))
        {
            return PasswordHasher.Verify(password, storedPassword);
        }

        if (!string.Equals(storedPassword, password, StringComparison.Ordinal))
        {
            return false;
        }

        UpgradePasswordHash(connection, account, password);
        return true;
    }

    public bool AccountExists(string account)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        const string sql = """
                           SELECT COUNT(1)
                           FROM Users
                           WHERE account = @account
                           """;

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@account", account);

        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    public void Create(string account, string password)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        const string sql = """
                           INSERT INTO Users (account, password)
                           VALUES (@account, @password)
                           """;

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@account", account);
        command.Parameters.AddWithValue("@password", PasswordHasher.Hash(password));
        command.ExecuteNonQuery();
    }

    private static void UpgradePasswordHash(SqlConnection connection, string account, string password)
    {
        const string sql = """
                           UPDATE Users
                           SET password = @password
                           WHERE account = @account
                           """;

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@account", account);
        command.Parameters.AddWithValue("@password", PasswordHasher.Hash(password));
        command.ExecuteNonQuery();
    }
}
