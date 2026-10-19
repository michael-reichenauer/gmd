using gmd.Cui.Common;

namespace gmdTest.Fixtures;

// The color of every rune of a Text as one letter each, so colors can be asserted as a picture
// lined up under the text itself. Shared by GraphText and DiffText.
//
// Uppercase is a normal color and lowercase its bright variant, the same convention ScreenText
// uses for a captured screen: 'M' magenta, 'm' bright magenta, 'W' white, 'D' dark, '.' black.
// The two background colors a diff marks its changes with are '-' (red background, i.e. removed)
// and '+' (green background, i.e. added), chosen so a diff's colors read as a diff, a search
// match is '*' (black on yellow, see Color.Match) and the log's mark of a commit marked for a diff
// '±' (see Color.DiffMark), as it is drawn. The colors of code, which have no RGB of their own
// in the 16 colors, are what they color: '/' a comment, '"' a string, '#' a number, 'K' a keyword,
// 'L' a control keyword (if, return), 'T' a type, 'F' a function, 'V' a variable, 'N' a constant,
// 'E' an escape, 'X' a tag and 'A' an attribute. ScreenText
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
        (Color.Match, '*'),
        (Color.DiffMark, '±'),
        (Code(ColorName.CodeComment), '/'),
        (Code(ColorName.CodeString), '"'),
        (Code(ColorName.CodeNumber), '#'),
        (Code(ColorName.CodeKeyword), 'K'),
        (Code(ColorName.CodeControl), 'L'),
        (Code(ColorName.CodeType), 'T'),
        (Code(ColorName.CodeFunction), 'F'),
        (Code(ColorName.CodeVariable), 'V'),
        (Code(ColorName.CodeConstant), 'N'),
        (Code(ColorName.CodeEscape), 'E'),
        (Code(ColorName.CodeTag), 'X'),
        (Code(ColorName.CodeAttribute), 'A'),
    ];

    static Color Code(ColorName name) => new(name, ColorName.Background);

    // A blank rune keeps its space, so the letters line up below the runes they belong to — but
    // only where the color is a foreground on the default background. A space carrying a
    // background color is the whole point of the mark: it is how a diff shows a changed indent
    // or a trailing space, which the text itself cannot show at all. Those keep their letter.
    public static string Of(Text text) =>
        string.Concat(
                text.Fragments.SelectMany(f =>
                    f.Text.Select(rune => rune == ' ' && !HasBackground(f.Color) ? ' ' : LetterOf(f.Color))
                )
            )
            .TrimEnd();

    static bool HasBackground(Color color) => color.Background != ColorName.Background;

    // The exact pair first, so the background colors keep their own letters, then the
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
