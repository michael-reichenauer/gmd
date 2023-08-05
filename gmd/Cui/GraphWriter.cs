using gmd.Cui.Common;

namespace gmd.Cui;

interface IGraphWriter
{
    Text ToText(Graph graph, int index, int maxWidth, string highlightPrimaryBranchName, bool isHoverIndex);
}

class GraphWriter : IGraphWriter
{
    public Text ToText(Graph graph, int index, int maxWidth, string highlightBranchName, bool isHoverIndex)
    {
        var text = new TextBuilder();
        var row = graph.GetRow(index);
        int rowLength = Math.Min(graph.RowLength, (maxWidth + 1) / 2);  // +1 to ensure /2 get correct column length
        for (int i = 0; i < rowLength; i++)
        {
            // Colors
            var column = row[i];
            var branchColor = GetBranchColor(column, highlightBranchName, isHoverIndex);
            var connectColor = column.ConnectColor;
            var passColor = column.PassColor;

            // Draw connect runes (left of the branch)
            if (column.ConnectSign == Sign.Pass &&
                passColor != Color.Black &&
                passColor != Color.White)
            {
                connectColor = passColor;
            }
            else if (column.ConnectSign.HasFlag(Sign.Pass))
            {
                connectColor = Color.White;
            }

            // First column does not have a left connect rune, so skip it
            if (i > 0) text.Color(connectColor, ConnectRune(column.ConnectSign));

            if (graph.HasMore && i == rowLength - 1)
            {   // Last column is the More, which only has connection (no branch rune)
                continue;
            }
            // Draw the branch rune
            if (column.BranchSign == Sign.Pass &&
                passColor != Color.Black &&
                passColor != Color.White)
            {
                branchColor = passColor;
            }
            else if (column.BranchSign == Sign.Pass)
            {
                branchColor = Color.White;
            }
            text.Color(branchColor, BranchRune(column.BranchSign));
        }

        return text;
    }

    Color GetBranchColor(GraphColumn column, string highlightBranchName, bool isCurrentIndex)
    {
        var isHighlightBranch = highlightBranchName != "" && column.Branch?.B.PrimaryName == highlightBranchName;

        if (isHighlightBranch && isCurrentIndex)
        {
            return column.BranchColor with { Background = Color.White };  // Current branch and current index
        }
        if (isHighlightBranch)
        {
            return column.BranchColor with { Background = Color.Dark }; // Current branch
        }

        return column.BranchColor;
    }


    string BranchRune(Sign bm)
    {
        // commit of a branch with only one commit (tip==bottom)
        if (bm.HasFlag(Sign.Tip) && bm.HasFlag(Sign.Bottom)
            && bm.HasFlag(Sign.ActiveTip) && hasLeft(bm)) return "┺";

        if (bm.HasFlag(Sign.Tip) && bm.HasFlag(Sign.Bottom) && hasLeft(bm)) return "╼";
        if (bm.HasFlag(Sign.Bottom) && bm.HasFlag(Sign.ActiveTip)) return "┗";

        // commit is tip
        if (bm.HasFlag(Sign.Tip) && bm.HasFlag(Sign.ActiveTip) && hasLeft(bm)) return "╊";
        if (bm.HasFlag(Sign.Tip) && bm.HasFlag(Sign.ActiveTip)) return "┣";
        if (bm.HasFlag(Sign.Tip) && hasLeft(bm)) return "┲";
        if (bm.HasFlag(Sign.Tip)) return "┏";

        // commit is bottom

        if (bm.HasFlag(Sign.Bottom) && hasRight(bm)) return "┗";
        if (bm.HasFlag(Sign.Bottom) && hasLeft(bm)) return "┺";
        if (bm.HasFlag(Sign.Bottom)) return "┗";

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
            case Sign.MergeFromLeft | Sign.BranchToLeft | Sign.Pass:
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

