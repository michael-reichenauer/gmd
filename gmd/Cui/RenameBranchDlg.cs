using gmd.Cui.Common;

namespace gmd.Cui;

interface IRenameBranchDlg
{
    R<string> Show(string branchName, bool hasRemote, IReadOnlyList<string> existingNames);
}

class RenameBranchDlg : IRenameBranchDlg
{
    // Characters and forms git refuses in a branch name (see 'git check-ref-format'). The name is
    // also passed to git as part of an unquoted command line, so white space has to be rejected
    // here rather than left to git.
    static readonly char[] invalidChars = [' ', '\t', '~', '^', ':', '?', '*', '[', '\\'];

    public R<string> Show(string branchName, bool hasRemote, IReadOnlyList<string> existingNames)
    {
        var dlg = new UIDialog("Rename Branch", 60, 9);
        dlg.AddLabel(1, 0, $"From: {branchName}");
        var name = dlg.AddTextField(1, 2, 56, branchName);
        if (hasRemote)
        { // The remote branch is renamed as well, which deletes the old remote branch, so say so
            dlg.AddLabel(1, 4, $"Also renames origin/{branchName}");
        }

        dlg.Validate(() => name.Text != "", "Empty branch name");
        dlg.Validate(() => name.Text != branchName, "Same branch name");
        dlg.Validate(() => IsValidName(name.Text), $"Invalid branch name:\n{name.Text}");
        dlg.Validate(
            () => !existingNames.Contains(name.Text) && !existingNames.Contains($"origin/{name.Text}"),
            $"Branch already exists:\n{name.Text}"
        );

        if (!dlg.ShowOkCancel(name))
            return R.Error();

        return name.Text;
    }

    internal static bool IsValidName(string name) =>
        name != ""
        && name.IndexOfAny(invalidChars) == -1
        && !name.Contains("..")
        && !name.Contains("@{")
        && !name.Contains("//")
        && !name.StartsWith('/')
        && !name.EndsWith('/')
        && !name.StartsWith('-')
        && !name.StartsWith('.')
        && !name.EndsWith('.')
        && !name.EndsWith(".lock");
}
