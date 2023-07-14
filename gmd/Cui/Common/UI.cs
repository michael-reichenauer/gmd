using Terminal.Gui;


namespace gmd.Cui.Common;

static class UI
{
    static internal void AssertOnUIThread() => Threading.AssertIsMainThread();

    static internal void RunInBackground(Func<Task> action)
    {
        action().RunInBackground();
    }

    static internal void RunDialog(Toplevel toplevel)
    {
        using (EnableInput())
        {
            Application.Run(toplevel);
        }
    }

    static Action? onActivated;
    static Action? onDeactivated;
    static internal void SetActions(Action? deactivated, Action? activated)
    {
        onDeactivated = deactivated;
        onActivated = activated;
    }

    public static void CloseDialog() => Application.RequestStop();

    static internal void StopInput()
    {
        Application.RootKeyEvent = (_) => true;
    }

    static internal void StartInput()
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
        buttons = buttons.Length == 0 ? new string[] { "OK" } : buttons;

        using (EnableInput())
        {
            return MessageDlg.ShowInfo(title, message, defaultButton, buttons);
        }
    }


    internal static int ErrorMessage(string message, int defaultButton = 0, params string[] buttons)
    {
        return ErrorMessage("Error !", message, defaultButton, buttons);
    }

    internal static int ErrorMessage(string title, string message, int defaultButton = 0, params string[] buttons)
    {
        buttons = buttons.Length == 0 ? new string[] { "OK" } : buttons;

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