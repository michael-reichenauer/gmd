using gmd.Server;
using gmd.Server.Private.Augmented.Private;

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

    public void Monitor(string workingFolder) { }

    public IDisposable Pause() => new NotPaused();

    public void SetReadRepoTime(DateTime time) { }

    public void SetReadStatusTime(DateTime time) { }

    class NotPaused : IDisposable
    {
        public void Dispose() { }
    }
}
