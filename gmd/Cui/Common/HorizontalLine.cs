using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;

namespace gmd.Cui.Common;

// A line across the whole width of the view, whatever that is, e.g. the border under a header. A
// label of line chars has the fixed length of its text, and so stops short of a terminal wider than
// that.
class HorizontalLine : View
{
    public HorizontalLine()
    {
        CanFocus = false;
        Height = 1;
        Width = Dim.Fill();
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        SetAttributeForRole(VisualRole.Normal);
        AddStr(0, 0, new string('─', Viewport.Width));
        return true;
    }
}
