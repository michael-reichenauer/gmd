using gmd.Cui.Common;
using Terminal.Gui;

namespace gmd.Cui;

interface IHelpDlg
{
    void Show();
}

class HelpDlg : IHelpDlg
{
    const string helpFile = "gmd.doc.help.md";
    const int width = 80;
    const int height = 30;

    public void Show()
    {
        if (!Try(out var content, out var e, Files.GetEmbeddedFileContentText(helpFile)))
        {
            UI.ErrorMessage($"Failed to read help file,\n{e}");
            return;
        }

        var dlg = new UIDialog("Help", width, height);

        var contentView = dlg.AddContentView(0, 0, Dim.Fill(), Dim.Fill() - 2, ToHelpText(content));
        contentView.IsShowCursor = false;
        contentView.IsScrollMode = true;
        contentView.RegisterKeyHandler(Key.Esc, () => dlg.Close());

        dlg.AddDlgClose(true);
        dlg.Show(contentView);
    }

    IReadOnlyList<Text> ToHelpText(string content)
    {
        var rows = content.Split('\n').Select(row =>
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

    (Text, int) GetColoredFragment(string row, int index)
    {
        char[] chars = new[] { '`', '*' };
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
            _ => throw Asserter.FailFast("Unexpected char")
        };
    }
}
