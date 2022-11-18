using Terminal.Gui;
using gmd.Cui.Common;


namespace gmd.Cui;

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

    static internal void StopInput()
    {
        Application.RootKeyEvent = (_) => true;
    }

    static internal void StartInput()
    {
        Application.RootKeyEvent = null;
    }

    public static MenuItem MenuSeparator(string text = "")
    {
        const int maxDivider = 25;
        if (text == "")
        {
            return new MenuItem(new string('─', maxDivider), "", () => { }, () => false);
        }

        text = text.Max(maxDivider - 6);
        string suffix = new string('─', Math.Max(0, maxDivider - text.Length - 6));
        return new MenuItem($"── {text} {suffix}──", "", () => { }, () => false);
    }

    internal static void Post(Action action)
    {
        Application.MainLoop.Invoke(action);
    }

    internal static void ShowCursor() => Application.Driver.SetCursorVisibility(CursorVisibility.Default);
    internal static void HideCursor() => Application.Driver.SetCursorVisibility(CursorVisibility.Invisible);

    internal static object AddTimeout(TimeSpan timeout, Func<MainLoop, bool> callback)
    {
        return Application.MainLoop.AddTimeout(timeout, callback);
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

    internal static int ErrorMessage(string message, params string[] buttons)
    {
        return ErrorMessage(message, 0, buttons);
    }

    internal static int ErrorMessage(string message, int defaultButton = 0, params string[] buttons)
    {
        buttons = buttons.Length == 0 ? new string[] { "OK" } : buttons;

        using (EnableInput())
        {
            return MessageDlg.ShowError(message, defaultButton, buttons);
        }
    }

    static Disposable EnableInput()
    {
        var rootKeyEvent = Application.RootKeyEvent;
        Application.RootKeyEvent = null;
        return new Disposable(() => Application.RootKeyEvent = rootKeyEvent);
    }

}