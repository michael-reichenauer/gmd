using ColorScheme = Terminal.Gui.ColorScheme;


namespace gmd.Cui.Common;

static class ColorSchemes
{
    internal static ColorScheme Dialog => new ColorScheme()
    {
        Normal = Color.BrightMagenta,
        Focus = Color.White,
        HotNormal = Color.White,
        HotFocus = Color.White,
        Disabled = Color.Dark,
    };

    internal static ColorScheme Scrollbar => new ColorScheme()
    {
        Normal = Color.BrightMagenta,
        Focus = Color.BrightMagenta,
        HotNormal = Color.BrightMagenta,
        HotFocus = Color.BrightMagenta,
        Disabled = Color.Dark,
    };

    internal static ColorScheme ErrorDialog => new ColorScheme()
    {
        Normal = Color.BrightRed,
        Focus = Color.White,
        HotNormal = Color.White,
        HotFocus = Color.White,
        Disabled = Color.Dark,
    };

    internal static ColorScheme InfoDialog => new ColorScheme()
    {
        Normal = Color.BrightCyan,
        Focus = Color.White,
        HotNormal = Color.White,
        HotFocus = Color.White,
        Disabled = Color.Dark,
    };

    internal static ColorScheme Label => new ColorScheme()
    {
        Normal = Color.White,
        Focus = new Color(Color.White, Color.Dark),
        HotNormal = Color.White,
        HotFocus = new Color(Color.White, Color.Dark),
        Disabled = Color.Dark,
    };

    internal static ColorScheme Indicator => new ColorScheme()
    {
        Normal = Color.Dark,
        Focus = Color.Dark,
        HotNormal = Color.Dark,
        HotFocus = Color.Dark,
        Disabled = Color.Dark,
    };

    internal static ColorScheme TextField => new ColorScheme()
    {
        Normal = Color.White,
        Focus = Color.White,
        HotNormal = Color.White,
        HotFocus = Color.White,
        Disabled = Color.Dark,
    };

    internal static ColorScheme CheckBox => new ColorScheme()
    {
        Normal = Color.White,
        Focus = new Color(Color.White, Color.Dark),
        HotNormal = Color.White,
        HotFocus = new Color(Color.White, Color.Dark),
        Disabled = Color.Dark,
    };

    internal static ColorScheme Button => new ColorScheme()
    {
        Normal = Color.White,
        Focus = new Color(Color.White, Color.Dark),
        HotNormal = Color.BrightCyan,
        HotFocus = new Color(Color.White, Color.Dark),
        Disabled = Color.Dark,
    };

    internal static ColorScheme Window => new ColorScheme()
    {
        Normal = Color.White,
        Focus = Color.White,
        HotNormal = Color.White,
        HotFocus = Color.White,
        Disabled = Color.Dark,
    };

    internal static ColorScheme Menu => new ColorScheme()
    {
        Normal = Color.White,
        Focus = new Color(Color.White, Color.Dark),
        HotNormal = Color.White,
        HotFocus = new Color(Color.White, Color.Dark),
        Disabled = Color.Dark,
    };

    internal static ColorScheme Border => new ColorScheme()
    {
        Normal = Color.BrightMagenta,
        Focus = Color.Dark,
        HotNormal = Color.Dark,
        HotFocus = Color.Dark,
        Disabled = Color.Dark,
    };


    // 		Colors.TopLevel.Normal = MakeColor (Color.BrightGreen, Color.Black);
    // 		Colors.TopLevel.Focus = MakeColor (Color.White, Color.Cyan);
    // 		Colors.TopLevel.HotNormal = MakeColor (Color.Brown, Color.Black);
    // 		Colors.TopLevel.HotFocus = MakeColor (Color.Blue, Color.Cyan);
    // 		Colors.TopLevel.Disabled = MakeColor (Color.DarkGray, Color.Black);

    // 		Colors.Base.Normal = MakeColor (Color.White, Color.Blue);
    // 		Colors.Base.Focus = MakeColor (Color.Black, Color.Gray);
    // 		Colors.Base.HotNormal = MakeColor (Color.BrightCyan, Color.Blue);
    // 		Colors.Base.HotFocus = MakeColor (Color.BrightBlue, Color.Gray);
    // 		Colors.Base.Disabled = MakeColor (Color.DarkGray, Color.Blue);

    // 		Colors.Dialog.Normal = MakeColor (Color.Black, Color.Gray);
    // 		Colors.Dialog.Focus = MakeColor (Color.White, Color.DarkGray);
    // 		Colors.Dialog.HotNormal = MakeColor (Color.Blue, Color.Gray);
    // 		Colors.Dialog.HotFocus = MakeColor (Color.BrightYellow, Color.DarkGray);
    // 		Colors.Dialog.Disabled = MakeColor (Color.Gray, Color.DarkGray);

    // 		Colors.Menu.Normal = MakeColor (Color.White, Color.DarkGray);
    // 		Colors.Menu.Focus = MakeColor (Color.White, Color.Black);
    // 		Colors.Menu.HotNormal = MakeColor (Color.BrightYellow, Color.DarkGray);
    // 		Colors.Menu.HotFocus = MakeColor (Color.BrightYellow, Color.Black);
    // 		Colors.Menu.Disabled = MakeColor (Color.Gray, Color.DarkGray);

    // 		Colors.Error.Normal = MakeColor (Color.Red, Color.White);
    // 		Colors.Error.Focus = MakeColor (Color.Black, Color.BrightRed);
    // 		Colors.Error.HotNormal = MakeColor (Color.Black, Color.White);
    // 		Colors.Error.HotFocus = MakeColor (Color.BrightRed, Color.Gray);
    // 		Colors.Error.Disabled = MakeColor (Color.DarkGray, Color.White);
}
