using gmd.Cui.Common;

namespace gmdTest.Fixtures;

// The color of every rune of a Text as one letter each, so colors can be asserted as a picture
// lined up under the text itself. Shared by GraphText and DiffText.
//
// Uppercase is a normal color and lowercase its bright variant, the same convention ScreenText
// uses for a captured screen: 'M' magenta, 'm' bright magenta, 'W' white, 'D' dark, '.' black.
// The two background colors a diff marks its changes with are '-' (red background, i.e. removed)
// and '+' (green background, i.e. added), chosen so a diff's colors read as a diff. ScreenText
// spells '-' as the terminal default instead — these are separate alphabets for separate
// fixtures, since one reads a Text and the other a terminal capture.
static class TextColors
{
    static readonly (Color Color, char Letter)[] Letters =
    [
        (Color.Blue, 'B'),
        (Color.Green, 'G'),
        (Color.Cyan, 'C'),
        (Color.Red, 'R'),
        (Color.Yellow, 'Y'),
        (Color.Magenta, 'M'),
        (Color.White, 'W'),
        (Color.Dark, 'D'),
        (Color.Black, '.'),
        (Color.BrightBlue, 'b'),
        (Color.BrightGreen, 'g'),
        (Color.BrightCyan, 'c'),
        (Color.BrightRed, 'r'),
        (Color.BrightYellow, 'y'),
        (Color.BrightMagenta, 'm'),
        (Color.RedBg, '-'),
        (Color.GreenBg, '+'),
    ];

    // A blank rune keeps its space, so the letters line up below the runes they belong to — but
    // only where the color is a foreground on the default background. A space carrying a
    // background colour is the whole point of the mark: it is how a diff shows a changed indent
    // or a trailing space, which the text itself cannot show at all. Those keep their letter.
    public static string Of(Text text) =>
        string.Concat(
                text.Fragments.SelectMany(f =>
                    f.Text.Select(rune => rune == ' ' && !HasBackground(f.Color) ? ' ' : LetterOf(f.Color))
                )
            )
            .TrimEnd();

    static bool HasBackground(Color color) => color.Background != Terminal.Gui.Color.Black;

    // The exact pair first, so the two background colors keep their own letters, then the
    // foreground alone. The fallback is what makes a highlighted or selected row readable: those
    // keep their foreground and only swap the background, which is not a pair listed above and
    // would otherwise render as a row of '?'. Assert the background itself to see the highlight.
    static char LetterOf(Color color)
    {
        var exact = Letters.FirstOrDefault(c => c.Color == color);
        if (exact.Letter != '\0')
            return exact.Letter;

        var foreground = Letters.FirstOrDefault(c => c.Color.Foreground == color.Foreground);
        return foreground.Letter != '\0' ? foreground.Letter : '?';
    }
}
