using gmd.Cui.Common;
using Terminal.Gui;

namespace gmd.Cui;

interface ICommitDlg
{
    bool Show(IRepo repo, bool isAmend, out string message);
}

class CommitDlg : ICommitDlg
{
    public bool Show(IRepo repo, bool isAmend, out string commitMessage)
    {
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

        var message = dlg.AddTextView(1, 4, 70, 10, messagePart);

        dlg.AddOK(true, () =>
        {
            if (GetMessage(subject, message) == "")
            {
                UI.ErrorMessage("Empty commit message");
                subject!.SetFocus();
                return false;
            }
            return true;
        });

        dlg.Show(subject);

        commitMessage = GetMessage(subject, message);
        return dlg.IsOK;
    }

    private bool OnKey(IRepo repo, Key key)
    {
        if (key == (Key.D | Key.CtrlMask))
        {
            repo.Cmd.ShowUncommittedDiff();
            return true;
        }

        return false;
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


    string SubjectText(TextField subjectField) => subjectField!.Text.ToString()?.Trim() ?? "";


    string GetMessage(TextField subjectField, TextView messageView)
    {
        string subjectText = SubjectText(subjectField);
        string msgText = messageView!.Text.ToString()?.TrimEnd() ?? "";
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

