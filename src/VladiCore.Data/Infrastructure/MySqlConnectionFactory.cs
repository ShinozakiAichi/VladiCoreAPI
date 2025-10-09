using System;
using System.Configuration;
using System.Data;
using MySql.Data.MySqlClient;

namespace VladiCore.Data.Infrastructure
{
    public interface IMySqlConnectionFactory
    {
        IDbConnection Create();
    }

    public class MySqlConnectionFactory : IMySqlConnectionFactory
    {
        private readonly string _connectionString;

        public MySqlConnectionFactory(string connectionStringOrName = "MySql")
        {
            var config = ConfigurationManager.ConnectionStrings[connectionStringOrName];
            _connectionString = config?.ConnectionString ?? connectionStringOrName;

            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                _connectionString = Environment.GetEnvironmentVariable("VLADICORE_CONNECTION");
            }

            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                throw new InvalidOperationException("MySQL connection string is not configured.");
            }
        }

        public IDbConnection Create()
        {
            var connection = new MySqlConnection(_connectionString);
            connection.Open();
            return connection;
        }
    }
}
