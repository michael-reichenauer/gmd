namespace gmd.Cui.Common;

// A view a paste from the terminal is put into, gmd's text inputs, as their own Paste puts it: a
// one-line field takes the first line, a text view all of it, over what is selected. UI gives each
// paste to the one the keys go to, see UI.Paste.
interface IPasteTarget
{
    void PasteText(string text);
}
