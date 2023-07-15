namespace gmd.Cui.Common;


class Text
{
    record Fragment(string Text, Common.Color Color);
    readonly List<Fragment> fragments = new List<Fragment>();
    public int Length { get; private set; } = 0;

    public static Text Empty => new Text();
    public static Text New => new Text();

    public Text Red(string text) => Add(Common.Color.Red, text);
    public Text Blue(string text) => Add(Common.Color.Blue, text);
    public Text White(string text, bool isHighlight = false) => Add(Common.Color.White, text);
    public Text Magenta(string text) => Add(Common.Color.Magenta, text);
    public Text BrightBlue(string text) => Add(Common.Color.BrightBlue, text);
    public Text BrightCyan(string text) => Add(Common.Color.BrightCyan, text);
    public Text BrightGreen(string text) => Add(Common.Color.BrightGreen, text);
    public Text BrightMagenta(string text) => Add(Common.Color.BrightMagenta, text);
    public Text BrightRed(string text) => Add(Common.Color.BrightRed, text);
    public Text BrightYellow(string text) => Add(Common.Color.BrightYellow, text);
    public Text Cyan(string text) => Add(Common.Color.Cyan, text);
    public Text Dark(string text) => Add(Common.Color.Dark, text);
    public Text Green(string text) => Add(Common.Color.Green, text);
    public Text Yellow(string text) => Add(Common.Color.Yellow, text);
    public Text Black(string text) => Add(Common.Color.Black, text);
    public Text Color(Common.Color color, string text) => Add(color, text);

    public Text ToHighlight() => ToHighlight(Common.Color.Dark);

    public Text ToHighlightGreen() => ToHighlight(Common.Color.Green);

    public Text ToHighlightRed() => ToHighlight(Common.Color.Red);

    public Text ToHighlight(Common.Color bg)
    {
        var newText = Text.New;
        foreach (var fragment in fragments)
        {
            var fg = fragment.Color;
            if (fg == Common.Color.Dark && bg == Common.Color.Dark) fg = Common.Color.White;
            var color = new Common.Color(fg, bg);
            newText.Color(color, fragment.Text);
        }

        return newText;
    }

    public Text Add(Common.Color color, string text)
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

    public Text Add(Text text)
    {
        text.fragments.ForEach(f => Color(f.Color, f.Text));
        return this;
    }

    public Text AddLine(int width)
    {
        if (!fragments.Any() || fragments[0].Text == "")
        {
            return this;
        }

        return Text.New.Color(fragments[0].Color, new string(fragments[0].Text[0], width));
    }

    public Text Subtext(int startIndex, int length, bool isFillRest = false)
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


    internal void Draw(Terminal.Gui.View view, int x, int y, int startIndex = 0, int length = int.MaxValue)
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

            Terminal.Gui.View.Driver.SetAttribute(fragment.Color);
            Terminal.Gui.View.Driver.AddStr(text);
            x += text.Length;
        }
    }
}

