using gmd.Server;
using gmd.Server.Private;

namespace gmdTest.Fixtures;

// A double for IFileMonitor, which watches the working folder and raises change events. Tests
// drive the pipeline directly, so nothing is watched and no events are ever raised.
class FakeFileMonitor : IFileMonitor
{
    public event Action<ChangeEvent>? FileChanged
    {
        add { }
        remove { }
    }

    public event Action<ChangeEvent>? RepoChanged
    {
        add { }
        remove { }
    }

    public void Monitor(string workingFolder, IReadOnlyList<string> excludedFolders) { }

    // How many times the monitor was paused, i.e. how many writes held it, and whether it is now
    public int PauseCount { get; private set; }
    public bool IsPaused => pauses > 0;
    int pauses;

    public IDisposable Pause()
    {
        PauseCount++;
        pauses++;
        return new Disposable(() => pauses--);
    }

    public void ReadingRepo() { }

    public void ReadingStatus() { }
}
