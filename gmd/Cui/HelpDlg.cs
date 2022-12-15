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


        ContentView contentView = new ContentView(ToHelpText(content))
        { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() - 2, IsNoCursor = true };


        Label sep1 = new Label(0, height - 4, new string('─', width - 2))
        { ColorScheme = ColorSchemes.Indicator };

        var dialog = Components.Dialog("Help", width, height, Buttons.OK());

        dialog.Add(sep1, contentView);
        contentView.SetFocus();

        UI.RunDialog(dialog);
    }

    IEnumerable<Text> ToHelpText(string content)
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
                return Text.New.Cyan(row);
            }

            var text = Text.New;
            int index = 0;
            while (index < row.Length)
            {
                (var fragment, index) = GetFragment(row, index);
                text.Add(fragment);
            }

            return text;
        });

        return rows;
    }

    (Text, int) GetFragment(string row, int index)
    {
        char[] chars = new[] { '`', '*' };
        int i1 = row.IndexOfAny(chars, index);
        if (i1 == -1)
        {
            return (Text.New.White(row.Substring(index)), row.Length);
        }
        char c = row[i1];
        int i2 = row.IndexOf(c, i1 + 1);
        if (i2 == -1)
        {
            return (Text.New.White(row.Substring(index)), row.Length);
        }

        int l = i2 - i1 - 1;
        Text text = Text.New.White(row.Substring(index, i1 - index));
        return c switch
        {
            '`' => (text.Yellow(row.Substring(i1 + 1, l)), i1 + l + 2),
            '*' => (text.Blue(row.Substring(i1 + 1, l)), i1 + l + 2),
            _ => throw Asserter.FailFast("Unexpected char")
        };
    }
}
