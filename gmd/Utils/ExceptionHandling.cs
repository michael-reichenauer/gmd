using System.Diagnostics;

namespace gmd.Utils;

// What happens when gmd fails on a bug, wherever it does: an exception no one caught, on the UI
// main loop, in a task or on a thread of its own, or a broken assert, which is reported before it
// is thrown (Asserter). The first is logged, kept as the Failure for Program to tell once the
// terminal is given back, and stops the UI main loop; any after it are logged. With a debugger
// attached it breaks there instead, and gmd runs on.
//
// The log is left open, for what is logged while gmd stops, and Program closes it at the end; only
// an exception that ends the process at once (UnhandledException) has the log written out first.
internal static class ExceptionHandling
{
    static readonly TimeSpan LogFlushTimeout = TimeSpan.FromSeconds(2);

    static int hasFailed;
    static Action stop = () => { };

    // What ended gmd, if an error did, for Program to say once the terminal is given back: a crash
    // used to end gmd without a word, as if it had simply quit
    public static string? Failure { get; private set; }

    public static void HandleUnhandledExceptions(Action stopMainLoop)
    {
        stop = stopMainLoop;

        // An exception on a thread of its own, after which the runtime ends the process
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Handle("Unhandled exception", e.ExceptionObject as Exception ?? new Exception($"{e.ExceptionObject}"));
            LogFile.CloseAsync().Wait(LogFlushTimeout);
        };

        // A task that failed and that nothing awaited, found when it is collected
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Handle("Unobserved task exception", e.Exception);
            e.SetObserved();
        };

        Asserter.Failed += e => Handle("Assert failed", e);
    }

    public static void OnBackgroundTaskException(Exception exception) => Handle("RunInBackground error", exception);

    // An error the UI main loop's own handler caught (Program.HandleUIMainLoopError). The loop ends
    // with it, so there is nothing to stop, only the failure to note.
    public static void OnMainLoopException(Exception exception)
    {
        if (IsFirst(exception, "Unhandled UI main loop exception"))
            Failure = Describe(exception);
    }

    static void Handle(string errorType, Exception exception)
    {
        if (!IsFirst(exception, errorType))
            return;

        Failure = Describe(exception);
        if (Debugger.IsAttached)
            Debugger.Break(); // An unhandled exception, an assert or a failed RunInBackground task
        else
            stop();
    }

    // Logs the failure, and says whether it is the first, i.e. the one that stops gmd, from
    // whichever thread it is on
    static bool IsFirst(Exception exception, string errorType)
    {
        var isFirst = Interlocked.Exchange(ref hasFailed, 1) == 0;
        Log.Exception(exception, isFirst ? errorType : $"{errorType}, after gmd has failed already");
        return isFirst;
    }

    // The innermost exception, since RunInBackground and the task machinery wrap the one that
    // actually failed in one that only says a task did
    static string Describe(Exception exception)
    {
        while (exception.InnerException != null)
            exception = exception.InnerException;
        return $"{exception.GetType().Name}: {exception.Message}";
    }
}
