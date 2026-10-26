using System.Globalization;
using System.Text.Json;
using FPVTrainingAssistant.Services;
using Microsoft.Data.Sqlite;

namespace FPVTrainingAssistant.Data;

// Small ADO.NET helpers. SQL/identifiers are code-owned; all values are parameters.
internal static class SqliteData
{
    public static SqliteCommand Command(SqliteConnection connection, SqliteTransaction? transaction, string sql, params object?[] values)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        for (var i = 0; i < values.Length; i++) command.Parameters.AddWithValue("$p" + i, values[i] ?? DBNull.Value);
        return command;
    }
    public static int Execute(SqliteConnection c, SqliteTransaction? t, string sql, params object?[] values)
    {
        using var command = Command(c, t, sql, values);
        return command.ExecuteNonQuery();
    }
    public static List<T> Query<T>(SqliteConnection c, SqliteTransaction? t, string sql, Func<SqliteDataReader,T> map, params object?[] values)
    {
        using var command = Command(c,t,sql,values);
        using var reader = command.ExecuteReader();
        var result = new List<T>();
        while (reader.Read()) result.Add(map(reader));
        return result;
    }
    public static string Utc(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    public static DateTimeOffset Date(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    public static string? Text(SqliteDataReader r, string column) => r.IsDBNull(r.GetOrdinal(column)) ? null : r.GetString(r.GetOrdinal(column));
    public static double? Number(SqliteDataReader r, string column) => r.IsDBNull(r.GetOrdinal(column)) ? null : r.GetDouble(r.GetOrdinal(column));
    public static bool? Bool(SqliteDataReader r, string column) => r.IsDBNull(r.GetOrdinal(column)) ? null : r.GetBoolean(r.GetOrdinal(column));
    public static string? Json<T>(T? value) => value is null ? null : JsonSerializer.Serialize(value, SessionJson.Options);
    public static T? ReadJson<T>(string? value) => value is null ? default : JsonSerializer.Deserialize<T>(value, SessionJson.Options);
}
