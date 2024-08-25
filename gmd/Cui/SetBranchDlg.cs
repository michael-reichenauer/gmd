using gmd.Cui.Common;
using Terminal.Gui;
using Color = gmd.Cui.Common.Color;

namespace gmd.Cui;

interface ISetBranchDlg
{
    R<string> Show(string commitSid, bool isBranchSetByUser, string niceName, IReadOnlyList<string> possibleBranches);
}


class SetBranchDlg : ISetBranchDlg
{
    IReadOnlyList<string> items = new List<string>();
    IReadOnlyList<Text> itemTexts = new List<Text>();


    public R<string> Show(string commitSid, bool isBranchSetByUser, string niceName,
        IReadOnlyList<string> possibleBranches)
    {
        if (possibleBranches.Count == 0) return R.Error();

        (var width, var height) = (50, 22);
        (var x, var y, var w, var h) = (1, 5, width - 5, height - 12);

        IReadOnlyList<string> items = possibleBranches;
        itemTexts = items.Select(item => item.Length > w - 1
            ? Text.Dark("…").White(item[Math.Max(0, item.Length - w - 1)..]).ToText()
            : Text.White(item.Max(w, true)).ToText()).ToList();

        var dlg = new UIDialog($"Set Commit {commitSid} Branch Manually", width, height, null, o => o.Y = 0);

        dlg.AddLabel(1, 3, "Select Branch:");
        var listView = dlg.AddContentView(x, y, w, h, itemTexts);
        listView.IsShowCursor = false;
        listView.IsScrollMode = false;
        listView.IsCursorMargin = false;
        listView.IsHighlightCurrentIndex = true;
        dlg.AddBorderView(listView, Color.Dark);

        dlg.AddLabel(1, 16, "Name:");
        var nameField = dlg.AddInputField(7, 16, width - 11, items[0]);
        listView.CurrentIndexChange += () => nameField.Text = items[listView.CurrentIndex];

        // Unset 
        var color = isBranchSetByUser ? Color.White : Color.Dark;
        dlg.AddLabel(1, 1, Text.Color(color, "Manually Set: ").Dark(isBranchSetByUser ? niceName : "-"));
        var isUnsetClicked = false;
        var unsetBtn = dlg.AddButton(width - 12, 1, "Clear", () =>
        {
            isUnsetClicked = true;
            nameField.Text = "";
            dlg.Close();
        });
        unsetBtn.Enabled = isBranchSetByUser;

        View focusView = items.Any() ? listView : nameField;
        if (!dlg.ShowOkCancel(focusView) && !isUnsetClicked) return R.Error();
        return nameField.Text;
    }
}
