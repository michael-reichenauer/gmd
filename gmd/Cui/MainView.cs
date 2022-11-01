using Terminal.Gui;


namespace gmd.Cui;

internal interface IMainView
{
    View View { get; }
}

internal class MainView : IMainView
{
    private RepoView repoView;
    private Toplevel toplevel;

    public View View => toplevel;

    internal MainView() : base()
    {
        toplevel = new Toplevel()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            WantMousePositionReports = false,
        };

        repoView = new RepoView();

        toplevel.Add(repoView.View);

        toplevel.Loaded += Start;
    }

    private void Start()
    {
        repoView.SetDataAsync().RunInBackground();
    }
}