using Terminal.Gui.ViewBase;

namespace gmd.Cui.Common;

static class MessageDlg
{
    const int pulseInterval = 100;

    internal static int ShowInfo(string title, string message, int defaultButton = 0, params string[] buttons) =>
        ShowFull(false, title, message, defaultButton, buttons);

    internal static int ShowError(string title, string message, int defaultButton = 0, params string[] buttons) =>
        ShowFull(true, title, message, defaultButton, buttons);

    // Shows a message box while the (already started) task is running, and closes it once the task
    // has completed. The dialog is run modally, which keeps pumping the main loop, so the task keeps
    // making progress while the message is shown (its awaits continue on the main loop). The caller
    // is expected to await the task afterwards to get at its result.
    //
    // With askCancel, a Cancel button, and Esc, call it, which asks the user and cancels the task if
    // so, saying whether it did; the box then says it is cancelling, until the task has stopped.
    // Without, it has no buttons.
    internal static void ShowWhile(string title, string message, Task task, Func<bool>? askCancel = null)
    {
        var cancelButton = askCancel == null ? null : new UIButton("Cancel");
        var isAsking = false;
        var isClosed = false;
        DialogView d = null!;

        // Asked while the task goes on, and the box is closed once both are done, since closing it
        // while the question is up would close the question instead
        void AskCancel()
        {
            if (askCancel == null || cancelButton is not { Enabled: true })
                return;

            isAsking = true;
            var isCancelled = askCancel();
            isAsking = false;
            if (task.IsCompleted)
            {
                d.RequestStop();
                return;
            }

            if (isCancelled)
            {
                cancelButton.Text = "Cancelling ...";
                cancelButton.Enabled = false;
            }
        }

        // The marquee a blank row below the text, and the button a blank row below the marquee: the
        // rows of the message, one blank and the marquee's, and the blank row a message box has above
        // its buttons. Without, the marquee is where a message box has that blank row.
        d =
            cancelButton == null
                ? CreateDialog(false, title, message + "\n", [])
                : CreateDialog(false, title, message + "\n\n", [cancelButton], AskCancel);
        if (cancelButton != null)
            cancelButton.Clicked += AskCancel;

        var marquee = new Marquee(Pos.Center(), Pos.AnchorEnd(cancelButton == null ? 1 : 3));
        d.Inner.Add(marquee.View);

        // The posted close can only run once the modal below is pumping the main loop, since this
        // is the main loop thread and it is blocked in Run until then. Should the dialog be closed
        // some other way first, isClosed keeps the post from stopping it again, and while the cancel
        // is asked, AskCancel closes it.
        task.ContinueWith(_ =>
            UI.Post(() =>
            {
                if (!isClosed && !isAsking && d.IsRunning)
                    d.RequestStop();
            })
        );

        // Pulsed on the main loop, as every view is touched: from a thread pool timer, an exception
        // ended the process, with no shutdown to give the terminal back
        var pulse = UI.AddTimeout(
            TimeSpan.FromMilliseconds(pulseInterval),
            () =>
            {
                marquee.Pulse();
                return true;
            }
        );
        try
        {
            UI.App.Run(d);
        }
        finally
        {
            UI.RemoveTimeout(pulse);
            d.Dispose();
        }
        isClosed = true;
    }

    static int ShowFull(
        bool useErrorColors,
        string title,
        string message,
        int defaultButton = 0,
        params string[] buttons
    )
    {
        if (defaultButton > buttons.Length - 1)
            defaultButton = buttons.Length - 1;
        var buttonList = buttons.Select((s, i) => new UIButton(s, i == defaultButton)).ToList();

        // The button clicked, -1 for none, i.e. Esc. Local to this box: a box shown over it, e.g. one
        // posted from the background, would otherwise leave its own answer for Esc here to return.
        var clicked = -1;
        var d = CreateDialog(
            useErrorColors,
            title,
            message,
            buttonList,
            focus: buttonList.ElementAtOrDefault(defaultButton)
        );
        for (int n = 0; n < buttonList.Count; n++)
        {
            int buttonId = n;
            buttonList[n].Clicked += () =>
            {
                clicked = buttonId;
                d.RequestStop();
            };
        }

        UI.App.Run(d);
        d.Dispose();
        return clicked;
    }

    // Creates the dialog sized to fit the message, with the buttons (if any) on its last row. With
    // onEsc, Esc calls it, where it otherwise closes the box.
    static DialogView CreateDialog(
        bool useErrorColors,
        string title,
        string message,
        IReadOnlyList<UIButton> buttons,
        Action? onEsc = null,
        View? focus = null
    )
    {
        var (cols, rows) = (UI.ScreenSize.Width, UI.ScreenSize.Height);
        int defaultWidth = 30;
        if (defaultWidth > cols / 2)
        {
            defaultWidth = (int)(cols * 0.60f);
        }
        int maxWidthLine = Math.Min(Lines(message).Max(l => TextWidth.Of(l)), cols);
        maxWidthLine = Math.Max(maxWidthLine, defaultWidth);

        var lines = Wrap(message, maxWidthLine);
        int textWidth = Math.Min(lines.Max(l => TextWidth.Of(l)), cols);
        int textHeight = lines.Count;
        int buttonsHeight = buttons.Count > 0 ? 1 : 0;
        // textHeight + (top + top padding + buttons + bottom)
        int msgBoxHeight = Math.Min(Math.Max(1, textHeight) + 3 + buttonsHeight, rows);
        int buttonsWidth = buttons.Sum(b => b.Label.Length) + Math.Max(0, buttons.Count - 1);

        var d = new DialogView(title, () => useErrorColors ? ColorSchemes.ErrorDialog : ColorSchemes.InfoDialog)
        {
            Height = msgBoxHeight,
            // textWidth + (left + right), as wide as the title and the buttons too, which are inside
            // the borders as the text is: without them a row of buttons wider than the text was cut
            Width = Math.Min(
                Math.Max(maxWidthLine, Math.Max(title.Length, Math.Max(textWidth, buttonsWidth) + 2)),
                cols
            ),
            InitialFocus = focus,
            OnEsc = onEsc,
        };
        d.AddContent(lines.Select((line, i) => new UILabel(0, i, Text.White(line))).ToList());
        d.AddButtons(buttons);
        return d;
    }

    static IReadOnlyList<string> Lines(string message) => message.Replace("\r\n", "\n").Split('\n');

    // The lines of the message, each wrapped at the width, at a space where there is one
    static List<string> Wrap(string message, int width)
    {
        List<string> wrapped = [];
        foreach (var line in Lines(message))
        {
            var rest = line;
            while (TextWidth.Of(rest) > width)
            {
                var cut = TextWidth.Slice(rest, 0, 0, width);
                var space = cut.LastIndexOf(' ');
                var at = space > 0 ? space : cut.Length;
                wrapped.Add(rest[..at].TrimEnd());
                rest = rest[at..].TrimStart();
            }
            wrapped.Add(rest);
        }
        return wrapped;
    }
}
