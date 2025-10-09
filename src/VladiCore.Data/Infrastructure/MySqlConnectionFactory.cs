using System.Data;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace VladiCore.Data.Infrastructure;

public interface IMySqlConnectionFactory
{
    IDbConnection Create();
}

public class MySqlConnectionFactory : IMySqlConnectionFactory
{
    private readonly string _connectionString;

    public MySqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");
    }

    public IDbConnection Create()
    {
        var connection = new MySqlConnection(_connectionString);
        connection.Open();
        return connection;
    }
}
