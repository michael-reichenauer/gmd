using gmd.Cui.Common;
using Terminal.Gui;
using Color = gmd.Cui.Common.Color;

namespace gmd.Cui;

interface ISetBranchDlg
{
    R<string> Show(string commitSid, IReadOnlyList<string> possibleBranches);
}


class SetBranchDlg : ISetBranchDlg
{
    IReadOnlyList<string> items = new List<string>();
    IReadOnlyList<Text> itemTexts = new List<Text>();


    public R<string> Show(string commitSid, IReadOnlyList<string> possibleBranches)
    {
        var x = 1;
        var y = 3;
        var w = 45;
        var h = 10;

        items = possibleBranches;
        itemTexts = items.Select(item => item.Length > w - 1
            ? Common.Text.Dark("…").White(item.Substring(item.Length - w - 1)).ToText()
            : Common.Text.White(item.Max(w, true)).ToText()).ToList();

        var dlg = new UIDialog($"Set Commit {commitSid} Branch Manually", 50, 20, null, o => o.Y = 0);

        dlg.AddLabel(1, 1, "Select Branch:");
        var listView = dlg.AddContentView(x, y, w, h, itemTexts);
        listView.IsShowCursor = false;
        listView.IsScrollMode = false;
        listView.IsCursorMargin = false;
        listView.IsHighlightCurrentIndex = true;
        dlg.AddBorderView(listView, Color.Dark);

        dlg.AddLabel(1, 14, "Name:");
        var nameField = dlg.AddInputField(7, 14, 39, items.FirstOrDefault() ?? "");
        listView.CurrentIndexChange += () => nameField.Text = items[listView.CurrentIndex];

        dlg.Validate(() => nameField.Text != "", "Empty branch name");

        View focusView = items.Any() ? listView : nameField;
        if (!dlg.ShowOkCancel(focusView)) return R.Error();

        Log.Info("Selected name: " + nameField.Text);
        return nameField.Text;
    }
}
