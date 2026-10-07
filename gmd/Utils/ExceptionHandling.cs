using System.Diagnostics;

namespace gmd.Utils;

// Handles unhandled exceptions top ensure they are logged and program is restarted or shut down
internal static class ExceptionHandling
{
    private static readonly TimeSpan MinTimeBeforeAutoRestart = TimeSpan.FromSeconds(10);

    private static bool hasDisplayedErrorMessageBox;
    private static bool hasFailed;
    private static bool hasShutdown;
    private static DateTime StartTime = DateTime.UtcNow;
    private static Action shutdown = () => { };

    // What ended gmd, if an error did, for Program to say once the terminal is given back: a crash
    // used to end gmd without a word, as if it had simply quit
    public static string? Failure { get; private set; }

    public static void HandleUnhandledExceptions(Action shutdownCallback)
    {
        shutdown = shutdownCallback;
        // Add the event handler for handling non-UI thread exceptions to the event.
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            HandleException("app domain exception", e.ExceptionObject as Exception ?? new Exception());

        // Log exceptions that hasn't been handled when a Task is finalized.
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            HandleException("unobserved task exception", e.Exception);
            e.SetObserved();
        };

        // Add event handler for fatal exceptions using catch condition "when (e.IsNotFatal())"
        FatalExceptionsExtensions.FatalException += (s, e) => HandleException(e.Message, e.Exception);

        // Add handler for asserts
        Asserter.AssertOccurred += (s, e) => HandleException("Assert failed", e.Exception);
    }

    public static void OnBackgroundTaskException(Exception exception)
    {
        HandleException("RunInBackground error", exception);
    }

    // An error the UI main loop's own handler caught (Program.HandleUIMainLoopError). The loop ends
    // with it, so there is nothing to shut down, only the failure to note.
    public static void OnMainLoopException(Exception exception)
    {
        if (hasFailed)
            return;

        hasFailed = true;
        Failure = Describe(exception);
        Log.Exception(exception, "Unhandled UI main loop exception");
    }

    // The innermost exception, since RunInBackground and the task machinery wrap the one that
    // actually failed in one that only says a task did
    static string Describe(Exception exception)
    {
        while (exception.InnerException != null)
            exception = exception.InnerException;
        return $"{exception.GetType().Name}: {exception.Message}";
    }

    // public static void HandleDispatcherUnhandledException()
    // {
    // 	// Add the event handler for handling UI thread exceptions to the event
    // 	Application.Current.DispatcherUnhandledException += (s, e) =>
    // 	{
    // 		HandleException("dispatcher exception", e.Exception);
    // 		e.Handled = true;
    // 	};

    // 	WpfBindingTraceListener.Register();

    // 	isDispatcherInitialized = true;
    // }

    static void HandleException(string errorType, Exception exception)
    {
        if (hasFailed)
        {
            return;
        }

        hasFailed = true;
        Failure = Describe(exception);

        string errorMessage = $"Unhandled {errorType}";
        Log.Exception(exception, errorMessage);

        if (Debugger.IsAttached)
        {
            // NOTE: If you end up here a task resulted in an unhandled exception
            Debugger.Break();
        }
        else
        {
            Shutdown(errorMessage, exception);
        }
    }

    static void Shutdown(string message, Exception e)
    {
        if (hasShutdown)
        {
            // Shutdown already in progress
            return;
        }

        hasShutdown = true;

        // if (isDispatcherInitialized)
        // {
        // 	var dispatcher = GetApplicationDispatcher();
        // 	if (dispatcher.CheckAccess())
        // 	{
        // 		ShowExceptionDialog(e);
        // 	}
        // 	else
        // 	{
        // 		dispatcher.Invoke(() => ShowExceptionDialog(e));
        // 	}
        // }

        if (Debugger.IsAttached)
        {
            Debugger.Break();
        }

        ConfigLogger.CloseAsync().Wait();

        shutdown();

        // if (DateTime.Now - StartTime >= MinTimeBeforeAutoRestart)
        // {
        // 	StartInstanceService.StartInstance(Environment.CurrentDirectory);
        // }

        // if (isDispatcherInitialized)
        // {
        // 	Application.Current.Shutdown(0);
        // }
        // else
        // {
        // 	throw new Exception($"Unhandled exception {message}", e);
        // }
    }

    private static void ShowExceptionDialog(Exception e)
    {
        if (hasDisplayedErrorMessageBox)
        {
            return;
        }

        if (DateTime.UtcNow - StartTime < MinTimeBeforeAutoRestart)
        {
            Console.WriteLine("Sorry, but an unexpected error just occurred");
            StartTime = DateTime.UtcNow;
        }

        hasDisplayedErrorMessageBox = true;
    }

    // private static Dispatcher GetApplicationDispatcher() =>
    // 	Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
}
