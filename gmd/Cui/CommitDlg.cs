
using gmd.Server;
using NStack;
using Terminal.Gui;

namespace gmd.Cui;

interface ICommitDlg
{
    bool Show(out string message);
}
class CommitDlg : ICommitDlg
{
    Dialog? dialog;
    TextField? subjectField;
    MessageTextView? messageView;
    IRepo repo;

    bool isOk = false;

    internal CommitDlg(IRepo repo)
    {
        this.repo = repo;
    }

    public bool Show(out string message)
    {
        message = "";

        var commit = repo.Repo.Commits[0];
        if (commit.Id != Repo.UncommittedId)
        {
            return false;
        }

        string branchName = commit.BranchName;
        (string subjectText, string messageText) = ParseMessage(repo);
        int filesCount = repo.Repo.Status.ChangesCount;

        Button okButton = new Button("OK", true);
        okButton.ColorScheme = ColorSchemes.ButtonColorScheme;
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
        cancelButton.ColorScheme = ColorSchemes.ButtonColorScheme;


        Label infoLabel = new Label(1, 0, $"Commit {filesCount} changes on '{branchName}':");

        subjectField = new TextField(1, 2, 50, "") { Text = subjectText };
        Label sep1 = new Label(0, 3, "└" + new string('─', 49) + "┘");

        messageView = new MessageTextView() { X = 1, Y = 4, Width = 69, Height = 10, Text = messageText };
        Label sep3 = new Label(0, 14, "└" + new string('─', 67) + "┘");

        dialog = new CustomDialog("Commit", 72, 18, new[] { okButton, cancelButton }, OnKey)
        {
            Border = { Effect3D = false, BorderStyle = BorderStyle.Rounded, BorderBrush = Color.Blue },
            ColorScheme = ColorSchemes.DialogColorScheme,
        };
        dialog.Closed += e => UI.HideCursor();
        dialog.Add(infoLabel, subjectField, sep1, messageView, sep3);

        subjectField.SetFocus();
        UI.ShowCursor();
        Application.Run(dialog);

        message = GetMessage();
        return isOk;
    }


    private bool OnKey(Key key)
    {
        if (key == (Key.D | Key.CtrlMask))
        {
            repo.ShowUncommittedDiff();
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

        public override Border Border { get => new Border() { }; set => base.Border = value; }
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

