using NStack;
using Terminal.Gui;


namespace gmd.Cui;

static class UI
{
    static internal void RunInBackground(Func<Task> action)
    {
        action().RunInBackground();
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

    internal static int InfoMessage(string title, string message, params ustring[] buttons)
    {
        return InfoMessage(title, message, 0, buttons);
    }

    internal static int InfoMessage(string title, string message, int defaultButton = 0, params ustring[] buttons)
    {
        buttons = buttons.Length == 0 ? new ustring[] { "OK" } : buttons;

        var border = new Border()
        {
            Effect3D = false,
            BorderStyle = BorderStyle.Rounded,
        };

        return MessageBox.Query(0, 0, title, message, defaultButton, border, buttons);
    }

    internal static int ErrorMessage(string message, params ustring[] buttons)
    {
        return ErrorMessage(message, 0, buttons);
    }

    internal static int ErrorMessage(string message, int defaultButton = 0, params ustring[] buttons)
    {
        buttons = buttons.Length == 0 ? new ustring[] { "OK" } : buttons;

        var border = new Border()
        {
            Effect3D = false,
            BorderStyle = BorderStyle.Rounded,
        };

        return MessageBox.ErrorQuery(0, 0, "Error", message, defaultButton, border, buttons);
    }
}