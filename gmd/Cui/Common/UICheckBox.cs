using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace gmd.Cui.Common;

// A check box, drawn as gmd's have always looked, '◙ Checkout' ticked and '□ Publish' not. Space on
// it, or a click, ticks or unticks it.
class UICheckBox : View
{
    const string Ticked = "◙";
    const string Unticked = "□";

    readonly string text;
    bool isChecked;

    public UICheckBox(string text, bool isChecked)
    {
        this.text = text;
        this.isChecked = isChecked;
        CanFocus = true;
        Height = 1;
        Width = text.Length + 2;
        this.WithScheme(() => ColorSchemes.CheckBox);
    }

    // Raised when the box is ticked or unticked, by the user or by setting Checked
    public event Action<bool>? Toggled;

    public bool Checked
    {
        get => isChecked;
        set
        {
            if (isChecked == value)
                return;
            isChecked = value;
            SetNeedsDraw();
            Toggled?.Invoke(isChecked);
        }
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        SetAttributeForRole(
            !Enabled ? VisualRole.Disabled
            : HasFocus ? VisualRole.Focus
            : VisualRole.Normal
        );
        AddStr(0, 0, $"{(isChecked ? Ticked : Unticked)} {text}");
        return true;
    }

    protected override bool OnKeyDown(Key key)
    {
        if (key != Key.Space)
            return false;
        Checked = !Checked;
        return true;
    }

    protected override bool OnMouseEvent(Mouse mouse)
    {
        if (!mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked))
            return false;
        if (!HasFocus)
            SetFocus();
        Checked = !Checked;
        return true;
    }
}
