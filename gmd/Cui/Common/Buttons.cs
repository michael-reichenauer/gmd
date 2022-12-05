using Terminal.Gui;

namespace gmd.Cui.Common;

class Buttons
{
    internal static Button Button(string text, Action clicked, bool isDefault = false)
    {
        Button button = new Button(text, isDefault) { ColorScheme = ColorSchemes.Button };
        button.Clicked += () => clicked();
        return button;
    }

    internal static Button Cancel(bool isDefault = false, Func<bool>? clicked = null)
    {
        Button button = new Button("Cancel", isDefault) { ColorScheme = ColorSchemes.Button };
        button.Clicked += () =>
        {
            if (clicked != null && !clicked())
            {
                return;
            }
            Application.RequestStop();
        };

        return button;
    }

    internal static Button OK(bool isDefault = true, Func<bool>? clicked = null)
    {
        Button button = new Button("OK", isDefault) { ColorScheme = ColorSchemes.Button };
        button.Clicked += () =>
        {
            if (clicked != null && !clicked())
            {
                return;
            }
            Application.RequestStop();
        };

        return button;
    }
}

