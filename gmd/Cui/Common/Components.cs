using Terminal.Gui;

namespace gmd.Cui.Common;

static class Components
{
    internal static Dialog Dialog(string title, int width, int height, params Button[] buttons) =>
        new Dialog(title, width, height, buttons)
        {
            Border = { Effect3D = false, BorderStyle = BorderStyle.Rounded },
            ColorScheme = ColorSchemes.Dialog,
        };

    internal static Dialog Dialog(string title, int width, int height, Func<Key, bool> onKey, params Button[] buttons) =>
        new CustomDialog(title, width, height, buttons, onKey)
        {
            Border = { Effect3D = false, BorderStyle = BorderStyle.Rounded },
            ColorScheme = ColorSchemes.Dialog,
        };


    internal static Label Label(int x, int y, string text) =>
        new Label(x, y, text) { ColorScheme = ColorSchemes.Label };

    internal static TextField TextField(int x, int y, int w, string text) =>
        new TextField(x, y, w, text) { ColorScheme = ColorSchemes.TextField };


    internal static TextView TextView(int x, int y, int w, int h, string text) =>
        new CustomTextView()
        { X = x, Y = y, Width = w, Height = h, Text = text, ColorScheme = ColorSchemes.TextField };

    internal static Label TextIndicator(View textField) =>
        new Label(textField.Frame.X - 1, textField.Frame.Y + textField.Frame.Height,
            "└" + new string('─', textField.Frame.Width) + "┘")
        { ColorScheme = ColorSchemes.Indicator };

    internal static CheckBox CheckBox(string name, bool isChecked, int x, int y) =>
        new CheckBox(name, isChecked)
        { X = x, Y = y, ColorScheme = ColorSchemes.CheckBox };


    class CustomDialog : Dialog
    {
        private readonly Func<Key, bool> onKey;

        public CustomDialog(string title, int width, int height, Button[] buttons, Func<Key, bool> onKey)
            : base(title, width, height, buttons)
        {
            this.onKey = onKey;
        }

        public override bool ProcessHotKey(KeyEvent keyEvent) => onKey?.Invoke(keyEvent.Key) ?? false;
    }

    class CustomTextView : TextView
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

}
