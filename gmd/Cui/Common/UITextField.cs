using Terminal.Gui;

namespace gmd.Cui.Common;

// A one line text input field, which unlike Terminal.Gui's TextField returns its text as a
// trimmed string rather than as a ustring.
class UITextField : TextField
{
    internal UITextField(int x, int y, int w, string text = "")
        : base(x, y, w, text)
    {
        ColorScheme = ColorSchemes.TextField;
    }

    public new string Text
    {
        get => base.Text?.ToString()?.Trim() ?? "";
        set => base.Text = value;
    }
}
