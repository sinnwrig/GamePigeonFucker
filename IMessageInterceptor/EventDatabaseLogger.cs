using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace IMessage;

internal sealed class EventDatabaseLogger : IDisposable
{
    private readonly SqliteConnection _rawConnection;
    private readonly SqliteConnection _classifiedConnection;
    private readonly object _lock = new();

    public EventDatabaseLogger(string rawDbPath, string classifiedDbPath)
    {
        _rawConnection = OpenConnection(rawDbPath);
        ExecuteNonQuery(_rawConnection, """
            CREATE TABLE IF NOT EXISTS raw_events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                logged_at TEXT NOT NULL,
                json TEXT NOT NULL
            );
            """);

        _classifiedConnection = OpenConnection(classifiedDbPath);
        ExecuteNonQuery(_classifiedConnection, """
            CREATE TABLE IF NOT EXISTS classified_events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                logged_at TEXT NOT NULL,
                event_type TEXT NOT NULL,
                message_guid TEXT NOT NULL,
                chat_identifier TEXT,
                chat_guid TEXT,
                timestamp TEXT NOT NULL,
                is_from_me INTEGER NOT NULL,
                payload TEXT NOT NULL
            );
            """);
    }

    private static SqliteConnection OpenConnection(string dbPath)
    {
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        return connection;
    }

    private static void ExecuteNonQuery(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public void LogRaw(JsonElement rawEvent)
    {
        lock (_lock)
        {
            using var command = _rawConnection.CreateCommand();
            command.CommandText = "INSERT INTO raw_events (logged_at, json) VALUES ($loggedAt, $json)";
            command.Parameters.AddWithValue("$loggedAt", DateTimeOffset.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$json", rawEvent.GetRawText());
            command.ExecuteNonQuery();
        }
    }

    public void LogClassified(ChatEvent chatEvent)
    {
        lock (_lock)
        {
            using var command = _classifiedConnection.CreateCommand();
            command.CommandText = """
                INSERT INTO classified_events
                    (logged_at, event_type, message_guid, chat_identifier, chat_guid, timestamp, is_from_me, payload)
                VALUES
                    ($loggedAt, $eventType, $messageGuid, $chatIdentifier, $chatGuid, $timestamp, $isFromMe, $payload)
                """;
            command.Parameters.AddWithValue("$loggedAt", DateTimeOffset.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$eventType", chatEvent.GetType().Name);
            command.Parameters.AddWithValue("$messageGuid", chatEvent.MessageGuid);
            command.Parameters.AddWithValue("$chatIdentifier", (object?)chatEvent.ChatIdentifier ?? DBNull.Value);
            command.Parameters.AddWithValue("$chatGuid", (object?)chatEvent.ChatGuid ?? DBNull.Value);
            command.Parameters.AddWithValue("$timestamp", chatEvent.Timestamp.ToString("O"));
            command.Parameters.AddWithValue("$isFromMe", chatEvent.IsFromMe ? 1 : 0);
            command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(chatEvent, chatEvent.GetType()));
            command.ExecuteNonQuery();
        }
    }

    public void Dispose()
    {
        _rawConnection.Dispose();
        _classifiedConnection.Dispose();
    }
}
