using ColorScheme = Terminal.Gui.ColorScheme;

namespace gmd.Cui.Common;

static class ColorSchemes
{
    internal static ColorScheme Dialog =>
        new ColorScheme()
        {
            Normal = Color.BrightMagenta,
            Focus = Color.White,
            HotNormal = Color.White,
            HotFocus = Color.White,
            Disabled = Color.Dark,
        };

    internal static ColorScheme Scrollbar =>
        new ColorScheme()
        {
            Normal = Color.BrightMagenta,
            Focus = Color.BrightMagenta,
            HotNormal = Color.BrightMagenta,
            HotFocus = Color.BrightMagenta,
            Disabled = Color.Dark,
        };

    internal static ColorScheme ErrorDialog =>
        new ColorScheme()
        {
            Normal = Color.BrightRed,
            Focus = Color.White,
            HotNormal = Color.White,
            HotFocus = Color.White,
            Disabled = Color.Dark,
        };

    internal static ColorScheme InfoDialog =>
        new ColorScheme()
        {
            Normal = Color.BrightCyan,
            Focus = Color.White,
            HotNormal = Color.White,
            HotFocus = Color.White,
            Disabled = Color.Dark,
        };

    internal static ColorScheme Progress =>
        new ColorScheme()
        {
            Normal = Color.Magenta,
            Focus = Color.Magenta,
            HotNormal = Color.Magenta,
            HotFocus = Color.Magenta,
            Disabled = Color.Magenta,
        };

    internal static ColorScheme Label =>
        new ColorScheme()
        {
            Normal = Color.White,
            Focus = new Color(Color.White, Color.Dark),
            HotNormal = Color.White,
            HotFocus = new Color(Color.White, Color.Dark),
            Disabled = Color.Dark,
        };

    internal static ColorScheme Indicator =>
        new ColorScheme()
        {
            Normal = Color.Dark,
            Focus = Color.Dark,
            HotNormal = Color.Dark,
            HotFocus = Color.Dark,
            Disabled = Color.Dark,
        };

    internal static ColorScheme TextField =>
        new ColorScheme()
        {
            Normal = Color.White,
            Focus = Color.White,
            HotNormal = Color.White,
            HotFocus = Color.White,
            Disabled = Color.Dark,
        };

    internal static ColorScheme CheckBox =>
        new ColorScheme()
        {
            Normal = Color.White,
            Focus = new Color(Color.White, Color.Dark),
            HotNormal = Color.White,
            HotFocus = new Color(Color.White, Color.Dark),
            Disabled = Color.Dark,
        };

    internal static ColorScheme Button =>
        new ColorScheme()
        {
            Normal = Color.White,
            Focus = new Color(Color.White, Color.Dark),
            HotNormal = Color.BrightCyan,
            HotFocus = new Color(Color.White, Color.Dark),
            Disabled = Color.Dark,
        };

    internal static ColorScheme Window =>
        new ColorScheme()
        {
            Normal = Color.White,
            Focus = Color.White,
            HotNormal = Color.White,
            HotFocus = Color.White,
            Disabled = Color.Dark,
        };

    internal static ColorScheme Menu =>
        new ColorScheme()
        {
            Normal = Color.White,
            Focus = new Color(Color.White, Color.Dark),
            HotNormal = Color.White,
            HotFocus = new Color(Color.White, Color.Dark),
            Disabled = Color.Dark,
        };

    internal static ColorScheme Border =>
        new ColorScheme()
        {
            Normal = Color.BrightMagenta,
            Focus = Color.Dark,
            HotNormal = Color.Dark,
            HotFocus = Color.Dark,
            Disabled = Color.Dark,
        };
}
