using Terminal.Gui;

namespace gmd.Cui.Common;

// A rectangle drawn in one color, used to frame another view, since the Border property of a
// Terminal.Gui view does not draw for all views.
class BorderView : View
{
    readonly Color color;

    public BorderView(Color color)
    {
        CanFocus = false;
        this.color = color;
    }

    public override void Redraw(Rect bounds)
    {
        base.Redraw(bounds);
        var b = bounds;

        Driver.SetAttribute(color);

        // Draw corners
        Move(b.X, b.Y);
        Driver.AddRune('┌');
        Move(b.X + b.Width - 1, b.Y);
        Driver.AddRune('┐');
        Move(b.X, b.Y + b.Height - 1);
        Driver.AddRune('└');
        Move(b.X + b.Width - 1, b.Y + b.Height - 1);
        Driver.AddRune('┘');

        // Draw top/bottom
        for (int i = 1; i < b.Width - 1; i++)
        {
            Move(b.X + i, b.Y);
            Driver.AddRune('─');
            Move(b.X + i, b.Y + b.Height - 1);
            Driver.AddRune('─');
        }

        // Draw left/right sides
        for (int i = 1; i < b.Height - 1; i++)
        {
            Move(b.X, b.Y + i);
            Driver.AddRune('│');
            Move(b.X + b.Width - 1, b.Y + i);
            Driver.AddRune('│');
        }
    }
}
