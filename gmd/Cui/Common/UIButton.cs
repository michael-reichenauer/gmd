using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace gmd.Cui.Common;

// A button, drawn as gmd's buttons have always looked, '[ Cancel ]', and the default one, which
// Enter presses wherever the focus is in its dialog (UIDialog), '[◦ OK ◦]'. Enter or space on it, or
// a click, presses it. gmd's own, rather than Terminal.Gui's, whose look and default button logic
// are its own and changed between versions.
class UIButton : View
{
    string text;
    bool isDefault;

    public UIButton(string text, bool isDefault = false)
    {
        this.text = text;
        this.isDefault = isDefault;
        CanFocus = true;
        Height = 1;
        Width = Label.Length;
        this.WithScheme(() => ColorSchemes.Button);
    }

    // Raised when the button is pressed
    public event Action? Clicked;

    public new string Text
    {
        get => text;
        set
        {
            text = value;
            Width = Label.Length;
            SetNeedsDraw();
        }
    }

    public bool IsDefault
    {
        get => isDefault;
        set
        {
            isDefault = value;
            Width = Label.Length;
            SetNeedsDraw();
        }
    }

    // The button as drawn
    public string Label => isDefault ? $"[◦ {text} ◦]" : $"[ {text} ]";

    public void Press()
    {
        if (Enabled)
            Clicked?.Invoke();
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        var role =
            !Enabled ? VisualRole.Disabled
            : HasFocus ? VisualRole.Focus
            : VisualRole.Normal;
        SetAttributeForRole(role);
        AddStr(0, 0, Label);

        // The first capital letter, in the hot color, as Terminal.Gui 1.x drew its buttons
        var hot = text.IndexOf(text.FirstOrDefault(char.IsUpper));
        if (Enabled && hot >= 0)
        {
            SetAttributeForRole(HasFocus ? VisualRole.HotFocus : VisualRole.HotNormal);
            AddStr(Label.IndexOf(text) + hot, 0, text[hot].ToString());
        }
        return true;
    }

    protected override bool OnKeyDown(Key key)
    {
        if (key == Key.Enter || key == Key.Space)
        {
            Press();
            return true;
        }
        return false;
    }

    protected override bool OnMouseEvent(Mouse mouse)
    {
        if (!mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked))
            return false;
        if (!HasFocus)
            SetFocus();
        Press();
        return true;
    }
}
