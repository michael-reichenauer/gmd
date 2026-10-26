using gmd.Common;
using gmd.Cui.Common;

namespace gmd.Cui.App;

interface IHelpPage
{
    // Opens the help in the browser, at the section of that name ('## <section>' in help.md), or at
    // the top, and copies its link where there is no browser to open it in
    Task ShowAsync(string section = "");
}

// The help is gmd/doc/help.md as GitHub shows it (Project.HelpUrl), which a terminal dialog could
// not: a page as wide as the window, searchable, with links between its sections.
class HelpPage : IHelpPage
{
    // The sections the side views open the help at, so it starts at what is about them
    internal const string DiffSection = "Diff and Blame";
    internal const string ConflictSection = "Resolving Conflicts";

    readonly IWebPage webPage;

    public HelpPage(IWebPage webPage)
    {
        this.webPage = webPage;
    }

    public Task ShowAsync(string section = "") => webPage.OpenAsync(Project.HelpUrl(section), "the help", "Help");
}
