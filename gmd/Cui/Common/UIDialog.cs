using Terminal.Gui;

namespace gmd.Cui.Common;

class UIDialog
{
    List<View> views = new List<View>();
    List<Button> buttons = new List<Button>();
    Dictionary<string, bool> buttonsClicked = new Dictionary<string, bool>();

    internal string Title { get; }
    internal int Width { get; }
    internal int Height { get; }

    internal bool IsOK => buttonsClicked.ContainsKey("OK");
    internal bool IsCanceled => buttonsClicked.ContainsKey("Cancel");


    internal UIDialog(string title, int width, int height)
    {
        Title = title;
        Width = width;
        Height = height;
    }


    internal Label AddLabel(int x, int y, string text)
    {
        var label = new Label(x, y, text) { ColorScheme = ColorSchemes.Label };
        views.Add(label);
        return label;
    }

    internal UITextField AddTextField(int x, int y, int w, string text = "")
    {
        var textField = new UITextField(x, y, w, text) { ColorScheme = ColorSchemes.TextField };
        views.Add(textField);
        var indicator = new Label(textField.Frame.X - 1, textField.Frame.Y + textField.Frame.Height,
            "└" + new string('─', textField.Frame.Width) + "┘")
        { ColorScheme = ColorSchemes.Indicator };
        views.Add(indicator);
        return textField;
    }

    internal TextView AddTextView(int x, int y, int w, int h, string text)
    {
        var textView = new TextView()
        { X = x, Y = y, Width = w, Height = h, Text = text, ColorScheme = ColorSchemes.TextField };
        views.Add(textView);
        return textView;
    }

    internal CheckBox AddCheckBox(string name, bool isChecked, int x, int y)
    {
        var checkBox = new CheckBox(name, isChecked)
        { X = x, Y = y, ColorScheme = ColorSchemes.CheckBox };
        views.Add(checkBox);
        return checkBox;
    }

    internal Button AddButton(string text, int x, int y, Action clicked)
    {
        var button = Buttons.Button(text, clicked);
        button.X = x;
        button.Y = y;
        views.Add(button);
        return button;
    }

    Button AddButton(string text, bool isDefault = false, Func<bool>? clicked = null)
    {
        Button button = new Button(text, isDefault) { ColorScheme = ColorSchemes.Button };
        button.Clicked += () =>
        {
            buttonsClicked[text] = true;
            if (clicked != null && !clicked())
            {
                return;
            }
            Application.RequestStop();
        };
        buttons.Add(button);

        return button;
    }

    internal Button AddOK(bool isDefault = true, Func<bool>? clicked = null) =>
        AddButton("OK", isDefault, clicked);

    internal Button AddCancel(bool isDefault = false, Func<bool>? clicked = null)
        => AddButton("Cancel", isDefault, clicked);


    internal bool Show(View? setViewFocused = null)
    {
        var dlg = new Dialog(Title, Width, Height, buttons.ToArray())
        {
            Border = { Effect3D = false, BorderStyle = BorderStyle.Rounded },
            ColorScheme = ColorSchemes.Dialog,
        };
        dlg.Add(views.ToArray());

        if (setViewFocused != null)
        {
            setViewFocused.SetFocus();
        }

        Application.Driver.GetCursorVisibility(out var cursorVisible);
        Application.Driver.SetCursorVisibility(CursorVisibility.Default);
        UI.RunDialog(dlg);
        Application.Driver.SetCursorVisibility(cursorVisible);
        return IsOK;
    }
}


class UITextField : TextField
{
    internal UITextField(int x, int y, int w, string text = "")
        : base(x, y, w, text)
    {
        ColorScheme = ColorSchemes.TextField;
    }

    public new string Text
    {
        get => base.Text?.ToString()?.Trim() ?? "";
        set => base.Text = value;
    }
}


