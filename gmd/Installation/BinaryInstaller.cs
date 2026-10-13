namespace gmd.Installation;

interface IBinaryInstaller
{
    // Downloads the binary of a release and puts it in place of the one at the target path, the
    // binary of the running gmd, which goes on running and is deleted at a later start (CleanUp)
    Task<Result> InstallAsync(string url, string version, string targetPath);

    // Deletes what earlier updates left beside the binary at the target path
    void CleanUp(string targetPath);
}

// Replaces the binary of the running gmd with that of a release. The download goes to a file of its
// own beside the binary, '<binary>.update_<random>', which is in place of the binary once it is
// complete; the file of a download that did not complete is deleted.
class BinaryInstaller : IBinaryInstaller
{
    // How long a download may go on with nothing coming before it is given up on. A release is some
    // 100 MB, so the download as a whole has no time limit: over a slow link it takes many minutes.
    static readonly TimeSpan StallTimeout = TimeSpan.FromMinutes(1);

    // How old a download has to be before CleanUp takes it to be left by a gmd that ended during it,
    // rather than one that another gmd is still writing. Far longer than any download takes.
    static readonly TimeSpan StaleDownloadAge = TimeSpan.FromDays(1);

    const string StagedInfix = ".update_";

    readonly IHttp http;
    readonly Random random = new();

    internal BinaryInstaller(IHttp http)
    {
        this.http = http;
    }

    public async Task<Result> InstallAsync(string url, string version, string targetPath)
    {
        var stagedPath = $"{targetPath}{StagedInfix}{RandomString(5)}";
        try
        {
            if (await DownloadAsync(url, stagedPath, StallTimeout) is Error downloadError)
            {
                Log.Warn($"Failed to download new version {version}, {downloadError}");
                return downloadError;
            }

            if (Install(stagedPath, targetPath) is Error installError)
            {
                Log.Warn($"Failed to install new version {version}, {installError}");
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

    Result Install(string stagedPath, string targetPath)
    {
        try
        {
            if (MakeExecutable(stagedPath, targetPath) is Error e)
                return e;

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
