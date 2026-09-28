using gmd.Cui.Common;
using Terminal.Gui;

namespace gmd.Cui.RepoView;

// The key-hint line at the bottom of the log view, see KeyHints for what it says, and the line a
// status message is shown on for a few seconds instead, see StatusLine. It asks for both when it is
// drawn, and the log view marks it for drawing whenever it draws itself, which is whenever
// anything the hints depend on changes: the row, the hoover, the selection or the repo.
class KeyHintBar : View
{
    readonly Func<IReadOnlyList<KeyHint>> getHints;
    readonly Func<StatusMessage?> getMessage;

    public KeyHintBar(Func<IReadOnlyList<KeyHint>> getHints, Func<StatusMessage?> getMessage)
    {
        this.getHints = getHints;
        this.getMessage = getMessage;
        X = 0;
        Y = Pos.AnchorEnd(1);
        Width = Dim.Fill();
        Height = 1;
        CanFocus = false;
    }

    public override void Redraw(Rect bounds)
    {
        Clear();
        var text = getMessage() is StatusMessage message
            ? KeyHints.ToText(message, Frame.Width)
            : KeyHints.ToText(getHints(), Frame.Width);
        text.Draw(this, 0, 0);
    }
}
