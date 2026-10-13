using gmd.Common.Spelling;
using gmd.Cui.Common;
using gmd.Server;
using Terminal.Gui;

namespace gmd.Cui;

interface ISquashDlg
{
    bool Show(IReadOnlyList<Server.Commit> commits, out string message);
}

class SquashDlg : ISquashDlg
{
    readonly ISpellChecker spellChecker;

    internal SquashDlg(ISpellChecker spellChecker)
    {
        this.spellChecker = spellChecker;
    }

    public bool Show(IReadOnlyList<Commit> commits, out string commitMessage)
    {
        var range = GetRange(commits);
        var combinedMessage = GetCombinedMessages(commits);

        (string subjectPart, string messagePart) = CommitMessage.Split(combinedMessage);

        var dlg = new UIDialog("Squash", 74, 18);

        dlg.AddLabel(1, 0, $"Squash {range} on '{commits[0].BranchNiceUniqueName}':");
        var subject = dlg.AddInputField(1, 2, 50, subjectPart, InputMarkers.Both, spellChecker);

        var message = dlg.AddMultiLineInputView(1, 4, 70, 10, messagePart, spellChecker);
        dlg.Validate(() => GetMessage(subject, message) != "", "Empty commit message");

        dlg.ShowOkCancel(subject);

        commitMessage = GetMessage(subject, message);
        return dlg.IsOK;
    }

    static string GetRange(IReadOnlyList<Commit> commits) => $"{commits.First().Sid}...{commits.Last().Sid}";

    static string GetCombinedMessages(IReadOnlyList<Commit> commits) =>
        commits.Reverse().Select(c => c.Message).Join("\n-----\n");

    static string GetMessage(UITextField subject, TextView message) =>
        CommitMessage.Join(subject.Text, message.Text.ToString() ?? "");
}
