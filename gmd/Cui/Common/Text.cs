namespace gmd.Cui.Common;


record Fragment(string Text, Common.Color Color);

class TextBuilder
{
    readonly List<Fragment> fragments;

    public TextBuilder() : this(new List<Fragment>()) { }

    public TextBuilder(Common.Color color, string text)
    : this(new List<Fragment>())
    {
        this.Color(color, text);
    }

    public TextBuilder(List<Fragment> fragments)
    {
        this.fragments = fragments;
    }


    public int Length { get; private set; } = 0;

    // Implicit conversion to Text
    public static implicit operator Text(TextBuilder textBuilder) => textBuilder.ToText();

    public TextBuilder Red(string text) => Add(Common.Color.Red, text);
    public TextBuilder Blue(string text) => Add(Common.Color.Blue, text);
    public TextBuilder White(string text, bool isHighlight = false) => Add(Common.Color.White, text);
    public TextBuilder Magenta(string text) => Add(Common.Color.Magenta, text);
    public TextBuilder BrightBlue(string text) => Add(Common.Color.BrightBlue, text);
    public TextBuilder BrightCyan(string text) => Add(Common.Color.BrightCyan, text);
    public TextBuilder BrightGreen(string text) => Add(Common.Color.BrightGreen, text);
    public TextBuilder BrightMagenta(string text) => Add(Common.Color.BrightMagenta, text);
    public TextBuilder BrightRed(string text) => Add(Common.Color.BrightRed, text);
    public TextBuilder BrightYellow(string text) => Add(Common.Color.BrightYellow, text);
    public TextBuilder Cyan(string text) => Add(Common.Color.Cyan, text);
    public TextBuilder Dark(string text) => Add(Common.Color.Dark, text);
    public TextBuilder Green(string text) => Add(Common.Color.Green, text);
    public TextBuilder Yellow(string text) => Add(Common.Color.Yellow, text);
    public TextBuilder Black(string text) => Add(Common.Color.Black, text);
    public TextBuilder Color(Common.Color color, string text) => Add(color, text);

    public TextBuilder Add(Text text)
    {
        text.Fragments.ForEach(f => Color(f.Color, f.Text));
        return this;
    }


    public override string ToString()
    {
        return string.Concat(fragments.Select(f => f.Text));
    }

    public Text ToText()
    {
        return new Text(fragments.ToList());
    }


    TextBuilder Add(Common.Color color, string text)
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
}



class Text
{
    readonly List<Fragment> fragments;

    public Text() : this(new List<Fragment>()) { }

    public Text(List<Fragment> fragments)
    {
        this.fragments = fragments;
        Length = fragments.Any() ? fragments.Sum(f => f.Text.Length) : 0;
    }

    public static Text Empty => new Text();

    public int Length { get; private set; } = 0;
    public IReadOnlyList<Fragment> Fragments => fragments;


    public static TextBuilder Red(string text) => new TextBuilder(Common.Color.Red, text);
    public static TextBuilder Blue(string text) => new TextBuilder(Common.Color.Blue, text);
    public static TextBuilder White(string text, bool isHighlight = false) => new TextBuilder(Common.Color.White, text);
    public static TextBuilder Magenta(string text) => new TextBuilder(Common.Color.Magenta, text);
    public static TextBuilder BrightBlue(string text) => new TextBuilder(Common.Color.BrightBlue, text);
    public static TextBuilder BrightCyan(string text) => new TextBuilder(Common.Color.BrightCyan, text);
    public static TextBuilder BrightGreen(string text) => new TextBuilder(Common.Color.BrightGreen, text);
    public static TextBuilder BrightMagenta(string text) => new TextBuilder(Common.Color.BrightMagenta, text);
    public static TextBuilder BrightRed(string text) => new TextBuilder(Common.Color.BrightRed, text);
    public static TextBuilder BrightYellow(string text) => new TextBuilder(Common.Color.BrightYellow, text);
    public static TextBuilder Cyan(string text) => new TextBuilder(Common.Color.Cyan, text);
    public static TextBuilder Dark(string text) => new TextBuilder(Common.Color.Dark, text);
    public static TextBuilder Green(string text) => new TextBuilder(Common.Color.Green, text);
    public static TextBuilder Yellow(string text) => new TextBuilder(Common.Color.Yellow, text);
    public static TextBuilder Black(string text) => new TextBuilder(Common.Color.Black, text);
    public static TextBuilder Color(Common.Color color, string text) => new TextBuilder(color, text);


    public static TextBuilder Add(Text text)
    {
        var tb = new TextBuilder(text.Fragments.ToList());
        return tb;
    }



    public Text ToHighlight() => ToHighlight(Common.Color.Dark);

    public Text ToHighlightGreen() => ToHighlight(Common.Color.Green);

    public Text ToHighlightRed() => ToHighlight(Common.Color.Red);

    public Text ToHighlight(Common.Color bg)
    {
        var newText = new TextBuilder();
        foreach (var fragment in fragments)
        {
            var fg = fragment.Color;
            if (fg == Common.Color.Dark && bg == Common.Color.Dark) fg = Common.Color.White;
            var color = new Common.Color(fg, bg);
            newText.Color(color, fragment.Text);
        }

        return newText;
    }


    public override string ToString()
    {
        return string.Concat(fragments.Select(f => f.Text));
    }

    public TextBuilder ToTextBuilder()
    {
        return new TextBuilder(fragments.ToList());
    }

    public Text ToLine(int width)
    {
        if (!fragments.Any() || fragments[0].Text == "")
        {
            return this;
        }

        return new TextBuilder().Color(fragments[0].Color, new string(fragments[0].Text[0], width));

    }

    public Text Subtext(int startIndex, int length, bool isFillRest = false)
    {
        var newText = new TextBuilder();
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


    public void Draw(Terminal.Gui.View view, int x, int y, int startIndex = 0, int length = int.MaxValue)
    {
        view.Move(x, y);
        Draw(startIndex, length);
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

