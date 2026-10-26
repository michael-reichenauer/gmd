using Terminal.Gui.Input;

namespace gmd.Cui.Common;

// A question about a list of things, all of which the answer acts on: the question above, the list
// below it in a frame, scrolled rather than cut however long it is, a note under it, e.g. what brings
// them back, and the buttons. A message box lists only what fits and says "and 40 more", which hid
// the very files a discard was about among the ones it was not (Discard All Changes listed the
// ignored files, not the changed ones). The list has the focus, so the arrows and the wheel scroll it,
// Enter presses the default button, and the letter after a button's '_' that button, as in ListDlg.
static class ConfirmListDlg
{
    const int MaxListHeight = 12;
    const int MinWidth = 50;

    // The button pressed, -1 for none, i.e. Esc or the default when it is Cancel
    internal static int Show(
        string title,
        string question,
        IReadOnlyList<Text> rows,
        string note,
        int defaultButton,
        params string[] buttons
    )
    {
        Asserter.Requires(buttons.Length > 0 && defaultButton >= 0 && defaultButton < buttons.Length);

        var questionLines = question.Split('\n');
        var noteLines = note == "" ? [] : note.Split('\n');
        var buttonsWidth = buttons.Sum(b => b.Replace("_", "").Length + 4 + 2);
        var textWidth = questionLines.Concat(noteLines).Select(TextWidth.Of).Append(buttonsWidth).Max();
        var rowsWidth = rows.Select(r => r.Length + 2).DefaultIfEmpty(0).Max();
        var width = Math.Min(Math.Max(MinWidth, Math.Max(textWidth, rowsWidth) + 6), UI.ScreenSize.Width - 2);
        var listWidth = width - 6;
        var listHeight = Math.Clamp(rows.Count, 1, MaxListHeight);

        // The question, a blank row, the list in its frame, a blank row, the note, a blank row if there
        // is one, and the buttons
        var listY = questionLines.Length + 2;
        var noteY = listY + listHeight + 2;
        var buttonsY = noteY + (noteLines.Length > 0 ? noteLines.Length + 1 : 0);
        var height = buttonsY + 3;

        var dlg = new UIDialog(title, width, height);
        for (var i = 0; i < questionLines.Length; i++)
            dlg.AddLabel(2, i, questionLines[i]);

        var list = dlg.AddContentView(
            2,
            listY,
            listWidth,
            listHeight,
            rows.Select(r => Text.White(" ").Add(r).ToText()).ToList()
        );
        list.IsShowCursor = false;
        list.IsScrollMode = true;
        dlg.AddBorderView(list, Color.Dark);

        for (var i = 0; i < noteLines.Length; i++)
            dlg.AddLabel(2, noteY + i, noteLines[i]);

        var clicked = -1;
        void Press(int index)
        {
            clicked = index;
            dlg.Close();
        }

        // Left to right, two apart, as a message box has them
        var x = 1;
        for (var i = 0; i < buttons.Length; i++)
        {
            var index = i;
            var button = dlg.AddButton(x, buttonsY, buttons[i], () => Press(index));
            button.IsDefault = i == defaultButton;
            x += buttons[i].Replace("_", "").Length + 4 + 2;

            if (buttons[i].IndexOf('_') is var at and >= 0 && at < buttons[i].Length - 1)
                list.RegisterLetterHandler((Key)char.ToLowerInvariant(buttons[i][at + 1]), () => Press(index));
        }

        list.RegisterKeyHandler(Key.Enter, () => Press(defaultButton));
        list.RegisterKeyHandler(Key.Esc, () => dlg.Close());

        dlg.Show(list);
        return clicked;
    }
}
