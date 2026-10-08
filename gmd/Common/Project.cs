namespace gmd.Common;

// Where gmd is on the web, for About and for what a crash says
static class Project
{
    public const string Url = "https://github.com/michael-reichenauer/gmd";
    public const string IssuesUrl = Url + "/issues";

    // The form of a problem report, which asks for the version and the log (.github/ISSUE_TEMPLATE)
    public const string ProblemUrl = IssuesUrl + "/new?template=bug_report.yml";
}
