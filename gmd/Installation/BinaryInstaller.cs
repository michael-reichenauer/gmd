using System.Security.Cryptography;

namespace gmd.Installation;

// The binary of a release to install: its version, its asset name, where to download it, and where
// to download the release's checksums, "" for a release from before they were made
record ReleaseBinary(Version Version, string Name, string Url, string ChecksumsUrl);

interface IBinaryInstaller
{
    // Downloads the binary of a release, checks it, and puts it in place of the one at the target
    // path, the binary of the running gmd, which goes on running and is deleted at a later start
    // (CleanUp). Cancelled, it stops before the new binary is put in place, an UpdateCancelledError.
    Task<Result> InstallAsync(ReleaseBinary binary, string targetPath, CancellationToken cancel = default);

    // Whether a new binary can be put in place of the one at the target path, i.e. whether its folder
    // can be written to, which the download is
    Result CanInstall(string targetPath);

    // Deletes what earlier updates left beside the binary at the target path
    void CleanUp(string targetPath);
}

// Replaces the binary of the running gmd with that of a release. The download goes to a file of its
// own beside the binary, '<binary>.update_<random>', and is checked before it replaces anything: a
// binary that does not start is the one thing an update cannot repair, since it is the binary that
// updates. Its checksum is checked against the release's, and it is run, to say its version, as CI
// runs it before it releases. A download that fails either check is deleted, as one that did not
// complete is.
//
// It can be cancelled until the new binary is put in place, and not while it is: on Windows that is
// two renames, and stopped between them, there is no gmd at all.
class BinaryInstaller : IBinaryInstaller
{
    // How long a download may go on with nothing coming before it is given up on. A release is some
    // 100 MB, so the download as a whole has no time limit: over a slow link it takes many minutes.
    static readonly TimeSpan StallTimeout = TimeSpan.FromMinutes(1);

    // How old a download has to be before CleanUp takes it to be left by a gmd that ended during it,
    // rather than one that another gmd is still writing. Far longer than any download takes.
    static readonly TimeSpan StaleDownloadAge = TimeSpan.FromDays(1);

    // How long the new binary may take to say its version. Far longer than it takes, which is well
    // under a second, but the first start of a new program can wait for a virus scan of all of it.
    static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(2);

    // How often, and how far apart, a rename on Windows is tried before it is given up on
    const int MoveAttempts = 5;
    static readonly TimeSpan MoveRetryDelay = TimeSpan.FromMilliseconds(200);

    const string StagedInfix = ".update_"; // A download, '<binary>.update_<random>'
    const string AsideInfix = ".tmp"; // A binary replaced on Windows, '<binary>.tmp_<random>'
    const string OldDownloads = "gmd.*.RTXZERT"; // Downloads older versions made in the temp folder

    readonly ICmd cmd;
    readonly IHttp http;
    readonly Random random = new();

    internal BinaryInstaller(ICmd cmd, IHttp http)
    {
        this.cmd = cmd;
        this.http = http;
    }

    public async Task<Result> InstallAsync(ReleaseBinary binary, string targetPath, CancellationToken cancel = default)
    {
        // With '.exe' on Windows, where a program is run by its name, as the check below runs it
        var stagedPath = $"{targetPath}{StagedInfix}{RandomString(5)}{(Build.IsWindows ? ".exe" : "")}";
        try
        {
            if (await DownloadAsync(binary.Url, stagedPath, StallTimeout, cancel) is Error downloadError)
            {
                Log.Warn($"Failed to download new version {binary.Version}, {downloadError}");
                return downloadError;
            }

            if (cancel.IsCancellationRequested)
                return Cancelled(binary);
            if (await VerifyChecksumAsync(binary, stagedPath, cancel) is Error checksumError)
            {
                Log.Warn($"Failed to verify new version {binary.Version}, {checksumError}");
                return checksumError;
            }

            if (MakeExecutable(stagedPath, targetPath) is Error modeError)
            {
                Log.Warn($"Failed to make new version {binary.Version} executable, {modeError}");
                return new Error("Failed to make the new version executable", modeError);
            }

            if (await CheckRunsAsync(stagedPath, binary.Version, cancel) is Error runError)
            {
                Log.Warn($"Failed to run new version {binary.Version}, {runError}");
                return runError;
            }

            // The last moment it can be cancelled, see above
            if (cancel.IsCancellationRequested)
                return Cancelled(binary);
            if (await InstallAsync(stagedPath, targetPath, Build.IsWindows, MoveRetryDelay) is Error installError)
            {
                Log.Warn($"Failed to install new version {binary.Version}, {installError}");
                return installError;
            }

            return Result.Ok;
        }
        finally
        {
            // What is left of a download that failed, or that was not installed
            if (File.Exists(stagedPath) && Result.Catch(() => File.Delete(stagedPath)) is Error e)
                Log.Warn($"Failed to delete {stagedPath}, {e}");
        }
    }

    // Tried with a file named as a download is, '<binary>.update_<random>', deleted as it is closed,
    // and by CleanUp should gmd end before that
    public Result CanInstall(string targetPath)
    {
        var path = $"{targetPath}{StagedInfix}{RandomString(5)}";
        return Result.Catch(() =>
            new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1,
                FileOptions.DeleteOnClose
            ).Dispose()
        );
    }

    static UpdateCancelledError Cancelled(ReleaseBinary binary)
    {
        Log.Info($"Cancelled the update to {binary.Version}");
        return new UpdateCancelledError();
    }

    public void CleanUp(string targetPath) => CleanUp(targetPath, Path.GetTempPath(), DateTime.UtcNow, Build.IsWindows);

    // Deletes the binaries replaced on Windows, once they no longer run, a download that a gmd ended
    // during, once it is a day old, and the downloads older versions of gmd left in the temp folder,
    // '/tmp/gmd.<version>.RTXZERT'. Older versions also named the download they put beside the
    // binary '<binary>.tmp_<random>', which the first pattern deletes too.
    internal void CleanUp(string targetPath, string tempFolder, DateTime now, bool isWindows)
    {
        var comparison = isWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var name = Path.GetFileName(targetPath);
        var folder = Path.GetDirectoryName(targetPath) ?? "";
        if (folder == "" || Result.Catch(() => Directory.GetFiles(folder)) is not string[] paths)
            return;

        foreach (var path in paths)
        {
            var fileName = Path.GetFileName(path);
            var isReplaced = fileName.StartsWith(name + AsideInfix, comparison);
            var isStaleDownload =
                fileName.StartsWith(name + StagedInfix, comparison)
                && now - File.GetLastWriteTimeUtc(path) > StaleDownloadAge;
            if (isReplaced || isStaleDownload)
                Delete(path);
        }

        if (Result.Catch(() => Directory.GetFiles(tempFolder, OldDownloads)) is string[] oldDownloads)
        {
            foreach (var path in oldDownloads)
                Delete(path);
        }
    }

    // A replaced binary that still runs on Windows cannot be deleted, and is deleted at a later start
    static void Delete(string path)
    {
        Log.Info($"Deleting {path}");
        if (Result.Catch(() => File.Delete(path)) is Error e)
            Log.Info($"Failed to delete {path}, {e.Message}");
    }

    // Streams the file at the url to a new file at the path, for as long as it keeps coming, or until
    // cancelled
    internal async Task<Result> DownloadAsync(
        string url,
        string path,
        TimeSpan stallTimeout,
        CancellationToken cancel = default
    )
    {
        using var stalled = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        try
        {
            Log.Info($"Downloading {url} ...");
            var t = Timing.Start();

            // The client's timeout, 100 seconds, is how long it may take to get the headers. It is not
            // a limit on reading what follows them, which reading the whole of it at once had been,
            // so a download slower than 100 MB in 100 seconds, about 8 Mbit/s, always failed.
            using var response = await http.Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel);
            response.EnsureSuccessStatusCode();

            await using var source = await response.Content.ReadAsStreamAsync(cancel);
            await using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            var buffer = new byte[81920];
            long size = 0;
            while (true)
            {
                stalled.CancelAfter(stallTimeout); // Moved on by every read, i.e. the time since the last
                var count = await source.ReadAsync(buffer, stalled.Token);
                if (count == 0)
                    break;
                await target.WriteAsync(buffer.AsMemory(0, count), cancel);
                size += count;
            }

            Log.Info($"Downloaded {size} bytes to {path} {t}");
            return Result.Ok;
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
            Log.Info($"Cancelled the download of {url}");
            return new UpdateCancelledError();
        }
        catch (OperationCanceledException e) when (stalled.IsCancellationRequested)
        {
            Log.Warn($"Download of {url} stalled, nothing came for {stallTimeout}");
            return new Error($"The download stopped, nothing came for {stallTimeout.TotalSeconds:0} seconds", e);
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            Log.Exception(e, "Failed to download latest binary");
            return new Error("Failed to download latest binary", e);
        }
    }

    // The checksum of the download against the one CI made for the release, which install.sh checks
    // its download against too. A release from before the checksums were made has none, and the run
    // of the new binary is then the only check.
    async Task<Result> VerifyChecksumAsync(ReleaseBinary binary, string path, CancellationToken cancel)
    {
        if (binary.ChecksumsUrl == "")
        {
            Log.Warn($"No {ReleaseChoice.ChecksumsName} for {binary.Version}, so the download is not verified");
            return Result.Ok;
        }

        var sumsResult = await DownloadTextAsync(binary.ChecksumsUrl, cancel);
        if (sumsResult is UpdateCancelledError cancelled)
            return cancelled;
        if (sumsResult is not string sums)
            return new Error(
                $"Failed to download the {ReleaseChoice.ChecksumsName} of the new version",
                sumsResult.Error
            );

        if (ExpectedChecksum(sums, binary.Name) is not string expected)
            return new Error(
                $"There is no checksum of {binary.Name} in the {ReleaseChoice.ChecksumsName} of the new version"
            );

        var actualResult = Result.Catch(() =>
        {
            using var file = File.OpenRead(path);
            return Convert.ToHexStringLower(SHA256.HashData(file));
        });
        if (actualResult is not string actual)
            return new Error("Failed to read the download", actualResult.Error);

        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            return new Error($"The download of {binary.Name} does not match its checksum, so it was not installed");

        Log.Info($"Checksum verified, {binary.Name} {actual}");
        return Result.Ok;
    }

    async Task<Result<string>> DownloadTextAsync(string url, CancellationToken cancel)
    {
        try
        {
            return await http.Client.GetStringAsync(url, cancel);
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
            return new UpdateCancelledError();
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            return new Error($"Failed to download {url}", e);
        }
    }

    // The checksum of a file in what 'sha256sum' writes: a line '<checksum>  <name>' per file, with
    // a '*' before the name for one read as binary. Null when the file is not there.
    internal static string? ExpectedChecksum(string sums, string name) =>
        sums.Split('\n')
            .Select(line => line.Trim().Split(' ', 2))
            .Where(parts => parts.Length == 2 && parts[1].Trim().TrimStart('*') == name)
            .Select(parts => parts[0])
            .FirstOrDefault();

    // Runs the new binary to say its version, as 'gmd --version', which CI runs too before it
    // releases a binary. One that does not run here is not installed, and neither is one that is
    // another version than its release says, which would be offered again at every start.
    //
    // It runs with a home folder of its own, since gmd writes a config file when it finds none, and
    // not as an askpass, which Cmd makes every program it starts be, so that the git it runs can ask
    // this gmd for a passphrase: as one, it would answer '--version' by asking this gmd to show a
    // login dialog. (On Windows the home folder is not HOME, but there the config file is there.)
    internal async Task<Result> CheckRunsAsync(string path, Version version, CancellationToken cancel = default)
    {
        var homeResult = Result.Catch(() => Directory.CreateTempSubdirectory("gmd-update-").FullName);
        if (homeResult is not string home)
            return new Error("Failed to make a folder to run the new version in", homeResult.Error);
        try
        {
            var environment = new Dictionary<string, string> { ["HOME"] = home, [Askpass.Variable] = "" };
            var run = cmd.RunAsync(path, "--version", home, environment: environment);
            if (await Task.WhenAny(run, Task.Delay(RunTimeout, cancel)) != run)
            {
                if (cancel.IsCancellationRequested)
                    return new UpdateCancelledError();
                return new Error($"The new version did not say its version within {RunTimeout.TotalSeconds:0} seconds");
            }

            var runResult = await run;
            if (runResult is not string output)
                return new Error("The new version does not run here, so it was not installed", runResult.Error);

            var said = output.Split('\n').LastOrDefault(line => line.Trim() != "")?.Trim() ?? "";
            if (!Version.TryParse(said, out var saidVersion) || saidVersion != version)
                return new Error($"The new version says it is '{said}' rather than {version}, so it was not installed");

            Log.Info($"New version runs, and is {said}");
            return Result.Ok;
        }
        finally
        {
            if (Result.Catch(() => Directory.Delete(home, true)) is Error e)
                Log.Warn($"Failed to delete {home}, {e}");
        }
    }

    // Puts the new binary in place of the binary at the target path, which runs.
    //
    // On Linux and macOS in one rename over it, which is atomic, so there is a binary at the path at
    // every moment: the rename aside and back that Windows needs left a moment with none, and none at
    // all when the second rename failed. A gmd running from the old binary goes on running it, since
    // the file system keeps it until the gmd ends. A binary is never written into, which Linux refuses
    // ('text file busy'), and which makes macOS end the signed program running from it.
    //
    // Windows lets the file of a running program be renamed, though not replaced or deleted, so there
    // the running binary is renamed aside first, '<binary>.tmp_<random>', which CleanUp deletes at a
    // later start, and the new one renamed in its place. Should that fail, the old one is renamed
    // back, so that a failed update never leaves no gmd at all.
    //
    // Takes the platform, so that the tests run the Windows way too.
    internal async Task<Result> InstallAsync(string stagedPath, string targetPath, bool isWindows, TimeSpan retryDelay)
    {
        if (!isWindows)
        {
            if (Result.Catch(() => File.Move(stagedPath, targetPath, overwrite: true)) is Error replaceError)
                return new Error("Failed to put the new version in place", replaceError);
            Log.Info($"Installed {stagedPath} as {targetPath}");
            return Result.Ok;
        }

        var asidePath = $"{targetPath}{AsideInfix}_{RandomString(5)}";
        if (await MoveAsync(targetPath, asidePath, retryDelay) is Error asideError)
            return new Error("Failed to move the running version aside", asideError);
        Log.Info($"Moved {targetPath} => {asidePath}");

        if (await MoveAsync(stagedPath, targetPath, retryDelay) is Error installError)
        {
            if (await MoveAsync(asidePath, targetPath, retryDelay) is Error backError)
            {
                Log.Error($"Failed to move {asidePath} back to {targetPath}, {backError}");
                return new Error(
                    $"Failed to put the new version in place, and to move the old one back: rename {asidePath} to {Path.GetFileName(targetPath)}",
                    installError
                );
            }

            Log.Warn($"Moved {asidePath} back to {targetPath}");
            return new Error("Failed to put the new version in place", installError);
        }

        Log.Info($"Installed {stagedPath} as {targetPath}");
        return Result.Ok;
    }

    // A rename on Windows, tried again a few times, since a program that has just ended can hold its
    // file for a moment, and a virus scan of a new program can hold it too
    static async Task<Result> MoveAsync(string source, string target, TimeSpan retryDelay)
    {
        for (int attempt = 1; ; attempt++)
        {
            var result = Result.Catch(() => File.Move(source, target));
            if (result is not Error e || attempt == MoveAttempts || !IsHeld(e))
                return result;

            Log.Info($"Failed to move {source} => {target}, attempt {attempt}, {e.Message}");
            await Task.Delay(retryDelay);
        }
    }

    // A file that is there but held open, rather than not there at all
    static bool IsHeld(Error e) =>
        e.Exception is UnauthorizedAccessException or IOException
        && e.Exception is not (FileNotFoundException or DirectoryNotFoundException);

    string RandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
    }

    // With the mode of the binary it replaces, which runs, so that the new one runs too: a download is
    // made as a plain file, 0644 under the usual umask. Set directly rather than by 'chmod +x <path>',
    // whose path, unquoted, failed every update of a gmd in a folder with a space in its name.
    static Result MakeExecutable(string stagedPath, string targetPath)
    {
        if (OperatingSystem.IsWindows())
            return Result.Ok; // Windows runs a file by its name

        return Result.Catch(() =>
        {
            var mode = File.GetUnixFileMode(targetPath) | UnixFileMode.UserRead | UnixFileMode.UserExecute;
            File.SetUnixFileMode(stagedPath, mode);
        });
    }
}
