using gmd.Cui.Common;
using Terminal.Gui;
using gmd.Cui.RepoView;
using gmd.Server;

namespace gmd.Cui;

interface ISquashDlg
{
    bool Show(IViewRepo repo, IReadOnlyList<Server.Commit> commits, out string message);
}


class SquashDlg : ISquashDlg
{
    IReadOnlyList<Server.Commit>? commits;
    UITextView message = null!;

    public bool Show(IViewRepo repo, IReadOnlyList<Commit> commits, out string commitMessage)
    {
        this.commits = commits;

        var range = GetRange(commits);
        var combinedMessage = GetCombinedMessages(commits);

        (string subjectPart, string messagePart) = ParseMessage(combinedMessage);

        var dlg = new UIDialog("Squash", 74, 18);

        dlg.AddLabel(1, 0, $"Squash {range} on '{commits[0].BranchNiceUniqueName}':");
        var subject = dlg.AddInputField(1, 2, 50, subjectPart, InputMarkers.Both);

        message = dlg.AddMultiLineInputView(1, 4, 70, 10, messagePart);
        dlg.Validate(() => GetMessage(subject, message) != "", "Empty commit message");

        dlg.ShowOkCancel(subject);

        commitMessage = GetMessage(subject, message);
        return dlg.IsOK;
    }

    string GetRange(IReadOnlyList<Commit> commits) =>
        $"{commits.First().Sid}...{commits.Last().Sid}";


    static (string, string) ParseMessage(string msg)
    {
        if (msg.Trim() == "")
        {
            return ("", "");
        }
        var lines = msg.Split('\n');
        if (lines.Length == 1)
        {
            return (lines[0], "");
        }

        string subject = lines[0];
        string message;
        if (lines.Length > 2 && lines[1] == "")
        {
            message = string.Join('\n', lines.Skip(2));
        }
        else
        {
            message = string.Join('\n', lines.Skip(1));
        }

        return (subject, message);
    }

    private string GetCombinedMessages(IReadOnlyList<Commit> commits)
    {
        if (commits == null || commits.Count == 0) return "";

        return commits.Reverse().Select(c => c.Message).Join("\n-----\n");
    }

    static string GetMessage(UITextField subject, TextView message)
    {
        string subjectText = subject.Text;
        string msgText = message.Text.ToString()?.TrimEnd() ?? "";
        if (msgText.Trim() == "")
        {
            msgText = "";
        }

        if (subjectText != "" && msgText.Length > 0)
        {
            return subjectText + "\n\n" + msgText;
        }
        if (subjectText != "")
        {
            return subjectText;
        }

        return msgText;
    }

}

