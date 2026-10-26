using System.IO;
using Microsoft.Data.Sqlite;

namespace FPVTrainingAssistant.Data;

public sealed class DatabaseConnectionFactory(IApplicationDataPathProvider paths)
{
    public string DatabasePath => paths.DatabasePath;

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath, ForeignKeys = true, Pooling = false, DefaultTimeout = 5
        }.ToString());
        try
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_keys = ON;";
            command.ExecuteNonQuery();
            return connection;
        }
        catch { connection.Dispose(); throw; }
    }

    internal void CreateDirectory() => Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(DatabasePath))!);
}
