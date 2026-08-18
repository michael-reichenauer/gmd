using Terminal.Gui;

namespace gmd.Cui.Common;

// A label that draws a styled Text and can be clicked, i.e. what Terminal.Gui's Label is not.
class UILabel : View
{
    Text text;

    public UILabel(int x, int y)
        : this(x, y, Text.Black(" "))
    {
        AutoSize = true;
        this.text = Text.Empty;
    } // For some reason text need to be set to something, otherwise it will not be drawn

    public UILabel(int x, int y, string text, bool autosize = true)
        : base(x, y, text)
    {
        AutoSize = autosize;
        this.text = Text.White(text);
    }

    public UILabel(int x, int y, Text text, bool autosize = true)
        : base(x, y, text.ToString())
    {
        AutoSize = autosize;
        this.text = text;
    }

    public event Action? Clicked;

    public override void Redraw(Rect bounds)
    {
        Clear();
        text.Draw(this, 0, 0);
    }

    public new Text Text
    {
        get => text;
        set
        {
            Width = text.Length;
            text = value;
            SetNeedsDisplay();
        }
    }

    public override bool OnMouseEvent(MouseEvent mouseEvent)
    {
        MouseEventArgs args = new MouseEventArgs(mouseEvent);
        if (OnMouseClick(args))
            return true;
        if (MouseEvent(mouseEvent))
            return true;

        if (mouseEvent.Flags == MouseFlags.Button1Clicked)
        {
            if (!HasFocus && SuperView != null)
            {
                if (!SuperView.HasFocus)
                {
                    SuperView.SetFocus();
                }
                SetFocus();
                SetNeedsDisplay();
            }

            OnClicked();
            return true;
        }
        return false;
    }

    public override bool OnEnter(View view)
    {
        Application.Driver.SetCursorVisibility(CursorVisibility.Invisible);

        return base.OnEnter(view);
    }

    public override bool ProcessHotKey(KeyEvent ke)
    {
        if (ke.Key == (Key.AltMask | HotKey))
        {
            if (!HasFocus)
            {
                SetFocus();
            }
            OnClicked();
            return true;
        }
        return base.ProcessHotKey(ke);
    }

    public virtual void OnClicked()
    {
        Clicked?.Invoke();
    }
}
