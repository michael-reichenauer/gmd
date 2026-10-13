using Terminal.Gui;

namespace gmd.Cui.Common;

static class UI
{
    internal static void AssertOnUIThread() => Threading.AssertIsMainThread();

    internal static void RunInBackground(Func<Task> action)
    {
        action().RunInBackground();
    }

    // Modal, whatever is passed. Terminal.Gui hands a key to every toplevel on the stack until one
    // handles it, and stops only at a modal one. A Dialog is modal of itself, but the diff, blame and
    // conflict views are plain toplevels, so a key they did not use went on down to the log view:
    // 'P' in a diff pushed every branch, and 'c' in the resolver reached the diff below it, which
    // closed the resolver with its decisions unsaved.
    internal static void RunDialog(Toplevel toplevel)
    {
        toplevel.Modal = true;
        using (EnableInput())
        {
            Application.Run(toplevel);
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

    internal static void StopInput()
    {
        keys.GitStarted();
        ApplyKeyGate();
    }

    internal static void StartInput()
    {
        keys.GitDone();
        ApplyKeyGate();
    }

    static void ApplyKeyGate() => Application.RootKeyEvent = keys.IsDropping ? DropKey : null;

    static bool DropKey(KeyEvent _) => true;

    internal static void Post(Action action)
    {
        Application.MainLoop.Invoke(action);
    }

    internal static void HideCursor() => Application.Driver.SetCursorVisibility(CursorVisibility.Invisible);

    internal static object AddTimeout(TimeSpan timeout, Func<MainLoop, bool> callback)
    {
        return Application.MainLoop?.AddTimeout(timeout, callback)!;
    }

    internal static object AddTimeout(TimeSpan timeout, Action callback)
    {
        return Application.MainLoop?.AddTimeout(
            timeout,
            (_) =>
            {
                callback();
                return false;
            }
        )!;
    }

    internal static void RemoveTimeout(object token) => Application.MainLoop?.RemoveTimeout(token);

    internal static void Shutdown()
    {
        Application.RequestStop();
    }

    // Ends the main loop from whichever thread a failure is on (ExceptionHandling); before the loop
    // runs there is none to end, and the exception thrown ends gmd
    internal static void ShutdownFromAnyThread()
    {
        if (Application.MainLoop is MainLoop loop)
            loop.Invoke(() => Application.RequestStop());
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

    // Shows a message box while the (already started) task is running. The box has no buttons and
    // is closed when the task completes, so it cannot be dismissed by the user. Input is left as it
    // is, i.e. a Progress spinner started by the caller keeps running while the message is shown.
    internal static void ShowMessageWhile(string title, string message, Task task)
    {
        MessageDlg.ShowWhile(title, message, task);
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
        ApplyKeyGate();
        onDeactivated?.Invoke();
        return new Disposable(() =>
        {
            keys.DialogClosed();
            ApplyKeyGate();
            onActivated?.Invoke();
        });
    }
}
