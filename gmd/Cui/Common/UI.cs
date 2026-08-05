using Terminal.Gui;

namespace gmd.Cui.Common;

static class UI
{
    internal static void AssertOnUIThread() => Threading.AssertIsMainThread();

    internal static void RunInBackground(Func<Task> action)
    {
        action().RunInBackground();
    }

    internal static void RunDialog(Toplevel toplevel)
    {
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

    public static void CloseDialog() => Application.RequestStop();

    internal static void StopInput()
    {
        Application.RootKeyEvent = (_) => true;
    }

    internal static void StartInput()
    {
        Application.RootKeyEvent = null;
    }

    internal static void Post(Action action)
    {
        Application.MainLoop.Invoke(action);
    }

    internal static void ShowCursor() => Application.Driver.SetCursorVisibility(CursorVisibility.Default);

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

    internal static void Shutdown()
    {
        Application.RequestStop();
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
        var rootKeyEvent = Application.RootKeyEvent;
        Application.RootKeyEvent = null;
        onDeactivated?.Invoke();
        return new Disposable(() =>
        {
            Application.RootKeyEvent = rootKeyEvent;
            onActivated?.Invoke();
        });
    }
}
