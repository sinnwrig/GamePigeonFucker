using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;

namespace IMessage;

internal sealed class ChatDatabaseTransport : IMessageTransport
{
    private static readonly DateTime MacEpoch = new(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly string _connectionString;
    private readonly InjectorClient _injector = new();
    private long _lastRowId;

    public ChatDatabaseTransport(string? databasePath = null)
    {
        var path = databasePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Messages", "chat.db");

        _connectionString = $"Data Source={path};Mode=ReadOnly;Cache=Shared";
    }

    public async Task WatchAsync(Action<InboundMessage> onMessage, CancellationToken cancellationToken)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        _lastRowId = await GetMaxRowIdAsync(connection, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            await foreach (var message in FetchNewMessagesAsync(connection, cancellationToken))
            {
                onMessage(message);
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }

    public Task SendAsync(string chatIdentifier, OutboundMessage message)
    {
        var chatGuid = BuildChatGuid(chatIdentifier);
        return _injector.SendAsync(chatGuid, message.Text, message.BalloonBundleId, message.RawPayload);
    }

    private static async Task<long> GetMaxRowIdAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT IFNULL(MAX(ROWID), 0) FROM message";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result);
    }

    private async IAsyncEnumerable<InboundMessage> FetchNewMessagesAsync(
        SqliteConnection connection,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                message.ROWID,
                message.guid,
                IFNULL(message.text, ''),
                IFNULL(message.balloon_bundle_id, ''),
                IFNULL(handle.id, ''),
                IFNULL(chat.chat_identifier, ''),
                message.date,
                message.is_from_me,
                message.payload_data
            FROM message
            LEFT JOIN handle ON message.handle_id = handle.ROWID
            LEFT JOIN chat_message_join ON chat_message_join.message_id = message.ROWID
            LEFT JOIN chat ON chat.ROWID = chat_message_join.chat_id
            WHERE message.ROWID > $lastRowId
            ORDER BY message.ROWID ASC
            """;
        command.Parameters.AddWithValue("$lastRowId", _lastRowId);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var rowId = reader.GetInt64(0);
            _lastRowId = Math.Max(_lastRowId, rowId);

            yield return new InboundMessage(
                Guid: reader.GetString(1),
                ChatIdentifier: reader.GetString(5),
                HandleId: reader.GetString(4),
                Text: reader.GetString(2),
                BalloonBundleId: reader.GetString(3),
                PayloadData: reader.IsDBNull(8) ? null : (byte[])reader.GetValue(8),
                Timestamp: MacEpoch.AddSeconds(reader.GetInt64(6) / 1_000_000_000.0),
                IsFromMe: reader.GetInt64(7) != 0);
        }
    }

    private static string BuildChatGuid(string chatIdentifier)
    {
        var isGroupChat = chatIdentifier.StartsWith("chat", StringComparison.Ordinal);
        var separator = isGroupChat ? "+" : "-";
        return $"iMessage;{separator};{chatIdentifier}";
    }
}
