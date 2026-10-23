using System.Collections.Concurrent;

namespace gmd.Cui.Common;

// The work posted to the main loop, by UI.Post and by every await on it, run at the start of each
// iteration of the main loop, a dialog's too. It is the synchronization context of the main loop,
// set as an iteration starts (Run sets Terminal.Gui's own whenever a view is run).
//
// Not Terminal.Gui 2.x's own, which runs posted work as a timer, holding the lock of the timers
// while it runs: a dialog opened by posted work, as most of gmd's are, after an await, runs its main
// loop inside that lock, and every thread that posted to the main loop meanwhile waited for the lock
// until the dialog closed, e.g. the git command the commit dialog's Ctrl-D started, whose diff never
// came. A queue that needs no lock has nothing to wait for.
sealed class MainLoopQueue : SynchronizationContext
{
    readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> queue = new();

    public override void Post(SendOrPostCallback d, object? state) => queue.Enqueue((d, state));

    // Runs the callback on the main loop and waits for it, or at once on the main loop itself
    public override void Send(SendOrPostCallback d, object? state)
    {
        if (SynchronizationContext.Current == this)
        {
            d(state);
            return;
        }

        using var done = new ManualResetEventSlim();
        Exception? error = null;
        Post(
            _ =>
            {
                try
                {
                    d(state);
                }
                catch (Exception e)
                {
                    error = e;
                }
                finally
                {
                    done.Set();
                }
            },
            null
        );
        done.Wait();
        if (error != null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }

    public override SynchronizationContext CreateCopy() => this;

    internal void Post(Action action) => Post(_ => action(), null);

    // Whether there is work posted that has not run yet
    internal bool HasPosted => !queue.IsEmpty;

    // Runs what is posted, what is posted meanwhile too, on the main loop as an iteration starts. A
    // dialog run by one of them runs the rest from its own main loop.
    internal void RunPosted()
    {
        if (SynchronizationContext.Current != this)
            SynchronizationContext.SetSynchronizationContext(this);

        while (queue.TryDequeue(out var work))
            work.Callback(work.State);
    }
}
