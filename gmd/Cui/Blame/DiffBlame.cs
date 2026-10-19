using gmd.Cui.Common;
using gmd.Cui.Diff;
using gmd.Server;

namespace gmd.Cui.Blame;

// The blame the diff view opens with b (see IDiffBlame): a blame view of its own, on top of the diff,
// with the cursor on the line, which Esc closes back to the diff. A new blame view each time, since a
// view keeps what it shows until it closes, and a diff opened from a blame can open a blame again.
class DiffBlame : IDiffBlame
{
    readonly IServer server;
    readonly IProgress progress;
    readonly Func<IBlameView> newBlameView; // Not made with the diff view, which a blame view makes

    public DiffBlame(IServer server, IProgress progress, Func<IBlameView> newBlameView)
    {
        this.server = server;
        this.progress = progress;
        this.newBlameView = newBlameView;
    }

    public async Task BlameAsync(Repo repo, BlameAt at)
    {
        Server.Blame blame;
        using (progress.Show())
        {
            var blameResult = await server.GetBlameAsync(at.Path, at.Reference, repo.Path);
            if (blameResult is not Server.Blame loaded)
            {
                UI.ErrorMessage($"Failed to blame {at.Path}\n{blameResult.Error.AllMessages()}");
                return;
            }
            blame = loaded;
        }

        newBlameView().Show(blame, repo, at.LineNbr);
    }
}
