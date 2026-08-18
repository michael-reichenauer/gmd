using Terminal.Gui;

namespace gmd.Cui.Common;

// The pulsing '[  ●●  ]' marquee that shows that a long running command is still working. It does
// not animate on its own; the owner has to call Pulse() regularly (see Progress, which shows one on
// the application bar, and MessageDlg.ShowWhile, which shows one inside the message box).
class Marquee
{
    const int barWidth = 6;

    // The bar and the two [] marks around it
    internal const int TotalWidth = barWidth + 2;

    readonly ProgressBar bar;

    // A plain View wrapped by this class rather than a 'Marquee : View', since Terminal.Gui paints
    // the columns not covered by the subviews for a View, but not for a subclass of it, and
    // Progress uses that to blank the column between the marquee and the repo path.
    internal View View { get; }

    internal Marquee(Pos x, Pos y, int width = TotalWidth, bool isVisible = true)
    {
        bar = new ProgressBar()
        {
            X = 1,
            Y = 0,
            Width = barWidth,
            ProgressBarStyle = ProgressBarStyle.MarqueeBlocks,
            SegmentCharacter = '●',
            BidirectionalMarquee = true,
            ColorScheme = ColorSchemes.Progress,
        };

        // The left and right [] marks
        var leftMark = new Label(0, 0, "[") { ColorScheme = ColorSchemes.Progress };
        var rightMark = new Label(barWidth + 1, 0, "]") { ColorScheme = ColorSchemes.Progress };

        View = new View()
        {
            X = x,
            Y = y,
            Width = width,
            Height = 1,
            ColorScheme = ColorSchemes.Progress,
            Visible = isVisible,
        };

        View.Add(leftMark, bar, rightMark);
    }

    internal bool IsVisible
    {
        get => View.Visible;
        set => View.Visible = value;
    }

    internal void Pulse() => bar.Pulse();
}
