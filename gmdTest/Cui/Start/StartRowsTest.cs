using gmd.Cui.Common;
using gmd.Cui.Start;
using gmdTest.Fixtures;

namespace gmdTest.Cui.Start;

// The start page as pictures, each row after a '|' so the indent is visible, with the selected item
// marked as it is drawn. The width is the content width of the view, i.e. one less than the
// terminal for the scrollbar, and the height what is left between the application bar and the
// key-hint line.
[TestClass]
public class StartRowsTest
{
    static StartRepo Repo(string name, string path, bool isRecent = true) =>
        new("/home/me/" + path, name, "~/" + path, isRecent);

    static readonly StartRepos Returning = new(
        "/home/me/Downloads",
        "~/Downloads",
        false,
        [Repo("gmd", "code/gmd"), Repo("web-shop", "code/web-shop"), Repo("dotfiles", "dotfiles")],
        [Repo("scratch", "Downloads/scratch", false)],
        0
    );

    static readonly StartRepos Newcomer = new("/home/me/Downloads", "~/Downloads", false, [], [], 0);

    const string NoUpdate = "";

    static string Picture(StartPage page, int selected = -2)
    {
        var row = page.RowOf(selected == -2 ? page.FirstItem : selected);
        return string.Join(
            "\n",
            Enumerable.Range(0, page.Count).Select(i => "|" + page.Row(i, i == row).ToString().TrimEnd())
        );
    }

    // Centered on a wide terminal, the most recent repository selected, so Enter opens it
    [TestMethod]
    public void TestAReturningUserIsShownTheRepositoriesFirst()
    {
        Assert.AreEqual(
            """
            |
            |                             Gmd · a Git client where you choose which branches are shown
            |                             ~/Downloads is not a git repository
            |
            |                             Recent repositories
            |                           ▸ 1  gmd       ~/code/gmd
            |                             2  web-shop  ~/code/web-shop
            |                             3  dotfiles  ~/dotfiles
            |
            |                             Repositories in ~/Downloads
            |                             4  scratch   ~/Downloads/scratch
            |
            |                             o  Open a folder ...
            |                             c  Clone a repository ...
            |                             i  Init a repository in ~/Downloads ...
            |                             ?  Help
            |                             a  About
            |                             q  Quit
            """,
            Picture(StartRows.Of(Returning, NoUpdate, 119, 37))
        );
    }

    // No repository to list yet: the actions are what there is, each said what it does
    [TestMethod]
    public void TestANewcomerIsToldWhatEachActionDoes()
    {
        Assert.AreEqual(
            """
            |
            |    Gmd · a Git client where you choose which branches are shown
            |    ~/Downloads is not a git repository
            |
            |  ▸ o  Open a folder ...            browse to a repository on this computer
            |    c  Clone a repository ...       copy one from GitHub or another server
            |    i  Init a repository here ...   start a new one in ~/Downloads
            |    ?  Help                         how to read the log, in the browser
            |    a  About                        the version, and where to report a problem
            |    q  Quit
            """,
            Picture(StartRows.Of(Newcomer, NoUpdate, 79, 21))
        );
    }

    // Too narrow for the explanations, they are cut, and with less room still left out
    [TestMethod]
    public void TestTheExplanationsAreCutThenLeftOutOnANarrowTerminal()
    {
        Assert.AreEqual(
            "|    o  Open a folder ...            browse to a reposi…",
            Picture(StartRows.Of(Newcomer, NoUpdate, 56, 21)).Split('\n')[4].Replace("▸", " ")
        );
        Assert.AreEqual("|  ▸ o  Open a folder ...", Picture(StartRows.Of(Newcomer, NoUpdate, 40, 21)).Split('\n')[4]);
    }

    [TestMethod]
    public void TestANewVersionIsOfferedUnderTheTitle()
    {
        var page = StartRows.Of(Returning, "1.5.1450.12", 79, 21);

        Assert.AreEqual(
            """
            |
            |         Gmd · a Git client where you choose which branches are shown
            |         ~/Downloads is not a git repository
            |
            |         u  Update to Gmd 1.5.1450.12 ...
            |
            |         Recent repositories
            """,
            string.Join("\n", Picture(page).Split('\n').Take(7))
        );
        Assert.AreEqual(StartAction.OpenRepo, page.Items[page.FirstItem].Action, "Enter still opens a repository");
    }

    // Only '-m' shows the page inside a repository, which is then not said to be no repository, and
    // init is not offered for it
    [TestMethod]
    public void TestStartedInsideARepositoryIsNotSaidToBeNone()
    {
        var inRepo = Returning with { Folder = "/home/me/code/gmd", ShownFolder = "~/code/gmd", IsFolderInRepo = true };

        var picture = Picture(StartRows.Of(inRepo, NoUpdate, 79, 21));

        Assert.IsFalse(picture.Contains("is not a git repository"), picture);
        StringAssert.Contains(picture, "i  Init a repository ...");
    }

    // Numbered across both lists, as far as there are keys for
    [TestMethod]
    public void TestRepositoriesFromTheTenthOnHaveNoNumber()
    {
        var many = Returning with
        {
            Recent = Enumerable.Range(1, 8).Select(i => Repo($"r{i}", $"code/r{i}")).ToList(),
            InFolder = Enumerable.Range(1, 3).Select(i => Repo($"f{i}", $"Downloads/f{i}", false)).ToList(),
        };

        var page = StartRows.Of(many, NoUpdate, 79, 40);

        var numbers = page.Items.Where(i => i.Action == StartAction.OpenRepo).Select(i => i.Key);
        CollectionAssert.AreEqual(new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "", "" }, numbers.ToArray());
        StringAssert.Contains(Picture(page), "|            f3        ~/Downloads/f3");
    }

    // A long name is cut at its end and a long path at its start, which is the part that tells it
    // apart, and with too little room for a path, it is left out
    [TestMethod]
    public void TestLongNamesAndPathsAreCut()
    {
        var long_ = Returning with
        {
            Recent = [Repo("a-repository-with-a-long-name", "code/somewhere/deep/down/a-repository-with-a-long-name")],
            InFolder = [],
        };

        Assert.AreEqual(
            "|  ▸ 1  a-repository-wi…  …own/a-repository-with-a-long-name",
            Picture(StartRows.Of(long_, NoUpdate, 60, 30)).Split('\n')[5]
        );
        Assert.AreEqual("|  ▸ 1  a-repository-wi…", Picture(StartRows.Of(long_, NoUpdate, 30, 30)).Split('\n')[5]);
    }

    // Least needed first: the spacing and the title, then the line saying why the page is shown,
    // then the repositories beyond the first three, so the actions stay in sight
    [TestMethod]
    public void TestAShortTerminalLeavesOutWhatIsLeastNeeded()
    {
        var many = Returning with
        {
            Recent = Enumerable.Range(1, 6).Select(i => Repo($"r{i}", $"code/r{i}")).ToList(),
            InFolder = Enumerable.Range(1, 5).Select(i => Repo($"f{i}", $"Downloads/f{i}", false)).ToList(),
        };

        static string Text(StartPage page) =>
            string.Join("\n", Enumerable.Range(0, page.Count).Select(i => page.Row(i, false).ToString().Trim()));

        var all = StartRows.Of(many, NoUpdate, 79, 40);
        Assert.AreEqual(25, all.Count);

        var dense = StartRows.Of(many, NoUpdate, 79, 24);
        Assert.AreEqual(20, dense.Count);
        Assert.IsFalse(Text(dense).Contains("Gmd ·"));
        StringAssert.StartsWith(Text(dense), "~/Downloads is not a git repository\nRecent repositories");

        var noWhy = StartRows.Of(many, NoUpdate, 79, 19);
        StringAssert.StartsWith(Text(noWhy), "Recent repositories");

        var fewer = StartRows.Of(many, NoUpdate, 79, 18);
        StringAssert.Contains(Text(fewer), "f3        ~/Downloads/f3\n… and 2 more\no  Open a folder ...");
        Assert.AreEqual(6, fewer.Items.Count(i => i.Repo?.IsRecent == true));

        var fewest = StartRows.Of(many, NoUpdate, 79, 10);
        StringAssert.Contains(Text(fewest), "r3        ~/code/r3\n… and 3 more\nRepositories in ~/Downloads");
        StringAssert.EndsWith(Text(fewest), "q  Quit");
    }

    [TestMethod]
    public void TestColorsOfTheTitleARepositoryAndAnAction()
    {
        var page = StartRows.Of(Returning, NoUpdate, 79, 21);

        Assert.AreEqual(
            "         mmm D D DDD DDDDDD DDDDD DDD DDDDDD DDDDD DDDDDDDD DDD DDDDD",
            TextColors.Of(page.Row(1, false))
        );
        Assert.AreEqual("         mmmmmm mmmmmmmmmmmm", TextColors.Of(page.Row(4, false)));
        Assert.AreEqual("         C  WWWWWWWW  DDDDDDDDDDDDDDD", TextColors.Of(page.Row(6, false)));
        Assert.AreEqual("         C  WWWW", TextColors.Of(page.Row(15, false)));
    }

    // The selected item is marked and has a bar behind it, as long for every item, from the marker to
    // past the longest of them
    [TestMethod]
    public void TestTheSelectedItemIsMarkedAndHighlighted()
    {
        var page = StartRows.Of(Returning, NoUpdate, 79, 21);
        var help = page.IndexOf(i => i.Action == StartAction.Help);
        var row = page.Row(page.RowOf(help), true);

        Assert.AreEqual("       ▸ ?  Help", row.ToString().TrimEnd());
        var highlighted = row.Fragments.Where(f => f.Color.Background == Color.Dark.Foreground).Sum(f => f.Text.Length);
        Assert.AreEqual("▸ i  Init a repository in ~/Downloads ... ".Length, highlighted);
    }

    // Up and Down step between items and stop at the ends, a click is on an item's row or nowhere
    [TestMethod]
    public void TestStepAndHitTest()
    {
        var page = StartRows.Of(Returning, NoUpdate, 79, 21);
        var first = page.FirstItem;

        Assert.AreEqual(0, first);
        Assert.AreEqual(0, page.Step(first, -1));
        Assert.AreEqual(1, page.Step(first, 1));
        Assert.AreEqual(page.Items.Count - 1, page.Step(first, 100));
        Assert.AreEqual(5, page.RowOf(first));
        Assert.AreEqual(first, page.ItemAt(5));
        Assert.AreEqual(-1, page.ItemAt(4), "The header");
        Assert.AreEqual(-1, page.ItemAt(99));
        var newcomer = StartRows.Of(Newcomer, NoUpdate, 79, 21);
        Assert.AreEqual(
            StartAction.OpenFolder,
            newcomer.Items[newcomer.FirstItem].Action,
            "With no repository, the first action"
        );
    }

    // The selected item is found again on the page laid out anew, by what it is rather than where
    [TestMethod]
    public void TestAnItemIsTheSameAfterTheListChanged()
    {
        var page = StartRows.Of(Returning, NoUpdate, 79, 21);
        var dotfiles = page.Items[page.IndexOf(i => i.Repo?.Name == "dotfiles")];

        var fewer = StartRows.Of(Returning with { Recent = Returning.Recent.Skip(1).ToList() }, NoUpdate, 79, 21);
        var found = fewer.Items[fewer.IndexOf(dotfiles.IsSame)];

        Assert.AreEqual("3", dotfiles.Key);
        Assert.AreEqual("2", found.Key);
    }
}
