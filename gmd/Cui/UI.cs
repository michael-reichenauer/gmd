using NStack;
using Terminal.Gui;


namespace gmd.Cui;

class UI
{
    public static void Post(Action action)
    {
        Application.MainLoop.Invoke(action);
    }

    public static void ShowCursor() => Application.Driver.SetCursorVisibility(CursorVisibility.Vertical);
    public static void HideCursor() => Application.Driver.SetCursorVisibility(CursorVisibility.Invisible);

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