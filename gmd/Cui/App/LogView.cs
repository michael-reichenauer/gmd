using gmd.Common;
using gmd.Cui.Common;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Runnable = Terminal.Gui.Views.Runnable;

namespace gmd.Cui.App;

interface ILogView
{
    // Shows the log, at its end, which is what gmd just did
    void Show();
}

// gmd's own log, ~/gmd.log, the run before this one first (LogFile.ReadLines), so that what went
// wrong can be read and copied into a bug report without leaving gmd: Help › View Log. The lines
// are as written, warnings and errors in their colors, and read when the view opens, and again with
// 'r', rather than followed as they are written.
class LogView : ILogView
{
    readonly IClipboardService clipboard;
    readonly IHelpPage helpPage;
    readonly Config config;

    ContentView contentView = null!;
    UILabel header = null!;
    KeyHintBar hintBar = null!;
    IReadOnlyList<string> lines = [];
    int rowStartX = 0;
    int maxLength = 0;
    IStatusLine status = new StatusLine();

    // The bottom row, for the key hints, unless they are turned off
    int HintsHeight => config.ShowKeyHints ? 1 : 0;

    public LogView(IClipboardService clipboard, IHelpPage helpPage, Config config)
    {
        this.clipboard = clipboard;
        this.helpPage = helpPage;
        this.config = config;
    }

    public void Show()
    {
        rowStartX = 0;
        status = new StatusLine();

        Runnable logView = new Runnable()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        }.WithScheme(() => ColorSchemes.Window);

        header = new UILabel(0, 0);
        var border = new HorizontalLine() { X = 0, Y = 1 }.WithScheme(() => ColorSchemes.Border);
        hintBar = new KeyHintBar(() => LogHints.For(contentView.SelectCount > 0), () => status.Current);

        contentView = new ContentView(OnGetContent)
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Fill(HintsHeight),
            IsShowCursor = false,
            IsScrollMode = false,
            IsHighlightCurrentIndex = true,
        };

        logView.Add(header, border, contentView, hintBar);
        hintBar.FollowStatus(status, config.ShowKeyHints, logView);
        contentView.CurrentIndexChange += () => hintBar.SetNeedsDraw();
        RegisterShortcuts(contentView);

        Read();
        UI.RunDialog(logView);
    }

    void RegisterShortcuts(ContentView view)
    {
        view.RegisterKeyHandler(Key.Esc, () => UI.Shutdown());
        // Letters in both cases. The view is modal (see UI.RunDialog), so a key not registered here
        // does nothing rather than reaching the log view.
        view.RegisterLetterHandler(Key.Q, () => UI.Shutdown());
        view.RegisterKeyHandler((Key)'?', () => helpPage.ShowAsync(HelpPage.ProblemsSection).RunInBackground());
        view.RegisterKeyHandler(Key.F1, () => helpPage.ShowAsync(HelpPage.ProblemsSection).RunInBackground());

        view.RegisterKeyHandler(Key.CursorLeft, () => ScrollSideways(-1));
        view.RegisterKeyHandler(Key.CursorRight, () => ScrollSideways(1));
        view.RegisterWheelSideways(ScrollSideways);
        view.RegisterLetterHandler(Key.C, Copy);
        view.RegisterKeyHandler(Key.C.WithCtrl, Copy);
        view.RegisterLetterHandler(Key.R, Read);
    }

    // The lines anew, with the cursor on the last, which is the newest
    void Read()
    {
        lines = LogFile.ReadLines();
        maxLength = lines.Count > 0 ? lines.Max(l => l.Length) : 0;
        header.Text = Text.Dark("Log  ")
            .White(Files.ShortHomePath(LogFile.FilePath, Files.HomePath))
            .Dark($"   {lines.Count} lines");
        // The Text setter sizes the label from the text it is replacing
        header.Width = Dim.Fill();

        contentView.ClearSelection();
        contentView.WhenSized(() =>
        {
            contentView.SetTotalCount(lines.Count);
            contentView.ShowCurrentIndex(Math.Max(0, lines.Count - 1));
        });
        contentView.SetNeedsDraw();
    }

    (IEnumerable<Text> rows, int total) OnGetContent(int firstRow, int rowCount, int currentIndex, int contentWidth)
    {
        var rows = lines
            .Skip(firstRow)
            .Take(rowCount)
            .Select(line =>
                LogRows.ToText(line, rowStartX < line.Length ? line[rowStartX..] : "").Subtext(0, contentWidth)
            );
        return (rows, lines.Count);
    }

    // Moves the lines by the columns given, right for more than 0, no further left than their start,
    // nor right than where the longest ends
    void ScrollSideways(int columns)
    {
        var startX = Math.Clamp(rowStartX + columns, 0, Math.Max(0, maxLength - contentView.ContentWidth));
        if (startX != rowStartX)
        {
            rowStartX = startX;
            contentView.SetNeedsDraw();
        }
    }

    // The lines selected (Shift and the arrows), or else as much of the end of the log as a bug report
    // takes, see LogFile.MaxCopyLength
    void Copy()
    {
        var selected = contentView.SelectCount > 0;
        var (text, count) = selected
            ? (string.Join("\n", lines.Skip(contentView.SelectStartIndex).Take(contentView.SelectCount)), 0)
            : LogFile.Tail(lines, LogFile.MaxCopyLength);
        if (clipboard.Set(text) is Error e)
        {
            status.Failure($"Failed to copy the log: {e.Message}");
            return;
        }

        contentView.ClearSelection();
        status.Info(
            selected ? "Copied the lines selected"
            : count == lines.Count ? "Copied the log"
            : $"Copied the last {count} lines of the log, as many as a bug report takes"
        );
    }
}

// How a line of the log is drawn: the start of a run in bright magenta, a warning in yellow and an
// error in red, by the level written after its time, and the rest as is
static class LogRows
{
    internal static Text ToText(string line, string shown) =>
        line.StartsWith(LogFile.StartLinePrefix) ? Text.BrightMagenta(shown)
        : line.Contains(" ERROR:") ? Text.Red(shown)
        : line.Contains(" WARN :") ? Text.Yellow(shown)
        : Text.White(shown);
}

// The log view's key-hint line, see KeyHintLine
static class LogHints
{
    public static IReadOnlyList<KeyHint> For(bool isSelected) =>
        [new("Esc", "close"), new("c", isSelected ? "copy selected" : "copy"), new("r", "reload"), new("⇧↓", "select")];
}
