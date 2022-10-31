using Terminal.Gui;

namespace gmd.Cui;

internal class MainView : Toplevel
{
    private RepoView repoView;

    internal MainView() : base()
    {
        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();

        repoView = new RepoView();
        Add(repoView);
    }

    internal async Task SetData()
    {
        await repoView.SetData();
    }
}