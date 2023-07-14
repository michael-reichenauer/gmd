using System.Text;
using Terminal.Gui;
using Color = Terminal.Gui.Attribute;


namespace gmd.Cui.Common;


class Text
{
    record Fragment(string Text, Color Color);
    readonly List<Fragment> fragments = new List<Fragment>();
    internal int Length { get; private set; } = 0;

    internal static Text None => new Text();
    internal static Text New => new Text();

    internal Text Red(string text) => Color(TextColor.Red, text);
    internal Text Blue(string text) => Color(TextColor.Blue, text);
    internal Text White(string text, bool isHighlight = false) => Color(TextColor.White, text);
    internal Text Magenta(string text) => Color(TextColor.Magenta, text);
    internal Text BrightBlue(string text) => Color(TextColor.BrightBlue, text);
    internal Text BrightCyan(string text) => Color(TextColor.BrightCyan, text);
    internal Text BrightGreen(string text) => Color(TextColor.BrightGreen, text);
    internal Text BrightMagenta(string text) => Color(TextColor.BrightMagenta, text);
    internal Text BrightRed(string text) => Color(TextColor.BrightRed, text);
    internal Text BrightYellow(string text) => Color(TextColor.BrightYellow, text);
    internal Text Cyan(string text) => Color(TextColor.Cyan, text);
    internal Text Dark(string text) => Color(TextColor.Dark, text);
    internal Text Green(string text) => Color(TextColor.Green, text);
    internal Text Yellow(string text) => Color(TextColor.Yellow, text);
    internal Text Black(string text) => Color(TextColor.Black, text);

    internal Text ToHighlight() => ToHighlight(Terminal.Gui.Color.DarkGray);

    internal Text ToHighlightGreen() => ToHighlight(Terminal.Gui.Color.Green);

    internal Text ToHighlightRed() => ToHighlight(Terminal.Gui.Color.Red);

    internal Text ToHighlight(Terminal.Gui.Color bgc)
    {
        var newText = Text.New;
        foreach (var fragment in fragments)
        {
            var color = TextColor.Make(fragment.Color.Foreground, bgc);
            newText.Color(color, fragment.Text);
        }
        return newText;
    }

    internal Text Color(Color color, string text)
    {
        if (fragments.Count == 0 || fragments[^1].Color != color)
        {
            fragments.Add(new Fragment(text, color));
        }
        else
        {
            fragments[^1] = fragments[^1] with { Text = fragments[^1].Text + text };
        }

        Length += text.Length;
        return this;
    }


    internal Text Add(Text text)
    {
        text.fragments.ForEach(f => Color(f.Color, f.Text));
        return this;
    }

    internal Text AddLine(int width)
    {
        if (!fragments.Any() || fragments[0].Text == "")
        {
            return this;
        }

        return Text.New.Color(fragments[0].Color, new string(fragments[0].Text[0], width));
    }

    internal Text Subtext(int startIndex, int length, bool isFillRest = false)
    {
        var newText = Text.New;
        int x = 0;
        foreach (var fragment in fragments)
        {
            string text = fragment.Text;
            int end = x + text.Length;
            if (end < startIndex)
            {
                // Text left of rowX s
                x += text.Length;
                continue;
            }

            if (x < startIndex)
            {
                text = text.Substring(startIndex - x);
                x += (startIndex - x);
            }

            if (x + text.Length >= (startIndex + length))
            {
                text = text.Substring(0, ((startIndex + length) - x));
            }

            if (text == "")
            {
                continue;
            }
            newText.Color(fragment.Color, text);
            x += text.Length;
        }

        if (isFillRest && newText.Length < length)
        {
            newText.Black(new string(' ', length - newText.Length));
        }

        return newText;
    }


    internal void Draw(View view, int x, int y, int startIndex = 0, int length = int.MaxValue)
    {
        view.Move(x, y);
        Draw(startIndex, length);
    }

    public override string ToString()
    {
        return string.Concat(fragments.Select(f => f.Text));
    }


    void Draw(int startIndex = 0, int length = int.MaxValue)
    {
        int x = 0;
        foreach (var fragment in fragments)
        {
            if (x >= length)
            {
                // Reached beyond last text to show
                return;
            }

            string text = fragment.Text;
            int end = x + text.Length;
            if (end < startIndex)
            {
                // Text left of rowX 
                x += text.Length;
                continue;
            }

            if (x < startIndex)
            {
                text = text.Substring(startIndex - x);
                x += (startIndex - x);
            }

            if (x + text.Length >= (startIndex + length))
            {
                text = text.Substring(0, ((startIndex + length) - x));
            }

            if (text == "")
            {
                continue;
            }

            View.Driver.SetAttribute(fragment.Color);
            View.Driver.AddStr(text);
            x += text.Length;
        }
    }
}

