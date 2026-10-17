using gmd.Cui.Common;

namespace gmd.Cui.Branches;

record MoveCommitsResult(string Name, bool IsPush);

interface IMoveCommitsDlg
{
    // The name of the new branch the commits go to, and whether to publish it
    Result<MoveCommitsResult> Show(string branchName, int count, string backToSid, bool isPublish);
}

// Asks for the new branch commits are moved to, e.g. ones made on main by mistake, and says what
// becomes of the branch they were on, as Create Branch asks for a branch
class MoveCommitsDlg : IMoveCommitsDlg
{
    public Result<MoveCommitsResult> Show(string branchName, int count, string backToSid, bool isPublish)
    {
        var dlg = new UIDialog("Move Commits to New Branch", 64, 11);
        dlg.AddLabel(1, 0, Heading(branchName, count));
        var nameField = dlg.AddTextField(1, 2, 50, "");
        dlg.AddLabel(1, 4, Text.Dark(About(branchName, backToSid)));
        var isPublishBox = dlg.AddCheckBox(1, 5, "Publish", isPublish);

        dlg.Validate(() => nameField.Text != "", "Empty branch name");
        dlg.ValidateRefName(() => nameField.Text, "branch");

        if (!dlg.ShowOkCancel(nameField))
            return new Error();

        return new MoveCommitsResult(nameField.Text, isPublishBox.Checked);
    }

    internal static string Heading(string branchName, int count) =>
        $"Move {(count == 1 ? "1 commit" : $"{count} commits")} of '{branchName}' to a new branch:";

    internal static string About(string branchName, string backToSid) =>
        $"It is checked out, and '{branchName}' goes back to {backToSid}.";
}
