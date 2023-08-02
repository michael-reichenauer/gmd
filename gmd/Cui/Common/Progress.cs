using Terminal.Gui;


namespace gmd.Cui.Common;

interface IProgress
{
    Disposable Show(bool isShowImmediately = false);
}


// cspell:ignore Wakeup
[SingleInstance]
class Progress : IProgress
{
    const int defaultInitialDelay = 800;
    const int progressWidth = 6;
    static readonly ColorScheme colorScheme = new ColorScheme()
    {
        Normal = Color.Magenta,
        Focus = Color.Magenta,
        HotNormal = Color.Magenta,
        HotFocus = Color.Magenta,
        Disabled = Color.Magenta,
    };

    Timer? progressTimer;
    int count = 0;
    Toplevel? currentParentView;
    View? progressView;

    public Disposable Show(bool isShowImmediately = false)
    {
        Start(isShowImmediately);
        return new Disposable(() => Stop());
    }

    void Start(bool isShowImmediately)
    {
        int initialDelay = isShowImmediately ? 0 : defaultInitialDelay;
        count++;
        if (count > 1)
        {   // Already started
            return;
        }

        var progressBar = new ProgressBar()
        {
            X = 1,
            Y = 0,
            Width = progressWidth,
            ProgressBarStyle = ProgressBarStyle.MarqueeBlocks,
            SegmentCharacter = '●',
            BidirectionalMarquee = true,
            ColorScheme = colorScheme,
        };

        // The left and right [] marks
        var leftMark = new Label(0, 0, "[") { ColorScheme = colorScheme };
        var rightMark = new Label(progressWidth + 1, 0, "]") { ColorScheme = colorScheme };

        progressView = new View()
        {
            X = 5,
            Y = 0,
            Width = progressWidth + 3,
            Height = 1,
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
        }, null, initialDelay, 100);

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
