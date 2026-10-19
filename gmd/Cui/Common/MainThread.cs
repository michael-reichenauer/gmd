namespace gmd.Cui.Common;

// The UI side of IMainThread, i.e. the one place where a layer below the UI reaches the
// Terminal.Gui main loop. See IMainThread.
class MainThread : IMainThread
{
    public void Post(Action action) => UI.Post(action);

    // Before the UI runs there is no main loop to call back on, and nothing to call back for: the
    // command line commands that read a repo (ProgramCommands, e.g. CI's 'gmd --updatechangelog')
    // end gmd once they have, and the file monitor asks for its tick as they read. Terminal.Gui
    // 1.x did nothing then too, where 2.x's application, an instance, is not there to ask.
    public void RunPeriodically(TimeSpan interval, Func<bool> callback)
    {
        if (UI.HasApp)
            UI.AddTimeout(interval, callback);
    }
}
