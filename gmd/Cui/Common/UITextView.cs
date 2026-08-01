using Terminal.Gui;

namespace gmd.Cui.Common;

// A multi line text input view, where tab moves focus to the next control instead of being
// inserted into the text.
class UITextView : TextView
{
    public override bool ProcessKey(KeyEvent keyEvent)
    {
        if (keyEvent.Key == Key.Tab)
        { // Ensure tab sets focus on next control and not insert tab in text
            return false;
        }
        return base.ProcessKey(keyEvent);
    }

    public new string Text
    {
        get => base.Text?.ToString()?.Trim() ?? "";
        set => base.Text = value;
    }

    public override Border Border
    {
        get => new Border() { };
        set => base.Border = value;
    }
}
