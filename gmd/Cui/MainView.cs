using Terminal.Gui;


namespace gmd.Cui;

interface IMainView
{
    View View { get; }
}

class MainView : IMainView
{
    Toplevel toplevel;
    readonly IRepoView repoView;

    public View View => toplevel;

    MainView(IRepoView repoView) : base()
    {
        this.repoView = repoView;
        toplevel = new Toplevel()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            WantMousePositionReports = false,
        };


        toplevel.Add(repoView.View);

        toplevel.Loaded += Start;
    }

    void Start()
    {
        repoView.SetDataAsync().RunInBackground();
    }
}