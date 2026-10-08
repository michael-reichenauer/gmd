namespace gmd.Common;

// Where gmd is on the web, for About, for what a crash says and for the help
static class Project
{
    public const string Url = "https://github.com/michael-reichenauer/gmd";
    public const string IssuesUrl = Url + "/issues";

    // The form of a problem report, which asks for the version and the log (.github/ISSUE_TEMPLATE)
    public const string ProblemUrl = IssuesUrl + "/new?template=bug_report.yml";

    public const string HelpFile = "gmd/doc/help.md";

    // The help as GitHub shows it, at the section of that name ('## <section>' in help.md) or at the
    // top. It is the file at the commit gmd was built from, so it tells of the keys and commands this
    // version has, whichever release or preview it is, and is on main for a build that is not CI's,
    // which knows no commit.
    public static string HelpUrl(string section = "") => HelpUrl(section, Build.CommitSha());

    internal static string HelpUrl(string section, string sha)
    {
        var url = $"{Url}/blob/{(sha != "" ? sha : "main")}/{HelpFile}";
        return section != "" ? $"{url}#{Anchor(section)}" : url;
    }

    // The id GitHub gives a heading, for a link to it: the heading in lower case, with every
    // character but letters, digits, spaces, '-' and '_' dropped, and the spaces then made '-'
    internal static string Anchor(string heading) =>
        new(
            heading
                .Trim()
                .ToLowerInvariant()
                .Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_')
                .Select(c => c == ' ' ? '-' : c)
                .ToArray()
        );
}
