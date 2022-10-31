using gmd.Utils.Git.Private;
using Terminal.Gui;

internal class MainView : Toplevel
{
    private ListView repoView;

    internal MainView() : base()
    {
        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();
        repoView = new ListView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        ColorScheme cs = new ColorScheme();
        cs.Normal = Application.Driver.MakeAttribute(Color.White, Color.Black);
        ColorScheme = cs;

        Add(repoView);
        AddKeyBinding(Key.Esc, Command.QuitToplevel);
    }

    internal async Task SetData()
    {
        var git = new GitRepo("");

        var commits = await git.GetLog();
        if (commits.IsFaulted)
        {
            return;
        }

        var lines = commits.Value.Select(c => $"┃ ┃ {c.Sid} {c.Subject.Max(50),-50} {c.Author.Max(10),-10}");

        repoView.SetSource(lines.ToList());
    }

    private string Max(string text, int max)
    {
        if (text.Length <= max)
        {
            return text;
        }

        return text.Substring(0, max);
    }
}