using gmd.Cui.App;
using gmd.Cui.Common;
using gmd.Cui.RepoView;
using gmd.Server;
using Terminal.Gui;

namespace gmdTest.Fixtures;

// Double for IRepoView, the log view, as the command classes and the menus reach it through their
// CommandContext: the shown repo, the cursor's row, the selection and what outlives a snapshot. The
// rest throws, as FakeGit does, so a test that starts depending on more of the view fails loudly
// rather than passing on nothing.
class FakeRepoView : IRepoView
{
    readonly FakeViewRepo viewRepo;

    public FakeRepoView(FakeViewRepo viewRepo)
    {
        this.viewRepo = viewRepo;
    }

    // A context for command classes and menus that act on the fake view, with no progress or status
    // line, which what the tests call never reaches
    public static CommandContext Context(FakeViewRepo viewRepo, IServer? server = null) =>
        new(new FakeRepoView(viewRepo), server!, null!, null!);

    public IViewRepo ViewRepo => viewRepo;
    public int CurrentIndex => viewRepo.CurrentIndex;
    public Selection Selection { get; set; } = new(0, 0, 0, 0, 0);
    public ShownHistory ShownHistory => viewRepo.ShownHistory;
    public SearchMatches SearchMatches => viewRepo.SearchMatches;
    public IReadOnlyList<HiddenBranchNews> HiddenNews => viewRepo.HiddenNews;

    public void ClearSelection() => Selection = new(0, 0, 0, 0, 0);

    public View View => throw Unsupported();
    public View DetailsView => throw Unsupported();
    public View ApplicationBarView => throw Unsupported();
    public View KeyHintView => throw Unsupported();
    public IApplicationBar ApplicationBar => throw Unsupported();

    public void MarkHiddenNewsSeen(IReadOnlyList<HiddenBranchNews> looked) => throw Unsupported();

    public Task<Result> ShowInitialRepoAsync(string path) => throw Unsupported();

    public Task<Result> ShowRepoAsync(string path) => throw Unsupported();

    public void UpdateRepoTo(Repo repo, string branchName = "") => throw Unsupported();

    public void UpdateRepoToAtCommit(Repo repo, string commitId) => throw Unsupported();

    public void Refresh(string addName = "", string commitId = "") => throw Unsupported();

    public Task RefreshAsync(string addName = "", string commitId = "") => throw Unsupported();

    public void RefreshAndCommit(string addName = "", string commitId = "", IReadOnlyList<Commit>? commits = null) =>
        throw Unsupported();

    public void RefreshAndFetch(string addName = "", string commitId = "") => throw Unsupported();

    public void ToggleDetails() => throw Unsupported();

    public void ShowFilter() => throw Unsupported();

    public void UpdateLayout() => throw Unsupported();

    public void SetStartPage(View page, Func<IReadOnlyList<KeyHint>> hints) => throw Unsupported();

    static NotSupportedException Unsupported() => new("FakeRepoView has no real view");
}
