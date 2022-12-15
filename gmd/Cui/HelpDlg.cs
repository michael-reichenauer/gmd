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
                int i1 = row.IndexOf('`', index);
                if (i1 != -1)
                {   // Maybe a code fragment
                    int i2 = row.IndexOf('`', i1 + 1);
                    if (i1 != -1)
                    {   // I a code fragment
                        text.White(row.Substring(index, i1 - index));
                        text.Yellow(row.Substring(i1 + 1, i2 - i1 - 1));
                        index = i2 + 1;
                        continue;
                    }
                }
                text.White(row.Substring(index));
                break;
            }

            return text;
        });

        return rows;
    }
}
