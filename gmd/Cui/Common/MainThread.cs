namespace gmd.Cui.Common;

// The UI side of IMainThread, i.e. the one place where a layer below the UI reaches the
// Terminal.Gui main loop. See IMainThread.
class MainThread : IMainThread
{
    public void Post(Action action) => UI.Post(action);

    // Before the UI runs there is no main loop to call back on, and nothing to call back for: a
    // repo read before it, as by a command line command, ends gmd once it has, and the file monitor
    // asks for its tick as it reads. Terminal.Gui 1.x did nothing then too, where 2.x's
    // application, an instance, is not there to ask.
    public void RunPeriodically(TimeSpan interval, Func<bool> callback)
    {
        if (UI.HasApp)
            UI.AddTimeout(interval, callback);
    }
}
