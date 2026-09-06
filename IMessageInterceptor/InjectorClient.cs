using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text.Json;

namespace IMessage;

internal sealed class InjectorClient
{
    private const string SocketPath = "/tmp/gamepigeonfucker-injector.sock";

    public async Task SendAsync(string chatGuid, string text, string? balloonBundleId, byte[]? payloadData)
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(SocketPath));

        var request = new
        {
            chatGuid,
            text,
            balloonBundleId,
            payloadDataBase64 = payloadData is null ? null : Convert.ToBase64String(payloadData),
        };

        await WriteFrameAsync(socket, JsonSerializer.SerializeToUtf8Bytes(request));

        var responseBytes = await ReadFrameAsync(socket);
        using var document = JsonDocument.Parse(responseBytes);
        if (!document.RootElement.GetProperty("ok").GetBoolean())
        {
            var error = document.RootElement.TryGetProperty("error", out var errorElement)
                ? errorElement.GetString()
                : "unknown error";
            throw new InvalidOperationException($"injector send failed: {error}");
        }
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
