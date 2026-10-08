using gmd.Common;
using gmd.Cui.Common;

namespace gmd.Cui;

interface IAboutDlg
{
    void Show();
}

class AboutDlg : IAboutDlg
{
    readonly Config config;

    public AboutDlg(Config config)
    {
        this.config = config;
    }

    public void Show()
    {
        var releases = config.Releases;
        var typeText = releases.IsPreview ? "(preview)" : "";
        var gmdVersion = Build.Version();
        var gmdBuildTime = Build.Time().ToLocalTime().IsoZone();
        var gmdSha = Build.Sha();
        var gitVersion = config.GitVersion;

        // No latest version is known until an update check has run, which it never does with update
        // checks turned off, and parsing the empty value crashed gmd
        var updates =
            !Version.TryParse(releases.LatestVersion, out var latest) ? "Not checked"
            : Build.Version() < latest ? $"{latest.Txt()} {typeText} is available"
            : "This is the latest version";

        // Where to find out more and to report a problem, with the log a report needs
        var msg =
            $"Version: {gmdVersion.Txt()} ({gmdSha}) \n"
            + $"Built:   {gmdBuildTime}\n"
            + $"Updates: {updates}\n"
            + $"Git:     {gitVersion} \n"
            + "\n"
            + $"Project: {Project.Url}\n"
            + $"Issues:  {Project.IssuesUrl}\n"
            + $"Log:     {ConfigLogger.FilePath}";

        UI.InfoMessage("About", msg);
    }
}
