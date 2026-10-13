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

    // Centered on a wide terminal, the most recent repository selected, so Enter opens it, and the
    // actions under a line
    [TestMethod]
    public void TestAReturningUserIsShownTheRepositoriesFirst()
    {
        Assert.AreEqual(
            """
            |
            |                                           Recent repositories
            |                                         ▸ 1  gmd       ~/code/gmd
            |                                           2  web-shop  ~/code/web-shop
            |                                           3  dotfiles  ~/dotfiles
            |
            |                                           Repositories in ~/Downloads
            |                                           4  scratch   ~/Downloads/scratch
            |
            |                                           ────────────────────────────────
            |                                           o  Open a folder ...
            |                                           c  Clone a repository ...
            |                                           i  Init a repository ...
            |                                           ?  Help
            |                                           a  About
            |                                           q  Quit
            """,
            Picture(StartRows.Of(Returning, NoUpdate, 119, 37))
        );
    }

    // No repository to list yet: the actions are what there is, each said what it does, with no line
    // above them, there being nothing to set them apart from
    [TestMethod]
    public void TestANewcomerIsToldWhatEachActionDoes()
    {
        Assert.AreEqual(
            """
            |
            |  ▸ o  Open a folder ...        browse to a repository on this computer
            |    c  Clone a repository ...   copy one from GitHub or another server
            |    i  Init a repository ...    start a new, empty one
            |    ?  Help                     how to read the log, in the browser
            |    a  About                    the version, and where to report a problem
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
            "|  ▸ o  Open a folder ...        browse to a reposi…",
            Picture(StartRows.Of(Newcomer, NoUpdate, 52, 21)).Split('\n')[1]
        );
        Assert.AreEqual(
            "|     ▸ o  Open a folder ...",
            Picture(StartRows.Of(Newcomer, NoUpdate, 40, 21)).Split('\n')[1]
        );
    }

    [TestMethod]
    public void TestANewVersionIsOfferedFirst()
    {
        var page = StartRows.Of(Returning, "1.5.1450.12", 79, 21);

        Assert.AreEqual(
            """
            |
            |                       u  Update to Gmd 1.5.1450.12 ...
            |
            |                       Recent repositories
            |                     ▸ 1  gmd       ~/code/gmd
            """,
            string.Join("\n", Picture(page).Split('\n').Take(5))
        );
        Assert.AreEqual(StartAction.OpenRepo, page.Items[page.FirstItem].Action, "Enter still opens a repository");
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
        StringAssert.Contains(Picture(page), "        f3        ~/Downloads/f3\n");
        StringAssert.Contains(Picture(page), "     9  f1        ~/Downloads/f1\n");
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
            Picture(StartRows.Of(long_, NoUpdate, 60, 30)).Split('\n')[2]
        );
        Assert.AreEqual("|  ▸ 1  a-repository-wi…", Picture(StartRows.Of(long_, NoUpdate, 30, 30)).Split('\n')[2]);
    }

    // Least needed first: the spacing, then the repositories beyond the first three, so the actions
    // stay in sight
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

        var all = StartRows.Of(many, NoUpdate, 79, 23);
        Assert.AreEqual(23, all.Count);

        var dense = StartRows.Of(many, NoUpdate, 79, 22);
        Assert.AreEqual(20, dense.Count);
        StringAssert.StartsWith(Text(dense), "Recent repositories\n1  r1");
        StringAssert.Contains(Text(dense), "f5        ~/Downloads/f5\n─");

        var fewer = StartRows.Of(many, NoUpdate, 79, 19);
        StringAssert.Contains(Text(fewer), "f3        ~/Downloads/f3\n… and 2 more\n─");
        Assert.AreEqual(6, fewer.Items.Count(i => i.Repo?.IsRecent == true));

        var fewest = StartRows.Of(many, NoUpdate, 79, 10);
        StringAssert.Contains(Text(fewest), "r3        ~/code/r3\n… and 3 more\nRepositories in ~/Downloads");
        StringAssert.EndsWith(Text(fewest), "q  Quit");
    }

    [TestMethod]
    public void TestColorsOfAHeaderARepositoryTheLineAndAnAction()
    {
        var page = StartRows.Of(Returning, NoUpdate, 79, 21);

        Assert.AreEqual("                       mmmmmm mmmmmmmmmmmm", TextColors.Of(page.Row(1, false)));
        Assert.AreEqual("                       C  WWWWWWWW  DDDDDDDDDDDDDDD", TextColors.Of(page.Row(3, false)));
        Assert.AreEqual("                       " + new string('m', 32), TextColors.Of(page.Row(9, false)));
        Assert.AreEqual("                       C  WWWW", TextColors.Of(page.Row(15, false)));
    }

    // The selected item is marked and has a bar behind it, as long for every item, from the marker to
    // past the longest of them
    [TestMethod]
    public void TestTheSelectedItemIsMarkedAndHighlighted()
    {
        var page = StartRows.Of(Returning, NoUpdate, 79, 21);
        var help = page.IndexOf(i => i.Action == StartAction.Help);
        var row = page.Row(page.RowOf(help), true);

        Assert.AreEqual("                     ▸ ?  Help", row.ToString().TrimEnd());
        var highlighted = row.Fragments.Where(f => f.Color.Background == Color.Dark.Foreground).Sum(f => f.Text.Length);
        Assert.AreEqual("▸ 4  scratch   ~/Downloads/scratch ".Length, highlighted);
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
        Assert.AreEqual(2, page.RowOf(first));
        Assert.AreEqual(first, page.ItemAt(2));
        Assert.AreEqual(-1, page.ItemAt(1), "The header");
        Assert.AreEqual(-1, page.ItemAt(9), "The line");
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
