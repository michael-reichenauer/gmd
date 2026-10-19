using Terminal.Gui.ViewBase;

namespace gmd.Cui.Common;

// A rectangle drawn in one color, used to frame another view, e.g. a multi line text input. It is
// under the view it frames (UIDialog.AddBorderView), which Terminal.Gui draws first and keeps it off,
// and it keeps the views under it off its lines, e.g. the text a drop down list is opened over.
class BorderView : View
{
    readonly Color color;

    public BorderView(Color color)
    {
        CanFocus = false;
        this.color = color;
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        var (w, h) = (Viewport.Width, Viewport.Height);
        if (w < 2 || h < 2)
            return true;

        SetAttribute(color);
        AddStr(0, 0, "┌" + new string('─', w - 2) + "┐");
        for (int i = 1; i < h - 1; i++)
        {
            AddStr(0, i, "│");
            AddStr(w - 1, i, "│");
        }
        AddStr(0, h - 1, "└" + new string('─', w - 2) + "┘");
        return true;
    }
}
