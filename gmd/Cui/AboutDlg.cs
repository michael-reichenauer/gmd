using gmd.Common;
using gmd.Cui.Common;

namespace gmd.Cui;

interface IAboutDlg
{
    void Show();
}

class AboutDlg : IAboutDlg
{
    readonly IState states;

    public AboutDlg(IState states)
    {
        this.states = states;
    }

    public void Show()
    {
        var releases = states.Get().Releases;
        var typeText = releases.IsPreview ? "(preview)" : "";
        var gmdVersion = Build.Version();
        var gmdBuildTime = Build.Time().IsoZone();
        var gmdSha = Build.Sha();
        var isAvailable = Build.Version() < Version.Parse(releases.LatestVersion);
        var gitVersion = states.Get().GitVersion;

        var msg =
            $"Version: {gmdVersion} ({gmdSha}) \n" +
            $"Built:   {gmdBuildTime}\n" +
            (isAvailable ? $"Updates: {releases.LatestVersion} {typeText} is available\n" :
                "Updates: Is latest version\n") +
            $"Git:     {gitVersion} ";

        UI.InfoMessage("About", msg);
    }
}

