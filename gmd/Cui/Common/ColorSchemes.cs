using Terminal.Gui;


namespace gmd.Cui.Common;

static class ColorSchemes
{
    internal static readonly ColorScheme ButtonColorScheme = new ColorScheme()
    {
        Normal = TextColor.Black,
        Focus = TextColor.Make(Color.White, Color.DarkGray),
        HotNormal = TextColor.Blue,
        HotFocus = TextColor.Make(Color.White, Color.DarkGray),
        Disabled = TextColor.Dark,
    };

    internal static readonly ColorScheme DialogColorScheme = new ColorScheme()
    {
        Normal = TextColor.White,
        Focus = TextColor.Make(Color.White, Color.DarkGray),
        HotNormal = TextColor.White,
        HotFocus = TextColor.Make(Color.White, Color.DarkGray),
        Disabled = TextColor.Dark,
    };

    internal static readonly ColorScheme ErrorDialogColorScheme = new ColorScheme()
    {
        Normal = TextColor.BrightRed,
        Focus = TextColor.Make(Color.White, Color.DarkGray),
        HotNormal = TextColor.White,
        HotFocus = TextColor.Make(Color.White, Color.DarkGray),
        Disabled = TextColor.Dark,
    };

    internal static readonly ColorScheme WindowColorScheme = new ColorScheme()
    {
        Normal = TextColor.White,
        Focus = TextColor.White,
        HotNormal = TextColor.White,
        HotFocus = TextColor.White,
        Disabled = TextColor.Dark,
    };

    internal static readonly ColorScheme MenuColorScheme = new ColorScheme()
    {
        Normal = TextColor.White,
        Focus = TextColor.Make(Color.White, Color.DarkGray),
        HotNormal = TextColor.White,
        HotFocus = TextColor.Make(Color.White, Color.DarkGray),
        Disabled = TextColor.Dark,
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
