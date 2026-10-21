using System.Drawing;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace gmd.Cui.Common;

// The application, i.e. the main loop, the screen and the modal runs, for the UI. Terminal.Gui 2.x's
// application is an instance (IApplication), made by Program and kept here, where 1.x's was static.
static class UI
{
    static IApplication? app;

    // What is posted to the main loop, by Post and by the awaits on it, see MainLoopQueue
    static readonly MainLoopQueue queue = new();

    internal static IApplication App => app ?? throw Asserter.Fail("The application has not started");

    // Whether there is an application, i.e. a main loop, which a unit test of a view has not
    internal static bool HasApp => app != null;

    internal static void SetApp(IApplication application)
    {
        app = application;
        application.Iteration += (_, _) => queue.RunPosted();
        application.Paste += (_, e) =>
        {
            Paste(e.Text);
            e.Handled = true;
        };

        // Keys are dropped while git works (KeyGate): the application's KeyDown is raised before any
        // view gets the key, and a key marked handled there goes no further
        application.Keyboard.KeyDown += (_, key) =>
        {
            if (keys.IsDropping)
                key.Handled = true;
            else if (typing != null && TypedText(key) is string text)
            {
                typing(text);
                key.Handled = true;
            }
        };
    }

    // A paste from the terminal (a bracketed paste, which Terminal.Gui turns on) goes into the input
    // the keys go to, down the focus of the view on top, and nowhere else: not as keys into a view
    // that takes no text, where its letters would be keys of the log. Terminal.Gui 2.5 gives it to
    // the view it recorded as focused, which as a dialog opens is the dialog's inside rather than
    // the field in it that has the focus (Runnable.RaiseIsModalChangedEvent records Focused, not
    // MostFocused), so a paste into a dialog's first field went nowhere until a key moved the focus.
    static void Paste(string text)
    {
        if (Current?.MostFocused is IPasteTarget target)
            target.PasteText(text);
    }

    // Draws in the palette of the Colors setting ("auto", "dark", "light" or "16") and the terminal:
    // what it can do, and its own background, which Terminal.Gui asks it for as it starts and which
    // picks the light palette or the dark one; everything is drawn again when the palette changes
    internal static void UsePalette(string setting)
    {
        var driver = App.Driver;
        var palette = Palette.Choose(
            setting,
            driver?.ColorCapabilities?.Capability ?? ColorCapabilityLevel.TrueColor,
            driver?.DefaultAttribute?.Background
        );
        if (palette == Palette.Current && Terminal.Gui.Drivers.Driver.Force16Colors == palette.IsSixteen)
            return;

        Palette.Use(palette);
        Log.Info($"Colors {palette.Name} for '{setting}', terminal {driver?.ColorCapabilities?.Capability}");
        App.LayoutAndDraw(forceRedraw: true);
    }

    // The size of the screen, which a menu or a message box is fitted into
    internal static Size ScreenSize => App.Screen.Size;

    // The view of the runnable on top, i.e. the dialog or view keys go to now
    internal static View? Current => App.TopRunnableView;

    internal static void AssertOnUIThread() => Threading.AssertIsMainThread();

    internal static void RunInBackground(Func<Task> action)
    {
        action().RunInBackground();
    }

    // Runs the view modally, until it is stopped. A runnable Terminal.Gui runs is on top of the
    // others, and keys go to it alone: a key it does not use goes nowhere, rather than on to the log
    // view below, as one did with 1.x unless the view was marked modal ('P' in a diff pushed every
    // branch, and 'c' in the resolver reached the diff below it, which closed the resolver).
    internal static void RunDialog(IRunnable runnable)
    {
        using (EnableInput())
        {
            App.Run(runnable);
        }
    }

    static Action? onActivated;
    static Action? onDeactivated;

    internal static void SetActions(Action? deactivated, Action? activated)
    {
        onDeactivated = deactivated;
        onActivated = activated;
    }

    // Keys are dropped while git works, but not in a dialog, see KeyGate
    static readonly KeyGate keys = new();

    internal static void StopInput() => keys.GitStarted();

    internal static void StartInput() => keys.GitDone();

    // Takes the text typed while it is set, rather than letting each key act where it lands, e.g. as a
    // menu closes to hand what is typed on to a dialog: each letter typed meanwhile was handed on too,
    // and 'loc' opened three Find Branch dialogs, the one on top with 'c' alone (Menu.OnTypeText)
    internal static IDisposable CatchTyping(Action<string> onTyped)
    {
        typing = onTyped;
        return new Disposable(() => typing = null);
    }

    static Action<string>? typing;

    internal static bool IsCatchingTyping => typing != null;

    // The text a key types, or null for one that types none, e.g. an arrow, Esc or a Ctrl key
    static string? TypedText(Key key) =>
        // Ctrl and Alt both is AltGr on Windows, which types '@', '{' and '\' on many keyboards
        (key.IsCtrl == key.IsAlt)
        && key.AsRune.Value >= '!'
        && !System.Text.Rune.IsControl(key.AsRune)
            ? key.AsRune.ToString()
            : null;

    // Runs the action on the main loop, after what runs now, from any thread (MainLoopQueue). Not
    // Terminal.Gui's Invoke, which runs it at once when called on the main loop: gmd posts to have it
    // run after the key or the draw it is in is done, e.g. the menu's first move off a greyed out item,
    // which run at once, before the menu was laid out, moved nowhere and called itself again, without
    // end.
    internal static void Post(Action action) => queue.Post(action);

    // Runs the action at once when on the main loop, and posts it there from any other thread: what
    // follows a change that can come from a thread of its own, e.g. a status message, drawn in the
    // same iteration of the main loop as the change when it is the main loop's, rather than one later
    internal static void OnMainLoop(Action action)
    {
        if (app?.MainThreadId == Environment.CurrentManagedThreadId)
            action();
        else
            Post(action);
    }

    // Terminal.Gui 2.x shows the cursor only where a view puts it (View.Cursor), so there is nothing
    // to hide; kept for the callers, which hid it after 1.x had shown it
    internal static void HideCursor() { }

    // Calls the callback on the main loop after the timeout, and again after each timeout for as
    // long as it returns true
    internal static object AddTimeout(TimeSpan timeout, Func<bool> callback) => App.AddTimeout(timeout, callback)!;

    internal static object AddTimeout(TimeSpan timeout, Action callback) =>
        App.AddTimeout(
            timeout,
            () =>
            {
                callback();
                return false;
            }
        )!;

    internal static void RemoveTimeout(object token) => App.RemoveTimeout(token);

    // Stops the runnable on top, e.g. a dialog
    internal static void Shutdown() => App.RequestStop();

    // Ends the main loop from whichever thread a failure is on (ExceptionHandling); before the loop
    // runs there is none to end, and the exception thrown ends gmd
    internal static void ShutdownFromAnyThread()
    {
        if (app is IApplication a)
            a.Invoke(() => a.RequestStop());
    }

    internal static int InfoMessage(string title, string message, params string[] buttons)
    {
        return InfoMessage(title, message, 0, buttons);
    }

    internal static int InfoMessage(string title, string message, int defaultButton = 0, params string[] buttons)
    {
        buttons = buttons.Length == 0 ? ["OK"] : buttons;

        using (EnableInput())
        {
            return MessageDlg.ShowInfo(title, message, defaultButton, buttons);
        }
    }

    // Shows a message box while the (already started) task is running. The box is closed when the
    // task completes, so it cannot be dismissed by the user. With askCancel, it has a Cancel button,
    // which calls it, see MessageDlg.ShowWhile, and takes keys for it; without, it has no buttons,
    // and input is left as it is, i.e. a Progress spinner started by the caller drops the keys.
    internal static void ShowMessageWhile(string title, string message, Task task, Func<bool>? askCancel = null)
    {
        if (askCancel == null)
        {
            MessageDlg.ShowWhile(title, message, task);
            return;
        }

        using (EnableInput())
        {
            MessageDlg.ShowWhile(title, message, task, askCancel);
        }
    }

    internal static int ErrorMessage(string message, int defaultButton = 0, params string[] buttons)
    {
        return ErrorMessage("Error !", message, defaultButton, buttons);
    }

    internal static int ErrorMessage(string title, string message, int defaultButton = 0, params string[] buttons)
    {
        buttons = buttons.Length == 0 ? ["OK"] : buttons;

        using (EnableInput())
        {
            return MessageDlg.ShowError(title, message, defaultButton, buttons);
        }
    }

    static Disposable EnableInput()
    {
        keys.DialogOpened();
        onDeactivated?.Invoke();
        return new Disposable(() =>
        {
            keys.DialogClosed();
            onActivated?.Invoke();
        });
    }
}
