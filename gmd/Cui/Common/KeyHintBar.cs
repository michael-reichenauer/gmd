using Terminal.Gui;

namespace gmd.Cui.Common;

// The key-hint line at the bottom of a view, see KeyHintLine, and the line a status message is shown
// on for a few seconds instead, see StatusLine. It asks for both when it is drawn, and the log view
// marks it for drawing whenever it draws itself, which is whenever anything the hints depend on
// changes: the row, the hoover, the selection or the repo.
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

    // Shows the messages of a view's own status line, as the log view shows its own (RepoView's
    // OnStatusChanged): the bar is shown for a message with the key hints turned off too, over the
    // view's last row, and put back once the message has been shown long enough, which nothing else
    // would redraw it for. For the diff and the blame, whose messages say what a search found. 'view'
    // is the one the bar is in, redrawn whole, since the row under the bar can be any of its views,
    // e.g. the blame's commit details.
    public void FollowStatus(IStatusLine status, bool isHintsShown, View view)
    {
        Visible = isHintsShown;
        status.Changed += () =>
            UI.Post(() =>
            {
                Update();
                UI.AddTimeout(
                    (status.Current?.Duration ?? StatusLine.Duration) + TimeSpan.FromMilliseconds(100),
                    _ =>
                    {
                        Update();
                        return false;
                    }
                );
            });

        void Update()
        {
            Visible = isHintsShown || status.Current != null;
            SetNeedsDisplay();
            view.SetNeedsDisplay(); // The row under the bar, when it was shown over it
        }
    }

    public override void Redraw(Rect bounds)
    {
        Clear();
        var text = getMessage() is StatusMessage message
            ? KeyHintLine.ToText(message, Frame.Width)
            : KeyHintLine.ToText(getHints(), Frame.Width);
        text.Draw(this, 0, 0);
    }
}
