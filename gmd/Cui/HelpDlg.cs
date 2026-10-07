using gmd.Cui.Common;
using Terminal.Gui;

namespace gmd.Cui;

interface IHelpDlg
{
    // The help, at the section of that name ('## <section>' in help.md), or at the top
    void Show(string section = "");
}

class HelpDlg : IHelpDlg
{
    internal const string HelpFile = "gmd.doc.help.md";

    // The sections the side views open the help at, so it starts at what is about them
    internal const string DiffSection = "Diff and Blame";
    internal const string ConflictSection = "Resolving Conflicts";

    const int width = 80;
    const int minHeight = 10;

    // The widest help row that is shown whole. The rows are not wrapped, so a longer one is cut
    // off at the dialog's right border, and the scroll bar is drawn over the last column inside it.
    // The box is no wider on a wide terminal: the text is written to this width, and longer lines
    // would read worse, while rewrapping it would break its tables and pictures. It is as tall as
    // the terminal, though, since more of it on a screen is what makes it quicker to read.
    internal const int TextWidth = width - 3;

    public void Show(string section = "")
    {
        var contentResult = Files.GetEmbeddedFileContentText(HelpFile);
        if (contentResult is not string content)
        {
            UI.ErrorMessage($"Failed to read help file,\n{contentResult.Error}");
            return;
        }

        var rows = ToHelpText(content);
        var sections = Sections(content);
        var height = Math.Max(minHeight, Application.Driver.Rows - 2);
        var dlg = new UIDialog("Help: m sections, ] [ next and previous", width, height);

        // Moved to the section once it has been laid out, since a scroll needs the view's height,
        // and posted from its first draw rather than done in it, as the conflict view does
        var isAtSection = section == "";
        ContentView contentView = null!;
        contentView = dlg.AddContentView(
            0,
            0,
            Dim.Fill(),
            Dim.Fill() - 2,
            (first, count, _, _) =>
            {
                if (!isAtSection)
                {
                    isAtSection = true;
                    if (sections.FirstOrDefault(s => s.Name == section) is var (_, index) && index > 0)
                        UI.Post(() => ScrollTo(contentView, index));
                }
                return (rows.Skip(first).Take(count), rows.Count);
            }
        );
        contentView.IsShowCursor = false;
        contentView.IsScrollMode = true;
        contentView.RegisterKeyHandler(Key.Esc, () => dlg.Close());

        // The sections, to jump to from the list or step through, since the help is many screens
        contentView.RegisterKeyHandler(
            Key.m,
            () =>
                Menu.Show(
                    "Sections",
                    Menu.Center,
                    0,
                    sections.Select(s => Menu.Item(s.Name, "", () => ScrollTo(contentView, s.Index)))
                )
        );
        contentView.RegisterKeyHandler((Key)']', () => StepSection(contentView, sections, 1));
        contentView.RegisterKeyHandler((Key)'[', () => StepSection(contentView, sections, -1));

        dlg.AddDlgClose(true);
        dlg.Show(contentView);
    }

    // The '## ' headings and the rows they are on, which are the lines of the help as written
    internal static IReadOnlyList<(string Name, int Index)> Sections(string content) =>
        content
            .Split('\n')
            .Select((line, index) => (Line: line.TrimEnd('\r'), Index: index))
            .Where(l => l.Line.StartsWith("## "))
            .Select(l => (l.Line[3..].Trim(), l.Index))
            .ToList();

    // The section after the one at the top of the view, or the one before it, or this one's
    // heading when it is scrolled into rather than at its top
    internal static int SectionIndexFrom(IReadOnlyList<(string Name, int Index)> sections, int firstIndex, int step) =>
        step > 0
            ? sections.Select(s => s.Index).FirstOrDefault(i => i > firstIndex, -1)
            : sections.Select(s => s.Index).LastOrDefault(i => i < firstIndex, 0);

    static void StepSection(ContentView view, IReadOnlyList<(string Name, int Index)> sections, int step)
    {
        var index = SectionIndexFrom(sections, view.FirstIndex, step);
        if (index >= 0)
            ScrollTo(view, index);
    }

    static void ScrollTo(ContentView view, int index) => view.Move(index - view.FirstIndex);

    internal static IReadOnlyList<Text> ToHelpText(string content)
    {
        var rows = content
            .Split('\n')
            .Select(row =>
            {
                row = row.TrimSuffix("\\");
                if (row.StartsWith("* "))
                {
                    row = "● " + row.Substring(2);
                }

                if (row.StartsWith("#"))
                {
                    return Text.Cyan(row);
                }

                var text = new TextBuilder();
                int index = 0;
                while (index < row.Length)
                {
                    (var fragment, index) = GetColoredFragment(row, index);
                    text.Add(fragment);
                }

                return text.ToText();
            });

        return rows.ToList();
    }

    static (Text, int) GetColoredFragment(string row, int index)
    {
        char[] chars = ['`', '*'];
        int i1 = row.IndexOfAny(chars, index);
        if (i1 == -1)
        {
            return (Text.White(row.Substring(index)), row.Length);
        }
        char c = row[i1];
        int i2 = row.IndexOf(c, i1 + 1);
        if (i2 == -1)
        {
            return (Text.White(row.Substring(index)), row.Length);
        }

        int l = i2 - i1 - 1;
        var text = Text.White(row.Substring(index, i1 - index));
        return c switch
        {
            '`' => (text.Yellow(row.Substring(i1 + 1, l)), i1 + l + 2),
            '*' => (text.Blue(row.Substring(i1 + 1, l)), i1 + l + 2),
            _ => throw Asserter.FailFast("Unexpected char"),
        };
    }
}
