using Terminal.Gui;

namespace gmd.Cui.Common;

// cspell:ignore Wakeup
static class MessageDlg
{
    const int pulseInterval = 100;

    internal static int ShowInfo(string title, string message, int defaultButton = 0, params string[] buttons) =>
        ShowFull(false, title, message, defaultButton, buttons);

    internal static int ShowError(string message, int defaultButton = 0, params string[] buttons) =>
        ShowFull(true, "Error !", message, defaultButton, buttons);

    internal static int ShowError(string title, string message, int defaultButton = 0, params string[] buttons) =>
        ShowFull(true, title, message, defaultButton, buttons);

    // Shows a message box with no buttons while the (already started) task is running, and closes
    // it once the task has completed. The dialog is run modally, which keeps pumping the main loop,
    // so the task keeps making progress while the message is shown (its awaits continue on the main
    // loop). The caller is expected to await the task afterwards to get at its result.
    internal static void ShowWhile(string title, string message, Task task)
    {
        var d = CreateDialog(false, title, message, []);

        // Only the modal itself is redrawn while it is running, so the Progress marquee on the
        // application bar below it would appear frozen. The box gets its own marquee instead, on
        // the row that the buttons of a normal message box would use.
        var marquee = new Marquee(Pos.Center(), Pos.AnchorEnd(1));
        d.Add(marquee.View);

        // The posted close can only run once the modal below is pumping the main loop, since this
        // is the main loop thread and it is blocked in Application.Run until then. Should the
        // dialog be closed some other way first, isClosed keeps the post from stopping whatever
        // toplevel happens to be current by then.
        var isClosed = false;
        task.ContinueWith(_ =>
            UI.Post(() =>
            {
                if (!isClosed)
                    Application.RequestStop(d);
            })
        );

        using (
            new Timer(
                _ =>
                {
                    marquee.Pulse();
                    Application.MainLoop.Driver.Wakeup();
                },
                null,
                pulseInterval,
                pulseInterval
            )
        )
        {
            // Run the modal; do not shutdown the main loop driver when done
            Application.Run(d);
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
        // Create button array for Dialog
        int count = 0;
        List<Button> buttonList = [];
        if (buttons != null && defaultButton > buttons.Length - 1)
        {
            defaultButton = buttons.Length - 1;
        }
        foreach (var s in buttons!)
        {
            var b = new Button(s) { ColorScheme = ColorSchemes.Button };
            if (count == defaultButton)
            {
                b.IsDefault = true;
            }
            buttonList.Add(b);
            count++;
        }

        var d = CreateDialog(useErrorColors, title, message, buttonList);

        // Setup actions
        Clicked = -1;
        for (int n = 0; n < buttonList.Count; n++)
        {
            int buttonId = n;
            var b = buttonList[n];
            b.Clicked += () =>
            {
                Clicked = buttonId;
                Application.RequestStop();
            };
            if (b.IsDefault)
            {
                b.SetFocus();
            }
        }

        // Run the modal; do not shutdown the main loop driver when done
        Application.Run(d);
        return Clicked;
    }

    // Creates the dialog sized to fit the message, with the buttons (if any) on its last row.
    static Dialog CreateDialog(bool useErrorColors, string title, string message, IReadOnlyList<Button> buttons)
    {
        int defaultWidth = 30;
        if (defaultWidth > Application.Driver.Cols / 2)
        {
            defaultWidth = (int)(Application.Driver.Cols * 0.60f);
        }
        int maxWidthLine = TextFormatter.MaxWidthLine(message);
        if (maxWidthLine > Application.Driver.Cols)
        {
            maxWidthLine = Application.Driver.Cols;
        }
        maxWidthLine = Math.Max(maxWidthLine, defaultWidth);

        int textWidth = Math.Min(TextFormatter.MaxWidth(message, maxWidthLine), Application.Driver.Cols);
        int textHeight = TextFormatter.MaxLines(message, textWidth); // message.Count (ustring.Make ('\n')) + 1;
        int buttonsHeight = buttons.Count > 0 ? 1 : 0;
        // textHeight + (top + top padding + buttons + bottom)
        int msgBoxHeight = Math.Min(Math.Max(1, textHeight) + 3 + buttonsHeight, Application.Driver.Rows);

        var d = new Dialog(title, buttons.ToArray())
        {
            Height = msgBoxHeight,
            Border = { Effect3D = false, BorderStyle = BorderStyle.Rounded },
            ColorScheme = useErrorColors ? ColorSchemes.ErrorDialog : ColorSchemes.InfoDialog,
        };

        d.Add(
            new Label(message)
            {
                LayoutStyle = LayoutStyle.Computed,
                TextAlignment = TextAlignment.Left,
                X = Pos.Center(),
                Y = Pos.Center(),
                Width = Dim.Fill(),
                Height = Dim.Fill(1),
                AutoSize = false,
                ColorScheme = ColorSchemes.Label,
            }
        );

        // Dynamically size Width, textWidth + (left + padding + padding + right)
        d.Width = Math.Min(
            Math.Max(maxWidthLine, Math.Max(title.Length, Math.Max(textWidth + 2, GetButtonsWidth(buttons)))),
            Application.Driver.Cols
        );

        return d;
    }

    public static int Clicked { get; private set; } = -1;

    internal static int GetButtonsWidth(IReadOnlyList<Button> buttons)
    {
        if (buttons.Count == 0)
        {
            return 0;
        }
        return buttons.Select(b => b.Bounds.Width).Sum();
    }
}
