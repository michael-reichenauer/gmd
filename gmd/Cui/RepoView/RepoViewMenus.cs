using gmd.Common;
using gmd.Cui.Common;

namespace gmd.Cui.RepoView;


interface IRepoViewMenus
{
    void ShowRepoMenu(int x, int y);
    void ShowCommitMenu(int x, int y, int index);
    void ShowBranchMenu(int x, int y, string branchName);
    void ShowCommitBranchesMenu(int x, int y);
    void ShowOpenBranchMenu(int x = Menu.Center, int y = 0);
    void ShowStashMenu(int x = Menu.Center, int y = 0);
    void ShowMergeFromMenu(int x = Menu.Center, int y = 0);
    void ShowOpenRepoMenu(int x = Menu.Center, int y = 0);
    void ShowDiffBranchToMenu(int x, int y, string branchName);
}


class RepoViewMenus : IRepoViewMenus
{
    readonly IRepoMenu repoMenu;
    readonly ICommitMenu commitMenu;
    readonly IBranchMenu branchMenu;

    internal RepoViewMenus(IViewRepo repo, Config config, IConfigDlg configDlg)
    {
        repoMenu = new RepoMenu(repo, repo.Cmds, config, configDlg);
        branchMenu = new BranchMenu(repoMenu, repo);
        commitMenu = new CommitMenu(repoMenu, branchMenu, repo);
    }

    public void ShowRepoMenu(int x, int y) => repoMenu.Show(x, y);

    public void ShowCommitMenu(int x, int y, int index) => commitMenu.Show(x, y, index);

    public void ShowBranchMenu(int x, int y, string branchName) => branchMenu.Show(x, y, branchName);

    public void ShowCommitBranchesMenu(int x, int y) => branchMenu.ShowCommitBranchesMenu(x, y);

    public void ShowMergeFromMenu(int x = Menu.Center, int y = 0) => branchMenu.ShowMergeFromMenu(x, y);

    public void ShowOpenRepoMenu(int x = Menu.Center, int y = 0) => repoMenu.Show(x, y);

    public void ShowOpenBranchMenu(int x = Menu.Center, int y = 0) => branchMenu.ShowOpenBranchMenu(x, y);

    public void ShowStashMenu(int x = Menu.Center, int y = 0) => commitMenu.ShowStashMenu(x, y);

    public void ShowDiffBranchToMenu(int x, int y, string branchName) =>
        branchMenu.ShowDiffBranchToMenu(x, y, branchName);
}
