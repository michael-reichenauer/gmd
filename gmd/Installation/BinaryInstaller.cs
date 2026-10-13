namespace gmd.Installation;

interface IBinaryInstaller
{
    // Downloads the binary of a release and puts it in place of the one at the target path, the
    // binary of the running gmd, which goes on running and is deleted at a later start (CleanUp)
    Task<Result> InstallAsync(string url, string version, string targetPath);

    // Deletes what earlier updates left beside the binary at the target path
    void CleanUp(string targetPath);
}

// cSpell:ignore RTXZERT
class BinaryInstaller : IBinaryInstaller
{
    const string tmpRandomSuffix = "RTXZERT";

    readonly ICmd cmd;
    readonly IHttp http;
    readonly Random random = new();

    // Data for download binary tasks to avoid multiple parallel tasks
    static string requestingUri = "";
    static Task<byte[]>? getBytesTask = null;

    internal BinaryInstaller(ICmd cmd, IHttp http)
    {
        this.cmd = cmd;
        this.http = http;
    }

    public async Task<Result> InstallAsync(string url, string version, string targetPath)
    {
        var downloadedPathResult = await DownloadBinaryAsync(url, version);
        if (downloadedPathResult is not string downloadedPath)
        {
            Log.Warn($"Failed to download new version, {downloadedPathResult.Error}");
            return downloadedPathResult.Error;
        }

        if (Install(downloadedPath, targetPath) is Error installError)
        {
            Log.Warn($"Failed to install new version, {installError}");
            return installError;
        }

        return Result.Ok;
    }

    public void CleanUp(string targetPath)
    {
        try
        {
            var tmpPathPrefix = targetPath + ".tmp";

            foreach (var path in Directory.GetFiles(Path.GetDirectoryName(targetPath) ?? ""))
            {
                if (path.StartsWith(tmpPathPrefix))
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

    Result Install(string downloadedPath, string targetPath)
    {
        try
        {
            // Move downloaded file next to this process file (replace must be on same volume)
            var newPath = GetTempPath(targetPath);
            if (!File.Exists(downloadedPath))
            {
                Log.Info($"No new file to install {downloadedPath}");
                return Result.Ok; // No new file to install, some other thread already installed it
            }

            File.Move(downloadedPath, newPath);
            Log.Info($"Move {downloadedPath} => {newPath} ...");
            if (MakeBinaryExecutable(newPath) is Error e)
                return e;

            var newThisPath = GetTempPath(targetPath);

            File.Move(targetPath, newThisPath);
            Log.Info($"Moved {targetPath} => {newThisPath}");
            File.Move(newPath, targetPath);
            Log.Info($"Installed {newPath}");
            return Result.Ok;
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            Log.Exception(e, "Failed install new file");
            return new Error("Failed to install new file", e);
        }
    }

    async Task<Result<string>> DownloadBinaryAsync(string downloadUrl, string version)
    {
        try
        {
            var targetPath = GetDownloadFilePath(version);
            if (File.Exists(targetPath))
            {
                Log.Info($"Already downloaded {targetPath}");
                return targetPath;
            }

            byte[] remoteFileData = await GetByteArrayAsync(http.Client, downloadUrl);

            File.WriteAllBytes(targetPath, remoteFileData);

            Log.Info($"Downloaded to    {targetPath}");
            return targetPath;
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            Log.Exception(e, "Failed to download latest binary");
            return new Error("Failed to download latest binary", e);
        }
    }

    static Task<byte[]> GetByteArrayAsync(HttpClient httpClient, string requestUri)
    {
        if (requestingUri == requestUri && getBytesTask != null)
        { // A request for this uri has already been started, lets reuse task
            Log.Info($"Download already started for {requestUri}");
            return getBytesTask;
        }

        // Start download task and remember task in case multiple requests for same request
        Log.Info($"Downloading from {requestUri} ...");
        requestingUri = requestUri;
        getBytesTask = httpClient.GetByteArrayAsync(requestUri);
        return getBytesTask;
    }

    static string GetDownloadFilePath(string version)
    {
        var name = $"gmd.{version}.{tmpRandomSuffix}";
        var tmpFolderPath = Path.GetTempPath();
        return Path.Combine(tmpFolderPath, name);
    }

    string GetTempPath(string targetPath) => $"{targetPath}.tmp_{RandomString(5)}";

    string RandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
    }

    Result MakeBinaryExecutable(string path)
    {
        if (Build.IsWindows)
            return Result.Ok; // Not needed on windows

        return cmd.Command("chmod", $"+x {path}", "");
    }
}
