using gmd.Cui.Common;

namespace gmd.Cui;

interface IGraphWriter
{
    Text ToText(GraphRow row);
}

class GraphWriter : IGraphWriter
{
    public Text ToText(GraphRow row)
    {
        Text text = Text.New;
        for (int i = 0; i < row.Width; i++)
        {
            // Colors
            var branchColor = row[i].BranchColor;
            var connectColor = row[i].ConnectColor;
            var passColor = row[i].PassColor;

            // Draw connect runes (left of the branch)
            if (row[i].Connect == Sign.Pass &&
                passColor != TextColor.None &&
                passColor != TextColor.Ambiguous)
            {
                connectColor = passColor;
            }
            else if (row[i].Connect.HasFlag(Sign.Pass))
            {
                connectColor = TextColor.Ambiguous;
            }

            text.Color(connectColor, ConnectRune(row[i].Connect));

            // Draw the branch rune
            if (row[i].Branch == Sign.Pass &&
                passColor != TextColor.None &&
                passColor != TextColor.Ambiguous)
            {
                branchColor = passColor;
            }
            else if (row[i].Branch == Sign.Pass)
            {
                branchColor = TextColor.Ambiguous;
            }
            text.Color(branchColor, BranchRune(row[i].Branch));
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
        if (bm.HasFlag(Sign.Bottom) && hasLeft(bm)) return "┺";
        if (bm.HasFlag(Sign.Bottom)) return "┚";

        // commit is within branch
        if (bm.HasFlag(Sign.Commit) && hasLeft(bm)) return "╊";
        if (bm.HasFlag(Sign.Commit)) return "┣";

        // commit is not part of branch
        if (bm.HasFlag(Sign.BLine) && hasLeft(bm)) return "╂";
        if (bm.HasFlag(Sign.BLine)) return "┃";

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
            case Sign.MergeFromLeft | Sign.BranchToLeft:
                return "├";
            case Sign.MergeFromLeft | Sign.ConnectLine:
                return "├";
            case Sign.MergeFromLeft | Sign.ConnectLine | Sign.BranchToLeft:
                return "├";
            case Sign.BranchToLeft:
                return "╰";
            case Sign.BranchToLeft | Sign.ConnectLine:
                return "├";
            case Sign.ConnectLine | Sign.Pass:
                return "┼";
            case Sign.ConnectLine | Sign.Pass | Sign.MergeFromLeft:
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

}

