using Terminal.Gui;

namespace gmd.Cui;

interface IProgress
{
    void While(Func<Task> action);
    T While<T>(Func<Task<T>> action);
}


[SingleInstance]
class Progress : IProgress
{
    const int intitialDelay = 800;
    const int progressWidth = 20;
    static readonly ColorScheme colorScheme = new ColorScheme() { Normal = Colors.Magenta };

    Timer? progressTimer;
    int count = 0;


    public void While(Func<Task> action)
    {
        Log.Info("Starting task with progress ...");
        UI.RunInBackground(async () =>
        {
            await action();
            Stop();
        });

        Show();
        Log.Info("Progress Done");
    }

    public T While<T>(Func<Task<T>> action)
    {
        Log.Info("Starting task with progress ...");

        T? result = default;
        UI.RunInBackground(async () =>
        {
            result = await action();
            Stop();
        });

        Show();
        Log.Info("Progress Done");
        return result!;
    }



    public void Show()
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
            ProgressBarStyle = ProgressBarStyle.MarqueeContinuous,
            BidirectionalMarquee = true,
            ColorScheme = colorScheme
        };

        var dialog = new Dialog("", progressWidth + 3, 3)
        {
            Border = { Effect3D = false, BorderStyle = BorderStyle.None },
            ColorScheme = colorScheme,
        };

        dialog.Add(progressBar);

        bool isFirstTime = false;
        progressTimer = new Timer(_ =>
        {
            if (!isFirstTime)
            {   // Show border after an intial short delay
                isFirstTime = true;
                dialog.Border.BorderStyle = BorderStyle.Rounded;
            }

            progressBar.Pulse();
            Application.MainLoop.Driver.Wakeup();
        }, null, intitialDelay, 100);

        Application.Run(dialog);
    }

    public void Stop()
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
        Application.RequestStop();
    }
}

