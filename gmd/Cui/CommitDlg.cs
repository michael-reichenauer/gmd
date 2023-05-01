using gmd.Cui.Common;
using Terminal.Gui;

namespace gmd.Cui;

interface ICommitDlg
{
    bool Show(IRepo repo, bool isAmend, out string message);
}

class CommitDlg : ICommitDlg
{
    public bool Show(IRepo repo, bool isAmend, out string message)
    {
        message = "";
        bool isOk = false;

        if (!isAmend && repo.Status.IsOk)
        {
            return false;
        }

        (string subjectText, string messageText) = ParseMessage(repo, isAmend);

        var commit = repo.Commits[0];
        int filesCount = repo.Status.ChangesCount;
        string branchName = commit.BranchName;
        var cmdText = isAmend ? "Amend" : "Commit";

        Label infoLabel = Components.Label(1, 0, $"{cmdText} {filesCount} changes on '{branchName}':");

        TextField subjectField = Components.TextField(1, 2, 50, subjectText);
        Label sep1 = Components.TextIndicator(subjectField);

        TextView messageView = Components.TextView(1, 4, 70, 10, messageText);
        var sep3 = Components.TextIndicator(messageView);

        Button okButton = Buttons.OK(true, () =>
        {
            if (GetMessage(subjectField, messageView) == "")
            {
                UI.ErrorMessage("Empty commit message");
                subjectField!.SetFocus();
                return false;
            }
            isOk = true;
            Application.RequestStop();
            return true;
        });

        Dialog dialog = Components.Dialog(cmdText, 74, 18, (key) => OnKey(repo, key), okButton, Buttons.Cancel());
        dialog.Closed += e => UI.HideCursor();
        dialog.Add(infoLabel, subjectField, sep1, messageView, sep3);

        subjectField.SetFocus();
        UI.ShowCursor();
        UI.RunDialog(dialog);

        message = GetMessage(subjectField, messageView);
        return isOk;
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
            var c = repo.Commit(repo.GetCurrentBranch().TipId);
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

