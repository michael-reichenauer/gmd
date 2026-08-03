using IOPath = System.IO.Path;

namespace gmdTest.Fixtures;

// A throwaway $HOME for a gmd run, and the whole of the hermeticity story for the end-to-end
// tests.
//
// gmd has no way to redirect where it keeps its state: the paths in ConfigService, ConfigLogger
// and Upgrader are all anchored on SpecialFolder.UserProfile with no flag, env var or setting to
// override them. On Unix that resolves $HOME, so pointing HOME at a temp folder is the only way
// to run gmd without writing to the developer's home. It matters more than it sounds, because a
// gmd run does not merely read:
//
//   ~/.gmdconfig    written on every run (GitVersion, and the opened repo into RecentFolders)
//   ~/gmd.log       TRUNCATED on every start
//   ~/.gmdstate*    DELETED on every start
//
// Redirecting HOME also isolates ~/.gitconfig from the git commands gmd runs, which is the same
// isolation TempRepo gets by setting its config locally.
//
// Nothing outside the temp folder is ever touched, and Dispose refuses to delete a path it did
// not create.
sealed class TempHome : IDisposable
{
    // Both the temp folder name and the guard in Dispose, i.e. only folders named like this are
    // ever deleted
    const string FolderPrefix = "gmdTest-home-";

    // Seeded so the updater never runs. Not optional: RepoView starts the update checker on
    // every startup, and Build.IsDevInstance() is false for the apphost these tests drive (it
    // only recognizes 'gmd.dll' and 'dotnet'), so without this the built binary really does call
    // the GitHub releases API. That is a network dependency in CI, and a released version newer
    // than the test build would put a '⇓' in the application bar and extra items in the repo
    // menu, mid-test.
    const string ConfigJson = """
        {
          "CheckUpdates": false,
          "AutoUpdate": false
        }
        """;

    TempHome(string path) => Path = path;

    // The folder to point HOME at
    public string Path { get; }

    public static TempHome Create()
    {
        var path = IOPath.Join(IOPath.GetTempPath(), $"{FolderPrefix}{Guid.NewGuid():N}");
        var home = new TempHome(path);
        home.Init();
        return home;
    }

    // What gmd logged during the run. The log is the first thing to look at when a screen is not
    // what a test expected, since unhandled exceptions are written there rather than to the screen.
    public string LogTail(int lines = 40)
    {
        var path = IOPath.Join(Path, "gmd.log");
        if (!File.Exists(path))
            return "(no gmd.log)";

        // The log is written by another process that may still hold it open
        if (!Try(out var text, out var _, () => File.ReadAllText(path)))
            return "(gmd.log could not be read)";

        return string.Join('\n', text.Split('\n').TakeLast(lines));
    }

    public void Dispose()
    {
        if (!Path.StartsWith(IOPath.GetTempPath()) || !IOPath.GetFileName(Path).StartsWith(FolderPrefix))
            throw new InvalidOperationException($"Refusing to delete '{Path}', it is not a temp home folder");

        // A failed cleanup should not fail a test, the folder is in temp and will be cleaned by
        // the system eventually
        if (Directory.Exists(Path) && !Try(out var e, () => Directory.Delete(Path, true)))
            Log.Warn($"Failed to delete temp home '{Path}', {e}");
    }

    void Init()
    {
        // All three have to exist before gmd starts: ConfigLogger's static constructor writes
        // the log file and fails fast if it cannot
        Directory.CreateDirectory(Path);
        Directory.CreateDirectory(IOPath.Join(Path, ".config"));
        Directory.CreateDirectory(IOPath.Join(Path, "tmp"));

        File.WriteAllText(IOPath.Join(Path, ".gmdconfig"), ConfigJson);
    }
}
