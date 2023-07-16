using Terminal.Gui;

namespace gmd.Cui.Common;


static class MessageDlg
{
    internal static int ShowInfo(string title, string message,
        int defaultButton = 0, params string[] buttons) =>
            ShowFull(false, 0, 0, title, message, defaultButton, buttons);

    internal static int ShowError(string message,
         int defaultButton = 0, params string[] buttons) =>
                ShowFull(true, 0, 0, "Error !", message, defaultButton, buttons);

    internal static int ShowError(string title, string message,
        int defaultButton = 0, params string[] buttons) =>
            ShowFull(true, 0, 0, title, message, defaultButton, buttons);


    static int ShowFull(bool useErrorColors, int width, int height, string title, string message,
            int defaultButton = 0, params string[] buttons)
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
        if (width == 0)
        {
            maxWidthLine = Math.Max(maxWidthLine, defaultWidth);
        }
        else
        {
            maxWidthLine = width;
        }
        int textWidth = Math.Min(TextFormatter.MaxWidth(message, maxWidthLine), Application.Driver.Cols);
        int textHeight = TextFormatter.MaxLines(message, textWidth); // message.Count (ustring.Make ('\n')) + 1;
        int msgBoxHeight = Math.Min(Math.Max(1, textHeight) + 4, Application.Driver.Rows); // textHeight + (top + top padding + buttons + bottom)

        // Create button array for Dialog
        int count = 0;
        List<Button> buttonList = new List<Button>();
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

        // Create Dialog (retain backwards compat by supporting specifying height/width)
        Dialog d;
        if (width == 0 & height == 0)
        {
            d = new Dialog(title, buttonList.ToArray())
            {
                Height = msgBoxHeight,
                Border = { Effect3D = false, BorderStyle = BorderStyle.Rounded },
                ColorScheme = ColorSchemes.InfoDialog,
            };
        }
        else
        {
            d = new Dialog(title, width, Math.Max(height, 4), buttonList.ToArray())
            {
                Border = { Effect3D = false, BorderStyle = BorderStyle.Rounded },
                ColorScheme = ColorSchemes.InfoDialog
            };
        }

        if (useErrorColors)
        {
            d.ColorScheme = ColorSchemes.ErrorDialog;
        }

        if (message != null)
        {
            var l = new Label(message)
            {
                LayoutStyle = LayoutStyle.Computed,
                TextAlignment = TextAlignment.Left,
                X = Pos.Center(),
                Y = Pos.Center(),
                Width = Dim.Fill(),
                Height = Dim.Fill(1),
                AutoSize = false,
                ColorScheme = ColorSchemes.Label,
            };
            d.Add(l);
        }

        if (width == 0 & height == 0)
        {
            // Dynamically size Width
            d.Width = Math.Min(Math.Max(maxWidthLine, Math.Max(title.Length, Math.Max(textWidth + 2, GetButtonsWidth(buttonList)))), Application.Driver.Cols); // textWidth + (left + padding + padding + right)
        }

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
