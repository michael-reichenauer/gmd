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
    public string Label => isDefault ? $"[◦ {Shown} ◦]" : $"[ {Shown} ]";

    // The text with no '_', which marks the letter after it as the button's key
    string Shown => text.Replace("_", "");

    // Where in Shown the letter in the hot color is: the one after a '_', else the first capital, as
    // Terminal.Gui 1.x drew its buttons; -1 for none
    int HotIndex =>
        text.IndexOf('_') is int i and >= 0 && i < text.Length - 1
            ? i
            : Shown.IndexOf(Shown.FirstOrDefault(char.IsUpper));

    // The key that presses the button wherever the focus is in its dialog: Alt and its hot letter,
    // e.g. Alt-O for OK, as with Terminal.Gui 1.x; null for a button with none
    public Key? AltKey => HotIndex >= 0 ? ((Key)char.ToLowerInvariant(Shown[HotIndex])).WithAlt : null;

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

        var hot = HotIndex;
        if (Enabled && hot >= 0)
        {
            SetAttributeForRole(HasFocus ? VisualRole.HotFocus : VisualRole.HotNormal);
            AddStr(Label.IndexOf(Shown) + hot, 0, Shown[hot].ToString());
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
