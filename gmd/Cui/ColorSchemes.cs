using Terminal.Gui;


namespace gmd.Cui;

static class ColorSchemes
{
    internal static readonly ColorScheme ButtonColorScheme = new ColorScheme()
    {
        Normal = Colors.Black,
        Focus = Colors.Make(Color.White, Color.DarkGray),
        HotNormal = Colors.Blue,
        HotFocus = Colors.Make(Color.White, Color.DarkGray),
        Disabled = Colors.Dark,
    };

    internal static readonly ColorScheme DialogColorScheme = new ColorScheme()
    {
        Normal = Colors.White,
        Focus = Colors.White,
        HotNormal = Colors.White,
        HotFocus = Colors.White,
        Disabled = Colors.Dark,
    };

    internal static readonly ColorScheme ErrorDialogColorScheme = new ColorScheme()
    {
        Normal = Colors.BrightRed,
        Focus = Colors.White,
        HotNormal = Colors.White,
        HotFocus = Colors.White,
        Disabled = Colors.Dark,
    };

    internal static readonly ColorScheme WindowColorScheme = new ColorScheme()
    {
        Normal = Colors.White,
        Focus = Colors.White,
        HotNormal = Colors.White,
        HotFocus = Colors.White,
        Disabled = Colors.Dark,
    };

    internal static readonly ColorScheme MenuColorScheme = new ColorScheme()
    {
        Normal = Colors.White,
        Focus = Colors.Make(Color.White, Color.DarkGray),
        HotNormal = Colors.White,
        HotFocus = Colors.Make(Color.White, Color.DarkGray),
        Disabled = Colors.Dark,
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
