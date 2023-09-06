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
        var gmdBuildTime = Build.Time().IsoZone();
        var gmdSha = Build.Sha();
        var latest = Version.Parse(releases.LatestVersion);
        var isAvailable = Build.Version() < latest;
        var gitVersion = config.GitVersion;

        var msg =
            $"Version: {gmdVersion.Txt()} ({gmdSha}) \n" +
            $"Built:   {gmdBuildTime}\n" +
            (isAvailable ?
                $"Updates: {latest.Txt} {typeText} is available\n" :
                "Updates: Is latest version\n") +
            $"Git:     {gitVersion} ";

        UI.InfoMessage("About", msg);
    }
}

