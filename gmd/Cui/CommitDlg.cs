using gmd.Cui.Common;
using Terminal.Gui;

namespace gmd.Cui;

interface ICommitDlg
{
    bool Show(IRepo repo, bool isAmend, IReadOnlyList<Server.Commit>? commits, out string message);
}

class CommitDlg : ICommitDlg
{
    IReadOnlyList<Server.Commit>? commits;
    UITextView message = null!;

    public bool Show(IRepo repo, bool isAmend, IReadOnlyList<Server.Commit>? commits, out string commitMessage)
    {
        this.commits = commits;
        if (!isAmend && repo.Status.IsOk)
        {
            commitMessage = "";
            return false;
        }

        (string subjectPart, string messagePart) = ParseMessage(repo, isAmend);

        var commit = repo.Commits[0];
        int filesCount = repo.Status.ChangesCount;
        string branchName = commit.BranchName;
        var title = isAmend ? "Amend" : "Commit";

        var dlg = new UIDialog(title, 74, 18, (key) => OnKey(repo, key));

        dlg.AddLabel(1, 0, $"{title} {filesCount} changes on '{branchName}':");
        var subject = dlg.AddInputField(1, 2, 50, subjectPart, InputMarkers.Both);

        message = dlg.AddMultiLineInputView(1, 4, 70, 10, messagePart);
        dlg.Validate(() => GetMessage(subject, message) != "", "Empty commit message");

        dlg.ShowOkCancel(subject);

        commitMessage = GetMessage(subject, message);
        return dlg.IsOK;
    }

    private bool OnKey(IRepo repo, Key key)
    {
        if (key == (Key.D | Key.CtrlMask) || key == (Key.Space | Key.CtrlMask))
        {
            repo.Cmd.ShowUncommittedDiff(true);
            return true;
        }
        if (key == (Key.A | Key.CtrlMask))
        {
            AddMergeMessages();
            return true;
        }

        return false;
    }

    private void AddMergeMessages()
    {
        if (commits == null || commits.Count == 0) return;

        // Indent all lines except the first in a commit message
        static string Indent(string msg) => string.Join('\n', msg.Split('\n')
            .Where((l, i) => !(i == 0 && l.StartsWith("Merge ") && !(i == 1 && l == "")))  // Skip "Merge " subjects and empty line after subject
            .Select((l, i) => i == 0 ? l : $"{l}"));

        var msg = message.Text.ToString();
        if (msg != "")
        {
            msg += "\n";
        }

        var text = commits
            .Select(c => Indent(c.Message))
            .Where(m => m.Trim() != "")
            .Select(m => $"- {m}")
            .Join("\n");
        text = text.Split('\n').Where(m => m.Trim() != "-").Select(l => l.TrimEnd()).Join("\n");
        message.Text = $"{msg}{text}";
        message.SetNeedsDisplay();
    }

    static (string, string) ParseMessage(IRepo repo, bool isAmend)
    {
        string msg = repo.Status.MergeMessage;

        if (isAmend)
        {
            var c = repo.GetCurrentCommit();
            msg = c.Message;
        }

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

