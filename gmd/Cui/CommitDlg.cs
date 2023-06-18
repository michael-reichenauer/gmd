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
        var subject = dlg.AddTextField(1, 2, 50, subjectPart);

        message = dlg.AddTextView(1, 4, 70, 10, messagePart);
        dlg.Validate(() => GetMessage(subject, message) != "", "Empty commit message");

        dlg.ShowOkCancel(subject);

        commitMessage = GetMessage(subject, message);
        return dlg.IsOK;
    }

    private bool OnKey(IRepo repo, Key key)
    {
        if (key == (Key.D | Key.CtrlMask) || key == (Key.Space | Key.CtrlMask))
        {
            repo.Cmd.ShowUncommittedDiff();
            return true;
        }
        if (key == (Key.A | Key.CtrlMask))
        {
            AddMergeMessages(repo);
            return true;
        }

        return false;
    }

    private void AddMergeMessages(IRepo repo)
    {
        if (commits == null || commits.Count == 0) return;

        var text = string.Join('\n', commits.Select(c => $"- {c.Message}"));
        message.Text = $"{message.Text}Merged commits:\n{text}";
        message.SetNeedsDisplay();
    }

    (string, string) ParseMessage(IRepo repo, bool isAmend)
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
        string message = "";
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


    string GetMessage(UITextField subject, TextView message)
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

