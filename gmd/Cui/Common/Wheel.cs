using Terminal.Gui.Input;

namespace gmd.Cui.Common;

// Which way a mouse event turned the wheel. Terminal.Gui writes a wheel turned sideways as the
// vertical one with Ctrl (WheeledLeft is Ctrl | WheeledUp, WheeledRight is Ctrl | WheeledDown), and
// so it writes the vertical wheel turned with Shift or Ctrl as well, so a HasFlag(WheeledUp) is true
// for a wheel turned left too: a sideways wheel scrolled gmd's views up and down. Ask these instead.
static class Wheel
{
    // 1 for a turn down, -1 up, and 0 for none, a turn sideways included
    public static int Vertical(MouseFlags flags) =>
        Sideways(flags) != 0 ? 0
        : flags.HasFlag(MouseFlags.WheeledDown) ? 1
        : flags.HasFlag(MouseFlags.WheeledUp) ? -1
        : 0;

    // 1 for a turn right, -1 left, and 0 for none: the sideways wheel some mice have by the thumb, or
    // the vertical one turned with Shift, as browsers and editors take it too
    public static int Sideways(MouseFlags flags) =>
        flags.HasFlag(MouseFlags.WheeledRight) ? 1
        : flags.HasFlag(MouseFlags.WheeledLeft) ? -1
        : 0;
}
