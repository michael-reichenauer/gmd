using Terminal.Gui;

namespace gmd.Cui;

interface IProgress
{
    Disposable Show();
}


[SingleInstance]
class Progress : IProgress
{
    const int intitialDelay = 800;
    const int progressWidth = 20;
    static readonly ColorScheme colorScheme = new ColorScheme() { Normal = Colors.Magenta };

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
            X = 0,
            Y = 0,
            Width = progressWidth,
            ProgressBarStyle = ProgressBarStyle.MarqueeBlocks,
            SegmentCharacter = '●',
            BidirectionalMarquee = true,
            ColorScheme = colorScheme
        };

        Border progressViewBorder = new Border()
        {
            BorderStyle = BorderStyle.None,
            DrawMarginFrame = false,
            BorderThickness = new Thickness(0, 0, 0, 0),
            BorderBrush = Color.Black,
            Padding = new Thickness(0, 0, 0, 0),
            Background = Color.Black,
            Effect3D = false,
        };

        progressView = new View()
        {
            X = Pos.Center(),
            Y = Pos.Center(),
            Width = progressWidth,
            Height = 1,
            Border = progressViewBorder,
            ColorScheme = colorScheme,
        };

        progressView.Add(progressBar);

        bool isFirstTime = false;
        progressTimer = new Timer(_ =>
        {
            if (!isFirstTime)
            {   // Show border after an intial short delay
                isFirstTime = true;
                progressView.Border.BorderStyle = BorderStyle.Rounded;
            }

            progressBar.Pulse();
            Application.MainLoop.Driver.Wakeup();
        }, null, intitialDelay, 100);

        currentParentView = Application.Current;
        currentParentView.Add(progressView);

        currentParentView.Activate += (_) => Log.Info("Active");
        currentParentView.Deactivate += (_) => Log.Info("Deactivate");

        Application.NotifyNewRunState += (d) => Log.Info("new run state");
        Application.NotifyStopRunState += (d) => Log.Info("new stop state");

        UI.StopInput();
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
        UI.StartInput();
    }
}
