using System.Net;
using System.Security.Cryptography;
using System.Text;
using gmd.Installation;
using gmdTest.Utils;

namespace gmdTest.Installation;

// Downloading the binary of a release, checking it, and putting it in place of the binary of the
// running gmd, in a temp folder standing in for the folder gmd is installed in. The download is
// answered by FakeHttp, or, for how HttpClient reads a slow or broken connection, by a
// LoopbackServer, and the new binary's answer when it is run by FakeCmd, or, for what really runs,
// by a shell script standing in for it.
[TestClass]
public class BinaryInstallerTest
{
    const string Url = "https://github.com/x/v1.6.1444.870/gmd_linux_x64";
    const string SumsUrl = "https://github.com/x/v1.6.1444.870/SHA256SUMS";
    const string FolderPrefix = "gmdTest-install-";
    static readonly Version NewVersion = new("1.6.1444.870");
    static readonly ReleaseBinary Binary = new(NewVersion, "gmd_linux_x64", Url, SumsUrl);

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
        var installer = new BinaryInstaller(Runs("1.6.1444.870"), Release("new gmd"));

        AssertOk(await installer.InstallAsync(Binary, binaryPath));

        Assert.AreEqual("new gmd", File.ReadAllText(binaryPath));
        Assert.AreEqual("", FilesNamed(".update_"), "the download is the binary now");
    }

    // Run as 'gmd --version', which every gmd release says, with a home of its own and not as an
    // askpass, which Cmd makes every program it runs be: as one, it would ask this gmd to show a
    // login dialog asking '--version'
    [TestMethod]
    public async Task TestTheNewBinaryIsRunToSayItsVersion()
    {
        var cmd = Runs("1.6.1444.870");

        AssertOk(await new BinaryInstaller(cmd, Release("new gmd")).InstallAsync(Binary, binaryPath));

        var run = cmd.Calls.Single();
        StringAssert.StartsWith(Path.GetFileName(run.Path), "gmd.update_");
        Assert.AreEqual("--version", run.Args);
        Assert.AreEqual("", run.Environment![Askpass.Variable]);
        Assert.AreNotEqual(Environment.GetEnvironmentVariable("HOME"), run.Environment["HOME"]);
        Assert.AreEqual(run.WorkingDirectory, run.Environment["HOME"]);
        Assert.IsFalse(Directory.Exists(run.WorkingDirectory), "its home is deleted after");
    }

    // A download that is not the binary of the release, e.g. one cut short, would replace gmd with a
    // file that does not start, which no update can repair
    [TestMethod]
    public async Task TestADownloadThatDoesNotMatchItsChecksumIsNotInstalled()
    {
        var cmd = Runs("1.6.1444.870");
        var installer = new BinaryInstaller(cmd, Release("new gmd", sums: Sums("another gmd")));

        var e = AssertError(await installer.InstallAsync(Binary, binaryPath));

        StringAssert.Contains(e.Message, "does not match its checksum");
        AssertNotInstalled();
        Assert.AreEqual(0, cmd.Calls.Count, "a download that does not match is not run");
    }

    [TestMethod]
    public async Task TestABinaryWithNoChecksumIsNotInstalled()
    {
        var sums = $"{Sha256("new gmd")}  gmd_osx_arm64\n";
        var installer = new BinaryInstaller(Runs("1.6.1444.870"), Release("new gmd", sums));

        var e = AssertError(await installer.InstallAsync(Binary, binaryPath));

        StringAssert.Contains(e.Message, "There is no checksum of gmd_linux_x64");
        AssertNotInstalled();
    }

    // The releases from before 0.97 have no checksums, and are checked by running them only
    [TestMethod]
    public async Task TestAReleaseWithNoChecksumsIsInstalled()
    {
        var installer = new BinaryInstaller(Runs("1.6.1444.870"), Release("new gmd"));

        AssertOk(await installer.InstallAsync(Binary with { ChecksumsUrl = "" }, binaryPath));

        Assert.AreEqual("new gmd", File.ReadAllText(binaryPath));
    }

    // A binary of another version than its release would be offered again at every start
    [TestMethod]
    public async Task TestABinaryOfAnotherVersionIsNotInstalled()
    {
        var installer = new BinaryInstaller(Runs("1.6.1444.518"), Release("new gmd"));

        var e = AssertError(await installer.InstallAsync(Binary, binaryPath));

        StringAssert.Contains(e.Message, "says it is '1.6.1444.518' rather than 1.6.1444.870");
        AssertNotInstalled();
    }

    [TestMethod]
    public async Task TestABinaryThatFailsToRunIsNotInstalled()
    {
        var cmd = new FakeCmd((_, _, _) => FakeCmd.Fail("Exec format error"));
        var installer = new BinaryInstaller(cmd, Release("new gmd"));

        var e = AssertError(await installer.InstallAsync(Binary, binaryPath));

        StringAssert.Contains(e.AllMessages(), "does not run here");
        StringAssert.Contains(e.AllMessages(), "Exec format error");
        AssertNotInstalled();
    }

    [TestMethod]
    public void TestTheChecksumOfAFile()
    {
        var sums = "aaa  gmd_linux_x64\r\nbbb *gmd.exe\nccc  gmd_linux\n";

        Assert.AreEqual("aaa", BinaryInstaller.ExpectedChecksum(sums, "gmd_linux_x64"));
        Assert.AreEqual("bbb", BinaryInstaller.ExpectedChecksum(sums, "gmd.exe"));
        Assert.AreEqual("ccc", BinaryInstaller.ExpectedChecksum(sums, "gmd_linux"));
        Assert.IsNull(BinaryInstaller.ExpectedChecksum(sums, "gmd_osx_arm64"));
        Assert.IsNull(BinaryInstaller.ExpectedChecksum("", "gmd_linux"));
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
        var installer = new BinaryInstaller(Runs("1.6.1444.870"), Release("new gmd"));

        AssertOk(await installer.InstallAsync(Binary, path));

        Assert.AreEqual("new gmd", File.ReadAllText(path));
        Assert.AreEqual(mode | UnixFileMode.GroupExecute, File.GetUnixFileMode(path));
    }

    [TestMethod]
    public async Task TestAFailedDownloadLeavesTheBinaryAndNoFile()
    {
        var installer = new BinaryInstaller(
            Runs("1.6.1444.870"),
            new FakeHttp(_ => FakeHttp.Status(HttpStatusCode.NotFound))
        );

        var e = AssertError(await installer.InstallAsync(Binary, binaryPath));

        StringAssert.Contains(e.AllMessages(), "404");
        AssertNotInstalled();
    }

    // A failed download used to be kept, and handed back to every later update of that version
    // until gmd was restarted, which then failed at once
    [TestMethod]
    public async Task TestAFailedDownloadIsDownloadedAgain()
    {
        var isConnected = false;
        var http = new FakeHttp(request =>
            isConnected
                ? ReleaseAnswer(request, "new gmd", Sums("new gmd"))
                : throw new HttpRequestException("No such host is known")
        );
        var installer = new BinaryInstaller(Runs("1.6.1444.870"), http);
        AssertError(await installer.InstallAsync(Binary, binaryPath));

        isConnected = true;
        AssertOk(await installer.InstallAsync(Binary, binaryPath));

        Assert.AreEqual(3, http.Calls.Count, "the binary twice and the checksums");
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
        var installer = new BinaryInstaller(Runs("1.6.1444.870"), http);

        var e = AssertError(await installer.DownloadAsync(Url, path, TimeSpan.FromMilliseconds(100)));

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
        var installer = new BinaryInstaller(Runs("1.6.1444.870"), LoopbackServer.Client(TimeSpan.FromSeconds(1)));
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
        var installer = new BinaryInstaller(Runs("1.6.1444.870"), LoopbackServer.Client(TimeSpan.FromSeconds(10)));
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
        var installer = new BinaryInstaller(Runs("1.6.1444.870"), LoopbackServer.Client(TimeSpan.FromSeconds(10)));

        var e = AssertError(await installer.InstallAsync(Binary with { Url = server.Url }, binaryPath));

        StringAssert.Contains(e.AllMessages(), "Failed to download");
        AssertNotInstalled();
    }

    // What really runs: a script standing in for the new binary, which says its version only when it
    // is run as gmd is to be, not as an askpass and with a home of its own
    [TestMethod, TestCategory("Integration")]
    public async Task TestANewBinaryThatRunsIsInstalled()
    {
        if (OperatingSystem.IsWindows())
            return;
        var script = $"""
            #!/bin/sh
            [ "$GMD_ASKPASS" = "1" ] && echo "run as an askpass" >&2 && exit 3
            [ "$HOME" = "{Environment.GetEnvironmentVariable("HOME")}" ] && echo "run in the real home" >&2 && exit 4
            echo 1.6.1444.870

            """;
        var installer = new BinaryInstaller(new Cmd(), Release(script));

        AssertOk(await installer.InstallAsync(Binary, binaryPath));

        Assert.AreEqual(script, File.ReadAllText(binaryPath));
    }

    [TestMethod, TestCategory("Integration")]
    public async Task TestANewBinaryThatDoesNotRunHereIsNotInstalled()
    {
        if (OperatingSystem.IsWindows())
            return;
        var installer = new BinaryInstaller(new Cmd(), Release("not a program for this machine\n"));

        var e = AssertError(await installer.InstallAsync(Binary, binaryPath));

        StringAssert.Contains(e.AllMessages(), "does not run here");
        AssertNotInstalled();
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

        new BinaryInstaller(Runs(""), new FakeHttp(_ => throw new InvalidOperationException())).CleanUp(
            binaryPath,
            now
        );

        Assert.AreEqual("gmd gmd.update_FRESH other", FilesNamed(""));
    }

    // The binary as it was, and nothing left of the download
    void AssertNotInstalled()
    {
        Assert.AreEqual("old gmd", File.ReadAllText(binaryPath));
        Assert.AreEqual("gmd", FilesNamed(""));
    }

    // GitHub with the release: the binary, and the checksums, of that binary unless others are given
    static FakeHttp Release(string binary, string? sums = null) =>
        new(request => ReleaseAnswer(request, binary, sums ?? Sums(binary)));

    static HttpResponseMessage ReleaseAnswer(HttpRequestMessage request, string binary, string sums) =>
        request.RequestUri?.ToString() == SumsUrl ? FakeHttp.Text(sums) : FakeHttp.Text(binary);

    // The new binary, as what it says when it is run
    static FakeCmd Runs(string output) => new(output);

    static string Sums(string binary) => $"{Sha256("other")}  gmd_osx_arm64\n{Sha256(binary)}  gmd_linux_x64\n";

    static string Sha256(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

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
