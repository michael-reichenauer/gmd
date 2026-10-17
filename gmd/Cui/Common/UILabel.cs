using System.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace gmd.Cui.Common;

// A label that draws a styled Text, which Terminal.Gui's Label does not, and says when it is clicked
// (Clicked, at the column and row of the label clicked), as the application bar needs. It takes the
// focus when clicked, if it can take it at all.
class UILabel : View
{
    Text text;
    readonly bool isAutoSize;

    public UILabel(int x, int y)
        : this(x, y, Text.Empty) { }

    public UILabel(int x, int y, Text text, bool autosize = true)
    {
        X = x;
        Y = y;
        Height = 1;
        Width = Math.Max(1, text.Length);
        isAutoSize = autosize;
        this.text = text;
    }

    // Raised for a click on the label, at its column and row in the label
    public event Action<int, int>? Clicked;

    protected override bool OnDrawingContent(DrawContext? context)
    {
        text.Draw(this, 0, 0);
        return true;
    }

    public new Text Text
    {
        get => text;
        set
        {
            if (isAutoSize)
                Width = Math.Max(1, value.Length);
            text = value;
            SetNeedsDraw();
        }
    }

    protected override bool OnMouseEvent(Mouse mouse)
    {
        if (!mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked))
            return false;

        if (CanFocus && !HasFocus)
            SetFocus();
        var at = mouse.Position ?? Point.Empty;
        Clicked?.Invoke(at.X, at.Y);
        return true;
    }
}
