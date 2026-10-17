using System.Drawing;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace gmd.Cui.Common;

// The application, i.e. the main loop, the screen and the modal runs, for the UI. Terminal.Gui 2.x's
// application is an instance (IApplication), made by Program and kept here, where 1.x's was static.
static class UI
{
    static IApplication? app;

    internal static IApplication App => app ?? throw Asserter.Fail("The application has not started");

    internal static void SetApp(IApplication application)
    {
        app = application;

        // Keys are dropped while git works (KeyGate): the application's KeyDown is raised before any
        // view gets the key, and a key marked handled there goes no further
        application.Keyboard.KeyDown += (_, key) =>
        {
            if (keys.IsDropping)
                key.Handled = true;
        };
    }

    // The size of the screen, which a menu or a message box is fitted into
    internal static Size ScreenSize => App.Screen.Size;

    // The view of the runnable on top, i.e. the dialog or view keys go to now
    internal static View? Current => App.TopRunnableView;

    internal static void AssertOnUIThread() => Threading.AssertIsMainThread();

    internal static void RunInBackground(Func<Task> action)
    {
        action().RunInBackground();
    }

    // Runs the view modally, until it is stopped. A runnable Terminal.Gui runs is on top of the
    // others, and keys go to it alone: a key it does not use goes nowhere, rather than on to the log
    // view below, as one did with 1.x unless the view was marked modal ('P' in a diff pushed every
    // branch, and 'c' in the resolver reached the diff below it, which closed the resolver).
    internal static void RunDialog(IRunnable runnable)
    {
        using (EnableInput())
        {
            App.Run(runnable);
        }
    }

    static Action? onActivated;
    static Action? onDeactivated;

    internal static void SetActions(Action? deactivated, Action? activated)
    {
        onDeactivated = deactivated;
        onActivated = activated;
    }

    // Keys are dropped while git works, but not in a dialog, see KeyGate
    static readonly KeyGate keys = new();

    internal static void StopInput() => keys.GitStarted();

    internal static void StartInput() => keys.GitDone();

    internal static void Post(Action action) => App.Invoke(action);

    // Terminal.Gui 2.x shows the cursor only where a view puts it (View.Cursor), so there is nothing
    // to hide; kept for the callers, which hid it after 1.x had shown it
    internal static void HideCursor() { }

    // Calls the callback on the main loop after the timeout, and again after each timeout for as
    // long as it returns true
    internal static object AddTimeout(TimeSpan timeout, Func<bool> callback) => App.AddTimeout(timeout, callback)!;

    internal static object AddTimeout(TimeSpan timeout, Action callback) =>
        App.AddTimeout(
            timeout,
            () =>
            {
                callback();
                return false;
            }
        )!;

    internal static void RemoveTimeout(object token) => App.RemoveTimeout(token);

    // Stops the runnable on top, e.g. a dialog
    internal static void Shutdown() => App.RequestStop();

    // Ends the main loop from whichever thread a failure is on (ExceptionHandling); before the loop
    // runs there is none to end, and the exception thrown ends gmd
    internal static void ShutdownFromAnyThread()
    {
        if (app is IApplication a)
            a.Invoke(() => a.RequestStop());
    }

    internal static int InfoMessage(string title, string message, params string[] buttons)
    {
        return InfoMessage(title, message, 0, buttons);
    }

    internal static int InfoMessage(string title, string message, int defaultButton = 0, params string[] buttons)
    {
        buttons = buttons.Length == 0 ? ["OK"] : buttons;

        using (EnableInput())
        {
            return MessageDlg.ShowInfo(title, message, defaultButton, buttons);
        }
    }

    // Shows a message box while the (already started) task is running. The box is closed when the
    // task completes, so it cannot be dismissed by the user. With askCancel, it has a Cancel button,
    // which calls it, see MessageDlg.ShowWhile, and takes keys for it; without, it has no buttons,
    // and input is left as it is, i.e. a Progress spinner started by the caller drops the keys.
    internal static void ShowMessageWhile(string title, string message, Task task, Func<bool>? askCancel = null)
    {
        if (askCancel == null)
        {
            MessageDlg.ShowWhile(title, message, task);
            return;
        }

        using (EnableInput())
        {
            MessageDlg.ShowWhile(title, message, task, askCancel);
        }
    }

    internal static int ErrorMessage(string message, int defaultButton = 0, params string[] buttons)
    {
        return ErrorMessage("Error !", message, defaultButton, buttons);
    }

    internal static int ErrorMessage(string title, string message, int defaultButton = 0, params string[] buttons)
    {
        buttons = buttons.Length == 0 ? ["OK"] : buttons;

        using (EnableInput())
        {
            return MessageDlg.ShowError(title, message, defaultButton, buttons);
        }
    }

    static Disposable EnableInput()
    {
        keys.DialogOpened();
        onDeactivated?.Invoke();
        return new Disposable(() =>
        {
            keys.DialogClosed();
            onActivated?.Invoke();
        });
    }
}
