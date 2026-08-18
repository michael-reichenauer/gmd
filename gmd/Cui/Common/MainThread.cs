namespace gmd.Cui.Common;

// The UI side of IMainThread, i.e. the one place where a layer below the UI reaches the
// Terminal.Gui main loop. See IMainThread.
class MainThread : IMainThread
{
    public void Post(Action action) => UI.Post(action);

    public void RunPeriodically(TimeSpan interval, Func<bool> callback) => UI.AddTimeout(interval, _ => callback());
}
