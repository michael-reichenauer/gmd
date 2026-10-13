using Terminal.Gui;

namespace gmd.Cui.Common;

interface IProgress
{
    Disposable Show(bool isShowImmediately = false);
}

// The marquee on the application bar while a command runs, shown once it has run for a moment and
// pulsed on the main loop, by its timeouts, as everything that touches a view is: it was pulsed from
// a System.Threading.Timer on the thread pool, where an exception ended the process, with no
// Application.Shutdown to give the terminal back.
[SingleInstance]
class Progress : IProgress
{
    const int defaultInitialDelay = 800;
    const int pulseInterval = 100;

    object? pulse; // The main loop timeout that shows and pulses the marquee
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
        StartPulse(initialDelay);

        currentParentView = Application.Current;
        currentParentView.Add(marquee.View);

        UI.SetActions(() => Deactivated(), () => Activated());
        UI.StopInput();
    }

    private void Activated()
    {
        // Only the dialog whose closing brings back the view the marquee is on counts. After a
        // dialog over a dialog closes (the spelling menu over the commit dialog), the outer one is
        // still up and nothing is running yet, so a marquee would pulse beside it for nothing.
        if (Application.Current != currentParentView)
            return;

        if (marquee != null)
        {
            // Back in front of the main view. Running a dialog brings the subview that has the focus,
            // i.e. the main view, to the front of the toplevel (Terminal.Gui's EnsuresTopOnFront in
            // Application.Begin), which puts it over the marquee that was added after it. Without
            // this the marquee kept pulsing behind the application bar once the commit dialog had
            // closed, so a long commit looked like a hung gmd while a push, which opens no dialog,
            // showed it.
            currentParentView?.BringSubviewToFront(marquee.View);
            marquee.IsVisible = true;
            StartPulse(200);
        }
    }

    private void Deactivated()
    {
        StopPulse();
        if (marquee != null)
        {
            marquee.IsVisible = false;
        }
    }

    // Shows the marquee after the delay, and pulses it from then on, until stopped
    void StartPulse(int delay)
    {
        StopPulse();
        pulse = UI.AddTimeout(
            TimeSpan.FromMilliseconds(delay),
            _ =>
            {
                if (marquee == null)
                    return false;
                marquee.IsVisible = true;
                marquee.Pulse();
                pulse = UI.AddTimeout(
                    TimeSpan.FromMilliseconds(pulseInterval),
                    _ =>
                    {
                        marquee?.Pulse();
                        return marquee != null;
                    }
                );
                return false;
            }
        );
    }

    void StopPulse()
    {
        if (pulse != null)
            UI.RemoveTimeout(pulse);
        pulse = null;
    }

    void Stop()
    {
        count--;
        if (count > 0)
        { // Not yet the last stop
            return;
        }

        StopPulse();
        currentParentView!.Remove(marquee!.View);
        currentParentView = null;
        marquee = null;
        UI.SetActions(null, null);
        UI.StartInput();
    }
}
