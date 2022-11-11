
using gmd.ViewRepos;
using NStack;
using Terminal.Gui;

namespace gmd.Cui;

interface ICommitDlg
{
    bool Show(IRepo repo, out string message);
}

class CommitDlg : ICommitDlg
{
    readonly IRepoCommands cmds;
    Dialog? dialog;
    TextField? subjectField;
    MessageTextView? messageView;
    IRepo? repo;

    bool isOk = false;
    internal CommitDlg(IRepoCommands cmds)
    {
        this.cmds = cmds;
    }

    public bool Show(IRepo repo, out string message)
    {
        message = "";
        this.repo = repo;

        var commit = repo.Repo.Commits[0];
        if (commit.Id != Repo.UncommittedId)
        {
            return false;
        }

        string branchName = commit.BranchName;
        (string subjectText, string messageText) = ParseMessage(repo);
        int filesCount = repo.Repo.Status.ChangesCount;

        Button okButton = new Button("OK", true);
        okButton.Clicked += () =>
        {
            if (GetMessage() == "")
            {
                UI.ErrorMessage("Empty commit message");
                subjectField!.SetFocus();
                return;
            }
            isOk = true;
            Application.RequestStop();
        };

        Button cancelButton = new Button("Cancel", false);
        cancelButton.Clicked += () => Application.RequestStop();

        dialog = new CustomDialog("Commit", 72, 17, new[] { okButton, cancelButton }, OnKey)
        {
            Border = { Effect3D = false }
        };

        Label infoLabel = new Label(0, 0, $"Commit {filesCount} files on {branchName}:");
        subjectField = new TextField(0, 1, 50, "some text");
        subjectField.Text = subjectText;

        messageView = new MessageTextView() { X = 0, Y = 3, Width = 70, Height = 10 };
        messageView.Text = messageText;

        dialog.Add(infoLabel, subjectField, messageView);
        subjectField.SetFocus();

        Application.Run(dialog);

        message = GetMessage();
        return isOk;
    }


    private bool OnKey(Key key)
    {
        if (key == (Key.D | Key.CtrlMask))
        {
            cmds.ShowUncommittedDiff(repo!);
            return true;
        }

        return false;
    }


    (string, string) ParseMessage(IRepo repo)
    {
        string msg = repo.Repo.Status.MergeMessage;

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


    string SubjectText() => subjectField!.Text.ToString()?.Trim() ?? "";


    string GetMessage()
    {
        string subjectText = SubjectText();
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

    class MessageTextView : TextView
    {
        public override bool ProcessKey(KeyEvent keyEvent)
        {
            if (keyEvent.Key == Key.Tab)
            {   // Ensure tab sets focus on next control and not insert tab in text
                return false;
            }
            return base.ProcessKey(keyEvent);
        }
    }

    class CustomDialog : Dialog
    {
        private readonly Func<Key, bool> onKey;

        public CustomDialog(ustring title, int width, int height, Button[] buttons, Func<Key, bool> onKey)
        : base(title, width, height, buttons)
        {
            this.onKey = onKey;
        }

        public override bool ProcessHotKey(KeyEvent keyEvent) => onKey(keyEvent.Key);
    }
}

