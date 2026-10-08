using gmd.Common;
using gmd.Cui.Common;

namespace gmd.Cui;

interface IHelpPage
{
    // Opens the help in the browser, at the section of that name ('## <section>' in help.md), or at
    // the top, and copies its link where there is no browser to open it in
    Task ShowAsync(string section = "");
}

// The help is gmd/doc/help.md as GitHub shows it (Project.HelpUrl), which a terminal dialog could
// not: a page as wide as the window, searchable, with links between its sections. Over ssh, or in a
// container with no $BROWSER, the link is copied for opening it on the machine the user is sitting
// at, and shown in a box as well, since not every terminal takes a copy, and since the status line
// is not seen from the diff, blame and conflict views or the start menu.
class HelpPage : IHelpPage
{
    // The sections the side views open the help at, so it starts at what is about them
    internal const string DiffSection = "Diff and Blame";
    internal const string ConflictSection = "Resolving Conflicts";

    readonly IBrowserService browser;
    readonly IClipboardService clipboard;
    readonly IStatusLine status;

    public HelpPage(IBrowserService browser, IClipboardService clipboard, IStatusLine status)
    {
        this.browser = browser;
        this.clipboard = clipboard;
        this.status = status;
    }

    public async Task ShowAsync(string section = "")
    {
        var url = Project.HelpUrl(section);
        if (await browser.OpenAsync(url) is not Error e)
        {
            status.Info("Opened the help in the browser");
            return;
        }

        if (clipboard.Set(url) is Error copyError)
        {
            UI.ErrorMessage(
                $"{e.Message}, and the link could not be copied either:\n\n{url}\n\n{copyError.AllMessages()}"
            );
            return;
        }

        UI.InfoMessage("Help", $"{e.Message}, so the link to the help\nis copied to the clipboard:\n\n{url}");
    }
}
