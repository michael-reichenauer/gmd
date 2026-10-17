using Terminal.Gui.Drivers;
using TgAttribute = Terminal.Gui.Drawing.Attribute;
using TgColor = Terminal.Gui.Drawing.Color;

namespace gmd.Cui.Common;

// The colors gmd draws with, by what they are rather than as RGB: which RGB a name is drawn in is
// the palette's, chosen as gmd starts, see Palette. Background is the background everything is
// drawn on, the terminal's own with the dark and the light palettes, black with the 16 colors.
enum ColorName
{
    Background,
    Black,
    Blue,
    Green,
    Cyan,
    Red,
    Magenta,
    Yellow,
    Dark,
    White,
    BrightBlue,
    BrightGreen,
    BrightCyan,
    BrightRed,
    BrightMagenta,
    BrightYellow,

    // A changed word's background in a diff, and its text on it
    RemovedBg,
    OnRemoved,
    AddedBg,
    OnAdded,

    // The branch colors, of which the palette has BranchCount
    Branch1,
    Branch2,
    Branch3,
    Branch4,
    Branch5,
    Branch6,
    Branch7,
    Branch8,
    Branch9,
    Branch10,
    Branch11,
    Branch12,
}

// A foreground and a background color, by name, drawn in the RGB of the palette in use when drawn
record Color
{
    // Predefined colors
    public static readonly Color Blue = Make(ColorName.Blue);
    public static readonly Color Green = Make(ColorName.Green);
    public static readonly Color Cyan = Make(ColorName.Cyan);
    public static readonly Color Red = Make(ColorName.Red);
    public static readonly Color Magenta = Make(ColorName.Magenta);
    public static readonly Color Yellow = Make(ColorName.Yellow);
    public static readonly Color Dark = Make(ColorName.Dark);
    public static readonly Color White = Make(ColorName.White);
    public static readonly Color Black = Make(ColorName.Black);
    public static readonly Color BrightBlue = Make(ColorName.BrightBlue);
    public static readonly Color BrightGreen = Make(ColorName.BrightGreen);
    public static readonly Color BrightCyan = Make(ColorName.BrightCyan);
    public static readonly Color BrightRed = Make(ColorName.BrightRed);
    public static readonly Color BrightMagenta = Make(ColorName.BrightMagenta);
    public static readonly Color BrightYellow = Make(ColorName.BrightYellow);

    public static readonly Color RedBg = new(ColorName.OnRemoved, ColorName.RemovedBg);
    public static readonly Color GreenBg = new(ColorName.OnAdded, ColorName.AddedBg);

    // A search match in the diff and the blame, black on yellow as a find marks it in most tools. The
    // row highlights leave it as it is (Text.ToHighlight), since the cursor is put on the match.
    public static readonly Color Match = new(ColorName.Black, ColorName.BrightYellow);

    public Color(Color fg, Color bg)
        : this(fg.Foreground, bg.Foreground) { }

    public Color(ColorName fg, ColorName bg)
    {
        Foreground = fg;
        Background = bg;
    }

    public ColorName Foreground { get; init; }
    public ColorName Background { get; init; }

    // The i:th branch color, of the palette's BranchCount
    public static Color Branch(int i) => Make(ColorName.Branch1 + i);

    // The attribute Terminal.Gui draws with, in the RGB of the palette in use now
    public static implicit operator TgAttribute(Color color) =>
        new(Palette.Current[color.Foreground], Palette.Current[color.Background]);

    public static implicit operator ColorName(Color color) => color.Foreground;

    static Color Make(ColorName color) => new(color, ColorName.Background);
}

// The RGB a color name is drawn in. Terminal.Gui 2.x writes every color as 24-bit RGB, unless it is
// told to write the 16 colors of the terminal's theme (Driver.Force16Colors), so gmd's look is its
// own palette's rather than the terminal theme's. Three: the dark one, Windows Terminal's Campbell
// scheme, which the README's pictures are drawn with, on the terminal's background; the light one,
// the same hues dark enough to read on white, on the terminal's background; and the 16 colors of
// the terminal's theme on black, which is how gmd looked with Terminal.Gui 1.x, for a terminal of 16
// colors, for whoever wants their theme's colors, and for the end-to-end tests, whose colors are
// asserted as the 16 escape codes.
sealed class Palette
{
    readonly IReadOnlyDictionary<ColorName, TgColor> colors;

    Palette(string name, bool isSixteen, int branchCount, IReadOnlyDictionary<ColorName, TgColor> colors)
    {
        Name = name;
        IsSixteen = isSixteen;
        BranchCount = branchCount;
        this.colors = colors;
    }

    public string Name { get; }

    // Drawn as the 16 colors of the terminal's theme, rather than as RGB
    public bool IsSixteen { get; }

    // How many of the branch colors there are, Branch1 and on: five of the 16 colors, those that
    // mean nothing else (main, deleted, ahead and behind have the others), twelve of RGB
    public int BranchCount { get; }

    // The colors branches are drawn in: the five of the 16 colors by their own names, as gmd has
    // always drawn them, else Branch1 and on
    public IReadOnlyList<Color> BranchColors =>
        IsSixteen
            ? [Color.Blue, Color.Green, Color.Cyan, Color.Red, Color.Yellow]
            : Enumerable.Range(0, BranchCount).Select(Color.Branch).ToList();

    public TgColor this[ColorName name] => colors[name];

    // The palette gmd draws with now, see Choose; a field, as the palettes are made after it. The
    // 16 colors until one is chosen, which a unit test, with no terminal, never has.
    static Palette? current;
    public static Palette Current => current ?? Sixteen;

    // Raised on the main thread when Current changes, for what has to be drawn again
    public static event Action? Changed;

    internal static void Use(Palette palette)
    {
        if (palette == Current && Driver.Force16Colors == palette.IsSixteen)
            return;
        current = palette;
        Driver.Force16Colors = palette.IsSixteen;
        Changed?.Invoke();
    }

    // The palette for the Colors setting ("auto", "dark", "light" or "16"), what Terminal.Gui found
    // the terminal can do, and the terminal's own background, which it asks the terminal for as it
    // starts and which is null until the answer is in, or with a terminal that does not answer
    internal static Palette Choose(string setting, ColorCapabilityLevel capability, TgColor? background)
    {
        if (setting == "16" || capability is ColorCapabilityLevel.Colors16 or ColorCapabilityLevel.NoColor)
            return Sixteen;
        if (setting == "light")
            return Light;
        if (setting == "dark")
            return Dark;
        return background is TgColor bg && !bg.IsDarkColor() ? Light : Dark;
    }

    public static readonly Palette Sixteen = new(
        "16",
        isSixteen: true,
        branchCount: 5,
        Colors(
            background: TgColor.Black,
            black: TgColor.Black,
            blue: TgColor.Blue,
            green: TgColor.Green,
            cyan: TgColor.Cyan,
            red: TgColor.Red,
            magenta: TgColor.Magenta,
            yellow: TgColor.Yellow,
            dark: TgColor.DarkGray,
            white: TgColor.White,
            brightBlue: TgColor.BrightBlue,
            brightGreen: TgColor.BrightGreen,
            brightCyan: TgColor.BrightCyan,
            brightRed: TgColor.BrightRed,
            brightMagenta: TgColor.BrightMagenta,
            brightYellow: TgColor.BrightYellow,
            removedBg: TgColor.Red,
            onRemoved: TgColor.White,
            addedBg: TgColor.BrightGreen,
            onAdded: TgColor.Black,
            branches: [TgColor.Blue, TgColor.Green, TgColor.Cyan, TgColor.Red, TgColor.Yellow]
        )
    );

    public static readonly Palette Dark = new(
        "dark",
        isSixteen: false,
        branchCount: 12,
        Colors(
            background: TgColor.None,
            black: Rgb(0x0C0C0C),
            blue: Rgb(0x0037DA),
            green: Rgb(0x13A10E),
            cyan: Rgb(0x3A96DD),
            red: Rgb(0xC50F1F),
            magenta: Rgb(0x881798),
            yellow: Rgb(0xC19C00),
            dark: Rgb(0x767676),
            white: Rgb(0xF2F2F2),
            brightBlue: Rgb(0x3B78FF),
            brightGreen: Rgb(0x16C60C),
            brightCyan: Rgb(0x61D6D6),
            brightRed: Rgb(0xE74856),
            brightMagenta: Rgb(0xB4009E),
            brightYellow: Rgb(0xF9F1A5),
            removedBg: Rgb(0x6E1E24),
            onRemoved: Rgb(0xF2F2F2),
            addedBg: Rgb(0x1E5A24),
            onAdded: Rgb(0xF2F2F2),
            branches:
            [
                Rgb(0x42A5F5), // blue
                Rgb(0x4CAF50), // green
                Rgb(0x26C6DA), // cyan
                Rgb(0xEF5350), // red
                Rgb(0xFFEE58), // yellow
                Rgb(0xFFA726), // orange
                Rgb(0x4DB6AC), // teal
                Rgb(0xC0CA33), // lime
                Rgb(0xF48FB1), // pink
                Rgb(0xA1887F), // brown
                Rgb(0x7986CB), // indigo
                Rgb(0xFF8A65), // coral
            ]
        )
    );

    public static readonly Palette Light = new(
        "light",
        isSixteen: false,
        branchCount: 12,
        Colors(
            background: TgColor.None,
            black: Rgb(0xFFFFFF),
            blue: Rgb(0x0037DA),
            green: Rgb(0x0E7A0B),
            cyan: Rgb(0x0B6E99),
            red: Rgb(0xC50F1F),
            magenta: Rgb(0x881798),
            yellow: Rgb(0x8A6D00),
            dark: Rgb(0x8C8C8C),
            white: Rgb(0x1E1E1E),
            brightBlue: Rgb(0x1F4FC8),
            brightGreen: Rgb(0x108A0A),
            brightCyan: Rgb(0x0E7C86),
            brightRed: Rgb(0xD13438),
            brightMagenta: Rgb(0xA01A9E),
            brightYellow: Rgb(0xF9F1A5),
            removedBg: Rgb(0xFFC8C8),
            onRemoved: Rgb(0x1E1E1E),
            addedBg: Rgb(0xC8F0C8),
            onAdded: Rgb(0x1E1E1E),
            branches:
            [
                Rgb(0x1565C0), // blue
                Rgb(0x2E7D32), // green
                Rgb(0x00838F), // cyan
                Rgb(0xC62828), // red
                Rgb(0xA07C00), // yellow
                Rgb(0xEF6C00), // orange
                Rgb(0x00796B), // teal
                Rgb(0x827717), // lime
                Rgb(0xAD1457), // pink
                Rgb(0x6D4C41), // brown
                Rgb(0x3949AB), // indigo
                Rgb(0xD84315), // coral
            ]
        )
    );

    static TgColor Rgb(int rgb) => new((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);

    static Dictionary<ColorName, TgColor> Colors(
        TgColor background,
        TgColor black,
        TgColor blue,
        TgColor green,
        TgColor cyan,
        TgColor red,
        TgColor magenta,
        TgColor yellow,
        TgColor dark,
        TgColor white,
        TgColor brightBlue,
        TgColor brightGreen,
        TgColor brightCyan,
        TgColor brightRed,
        TgColor brightMagenta,
        TgColor brightYellow,
        TgColor removedBg,
        TgColor onRemoved,
        TgColor addedBg,
        TgColor onAdded,
        IReadOnlyList<TgColor> branches
    )
    {
        var colors = new Dictionary<ColorName, TgColor>
        {
            [ColorName.Background] = background,
            [ColorName.Black] = black,
            [ColorName.Blue] = blue,
            [ColorName.Green] = green,
            [ColorName.Cyan] = cyan,
            [ColorName.Red] = red,
            [ColorName.Magenta] = magenta,
            [ColorName.Yellow] = yellow,
            [ColorName.Dark] = dark,
            [ColorName.White] = white,
            [ColorName.BrightBlue] = brightBlue,
            [ColorName.BrightGreen] = brightGreen,
            [ColorName.BrightCyan] = brightCyan,
            [ColorName.BrightRed] = brightRed,
            [ColorName.BrightMagenta] = brightMagenta,
            [ColorName.BrightYellow] = brightYellow,
            [ColorName.RemovedBg] = removedBg,
            [ColorName.OnRemoved] = onRemoved,
            [ColorName.AddedBg] = addedBg,
            [ColorName.OnAdded] = onAdded,
        };
        // A palette of fewer branch colors repeats them, so every name has an RGB
        for (int i = 0; i < 12; i++)
            colors[ColorName.Branch1 + i] = branches[i % branches.Count];
        return colors;
    }
}
