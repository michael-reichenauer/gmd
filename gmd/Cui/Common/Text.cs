namespace gmd.Cui.Common;


record TextFragment(string Text, Common.Color Color);


// An immutable text with fragments of multiple colors, e.g.
// Text text = Text.Red("Red text").Blue("Blue text"));
// Supports highlighting and subtext and drawing on a view
// Cooperates automatically with TextBuilder to seamlessly compose a text with multiple colors
class Text
{
    readonly List<TextFragment> fragments;

    public Text() : this(new List<TextFragment>()) { }

    public Text(IEnumerable<TextFragment> fragments)
    {
        this.fragments = fragments.ToList();
        Length = fragments.Any() ? fragments.Sum(f => f.Text.Length) : 0;
    }

    public static Text Empty => new Text();

    public int Length { get; private set; } = 0;
    public IReadOnlyList<TextFragment> Fragments => fragments;

    // Initial fragment of text with color, additional fragments will be added using TextBuilder
    public static TextBuilder Red(string text) => new TextBuilder(Common.Color.Red, text);
    public static TextBuilder Blue(string text) => new TextBuilder(Common.Color.Blue, text);
    public static TextBuilder White(string text) => new TextBuilder(Common.Color.White, text);
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
        var tb = new TextBuilder(text.Fragments);
        return tb;
    }

    // Converter methods
    public TextBuilder ToTextBuilder() => new TextBuilder(fragments.ToList());

    public override string ToString() => string.Concat(fragments.Select(f => f.Text));

    public Text ToHighlight() => ToHighlight(Common.Color.Dark);

    public Text ToHighlightGreen() => ToHighlight(Common.Color.Green);

    public Text ToHighlightRed() => ToHighlight(Common.Color.Red);

    public Text ToHighlight(Common.Color newBg)
    {
        var newText = new TextBuilder();

        // Replace all fragments with new background color,
        // and make sure foreground color is readable on new background
        foreach (var fragment in fragments)
        {
            var fg = fragment.Color.Foreground;

            // Make sure foreground color is readable on new background
            if (fg == newBg && fg == Common.Color.White || fg == Common.Color.Yellow)
            {   // White or yellow on white is not readable 
                fg = Common.Color.Black;
            }
            else if (fg == newBg && fg == Common.Color.Black)
            {   // Black on black is not readable
                fg = Common.Color.White;
            }
            else if (fg == newBg)
            {   // Same color as background, use white, since background is not wite or yellow
                fg = Common.Color.White;
            }

            var color = new Common.Color(fg, newBg);
            newText.Color(color, fragment.Text);
        }

        return newText;
    }

    // Used to creates lines like e.g. '───', Stretches the first char to fill the width. 
    public Text ToLine(int width)
    {
        if (!fragments.Any() || fragments[0].Text == "")
        {
            return this;
        }

        return new TextBuilder().Color(fragments[0].Color, new string(fragments[0].Text[0], width));
    }

    // Returns a portion of the text, starting at startIndex, and with length
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

    // Draws the text on the view, starting at x,y, and with length
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



// TextBuilder is a mutable version of Text, to make it easier to compose a text with multiple colors
class TextBuilder
{
    readonly List<TextFragment> fragments;

    public TextBuilder()
    {
        this.fragments = new List<TextFragment>();
    }

    public TextBuilder(Common.Color color, string text)
    {
        this.fragments = new List<TextFragment>();
        this.Color(color, text);
    }

    public TextBuilder(IEnumerable<TextFragment> fragments)
    {
        this.fragments = fragments.ToList();
    }


    public int Length { get; private set; } = 0;

    // Implicit conversion to Text
    public static implicit operator Text(TextBuilder textBuilder) => textBuilder.ToText();

    // ToTest and ToString
    public Text ToText() => new Text(fragments);
    public override string ToString() => string.Concat(fragments.Select(f => f.Text));


    // Methods to compose a text with multiple colors
    public TextBuilder Red(string text) => Add(Common.Color.Red, text);
    public TextBuilder Blue(string text) => Add(Common.Color.Blue, text);
    public TextBuilder White(string text) => Add(Common.Color.White, text);
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
        text.Fragments.ForEach(f => Add(f.Color, f.Text));
        return this;
    }


    TextBuilder Add(Common.Color color, string text)
    {
        Length += text.Length;
        if (!fragments.Any())
        {   // First fragment
            fragments.Add(new TextFragment(text, color));
            return this;
        }

        var f = fragments[^1];
        if (f.Color != color)
        {   // Color differs from previous fragment, add new fragment with new color
            fragments.Add(new TextFragment(text, color));
            return this;
        }

        // Same color as previous fragment, append text to previous fragment to avoid too many fragments
        fragments[^1] = f with { Text = f.Text + text };
        return this;
    }
}


