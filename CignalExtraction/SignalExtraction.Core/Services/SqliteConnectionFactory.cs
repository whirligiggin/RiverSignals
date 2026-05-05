using Microsoft.Data.Sqlite;

namespace SignalExtraction.Core.Services;

internal static class SqliteConnectionFactory
{
    public static SqliteConnection CreateOpenConnection(string connectionString)
    {
        EnsureDataSourceDirectory(connectionString);

        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    private static void EnsureDataSourceDirectory(string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        var dataSource = builder.DataSource;

        if (string.IsNullOrWhiteSpace(dataSource) ||
            string.Equals(dataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var directoryPath = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }
}
