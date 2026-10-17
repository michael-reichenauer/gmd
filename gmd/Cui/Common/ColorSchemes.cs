using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;

namespace gmd.Cui.Common;

// The colors of the Terminal.Gui views gmd uses as they are, e.g. the text inputs, as Terminal.Gui
// schemes. Made anew each time a view reads its scheme (WithScheme), so they are in the palette in
// use when drawn, which can change once the terminal has said what its background is.
static class ColorSchemes
{
    internal static Scheme Dialog => Of(normal: Color.BrightMagenta, focus: Color.White, hot: Color.White);

    internal static Scheme Scrollbar => Of(normal: Color.BrightMagenta, focus: Color.BrightMagenta);

    internal static Scheme ErrorDialog => Of(normal: Color.BrightRed, focus: Color.White, hot: Color.White);

    internal static Scheme InfoDialog => Of(normal: Color.BrightCyan, focus: Color.White, hot: Color.White);

    internal static Scheme Progress => Of(normal: Color.Magenta, focus: Color.Magenta, disabled: Color.Magenta);

    internal static Scheme Label => Of(normal: Color.White, focus: new Color(Color.White, Color.Dark));

    internal static Scheme Indicator => Of(normal: Color.Dark, focus: Color.Dark);

    // A text input: the text in white, a selection on dark
    internal static Scheme TextField => Of(normal: Color.White, focus: Color.White);

    internal static Scheme CheckBox => Of(normal: Color.White, focus: new Color(Color.White, Color.Dark));

    internal static Scheme Button =>
        Of(normal: Color.White, focus: new Color(Color.White, Color.Dark), hot: Color.BrightCyan);

    internal static Scheme Window => Of(normal: Color.White, focus: Color.White);

    internal static Scheme Menu => Of(normal: Color.White, focus: new Color(Color.White, Color.Dark));

    internal static Scheme Border => Of(normal: Color.BrightMagenta, focus: Color.Dark);

    // Has the view read its scheme from gmd's colors, as they are when it is drawn. The scheme is set
    // too, since a view with none of its own asks its superview for its colors, never raising the
    // event that gives it this one.
    internal static T WithScheme<T>(this T view, Func<Scheme> scheme)
        where T : View
    {
        view.SetScheme(scheme());
        view.GettingScheme += (_, e) =>
        {
            e.Result = scheme();
            e.Handled = true;
        };
        return view;
    }

    // Every role a view may draw with is set, so none is derived by Terminal.Gui's own color math
    static Scheme Of(Color normal, Color focus, Color? hot = null, Color? disabled = null) =>
        new(normal)
        {
            Focus = focus,
            HotNormal = hot ?? normal,
            HotFocus = hot ?? focus,
            Active = new Color(Color.White, Color.Dark),
            HotActive = new Color(Color.White, Color.Dark),
            Highlight = focus,
            Editable = normal,
            ReadOnly = normal,
            Disabled = disabled ?? Color.Dark,
        };
}
