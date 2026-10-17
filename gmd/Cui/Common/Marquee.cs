using Terminal.Gui.ViewBase;

namespace gmd.Cui.Common;

// The pulsing '[  ●●  ]' marquee that shows that a long running command is still working. It does
// not animate on its own; the owner has to call Pulse() regularly (see Progress, which shows one on
// the application bar, and MessageDlg.ShowWhile, which shows one inside the message box).
class Marquee
{
    const int barWidth = 6;
    const int blockWidth = 2;

    // The bar and the two [] marks around it
    internal const int TotalWidth = barWidth + 2;

    readonly MarqueeView view;

    internal View View => view;

    internal Marquee(Pos x, Pos y, int width = TotalWidth, bool isVisible = true)
    {
        view = new MarqueeView()
        {
            X = x,
            Y = y,
            Width = width,
            Height = 1,
            Visible = isVisible,
        };
    }

    internal bool IsVisible
    {
        get => view.Visible;
        set => view.Visible = value;
    }

    internal void Pulse() => view.Pulse();

    // The marks and the block, which goes from one end of the bar to the other and back; the columns
    // past the marks, of a view wider than them, are drawn blank
    class MarqueeView : View
    {
        int position;
        int step = 1;

        internal MarqueeView()
        {
            CanFocus = false;
            this.WithScheme(() => ColorSchemes.Progress);
        }

        internal void Pulse()
        {
            if (position + step < 0 || position + step > barWidth - blockWidth)
                step = -step;
            position += step;
            SetNeedsDraw();
        }

        protected override bool OnDrawingContent(DrawContext? context)
        {
            var bar = new string(' ', position) + new string('●', blockWidth);
            Common
                .Text.Magenta("[" + bar.PadRight(barWidth) + "]")
                .ToText()
                .Subtext(0, Viewport.Width, isFillRest: true)
                .Draw(this, 0, 0);
            return true;
        }
    }
}
