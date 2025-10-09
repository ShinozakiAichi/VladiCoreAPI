using System.IO;
using MySql.Data.MySqlClient;
using NUnit.Framework;

namespace VladiCore.Tests.Infrastructure
{
    [SetUpFixture]
    public class TestDatabaseFixture
    {
        [OneTimeSetUp]
        public void Initialize()
        {
            using (var connection = new MySqlConnection(TestConfiguration.ConnectionString))
            {
                connection.Open();
                ExecuteScripts(connection, "db/migrations/mysql");
                ExecuteScripts(connection, "db/seed");
            }
        }

        private static void ExecuteScripts(MySqlConnection connection, string relativePath)
        {
            var root = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", relativePath);
            foreach (var file in Directory.GetFiles(root, "*.sql"))
            {
                var scriptText = File.ReadAllText(file);
                var script = new MySqlScript(connection, scriptText);
                script.Execute();
            }
        }
    }
}
