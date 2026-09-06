using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text.Json;

namespace IMessage;

internal sealed class InjectorClient
{
    private const string SocketPath = "/tmp/gamepigeonfucker-injector.sock";

    public async Task SendAsync(string chatGuid, string text, string? balloonBundleId, byte[]? payloadData)
    {
        var request = new
        {
            chatGuid,
            text,
            balloonBundleId,
            payloadDataBase64 = payloadData is null ? null : Convert.ToBase64String(payloadData),
        };

        var root = await SendRequestAsync(request);
        RequireOk(root, "send");
    }

    public async Task<string> SendViaAccountAsync(
        string accountUniqueId,
        string recipientHandleId,
        string text,
        string? senderIdentityId)
    {
        var request = new
        {
            cmd = "sendViaAccount",
            accountUniqueID = accountUniqueId,
            recipientHandleID = recipientHandleId,
            senderIdentityID = senderIdentityId,
            text,
        };

        var root = await SendRequestAsync(request);
        RequireOk(root, "sendViaAccount");
        return root.TryGetProperty("chatDescription", out var chatDescription)
            ? chatDescription.GetString() ?? ""
            : "";
    }

    public async Task<IReadOnlyList<ImAccountInfo>> ListAccountsAsync()
    {
        var root = await SendRequestAsync(new { cmd = "listAccounts" });
        RequireOk(root, "listAccounts");

        var accounts = new List<ImAccountInfo>();
        foreach (var account in root.GetProperty("accounts").EnumerateArray())
        {
            accounts.Add(new ImAccountInfo(
                ClassName: GetStringOrEmpty(account, "class"),
                UniqueId: GetFieldOrError(account, "uniqueID"),
                LoginImHandle: GetFieldOrError(account, "loginIMHandle"),
                Aliases: GetFieldOrError(account, "aliases"),
                LoginHandles: GetFieldOrError(account, "loginHandles")));
        }

        return accounts;
    }

    private static string GetStringOrEmpty(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) ? value.GetString() ?? "" : "";

    private static string GetFieldOrError(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value))
        {
            return value.GetString() ?? "";
        }

        return element.TryGetProperty(propertyName + "_error", out var error)
            ? error.GetString() ?? ""
            : "";
    }

    private static void RequireOk(JsonElement root, string command)
    {
        if (!root.GetProperty("ok").GetBoolean())
        {
            var error = root.TryGetProperty("error", out var errorElement)
                ? errorElement.GetString()
                : "unknown error";
            throw new InvalidOperationException($"injector {command} failed: {error}");
        }
    }

    private static async Task<JsonElement> SendRequestAsync(object request)
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(SocketPath));

        await WriteFrameAsync(socket, JsonSerializer.SerializeToUtf8Bytes(request));

        var responseBytes = await ReadFrameAsync(socket);
        using var document = JsonDocument.Parse(responseBytes);
        return document.RootElement.Clone();
    }

    private static async Task WriteFrameAsync(Socket socket, byte[] payload)
    {
        var length = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)payload.Length);
        await socket.SendAsync(length, SocketFlags.None);
        await socket.SendAsync(payload, SocketFlags.None);
    }

    private static async Task<byte[]> ReadFrameAsync(Socket socket)
    {
        var lengthBuffer = await ReadExactAsync(socket, 4);
        var length = BinaryPrimitives.ReadUInt32BigEndian(lengthBuffer);
        return await ReadExactAsync(socket, (int)length);
    }

    private static async Task<byte[]> ReadExactAsync(Socket socket, int count)
    {
        var buffer = new byte[count];
        var received = 0;
        while (received < count)
        {
            var n = await socket.ReceiveAsync(buffer.AsMemory(received, count - received), SocketFlags.None);
            if (n == 0)
            {
                throw new IOException("injector connection closed");
            }

            received += n;
        }

        return buffer;
    }
}
