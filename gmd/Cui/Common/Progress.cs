using Terminal.Gui;


namespace gmd.Cui.Common;

interface IProgress
{
    Disposable Show();
}


[SingleInstance]
class Progress : IProgress
{
    const int intitialDelay = 800;
    const int progressWidth = 15;
    static readonly ColorScheme colorScheme = new ColorScheme()
    {
        Normal = TextColor.Magenta,
        Focus = TextColor.Magenta,
        HotNormal = TextColor.Magenta,
        HotFocus = TextColor.Magenta,
        Disabled = TextColor.Magenta,
    };

    Timer? progressTimer;
    int count = 0;
    Toplevel? currentParentView;
    View? progressView;

    public Disposable Show()
    {
        Start();
        return new Disposable(() => Stop());
    }

    void Start()
    {
        count++;
        if (count > 1)
        {   // Already started
            return;
        }

        var progressBar = new ProgressBar()
        {
            X = 2,
            Y = 1,
            Width = progressWidth,
            ProgressBarStyle = ProgressBarStyle.MarqueeBlocks,
            SegmentCharacter = '●',
            BidirectionalMarquee = true,
            ColorScheme = colorScheme,
        };

        // The left and right [] marks
        var leftMark = new Label(1, 1, "[") { ColorScheme = colorScheme };
        var rightMark = new Label(progressWidth + 2, 1, "]") { ColorScheme = colorScheme };

        progressView = new View()
        {
            X = Pos.Center(),
            Y = Pos.Center(),
            Width = progressWidth + 4,
            Height = 3,
            ColorScheme = colorScheme,
            Visible = false,
        };

        progressView.Add(leftMark, progressBar, rightMark);

        bool isFirst = true;
        progressTimer = new Timer(_ =>
        {
            if (progressView == null) return;
            if (isFirst)
            {
                isFirst = false;
                progressView.Visible = true;
            }
            progressBar.Pulse();
            Application.MainLoop.Driver.Wakeup();
        }, null, intitialDelay, 100);

        currentParentView = Application.Current;
        currentParentView.Add(progressView);

        UI.SetActions(() => Deactivated(), () => Activated());
        UI.StopInput();
    }

    private void Activated()
    {
        if (progressView != null)
        {
            progressView.Visible = true;
        }

        progressTimer?.Change(200, 100);
    }

    private void Deactivated()
    {
        progressTimer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
        if (progressView != null)
        {
            progressView.Visible = false;
        }
    }

    void Stop()
    {
        count--;
        if (count > 0)
        {   // Not yet the last stop
            return;
        }

        if (progressTimer != null)
        {
            progressTimer.Dispose();
            progressTimer = null;
        }
        currentParentView!.Remove(progressView);
        currentParentView = null;
        progressView = null;
        UI.SetActions(null, null);
        UI.StartInput();
    }
}
