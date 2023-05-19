using gmd.Common;
using gmd.Cui.Common;
using gmd.Git;

namespace gmd.Cui;

interface IAboutDlg
{
    void Show();
}

class AboutDlg : IAboutDlg
{
    readonly IGit git;
    private readonly IState states;

    public AboutDlg(IGit git, IState states)
    {
        this.git = git;
        this.states = states;
    }

    public void Show()
    {
        UI.Post(async () =>
        {
            var releases = states.Get().Releases;
            var typeText = releases.IsPreview ? "(preview)" : "";
            var gmdVersion = Build.Version();
            var gmdBuildTime = Build.Time().ToUniversalTime().Iso();
            var gmdSha = Build.Sha();
            if (!Try(out var gitVersion, out var e, await git.Version())) gitVersion = "0.0";

            var msg =
                $"Version: {gmdVersion} ({gmdSha}) \n" +
                $"Built:   {gmdBuildTime}Z \n" +
                $"Remote:  {releases.LatestVersion} {typeText}\n" +
                $"Git:     {gitVersion} ";

            UI.InfoMessage("About", msg);
        });
    }
}

