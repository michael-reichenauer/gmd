namespace gmd.Cui.Common;

interface IWebPage
{
    // Opens the page in the browser, or copies its link where there is none to open it in. The name
    // is what the page is in the messages ("the help"), the title that of the box saying it was
    // copied. A link too long to read in a box, e.g. a prefilled issue form, is not shown in it. A
    // note is what to do next with the page, said with either.
    Task OpenAsync(string url, string name, string title, bool isLinkShown = true, string note = "");
}

// A page of gmd's on the web, the help, the change log or an issue form, opened in the browser. Over
// ssh, or in a container with no $BROWSER, the link is copied for opening it on the machine the user
// is sitting at, and said in a box as well, since not every terminal takes a copy, and since the
// status line is not seen from the diff, blame and conflict views.
class WebPage : IWebPage
{
    readonly IBrowserService browser;
    readonly IClipboardService clipboard;
    readonly IStatusLine status;

    public WebPage(IBrowserService browser, IClipboardService clipboard, IStatusLine status)
    {
        this.browser = browser;
        this.clipboard = clipboard;
        this.status = status;
    }

    public async Task OpenAsync(string url, string name, string title, bool isLinkShown = true, string note = "")
    {
        if (await browser.OpenAsync(url) is not Error e)
        {
            status.Info($"Opened {name} in the browser{(note != "" ? $", {note}" : "")}");
            return;
        }

        if (clipboard.Set(url) is Error copyError)
        {
            UI.ErrorMessage(
                $"{e.Message}, and the link could not be copied either:\n\n{url}\n\n{copyError.AllMessages()}"
            );
            return;
        }

        var link = isLinkShown ? $":\n\n{url}" : ".";
        var then = note != "" ? $"\n\n{char.ToUpper(note[0])}{note[1..]}." : "";
        UI.InfoMessage(title, $"{e.Message}, so the link to {name}\nis copied to the clipboard{link}{then}");
    }
}
