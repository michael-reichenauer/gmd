using System.Security.Cryptography;

namespace gmd.Installation;

// The binary of a release to install: its version, its asset name, where to download it, and where
// to download the release's checksums, "" for a release from before they were made
record ReleaseBinary(Version Version, string Name, string Url, string ChecksumsUrl);

interface IBinaryInstaller
{
    // Downloads the binary of a release, checks it, and puts it in place of the one at the target
    // path, the binary of the running gmd, which goes on running and is deleted at a later start
    // (CleanUp)
    Task<Result> InstallAsync(ReleaseBinary binary, string targetPath);

    // Deletes what earlier updates left beside the binary at the target path
    void CleanUp(string targetPath);
}

// Replaces the binary of the running gmd with that of a release. The download goes to a file of its
// own beside the binary, '<binary>.update_<random>', and is checked before it replaces anything: a
// binary that does not start is the one thing an update cannot repair, since it is the binary that
// updates. Its checksum is checked against the release's, and it is run, to say its version, as CI
// runs it before it releases. A download that fails either check is deleted, as one that did not
// complete is.
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

    const string StagedInfix = ".update_";

    readonly ICmd cmd;
    readonly IHttp http;
    readonly Random random = new();

    internal BinaryInstaller(ICmd cmd, IHttp http)
    {
        this.cmd = cmd;
        this.http = http;
    }

    public async Task<Result> InstallAsync(ReleaseBinary binary, string targetPath)
    {
        // With '.exe' on Windows, where a program is run by its name, as the check below runs it
        var stagedPath = $"{targetPath}{StagedInfix}{RandomString(5)}{(Build.IsWindows ? ".exe" : "")}";
        try
        {
            if (await DownloadAsync(binary.Url, stagedPath, StallTimeout) is Error downloadError)
            {
                Log.Warn($"Failed to download new version {binary.Version}, {downloadError}");
                return downloadError;
            }

            if (await VerifyChecksumAsync(binary, stagedPath) is Error checksumError)
            {
                Log.Warn($"Failed to verify new version {binary.Version}, {checksumError}");
                return checksumError;
            }

            if (MakeExecutable(stagedPath, targetPath) is Error modeError)
            {
                Log.Warn($"Failed to make new version {binary.Version} executable, {modeError}");
                return new Error("Failed to make the new version executable", modeError);
            }

            if (await CheckRunsAsync(stagedPath, binary.Version) is Error runError)
            {
                Log.Warn($"Failed to run new version {binary.Version}, {runError}");
                return runError;
            }

            if (Install(stagedPath, targetPath) is Error installError)
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

    public void CleanUp(string targetPath) => CleanUp(targetPath, DateTime.UtcNow);

    internal void CleanUp(string targetPath, DateTime now)
    {
        try
        {
            var tmpPathPrefix = targetPath + ".tmp";
            var stagedPathPrefix = targetPath + StagedInfix;

            foreach (var path in Directory.GetFiles(Path.GetDirectoryName(targetPath) ?? ""))
            {
                var isStale =
                    path.StartsWith(stagedPathPrefix) && now - File.GetLastWriteTimeUtc(path) > StaleDownloadAge;
                if (path.StartsWith(tmpPathPrefix) || isStale)
                {
                    Log.Info($"Deleting {path}");
                    if (Result.Catch(() => File.Delete(path)) is Error e)
                        Log.Info($"Failed to delete {e}");
                }
            }
        }
        catch (Exception e)
        {
            Log.Info($"Failed to clean {e.Message}");
        }
    }

    // Streams the file at the url to a new file at the path, for as long as it keeps coming
    internal async Task<Result> DownloadAsync(string url, string path, TimeSpan stallTimeout)
    {
        using var stalled = new CancellationTokenSource();
        try
        {
            Log.Info($"Downloading {url} ...");
            var t = Timing.Start();

            // The client's timeout, 100 seconds, is how long it may take to get the headers. It is not
            // a limit on reading what follows them, which reading the whole of it at once had been,
            // so a download slower than 100 MB in 100 seconds, about 8 Mbit/s, always failed.
            using var response = await http.Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            await using var source = await response.Content.ReadAsStreamAsync();
            await using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            var buffer = new byte[81920];
            long size = 0;
            while (true)
            {
                stalled.CancelAfter(stallTimeout); // Moved on by every read, i.e. the time since the last
                var count = await source.ReadAsync(buffer, stalled.Token);
                if (count == 0)
                    break;
                await target.WriteAsync(buffer.AsMemory(0, count));
                size += count;
            }

            Log.Info($"Downloaded {size} bytes to {path} {t}");
            return Result.Ok;
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
    async Task<Result> VerifyChecksumAsync(ReleaseBinary binary, string path)
    {
        if (binary.ChecksumsUrl == "")
        {
            Log.Warn($"No {ReleaseChoice.ChecksumsName} for {binary.Version}, so the download is not verified");
            return Result.Ok;
        }

        var sumsResult = await DownloadTextAsync(binary.ChecksumsUrl);
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

    async Task<Result<string>> DownloadTextAsync(string url)
    {
        try
        {
            return await http.Client.GetStringAsync(url);
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
    internal async Task<Result> CheckRunsAsync(string path, Version version)
    {
        var homeResult = Result.Catch(() => Directory.CreateTempSubdirectory("gmd-update-").FullName);
        if (homeResult is not string home)
            return new Error("Failed to make a folder to run the new version in", homeResult.Error);
        try
        {
            var environment = new Dictionary<string, string> { ["HOME"] = home, [Askpass.Variable] = "" };
            var run = cmd.RunAsync(path, "--version", home, environment: environment);
            if (await Task.WhenAny(run, Task.Delay(RunTimeout)) != run)
                return new Error($"The new version did not say its version within {RunTimeout.TotalSeconds:0} seconds");

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

    Result Install(string stagedPath, string targetPath)
    {
        try
        {
            var newThisPath = GetTempPath(targetPath);

            File.Move(targetPath, newThisPath);
            Log.Info($"Moved {targetPath} => {newThisPath}");
            File.Move(stagedPath, targetPath);
            Log.Info($"Installed {stagedPath}");
            return Result.Ok;
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            Log.Exception(e, "Failed install new file");
            return new Error("Failed to install new file", e);
        }
    }

    string GetTempPath(string targetPath) => $"{targetPath}.tmp_{RandomString(5)}";

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
