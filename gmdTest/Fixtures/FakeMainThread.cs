using gmd.Utils;

namespace gmdTest.Fixtures;

// A double for IMainThread, the seam between the layers below the UI and the Terminal.Gui main
// loop. Posted actions are queued rather than run, and the periodic callback is captured, so a
// test drives both by hand instead of waiting for a main loop that does not exist.
class FakeMainThread : IMainThread
{
    readonly List<Action> posted = [];

    // The callback and interval of the latest RunPeriodically registration, null until the first.
    public Func<bool>? Periodic { get; private set; }
    public TimeSpan Interval { get; private set; }

    // Number of RunPeriodically registrations made.
    public int PeriodicCount { get; private set; }

    // Number of actions posted so far, whether or not they have been run.
    public int PostedCount { get; private set; }

    public void Post(Action action)
    {
        PostedCount++;
        posted.Add(action);
    }

    public void RunPeriodically(TimeSpan interval, Func<bool> callback)
    {
        PeriodicCount++;
        Interval = interval;
        Periodic = callback;
    }

    // Runs the periodic callback, then every action it posted, i.e. one turn of the main loop.
    public void Tick()
    {
        Periodic?.Invoke();
        RunPosted();
    }

    // Runs the actions posted so far, in order.
    public void RunPosted()
    {
        var actions = posted.ToList();
        posted.Clear();
        actions.ForEach(a => a());
    }
}
