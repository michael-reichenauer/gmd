using gmd.Common.Spelling;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace gmd.Cui.Common;

enum InputMarkers
{
    None,
    Left,
    Right,
    Both,
}

// A mouse event as a dialog's own handler gets it (UIDialog.RegisterMouseHandler): the column and row
// in the dialog, its frame included, since the dialog has the mouse, and the buttons and keys of it
record MouseEvent(int X, int Y, MouseFlags Flags);

// Builds and shows a dialog: the Add* methods add the views (in UILabel.cs, UITextField.cs,
// UITextView.cs, UIComboTextField.cs, UIButton.cs, UICheckBox.cs, ContentView.cs and BorderView.cs)
// that the dialog is made of, and Show() puts them into a DialogView and runs it modally until a
// button closes it.
class UIDialog
{
    readonly List<View> views = [];
    readonly List<UIButton> buttons = [];
    readonly Dictionary<string, bool> buttonsClicked = [];
    readonly Func<Key, bool>? onKey;
    Func<MouseEvent, bool>? onMouse;
    readonly Action<View>? options;
    readonly TaskCompletionSource<bool> done = new TaskCompletionSource<bool>();

    record Validation(Func<bool> IsValid, string ErrorMsg);

    readonly List<Validation> validations = [];

    // The spell checked inputs, whose red words the hint let into the frame of the multi line one
    // counts; the hint is redrawn when any of them is
    readonly List<Func<int>> misspelledCounts = [];
    UILabel? spellHint;
    int spellHintWidth;

    internal string Title { get; }
    internal Dim Width { get; }
    internal Dim Height { get; }

    internal bool IsOK => buttonsClicked.ContainsKey("OK");

    public View View { get; internal set; } = null!;

    DialogView dlg = null!;

    // The views in the order they are added to the dialog, which is the order a mouse event finds
    // them in, last first
    internal IReadOnlyList<View> Views => views;

    internal UIDialog(string title, Dim width, Dim height, Func<Key, bool>? onKey = null, Action<View>? options = null)
    {
        Title = title;
        Width = width;
        Height = height;
        this.onKey = onKey;
        this.options = options;
    }

    public Task CloseAsync()
    {
        if (dlg?.IsRunning == true)
            dlg.RequestStop();
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
        var textField = new UITextField(x, y, w, text);
        views.Add(textField);

        views.Add(Indicator(x - 1, y + 1, "└" + new string('─', w) + "┘"));
        views.Add(Indicator(x - 1, y, "│"));
        views.Add(Indicator(x + w, y, "│"));

        return textField;
    }

    internal UITextField AddInputField(
        int x,
        int y,
        int w,
        string text = "",
        InputMarkers markers = InputMarkers.Right,
        ISpellChecker? spellChecker = null
    )
    {
        var textField = new UITextField(x, y, w, text) { SpellChecker = spellChecker };
        views.Add(textField);
        if (spellChecker != null)
        {
            misspelledCounts.Add(() => textField.MisspelledCount);
            textField.Redrawn += UpdateSpellHint;
        }

        if (markers == InputMarkers.Left || markers == InputMarkers.Both)
            views.Add(Indicator(x - 1, y, "["));
        if (markers == InputMarkers.Right || markers == InputMarkers.Both)
            views.Add(Indicator(x + w, y, "]"));

        return textField;
    }

    internal UITextView AddMultiLineInputView(
        int x,
        int y,
        int w,
        int h,
        string text,
        ISpellChecker? spellChecker = null
    )
    {
        var textView = new UITextView()
        {
            X = x,
            Y = y,
            Width = w,
            Height = h,
            Text = text,
            SpellChecker = spellChecker,
        };
        views.Add(textView);

        AddBorderView(textView, Color.Dark);
        if (spellChecker != null)
        {
            spellHintWidth = w + 2;
            spellHint = new UILabel(x - 1, y + h, SpellHint.Edge(0, spellHintWidth, Color.Dark), autosize: false);
            views.Add(spellHint);
            misspelledCounts.Add(() => textView.MisspelledCount);
            textView.Redrawn += UpdateSpellHint;
        }
        return textView;
    }

    // Called as an input is drawn, so the hint is set once the draw is done: Terminal.Gui marks every
    // view drawn once a draw is done, and the hint, drawn before the input, would not be drawn anew
    void UpdateSpellHint()
    {
        if (spellHint == null)
            return;
        var hint = SpellHint.Edge(misspelledCounts.Sum(count => count()), spellHintWidth, Color.Dark);
        if (hint.ToString() == spellHint.Text.ToString())
            return;
        UI.Post(() => spellHint.Text = hint);
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
        var textField = new UIComboTextField(x, y, w - 2, h, getItems, text);
        views.Add(textField);

        var comboMarc = new UILabel(x + w - 1, y, Text.BrightMagenta("▼"));
        comboMarc.Clicked += (_, _) => textField.OnMarkerClicked();
        views.Add(comboMarc);

        views.Add(Indicator(x - 1, y + 1, "└" + new string('─', w) + "┘"));
        views.Add(Indicator(x - 1, y, "│"));
        views.Add(Indicator(x + w, y, "│"));

        return textField;
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

    internal UICheckBox AddCheckBox(int x, int y, string name, bool isChecked)
    {
        var checkBox = new UICheckBox(name, isChecked) { X = x, Y = y };
        views.Add(checkBox);
        return checkBox;
    }

    // The border goes under the view it frames: the last view added is the one a mouse event finds,
    // and a border covers the whole rectangle, so one added over a text view took every click
    internal BorderView AddBorderView(View view, Color color)
    {
        var borderView = AddBorderView(view.X - 1, view.Y - 1, view.Width + 2, view.Height + 2, color);
        views.Remove(borderView);
        views.Insert(Math.Max(0, views.IndexOf(view)), borderView);
        return borderView;
    }

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

    internal UIButton AddButton(int x, int y, string text, Action clicked)
    {
        var button = new UIButton(text) { X = x, Y = y };
        button.Clicked += clicked;
        views.Add(button);
        return button;
    }

    UIButton AddDlgButton(string text, bool isDefault = false, bool isValidateAll = false)
    {
        var button = new UIButton(text, isDefault);
        button.Clicked += () =>
        {
            if (isValidateAll && !ValidateAll())
            {
                return;
            }

            buttonsClicked[text] = true;
            dlg.RequestStop();
        };
        buttons.Add(button);

        return button;
    }

    internal UIButton AddDlgOK(bool isDefault = true) => AddDlgButton("OK", isDefault, true);

    internal UIButton AddDlgCancel(bool isDefault = false) => AddDlgButton("Cancel", isDefault);

    internal UIButton AddDlgClose(bool isDefault = false) => AddDlgButton("Close", isDefault);

    internal void Add(View view)
    {
        views.Add(view);
    }

    // OK is the default, i.e. what Enter does, unless Cancel is asked for, for a dialog a slip of
    // Enter should not run. Cancel then has the focus too, since Enter on a focused button presses it
    // whatever the default is, so OK is a deliberate Left (or Tab) and Enter, or a click.
    internal bool ShowOkCancel(View? setViewFocused = null, bool isCancelDefault = false)
    {
        AddDlgOK(!isCancelDefault);
        var cancel = AddDlgCancel(isCancelDefault);
        return Show(setViewFocused ?? (isCancelDefault ? cancel : null));
    }

    internal bool Show(View? setViewFocused = null, Action? onAfterAdd = null)
    {
        dlg = new DialogView(Title, () => ColorSchemes.Dialog, onKey, onMouse)
        {
            Width = Width,
            Height = Height,
            InitialFocus = setViewFocused,
        };
        View = dlg;
        options?.Invoke(dlg);
        dlg.AddContent(views);
        dlg.AddButtons(buttons);

        onAfterAdd?.Invoke();

        UI.RunDialog(dlg);
        dlg.Dispose();
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

    // A mark beside an input, e.g. its end '│', in the dark of the markers
    static UILabel Indicator(int x, int y, string text) => new(x, y, Text.Dark(text));
}

// The dialog UIDialog runs, and the frame MessageDlg runs its boxes in: a rounded frame with the
// title in it, '╭ Title ───╮', the views inside the frame, at its column and row 0, and the buttons
// centered on the last row inside the frame, as gmd's dialogs have always looked. Enter presses the
// default button, unless the view with the focus uses it, and Esc closes the dialog, as Terminal.Gui's
// quit key does to the runnable on top. A dialog with keys of its own (onKey) gets them before its
// views do, and one with a mouse handler of its own (onMouse) has the mouse while it runs.
class DialogView : Runnable
{
    readonly string title;
    readonly Func<Key, bool>? onKey;
    readonly Func<MouseEvent, bool>? onMouse;
    readonly View inner;
    readonly List<UIButton> buttons = [];
    EventHandler<Key>? keyHandler;

    internal DialogView(
        string title,
        Func<Terminal.Gui.Drawing.Scheme> scheme,
        Func<Key, bool>? onKey = null,
        Func<MouseEvent, bool>? onMouse = null
    )
    {
        this.title = title;
        this.onKey = onKey;
        this.onMouse = onMouse;
        X = Pos.Center();
        Y = Pos.Center();
        CanFocus = true;
        this.WithScheme(scheme);

        inner = new View()
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(1),
            Height = Dim.Fill(1),
            CanFocus = true,
        }.WithScheme(() => ColorSchemes.Window);
        Add(inner);
    }

    // The view given the focus as the dialog opens, rather than its first one
    internal View? InitialFocus { get; init; }

    // What Esc does, where it otherwise closes the dialog, e.g. asks whether to cancel what runs
    internal Action? OnEsc { get; init; }

    // The inside of the frame, which the views are in
    internal View Inner => inner;

    internal void AddContent(IEnumerable<View> views)
    {
        foreach (var view in views)
            inner.Add(view);
    }

    // The buttons centered on the last row inside the frame, a space between each
    internal void AddButtons(IReadOnlyList<UIButton> dialogButtons)
    {
        buttons.AddRange(dialogButtons);
        int width = dialogButtons.Sum(b => b.Label.Length) + Math.Max(0, dialogButtons.Count - 1);
        int offset = 0;
        foreach (var button in dialogButtons)
        {
            var at = offset;
            button.X = Pos.Func(_ => Math.Max(0, (inner.Viewport.Width - width) / 2) + at, inner);
            button.Y = Pos.AnchorEnd(1);
            inner.Add(button);
            offset += button.Label.Length + 1;
        }
    }

    protected override void OnIsRunningChanged(bool newIsRunning)
    {
        base.OnIsRunningChanged(newIsRunning);
        if (newIsRunning)
        {
            InitialFocus?.SetFocus();
            if (onKey != null)
            {
                keyHandler = (_, key) =>
                {
                    if (UI.Current == this && !key.Handled && onKey(key))
                        key.Handled = true;
                };
                App!.Keyboard.KeyDown += keyHandler;
            }
            if (onMouse != null)
                App!.Mouse.GrabMouse(this);
            return;
        }

        if (keyHandler != null && App != null)
            App.Keyboard.KeyDown -= keyHandler;
        keyHandler = null;
        if (onMouse != null && App?.Mouse.IsGrabbed(this) == true)
            App.Mouse.UngrabMouse();
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        var (w, h) = (Viewport.Width, Viewport.Height);
        if (w < 2 || h < 2)
            return true;

        SetAttributeForRole(Terminal.Gui.Drawing.VisualRole.Normal);
        var top = title == "" ? "" : $" {title} ";
        top = top.Length > w - 2 ? top[..(w - 2)] : top;
        AddStr(0, 0, "╭" + top + new string('─', w - 2 - top.Length) + "╮");
        for (int i = 1; i < h - 1; i++)
        {
            AddStr(0, i, "│");
            AddStr(w - 1, i, "│");
        }
        AddStr(0, h - 1, "╰" + new string('─', w - 2) + "╯");
        return true;
    }

    protected override bool OnKeyDown(Key key)
    {
        if (key == Key.Esc && OnEsc != null)
        {
            OnEsc();
            return true;
        }
        if (key == Key.Enter && buttons.FirstOrDefault(b => b.IsDefault && b.Enabled) is UIButton button)
        {
            button.Press();
            return true;
        }
        return false;
    }

    protected override bool OnMouseEvent(Mouse mouse)
    {
        if (onMouse == null)
            return false;
        var at = mouse.Position ?? System.Drawing.Point.Empty;
        return onMouse(new MouseEvent(at.X, at.Y, mouse.Flags));
    }
}
