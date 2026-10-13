using System.Net;
using System.Net.Sockets;
using System.Text;
using gmd.Installation;

namespace gmdTest.Installation;

// An HTTP server on the loopback interface, answering every request as the test scripts it, down to
// how fast the body comes and whether it ends early. FakeHttp cannot do that: there it is the
// test's own stream that is read, and what is under test here is how HttpClient reads a connection.
//
//     using var server = new LoopbackServer(async (stream, ct) =>
//     {
//         await LoopbackServer.WriteAsync(stream, LoopbackServer.Headers(contentLength: 1000));
//         ...
//     });
//     await installer.DownloadAsync(server.Url, path, stallTimeout);
sealed class LoopbackServer : IDisposable
{
    readonly TcpListener listener = new(IPAddress.Loopback, 0);
    readonly CancellationTokenSource stopped = new();
    readonly Func<Stream, CancellationToken, Task> respond;

    public LoopbackServer(Func<Stream, CancellationToken, Task> respond)
    {
        this.respond = respond;
        listener.Start();
        _ = ServeAsync(); // Ends when the server is disposed
    }

    public string Url => $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/gmd";

    // The headers of a response with a body of the length given, which the client reads up to
    public static string Headers(int contentLength) =>
        $"HTTP/1.1 200 OK\r\nContent-Length: {contentLength}\r\nConnection: close\r\n\r\n";

    public static async Task WriteAsync(Stream stream, string text, CancellationToken ct = default)
    {
        await stream.WriteAsync(Encoding.ASCII.GetBytes(text), ct);
        await stream.FlushAsync(ct);
    }

    // An HttpClient of its own, with the timeout given, as the updater's is but for its length
    public static IHttp Client(TimeSpan timeout) => new ClientHttp(new HttpClient { Timeout = timeout });

    public void Dispose()
    {
        stopped.Cancel();
        listener.Stop();
    }

    async Task ServeAsync()
    {
        while (!stopped.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(stopped.Token);
            }
            catch (Exception e) when (e is OperationCanceledException or SocketException or ObjectDisposedException)
            {
                return;
            }

            _ = Task.Run(() => AnswerAsync(client));
        }
    }

    async Task AnswerAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                await ReadRequestAsync(stream);
                await respond(stream, stopped.Token);
            }
            catch (Exception e) when (e is IOException or OperationCanceledException or SocketException)
            {
                // The client closed the connection, or the server was stopped
            }
        }
    }

    // Up to the empty line that ends the headers; a GET has no body
    static async Task ReadRequestAsync(Stream stream)
    {
        var request = new StringBuilder();
        var buffer = new byte[1];
        while (!request.ToString().EndsWith("\r\n\r\n") && await stream.ReadAsync(buffer) == 1)
            request.Append((char)buffer[0]);
    }

    record ClientHttp(HttpClient Client) : IHttp;
}
