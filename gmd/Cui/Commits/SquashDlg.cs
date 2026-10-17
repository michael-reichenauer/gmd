using gmd.Common.Spelling;
using gmd.Cui.Common;
using gmd.Server;
using Terminal.Gui;

namespace gmd.Cui.Commits;

interface ISquashDlg
{
    // The message of the squash, and whether the force push box, when offered, was ticked
    bool Show(
        Repo repo,
        IReadOnlyList<Server.Commit> commits,
        ForcePush forcePush,
        out string message,
        out bool isForcePush
    );
}

class SquashDlg : ISquashDlg
{
    readonly ISpellChecker spellChecker;

    internal SquashDlg(ISpellChecker spellChecker)
    {
        this.spellChecker = spellChecker;
    }

    public bool Show(
        Repo repo,
        IReadOnlyList<Commit> commits,
        ForcePush forcePush,
        out string commitMessage,
        out bool isForcePush
    )
    {
        var range = GetRange(commits);
        var combinedMessage = GetCombinedMessages(commits);

        (string subjectPart, string messagePart) = CommitMessage.Split(combinedMessage);

        var pushRows = forcePush == ForcePush.NotOffered ? 0 : 1;
        var dlg = new UIDialog("Squash", 74, 18 + pushRows);

        dlg.AddLabel(1, 0, $"Squash {range} on '{commits[0].BranchNiceUniqueName}':");
        var subject = dlg.AddInputField(1, 2, 50, subjectPart, InputMarkers.Both, spellChecker);

        var message = dlg.AddMultiLineInputView(1, 4, 70, 10, messagePart, spellChecker);
        dlg.Validate(() => GetMessage(subject, message) != "", "Empty commit message");

        var push =
            forcePush == ForcePush.NotOffered
                ? null
                : dlg.AddCheckBox(1, 15, CommitDlg.ForcePushLabel(repo), forcePush == ForcePush.Ticked);

        dlg.ShowOkCancel(subject);

        commitMessage = GetMessage(subject, message);
        isForcePush = dlg.IsOK && push?.Checked == true;
        return dlg.IsOK;
    }

    static string GetRange(IReadOnlyList<Commit> commits) => $"{commits.First().Sid}...{commits.Last().Sid}";

    static string GetCombinedMessages(IReadOnlyList<Commit> commits) =>
        commits.Reverse().Select(c => c.Message).Join("\n-----\n");

    static string GetMessage(UITextField subject, TextView message) =>
        CommitMessage.Join(subject.Text, message.Text.ToString() ?? "");
}
