using Terminal.Gui;

namespace gmd.Cui.Common;

class UIDialog
{
    readonly List<View> views = new List<View>();
    readonly List<Button> buttons = new List<Button>();
    readonly Dictionary<string, bool> buttonsClicked = new Dictionary<string, bool>();
    readonly Func<Key, bool>? onKey;
    Func<MouseEvent, bool>? onMouse;
    readonly Action<Dialog>? options;
    readonly TaskCompletionSource<bool> done = new TaskCompletionSource<bool>();

    record Validation(Func<bool> IsValid, string ErrorMsg);
    readonly List<Validation> validations = new List<Validation>();

    internal string Title { get; }
    internal Dim Width { get; }
    internal Dim Height { get; }

    internal bool IsOK => buttonsClicked.ContainsKey("OK");
    internal bool IsCanceled => buttonsClicked.ContainsKey("Cancel");

    public View View { get; internal set; } = null!;

    Dialog dlg = null!;

    internal UIDialog(string title, Dim width, Dim height,
        Func<Key, bool>? onKey = null, Action<Dialog>? options = null)
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

    internal UIComboTextField AddComboTextField(int x, int y, int w, int h, Func<IReadOnlyList<string>> getItems, string text = "")
    {
        var textField = new UIComboTextField(x, y, w - 2, h, getItems, text) { ColorScheme = ColorSchemes.TextField };
        views.Add(textField);

        var comboMarc = new Label(x + w - 1, y, "▼") { ColorScheme = ColorSchemes.Scrollbar };
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
        { X = x, Y = y, Width = w, Height = h, Text = text, ColorScheme = ColorSchemes.TextField };
        views.Add(textView);

        var indicator = new Label(textView.Frame.X - 1, textView.Frame.Y + textView.Frame.Height,
        "└" + new string('─', textView.Frame.Width) + "┘")
        { ColorScheme = ColorSchemes.Indicator };
        views.Add(indicator);

        return textView;
    }

    internal ContentView AddContentView(int x, int y, Dim w, Dim h, GetContentCallback onGetContent)
    {
        var contentView = new ContentView(onGetContent)
        { X = x, Y = y, Width = w, Height = h };
        views.Add(contentView);
        return contentView;
    }

    internal ContentView AddContentView(int x, int y, Dim w, Dim h, IReadOnlyList<Text> content)
    {
        var contentView = new ContentView(content)
        { X = x, Y = y, Width = w, Height = h };
        views.Add(contentView);
        return contentView;
    }


    internal CheckBox AddCheckBox(int x, int y, string name, bool isChecked)
    {
        var checkBox = new CheckBox(name, isChecked)
        { X = x, Y = y, ColorScheme = ColorSchemes.CheckBox };
        views.Add(checkBox);
        return checkBox;
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

    internal Button AddDlgCancel(bool isDefault = false, Func<bool>? clicked = null)
        => AddDlgButton("Cancel", isDefault, clicked);


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
        dlg = onKey != null || onMouse != null ?
            new CustomDialog(Title, buttons.ToArray(), onKey, onMouse)
            {
                Border = { Effect3D = false, BorderStyle = BorderStyle.Rounded },
                ColorScheme = ColorSchemes.Dialog,
                Width = Width,
                Height = Height,
            } :
            new Dialog(Title, buttons.ToArray())
            {
                Border = { Effect3D = false, BorderStyle = BorderStyle.Rounded },
                ColorScheme = ColorSchemes.Dialog,
                Width = Width,
                Height = Height,
            };
        View = dlg;
        if (options != null)
        {
            options(dlg);
        }
        dlg.Add(views.ToArray());

        onAfterAdd?.Invoke();

        if (setViewFocused != null)
        {
            setViewFocused.SetFocus();
        }

        Application.Driver.GetCursorVisibility(out var cursorVisible);
        if (setViewFocused is TextView || setViewFocused is TextField)
        {
            Application.Driver.SetCursorVisibility(CursorVisibility.Default);
        }

        if (onMouse != null) Application.GrabMouse(dlg);

        if (onAfterShow != null)
        {
            UI.AddTimeout(TimeSpan.FromMilliseconds(100), (_) => { onAfterShow(dlg); return false; });
        }
        UI.RunDialog(dlg);
        if (onMouse != null) Application.UngrabMouse();
        Application.Driver.SetCursorVisibility(cursorVisible);
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

class UILabel : View
{
    Text text;

    public UILabel(int x, int y, Text text) : base(x, y, text.ToString())
    {
        this.text = text;
        Width = text.Length;
        SetNeedsDisplay();
    }

    public override void Redraw(Rect bounds)
    {
        Clear();
        text.Draw(this, 0, 0);
    }

    public new Text Text
    {
        get => text;
        set
        {
            Width = text.Length;
            text = value;
            SetNeedsDisplay();
        }
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


class UIComboTextField : TextField
{
    private readonly int x;
    private readonly int y;
    private readonly int w;
    readonly int h;
    readonly Func<IReadOnlyList<string>> getItems;
    readonly Label borderTop;
    readonly List<Label> borderSides;
    readonly Label borderBottom;
    readonly ContentView listView;
    private List<string> items = new List<string>();
    IReadOnlyList<Text> itemTexts = new List<Text>();
    bool isShowList = false;

    internal UIComboTextField(int x, int y, int w, int h, Func<IReadOnlyList<string>> getItems, string text = "")
        : base(x, y, w, text)
    {
        ColorScheme = ColorSchemes.TextField;
        this.x = x;
        this.y = y;
        this.w = w;
        this.h = h;
        this.getItems = getItems;

        listView = new ContentView(OnGetContent) { X = x, Y = y + 2, Width = w + 2, Height = h - 1, };

        // For some reason the list view will not show border using the Border property, lets just draw it manually
        borderTop = new Label(x - 1, y + 1, "├" + new string('─', w + 2) + "┤") { ColorScheme = ColorSchemes.Scrollbar };
        borderSides = Enumerable.Range(0, h).Select(i => new Label(x - 1, y + 2 + i, "│" + new string('─', w + 2) + "│") { ColorScheme = ColorSchemes.Scrollbar }).ToList();
        borderBottom = new Label(x - 1, y + h + 1, "└" + new string('─', w + 2) + "┘") { ColorScheme = ColorSchemes.Scrollbar };
    }

    public override bool ProcessKey(KeyEvent keyEvent)
    {
        if (keyEvent.Key == Key.CursorDown)
        {   // User press down, show list view (if not already shown)
            if (isShowList) return base.ProcessKey(keyEvent);
            ShowListView();
            return true;
        }

        return base.ProcessKey(keyEvent);
    }

    public new string Text
    {
        get => base.Text?.ToString()?.Trim() ?? "";
        set => base.Text = value;
    }

    void ShowListView()
    {
        isShowList = true;
        items = getItems().ToList();
        itemTexts = items.Select(item => item.Length > w + 1
            ? Common.Text.Dark("…").White(item.Substring(item.Length - w)).ToText()
            : Common.Text.White(item.Max(w + 1, true)).ToText()).ToList();

        listView.RegisterKeyHandler(Key.Esc, () => CloseListView());

        listView.RegisterKeyHandler(Key.Enter, () =>
        {   // User select some item
            if (itemTexts.Count > 0)
            {
                UI.Post(() =>
                {
                    this.Text = items[listView.CurrentIndex].ToString().Trim();
                    this.CursorPosition = this.Text.Length;
                });
            };

            CloseListView();
        });

        listView.IsShowCursor = false;
        listView.IsScrollMode = false;
        listView.IsCursorMargin = false;
        listView.ColorScheme = ColorSchemes.TextField;
        listView.SetNeedsDisplay();

        Dialog dlg = (Dialog)this.SuperView.SuperView;
        dlg.Add(borderTop);
        borderSides.ForEach(bs => dlg.Add(bs));
        dlg.Add(borderBottom);
        dlg.Add(listView);
        dlg.SetNeedsDisplay();
    }

    void CloseListView()
    {
        Dialog dlg = (Dialog)this.SuperView.SuperView;
        dlg.Remove(listView);

        dlg.Remove(borderTop);
        borderSides.ForEach(bs => dlg.Remove(bs));
        dlg.Remove(borderBottom);
        dlg.SetNeedsDisplay();

        this.isShowList = false;
    }


    (IEnumerable<Text> rows, int total) OnGetContent(int firstIndex, int count, int currentIndex, int width)
    {
        var rows = itemTexts.Skip(firstIndex).Take(count).Select((item, i) =>
        {
            // Show selected or unselected commit row 
            var isSelectedRow = i + firstIndex == currentIndex;
            return isSelectedRow ? item.ToHighlight() : item;
        });

        return (rows, itemTexts.Count);
    }
}


class UITextView : TextView
{
    public override bool ProcessKey(KeyEvent keyEvent)
    {
        if (keyEvent.Key == Key.Tab)
        {   // Ensure tab sets focus on next control and not insert tab in text
            return false;
        }
        return base.ProcessKey(keyEvent);
    }

    public new string Text
    {
        get => base.Text?.ToString()?.Trim() ?? "";
        set => base.Text = value;
    }

    public override Border Border { get => new Border() { }; set => base.Border = value; }
}




