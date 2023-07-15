using gmd.Cui.Common;

namespace gmd.Cui;

interface IGraphWriter
{
    Text ToText(GraphRow row, int maxWidth);
}

class GraphWriter : IGraphWriter
{
    public Text ToText(GraphRow row, int maxWidth)
    {
        Text text = Text.New;
        int width = Math.Min(row.Width, maxWidth / 2);
        for (int i = 0; i < width; i++)
        {
            // Colors
            var branchColor = row[i].BranchColor;
            var connectColor = row[i].ConnectColor;
            var passColor = row[i].PassColor;

            // Draw connect runes (left of the branch)
            if (row[i].ConnectSign == Sign.Pass &&
                passColor != Color.Black &&
                passColor != Color.White)
            {
                connectColor = passColor;
            }
            else if (row[i].ConnectSign.HasFlag(Sign.Pass))
            {
                connectColor = Color.White;
            }

            text.Color(connectColor, ConnectRune(row[i].ConnectSign));

            // Draw the branch rune
            if (row[i].BranchSign == Sign.Pass &&
                passColor != Color.Black &&
                passColor != Color.White)
            {
                branchColor = passColor;
            }
            else if (row[i].BranchSign == Sign.Pass)
            {
                branchColor = Color.White;
            }
            text.Color(branchColor, BranchRune(row[i].BranchSign));
        }

        return text;
    }


    string BranchRune(Sign bm)
    {
        // commit of a branch with only one commit (tip==bottom)
        if (bm.HasFlag(Sign.Tip) && bm.HasFlag(Sign.Bottom)
            && bm.HasFlag(Sign.ActiveTip) && hasLeft(bm)) return "┺";

        if (bm.HasFlag(Sign.Tip) && bm.HasFlag(Sign.Bottom) && hasLeft(bm)) return "╼";

        // commit is tip
        if (bm.HasFlag(Sign.Tip) && bm.HasFlag(Sign.ActiveTip) && hasLeft(bm)) return "╊";
        if (bm.HasFlag(Sign.Tip) && bm.HasFlag(Sign.ActiveTip)) return "┣";
        if (bm.HasFlag(Sign.Tip) && hasLeft(bm)) return "┲";
        if (bm.HasFlag(Sign.Tip)) return "┏";

        // commit is bottom
        if (bm.HasFlag(Sign.Bottom) && hasRight(bm)) return "┗";
        if (bm.HasFlag(Sign.Bottom) && hasLeft(bm)) return "┺";
        if (bm.HasFlag(Sign.Bottom)) return "┚";

        // commit is within branch
        if (bm.HasFlag(Sign.Commit) && hasLeft(bm)) return "╊";
        if (bm.HasFlag(Sign.Commit)) return "┣";

        // commit is not part of branch
        if (bm.HasFlag(Sign.BLine) && hasLeft(bm)) return "╂";
        if (bm.HasFlag(Sign.BLine)) return "┃";
        if (bm.HasFlag(Sign.Resolve)) return "Φ";

        if (bm == Sign.Pass) return "─";
        if (bm == Sign.Blank) return " ";

        Log.Warn($"Unknown branch rune {bm}");
        return "*";
    }



    string ConnectRune(Sign bm)
    {
        switch (bm)
        {
            case Sign.MergeFromRight:
                return "╮";
            case Sign.MergeFromRight | Sign.Pass:
                return "┬";
            case Sign.MergeFromRight | Sign.ConnectLine:
                return "┤";
            case Sign.MergeFromRight | Sign.BranchToRight:
                return "┤";
            case Sign.MergeFromRight | Sign.BranchToRight | Sign.Pass:
                return "┴";
            case Sign.MergeFromRight | Sign.BranchToRight | Sign.ConnectLine:
                return "┤";
            case Sign.BranchToRight:
                return "╯";
            case Sign.BranchToRight | Sign.ConnectLine | Sign.Pass:
                return "┼";
            case Sign.BranchToRight | Sign.ConnectLine | Sign.Pass | Sign.MergeFromRight:
                return "┼";

            case Sign.BranchToRight | Sign.Pass:
                return "┴";
            case Sign.BranchToRight | Sign.ConnectLine:
                return "┤";
            case Sign.MergeFromLeft:
                return "╭";
            case Sign.MergeFromLeft | Sign.Pass:
                return "╭";
            case Sign.MergeFromLeft | Sign.BranchToLeft:
                return "├";
            case Sign.MergeFromLeft | Sign.ConnectLine:
                return "├";
            case Sign.MergeFromLeft | Sign.ConnectLine | Sign.BranchToLeft:
                return "├";
            case Sign.MergeFromLeft | Sign.MergeFromRight | Sign.BranchToLeft:
                return "├";
            case Sign.MergeFromLeft | Sign.MergeFromRight | Sign.BranchToRight:
                return "├";
            case Sign.MergeFromLeft | Sign.MergeFromRight:
                return "├";
            case Sign.MergeFromLeft | Sign.MergeFromRight | Sign.ConnectLine:
                return "├";
            case Sign.MergeFromLeft | Sign.BranchToRight:
                return "├";
            case Sign.BranchToLeft:
                return "╰";
            case Sign.BranchToLeft | Sign.Pass:
                return "╰";
            case Sign.BranchToLeft | Sign.ConnectLine:
                return "├";
            case Sign.ConnectLine | Sign.Pass:
                return "┼";
            case Sign.ConnectLine | Sign.Pass | Sign.MergeFromLeft:
                return "┼";
            case Sign.ConnectLine | Sign.Pass | Sign.MergeFromRight:
                return "┼";
            case Sign.ConnectLine:
                return "│";
            case Sign.Pass:
                return "─";
            case Sign.Blank:
                return " ";
            default:
                Log.Warn($"Unknown Connect rune {bm}");
                return "*";
        }
    }


    bool hasLeft(Sign bm)
    {
        return bm.HasFlag(Sign.BranchToLeft) ||
            bm.HasFlag(Sign.MergeFromLeft) ||
            bm.HasFlag(Sign.Pass);
    }

    bool hasRight(Sign bm)
    {
        return bm.HasFlag(Sign.MergeFromRight);
    }
}

