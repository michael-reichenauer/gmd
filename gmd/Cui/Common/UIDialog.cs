using Terminal.Gui;

namespace gmd.Cui.Common;

enum InputMarkers
{
    None,
    Left,
    Right,
    Both,
}

// Builds and shows a dialog: the Add* methods add the views (in UILabel.cs, UITextField.cs,
// UITextView.cs, UIComboTextField.cs, ContentView.cs and BorderView.cs) that the dialog is made
// of, and Show() puts them into a Terminal.Gui Dialog and runs it modally until a button closes it.
class UIDialog
{
    readonly List<View> views = [];
    readonly List<Button> buttons = [];
    readonly Dictionary<string, bool> buttonsClicked = [];
    readonly Func<Key, bool>? onKey;
    Func<MouseEvent, bool>? onMouse;
    readonly Action<Dialog>? options;
    readonly TaskCompletionSource<bool> done = new TaskCompletionSource<bool>();

    record Validation(Func<bool> IsValid, string ErrorMsg);

    readonly List<Validation> validations = [];

    internal string Title { get; }
    internal Dim Width { get; }
    internal Dim Height { get; }

    internal bool IsOK => buttonsClicked.ContainsKey("OK");
    internal bool IsCanceled => buttonsClicked.ContainsKey("Cancel");

    public View View { get; internal set; } = null!;

    Dialog dlg = null!;

    internal UIDialog(
        string title,
        Dim width,
        Dim height,
        Func<Key, bool>? onKey = null,
        Action<Dialog>? options = null
    )
    {
        Title = title;
        Width = width;
        Height = height;
        this.onKey = onKey;
        this.options = options;
    }

    public Task CloseAsync()
    {
        Application.RequestStop();
        return done.Task;
    }

    public void Close() => CloseAsync().RunInBackground();

    public void RegisterMouseHandler(Func<MouseEvent, bool> onMouse)
    {
        this.onMouse = onMouse;
    }

    internal UILabel AddLabel(int x, int y, string text = "") => AddLabel(x, y, Text.White(text == "" ? " " : text));

    internal UILabel AddLabel(int x, int y, Text text)
    {
        var label = new UILabel(x, y, text);
        views.Add(label);
        return label;
    }

    internal UITextField AddTextField(int x, int y, int w, string text = "")
    {
        var textField = new UITextField(x, y, w, text) { ColorScheme = ColorSchemes.TextField };
        views.Add(textField);

        var line = new Label(x - 1, y + 1, "└" + new string('─', w) + "┘") { ColorScheme = ColorSchemes.Indicator };
        views.Add(line);

        var startMark = new Label(x - 1, y, "│") { ColorScheme = ColorSchemes.Indicator };
        views.Add(startMark);
        var endMarc = new Label(x + w, y, "│") { ColorScheme = ColorSchemes.Indicator };
        views.Add(endMarc);

        return textField;
    }

    internal UITextField AddInputField(int x, int y, int w, string text = "", InputMarkers markers = InputMarkers.Right)
    {
        var textField = new UITextField(x, y, w, text) { ColorScheme = ColorSchemes.TextField };
        views.Add(textField);

        if (markers == InputMarkers.Left || markers == InputMarkers.Both)
        {
            var startMark = new Label(x - 1, y, "[") { ColorScheme = ColorSchemes.Indicator };
            views.Add(startMark);
        }
        if (markers == InputMarkers.Right || markers == InputMarkers.Both)
        {
            var endMarc = new Label(x + w, y, "]") { ColorScheme = ColorSchemes.Indicator };
            views.Add(endMarc);
        }

        return textField;
    }

    internal UITextView AddMultiLineInputView(int x, int y, int w, int h, string text)
    {
        var textView = new UITextView()
        {
            X = x,
            Y = y,
            Width = w,
            Height = h,
            Text = text,
            ColorScheme = ColorSchemes.TextField,
        };
        views.Add(textView);

        AddBorderView(textView, Color.Dark);
        return textView;
    }

    internal UIComboTextField AddComboTextField(
        int x,
        int y,
        int w,
        int h,
        Func<IReadOnlyList<string>> getItems,
        string text = ""
    )
    {
        var textField = new UIComboTextField(x, y, w - 2, h, getItems, text) { ColorScheme = ColorSchemes.TextField };
        views.Add(textField);

        var comboMarc = new Label(x + w - 1, y, "▼") { ColorScheme = ColorSchemes.Scrollbar };
        comboMarc.MouseClick += (e) => textField.OnFieldMouseClicked(e);
        views.Add(comboMarc);

        var line = new Label(x - 1, y + 1, "└" + new string('─', w) + "┘") { ColorScheme = ColorSchemes.Indicator };
        views.Add(line);

        var startMark = new Label(x - 1, y, "│") { ColorScheme = ColorSchemes.Indicator };
        views.Add(startMark);
        var endMark = new Label(x + w, y, "│") { ColorScheme = ColorSchemes.Indicator };
        views.Add(endMark);

        return textField;
    }

    internal UITextView AddTextView(int x, int y, int w, int h, string text)
    {
        var textView = new UITextView()
        {
            X = x,
            Y = y,
            Width = w,
            Height = h,
            Text = text,
            ColorScheme = ColorSchemes.TextField,
        };
        views.Add(textView);

        var indicator = new Label(
            textView.Frame.X - 1,
            textView.Frame.Y + textView.Frame.Height,
            "└" + new string('─', textView.Frame.Width) + "┘"
        )
        {
            ColorScheme = ColorSchemes.Indicator,
        };
        views.Add(indicator);

        return textView;
    }

    internal ContentView AddContentView(int x, int y, Dim w, Dim h, GetContentCallback onGetContent)
    {
        var contentView = new ContentView(onGetContent)
        {
            X = x,
            Y = y,
            Width = w,
            Height = h,
        };
        views.Add(contentView);
        return contentView;
    }

    internal ContentView AddContentView(int x, int y, Dim w, Dim h, IReadOnlyList<Text> content)
    {
        var contentView = new ContentView(content)
        {
            X = x,
            Y = y,
            Width = w,
            Height = h,
        };
        views.Add(contentView);
        return contentView;
    }

    internal CheckBox AddCheckBox(int x, int y, string name, bool isChecked)
    {
        var checkBox = new CheckBox(name, isChecked)
        {
            X = x,
            Y = y,
            ColorScheme = ColorSchemes.CheckBox,
        };
        views.Add(checkBox);
        return checkBox;
    }

    internal BorderView AddBorderView(View view, Color color) =>
        AddBorderView(view.X - 1, view.Y - 1, view.Width + 2, view.Height + 2, color);

    internal BorderView AddBorderView(Pos x, Pos y, Dim w, Dim h, Color color)
    {
        var borderView = new BorderView(color)
        {
            X = x,
            Y = y,
            Width = w,
            Height = h,
        };
        views.Add(borderView);
        return borderView;
    }

    internal Button AddButton(int x, int y, string text, Action clicked)
    {
        Button button = new Button(text) { ColorScheme = ColorSchemes.Button };
        button.Clicked += () => clicked();

        button.X = x;
        button.Y = y;
        views.Add(button);
        return button;
    }

    Button AddDlgButton(string text, bool isDefault = false, Func<bool>? clicked = null, bool isValidateAll = false)
    {
        Button button = new Button(text, isDefault) { ColorScheme = ColorSchemes.Button };
        button.Clicked += () =>
        {
            if (isValidateAll && !ValidateAll())
            {
                return;
            }

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

    internal Button AddDlgOK(bool isDefault = true, Func<bool>? clicked = null) =>
        AddDlgButton("OK", isDefault, clicked, true);

    internal Button AddDlgCancel(bool isDefault = false, Func<bool>? clicked = null) =>
        AddDlgButton("Cancel", isDefault, clicked);

    internal Button AddDlgClose(bool isDefault = false, Func<bool>? clicked = null) =>
        AddDlgButton("Close", isDefault, clicked);

    internal void Add(View view)
    {
        views.Add(view);
    }

    internal bool ShowOkCancel(View? setViewFocused = null)
    {
        AddDlgOK();
        AddDlgCancel();
        return Show(setViewFocused);
    }

    internal bool Show(View? setViewFocused = null, Action? onAfterAdd = null, Action<View>? onAfterShow = null)
    {
        dlg =
            onKey != null || onMouse != null
                ? new CustomDialog(Title, buttons.ToArray(), onKey, onMouse)
                {
                    Border = { Effect3D = false, BorderStyle = BorderStyle.Rounded },
                    ColorScheme = ColorSchemes.Dialog,
                    Width = Width,
                    Height = Height,
                }
                : new Dialog(Title, buttons.ToArray())
                {
                    Border = { Effect3D = false, BorderStyle = BorderStyle.Rounded },
                    ColorScheme = ColorSchemes.Dialog,
                    Width = Width,
                    Height = Height,
                };
        View = dlg;
        options?.Invoke(dlg);
        dlg.Add(views.ToArray());

        onAfterAdd?.Invoke();
        setViewFocused?.SetFocus();

        // Application.Driver.GetCursorVisibility(out var cursorVisible);
        // if (setViewFocused is TextView || setViewFocused is TextField)
        // {
        //     Application.Driver.SetCursorVisibility(CursorVisibility.Default);
        // }

        if (onMouse != null)
            Application.GrabMouse(dlg);

        if (onAfterShow != null)
        {
            UI.AddTimeout(
                TimeSpan.FromMilliseconds(100),
                (_) =>
                {
                    onAfterShow(dlg);
                    return false;
                }
            );
        }
        UI.RunDialog(dlg);
        if (onMouse != null)
            Application.UngrabMouse();
        Application.Driver.SetCursorVisibility(CursorVisibility.Invisible);
        done.TrySetResult(true);
        return IsOK;
    }

    internal void Validate(Func<bool> IsValid, string errorMsg)
    {
        validations.Add(new Validation(IsValid, errorMsg));
    }

    bool ValidateAll()
    {
        foreach (var validation in validations)
        {
            if (!validation.IsValid())
            {
                UI.ErrorMessage(validation.ErrorMsg);
                return false;
            }
        }
        return true;
    }

    internal UILabel AddLine(int x, int y, int width)
    {
        return AddLabel(x, y, new string('─', width));
    }

    class CustomDialog : Dialog
    {
        private readonly Func<Key, bool>? onKey;
        private readonly Func<MouseEvent, bool>? onMouse;

        public CustomDialog(string title, Button[] buttons, Func<Key, bool>? onKey, Func<MouseEvent, bool>? onMouse)
            : base(title, buttons)
        {
            this.onKey = onKey;
            this.onMouse = onMouse;
        }

        public override bool ProcessHotKey(KeyEvent keyEvent) => onKey?.Invoke(keyEvent.Key) ?? false;

        public override bool MouseEvent(MouseEvent ev) => onMouse?.Invoke(ev) ?? false;
    }
}
