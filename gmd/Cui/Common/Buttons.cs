using Terminal.Gui;

namespace gmd.Cui.Common;

class Buttons
{
    internal static Button Cancel(bool isDefault = false, Func<bool>? clicked = null)
    {
        Button button = new Button("Cancel", isDefault) { ColorScheme = ColorSchemes.ButtonColorScheme };
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

    internal static Button OK(bool isDefault = false, Func<bool>? clicked = null)
    {
        Button button = new Button("OK", isDefault) { ColorScheme = ColorSchemes.ButtonColorScheme };
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

