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

    Timer? progressTimer;
    int count = 0;
    Toplevel? currentParentView;
    Marquee? marquee;

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
        { // Already started
            return;
        }

        // One column wider than the marquee itself, to keep a blank between it and the repo path
        marquee = new Marquee(5, 0, Marquee.TotalWidth + 1, isVisible: false);

        bool isFirst = true;
        progressTimer = new Timer(
            _ =>
            {
                if (marquee == null)
                    return;
                if (isFirst)
                {
                    isFirst = false;
                    marquee.IsVisible = true;
                }
                marquee.Pulse();
                Application.MainLoop.Driver.Wakeup();
            },
            null,
            initialDelay,
            100
        );

        currentParentView = Application.Current;
        currentParentView.Add(marquee.View);

        UI.SetActions(() => Deactivated(), () => Activated());
        UI.StopInput();
    }

    private void Activated()
    {
        if (marquee != null)
        {
            marquee.IsVisible = true;
        }

        progressTimer?.Change(200, 100);
    }

    private void Deactivated()
    {
        progressTimer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
        if (marquee != null)
        {
            marquee.IsVisible = false;
        }
    }

    void Stop()
    {
        count--;
        if (count > 0)
        { // Not yet the last stop
            return;
        }

        if (progressTimer != null)
        {
            progressTimer.Dispose();
            progressTimer = null;
        }
        currentParentView!.Remove(marquee!.View);
        currentParentView = null;
        marquee = null;
        UI.SetActions(null, null);
        UI.StartInput();
    }
}
