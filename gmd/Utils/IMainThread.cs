namespace gmd.Utils;

// The UI main loop, reduced to the two things the layers below the UI need from it: raising an
// event on the main thread and being called back periodically. Lets those layers stay unaware of
// the UI layer and of Terminal.Gui. Implemented by MainThread in Cui/Common.
interface IMainThread
{
    // Queues the action to run on the main thread.
    void Post(Action action);

    // Calls the callback on the main thread every interval, until it returns false.
    void RunPeriodically(TimeSpan interval, Func<bool> callback);
}
