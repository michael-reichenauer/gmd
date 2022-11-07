
using Terminal.Gui;

namespace gmd.Cui;

class CommitDlg
{
    readonly Dialog dialog;
    readonly TextField subject;
    readonly MessageText message;

    bool isOk = false;
    public CommitDlg()
    {
        Button okButton = new Button("OK", true);
        okButton.Clicked += () =>
        {
            isOk = true;
            Application.RequestStop();
        };
        Button cancelButton = new Button("Cancel", false);
        cancelButton.Clicked += () => Application.RequestStop();


        dialog = new Dialog("Commit", 72, 20, new Button[] { okButton, cancelButton })
        {
            Border = { Effect3D = false }
        };

        int filesCount = 0;
        string branchName = "Some/branch";

        var label = new Label(0, 0, $"Commit {filesCount} files on {branchName}:");
        subject = new TextField(0, 1, 50, "some text");

        message = new MessageText() { X = 0, Y = 3, Width = 70, Height = 10 };
        message.Text = "Message";


        dialog.Add(label, subject, message);
        subject.SetFocus();
    }

    internal bool Show()
    {
        Application.Run(dialog);
        Log.Info($"Subject '{subject.Text}'");
        Log.Info($"Message '{message.Text}'");
        return isOk;
    }

    class MessageText : TextView
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
}

