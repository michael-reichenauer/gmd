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
    //readonly ScrollView scrollView;

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

        // scrollView = new ScrollView(new Rect(0, 0, 50, 20))
        // {
        //     // X = 0,
        //     // Y = 0,
        //     // Width = Dim.Fill(),
        //     // Height = Dim.Fill(),
        //     ContentSize = new Size(50, 20),
        //     ColorScheme = Terminal.Gui.Colors.Dialog,
        //     WantMousePositionReports = false,
        // };

        // scrollView.DrawContent += (r) =>
        // {
        //     Log.Info($"top level frame {toplevel.Frame}");
        //     scrollView.Frame = toplevel.Frame;
        //     Log.Info($"repo Content {repoView.GetContentSize()}");
        //     scrollView.ContentSize = repoView.GetContentSize();
        // };


        // scrollView.Add(repoView.View);

        toplevel.Add(repoView.View);

        toplevel.Loaded += Start;
    }

    void Start()
    {
        repoView.SetDataAsync().RunInBackground();
    }
}