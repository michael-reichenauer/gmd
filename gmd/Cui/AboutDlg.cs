using gmd.Cui.Common;
using gmd.Git;

namespace gmd.Cui;

interface IAboutDlg
{
    void Show();
}

class AboudDlg : IAboutDlg
{
    readonly IGit git;

    public AboudDlg(IGit git)
    {
        this.git = git;
    }

    public void Show()
    {
        UI.Post(async () =>
        {
            var gmdVersion = Build.Version();
            var gmdBuildTime = Build.Time().ToUniversalTime().Iso();
            var gmdSha = Build.Sha();
            if (!Try(out var gitVersion, out var e, await git.Version())) gitVersion = "0.0";

            var msg =
                $"Version: {gmdVersion} ({gmdSha}) \n" +
                $"Built:   {gmdBuildTime}Z \n" +
                $"Git:     {gitVersion} ";

            UI.InfoMessage("About", msg);
        });
    }
}

