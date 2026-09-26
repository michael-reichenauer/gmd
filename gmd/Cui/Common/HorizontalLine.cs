using Terminal.Gui;

namespace gmd.Cui.Common;

// A line across the whole width of the view, whatever that is, e.g. the border under a header. A
// label of line chars has the fixed length of its text, and so stops short of a terminal wider than
// that. Not Terminal.Gui's LineView, whose constructor reads the driver: the application bar is made
// before Application.Init, when there is none yet.
class HorizontalLine : View
{
    public HorizontalLine()
    {
        CanFocus = false;
        Height = 1;
        Width = Dim.Fill();
    }

    public override void Redraw(Rect bounds)
    {
        Driver.SetAttribute(GetNormalColor());
        Move(0, 0);
        for (int i = 0; i < Bounds.Width; i++)
            Driver.AddRune('─');
    }
}
