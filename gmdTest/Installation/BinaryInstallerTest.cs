using System.Net;
using System.Text;
using gmd.Installation;

namespace gmdTest.Installation;

// Downloading the binary of a release and putting it in place of the binary of the running gmd, in
// a temp folder standing in for the folder gmd is installed in. The download is answered by
// FakeHttp, or, for how HttpClient reads a slow or broken connection, by a LoopbackServer.
[TestClass]
public class BinaryInstallerTest
{
    const string Url = "https://github.com/x/v1.6.1444.870/gmd_linux_x64";
    const string FolderPrefix = "gmdTest-install-";

    string folder = "";
    string binaryPath = "";

    [TestInitialize]
    public void Init()
    {
        folder = Directory.CreateTempSubdirectory(FolderPrefix).FullName;
        binaryPath = Path.Join(folder, "gmd");
        File.WriteAllText(binaryPath, "old gmd");
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Only ever a folder this test made, in temp
        if (folder.StartsWith(Path.GetTempPath()) && Path.GetFileName(folder).StartsWith(FolderPrefix))
            Directory.Delete(folder, true);
    }

    [TestMethod]
    public async Task TestTheDownloadReplacesTheBinary()
    {
        var installer = NewInstaller(new FakeHttp(_ => FakeHttp.Text("new gmd")));

        AssertOk(await installer.InstallAsync(Url, "1.6.1444.870", binaryPath));

        Assert.AreEqual("new gmd", File.ReadAllText(binaryPath));
        Assert.AreEqual("", FilesNamed(".update_"), "the download is the binary now");
    }

    // The new binary runs as the one it replaces did. It was made so by 'chmod +x <path>' with the path
    // unquoted, which failed every update of a gmd installed in a folder with a space in its name.
    [TestMethod]
    public async Task TestTheNewBinaryIsExecutableInAFolderWithASpace()
    {
        if (OperatingSystem.IsWindows())
            return;
        var spaced = Path.Join(folder, "my apps");
        Directory.CreateDirectory(spaced);
        var path = Path.Join(spaced, "gmd");
        File.WriteAllText(path, "old gmd");
        var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead;
        File.SetUnixFileMode(path, mode | UnixFileMode.GroupExecute);
        var installer = NewInstaller(new FakeHttp(_ => FakeHttp.Text("new gmd")));

        AssertOk(await installer.InstallAsync(Url, "1.6.1444.870", path));

        Assert.AreEqual("new gmd", File.ReadAllText(path));
        Assert.AreEqual(mode | UnixFileMode.GroupExecute, File.GetUnixFileMode(path));
    }

    [TestMethod]
    public async Task TestAFailedDownloadLeavesTheBinaryAndNoFile()
    {
        var installer = NewInstaller(new FakeHttp(_ => FakeHttp.Status(HttpStatusCode.NotFound)));

        var e = AssertError(await installer.InstallAsync(Url, "1.6.1444.870", binaryPath));

        StringAssert.Contains(e.AllMessages(), "404");
        Assert.AreEqual("old gmd", File.ReadAllText(binaryPath));
        Assert.AreEqual("gmd", FilesNamed(""));
    }

    // A failed download used to be kept, and handed back to every later update of that version
    // until gmd was restarted, which then failed at once
    [TestMethod]
    public async Task TestAFailedDownloadIsDownloadedAgain()
    {
        var isConnected = false;
        var http = new FakeHttp(_ =>
            isConnected ? FakeHttp.Text("new gmd") : throw new HttpRequestException("No such host is known")
        );
        var installer = NewInstaller(http);
        AssertError(await installer.InstallAsync(Url, "1.6.1444.870", binaryPath));

        isConnected = true;
        AssertOk(await installer.InstallAsync(Url, "1.6.1444.870", binaryPath));

        Assert.AreEqual(2, http.Calls.Count);
        Assert.AreEqual("new gmd", File.ReadAllText(binaryPath));
    }

    [TestMethod]
    public async Task TestADownloadThatStopsComingIsGivenUp()
    {
        var http = new FakeHttp(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new NeverEndingStream()),
        });
        var path = Path.Join(folder, "gmd.update_ABCDE");

        var e = AssertError(await NewInstaller(http).DownloadAsync(Url, path, TimeSpan.FromMilliseconds(100)));

        StringAssert.Contains(e.Message, "nothing came for");
    }

    // A slow link: the whole download takes longer than the client's timeout, which is a limit on
    // getting the headers only, since there is no telling how long some 100 MB takes. With the body
    // read at once under that timeout, as before, this failed.
    [TestMethod, TestCategory("Integration")]
    public async Task TestASlowDownloadTakesAsLongAsItTakes()
    {
        const int Chunks = 10;
        const int ChunkSize = 1000;
        using var server = new LoopbackServer(
            async (stream, ct) =>
            {
                await LoopbackServer.WriteAsync(stream, LoopbackServer.Headers(Chunks * ChunkSize), ct);
                for (int i = 0; i < Chunks; i++)
                {
                    await Task.Delay(200, ct);
                    await LoopbackServer.WriteAsync(stream, new string('x', ChunkSize), ct);
                }
            }
        );
        var installer = new BinaryInstaller(LoopbackServer.Client(TimeSpan.FromSeconds(1)));
        var path = Path.Join(folder, "gmd.update_ABCDE");

        AssertOk(await installer.DownloadAsync(server.Url, path, TimeSpan.FromSeconds(5)));

        Assert.AreEqual(Chunks * ChunkSize, new FileInfo(path).Length);
    }

    [TestMethod, TestCategory("Integration")]
    public async Task TestAStalledDownloadIsGivenUp()
    {
        using var server = new LoopbackServer(
            async (stream, ct) =>
            {
                await LoopbackServer.WriteAsync(stream, LoopbackServer.Headers(2000), ct);
                await LoopbackServer.WriteAsync(stream, new string('x', 1000), ct);
                await Task.Delay(Timeout.Infinite, ct);
            }
        );
        var installer = new BinaryInstaller(LoopbackServer.Client(TimeSpan.FromSeconds(10)));
        var path = Path.Join(folder, "gmd.update_ABCDE");

        var e = AssertError(await installer.DownloadAsync(server.Url, path, TimeSpan.FromMilliseconds(500)));

        StringAssert.Contains(e.Message, "nothing came for");
    }

    // A connection that ends before all of the binary came is a failed download, not a short binary
    [TestMethod, TestCategory("Integration")]
    public async Task TestADownloadCutShortIsAnError()
    {
        using var server = new LoopbackServer(
            async (stream, ct) =>
            {
                await LoopbackServer.WriteAsync(stream, LoopbackServer.Headers(2000), ct);
                await LoopbackServer.WriteAsync(stream, new string('x', 1000), ct);
            }
        );
        var installer = new BinaryInstaller(LoopbackServer.Client(TimeSpan.FromSeconds(10)));

        var e = AssertError(await installer.InstallAsync(server.Url, "1.6.1444.870", binaryPath));

        StringAssert.Contains(e.AllMessages(), "Failed to download");
        Assert.AreEqual("old gmd", File.ReadAllText(binaryPath));
        Assert.AreEqual("gmd", FilesNamed(""));
    }

    // What an update left: the binary replaced, deleted at a later start, and a download a gmd ended
    // during, but not one another gmd is downloading right now
    [TestMethod]
    public void TestCleanUpDeletesWhatUpdatesLeft()
    {
        var now = DateTime.UtcNow;
        File.WriteAllText(binaryPath + ".tmp_ABCDE", "replaced gmd");
        File.WriteAllText(binaryPath + ".update_OLDER", "download left");
        File.SetLastWriteTimeUtc(binaryPath + ".update_OLDER", now - TimeSpan.FromDays(2));
        File.WriteAllText(binaryPath + ".update_FRESH", "download going on");
        File.WriteAllText(Path.Join(folder, "other"), "not gmd's");

        NewInstaller(new FakeHttp(_ => throw new InvalidOperationException())).CleanUp(binaryPath, now);

        Assert.AreEqual("gmd gmd.update_FRESH other", FilesNamed(""));
    }

    BinaryInstaller NewInstaller(FakeHttp http) => new(http);

    // The names of the files in the folder whose names contain the text, sorted
    string FilesNamed(string text) =>
        string.Join(
            " ",
            Directory.GetFiles(folder).Select(f => Path.GetFileName(f)).Where(n => n.Contains(text)).Order()
        );

    // A body that never comes, until the read is cancelled
    class NeverEndingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            await Task.Delay(Timeout.Infinite, ct);
            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
