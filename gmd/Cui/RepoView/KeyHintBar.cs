using Terminal.Gui;

namespace gmd.Cui.RepoView;

// The key-hint line at the bottom of the log view, see KeyHints for what it says. It asks for the
// hints when it is drawn, and the log view marks it for drawing whenever it draws itself, which is
// whenever anything the hints depend on changes: the row, the hoover, the selection or the repo.
class KeyHintBar : View
{
    readonly Func<IReadOnlyList<KeyHint>> getHints;

    public KeyHintBar(Func<IReadOnlyList<KeyHint>> getHints)
    {
        this.getHints = getHints;
        X = 0;
        Y = Pos.AnchorEnd(1);
        Width = Dim.Fill();
        Height = 1;
        CanFocus = false;
    }

    public override void Redraw(Rect bounds)
    {
        Clear();
        KeyHints.ToText(getHints(), Frame.Width).Draw(this, 0, 0);
    }
}
