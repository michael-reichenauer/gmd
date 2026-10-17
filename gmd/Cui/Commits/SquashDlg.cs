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
        var combinedMessage = CombinedMessage(commits);

        (string subjectPart, string messagePart) = CommitMessage.Split(combinedMessage);

        var pushRows = forcePush == ForcePush.NotOffered ? 0 : 1;
        var dlg = new UIDialog("Squash", 74, 18 + pushRows);

        dlg.AddLabel(1, 0, Heading(commits));
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

    // "Squash 3 commits on 'feature', 80daa352 to 4050decb:", oldest to newest, as they were made
    internal static string Heading(IReadOnlyList<Commit> commits) =>
        $"Squash {commits.Count} commits on '{commits[0].BranchNiceUniqueName}', {commits[^1].Sid} to {commits[0].Sid}:";

    // The message the squash starts with: the oldest commit's, which the others were made on, and the
    // newer ones listed below it, oldest first, each subject as a '*' item with its body indented under
    // it, as GitHub writes the message of a squash merge. The commits are given newest first.
    internal static string CombinedMessage(IReadOnlyList<Commit> commits)
    {
        var (subject, body) = CommitMessage.Split(commits[^1].Message);
        List<string> lines = body.Trim() == "" ? [] : [body.Trim(), ""];
        foreach (var c in commits.Reverse().Skip(1))
        {
            var (s, b) = CommitMessage.Split(c.Message);
            lines.Add($"* {s}");
            lines.AddRange(b.Trim().Split('\n').Where(l => l.Trim() != "").Select(l => $"  {l.TrimEnd()}"));
        }
        return CommitMessage.Join(subject, string.Join('\n', lines));
    }

    static string GetMessage(UITextField subject, TextView message) =>
        CommitMessage.Join(subject.Text, message.Text.ToString() ?? "");
}
