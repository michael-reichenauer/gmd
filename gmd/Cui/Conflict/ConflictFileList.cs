using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.Conflict;

// The list of conflicted files beside the resolver: every file the operation stopped on, in the
// order git gave them, those resolved since ticked, and the one open marked with how far it is. The
// rows and the index math, with no view, so it is unit testable.
record ConflictFileList(IReadOnlyList<ConflictedFile> Files, IReadOnlySet<string> Resolved, string Open)
{
    public static readonly ConflictFileList None = new([], new HashSet<string>(), "");

    // Drawn on a terminal at least this wide, where the two sides beside it still have room, as the
    // diff draws its list of files
    public const int MinTerminalWidth = 150;
    const int MinWidth = 24;
    const int MaxWidth = 40;

    public int LeftCount => Files.Count(f => !Resolved.Contains(f.Path));

    public int IndexOfOpen => Files.FindIndexBy(f => f.Path == Open);

    // How wide the list is drawn on a terminal of the width, its separator included, 0 when it is not
    public int WidthFor(int terminalWidth) =>
        Files.Count == 0 || terminalWidth < MinTerminalWidth
            ? 0
            : Math.Clamp(Files.Max(f => f.Path.Length) + 14, MinWidth, Math.Min(MaxWidth, terminalWidth / 5));

    // The next or the previous file still conflicted from the open one, round the end, "" for none
    public string NextOpen(int step)
    {
        var at = IndexOfOpen;
        for (var n = 1; n < Files.Count; n++)
        {
            var file = Files[((at + step * n) % Files.Count + Files.Count) % Files.Count];
            if (!Resolved.Contains(file.Path))
                return file.Path;
        }
        return "";
    }

    // The rows of the list, of the width with its separator: the title saying how many are left,
    // then a file per row, '✓' resolved and '●' not, the open one marked and saying how far it is,
    // e.g. '1 of 2', and the selected one highlighted while the list has the keys
    public IReadOnlyList<Text> Rows(int width, int selected, bool isFocused, string openProgress)
    {
        var textWidth = Math.Max(0, width - 2);
        var left = LeftCount;
        var title = Text.White("Conflicts ").Add(left == 0 ? Text.Green("all resolved") : Text.Red($"{left} left"));
        List<Text> rows = [Separated(title, textWidth)];
        for (var i = 0; i < Files.Count; i++)
        {
            var file = Files[i];
            var isOpen = file.Path == Open;
            var note = isOpen && openProgress != "" ? $"  {openProgress}" : "";
            var text = (isOpen ? Text.Magenta("▌") : Text.Black(" "))
                .Add(Resolved.Contains(file.Path) ? Text.Green("✓ ") : Text.Red("● "))
                .Add(PathText(file.Path, textWidth - 3 - note.Length))
                .Dark(note);
            var row = Separated(text, textWidth);
            rows.Add(i == selected && isFocused ? Highlighted(row, textWidth) : row);
        }
        return rows;
    }

    // A path, its folder dark so its name stands out, cut from the front to the width
    static Text PathText(string path, int width)
    {
        var slash = path.LastIndexOf('/');
        var text = Text.Dark(slash > 0 ? path[..(slash + 1)] : "")
            .White(slash > 0 ? path[(slash + 1)..] : path)
            .ToText();
        return text.Length <= width || width < 2
            ? text
            : Text.Dark("…").Add(text.Subtext(text.Length - (width - 1), width - 1));
    }

    static Text Separated(Text text, int width) => text.Subtext(0, width, isFillRest: true).ToTextBuilder().Dark(" │");

    static Text Highlighted(Text row, int width) =>
        row.Subtext(0, width).ToHighlight().ToTextBuilder().Add(row.Subtext(width, row.Length - width));
}
