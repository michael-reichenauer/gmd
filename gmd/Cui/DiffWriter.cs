using Terminal.Gui;

namespace gmd.Cui;

interface IDiffWriter
{
    void WriteDiffPage(DiffRows diffRows, int contentWidth, int firstRow, int rowCount, int currentRow);
}

class DiffWriter : IDiffWriter
{
    private readonly ColorText text;


    public DiffWriter(View view, int startX)
    {
        this.text = new ColorText(view, startX);
    }

    public void WriteDiffPage(DiffRows diffRows, int contentWidth, int firstRow, int rowCount, int currentRow)
    {
        text.Reset();


        diffRows.Rows.Skip(firstRow).Take(rowCount)
            .ForEach(row =>
            {
                text.BrightBlue(Text(row.Left, 30));
                text.EoL();
            });

        text.BrightRed(Text("Some diff", 30));
        text.BrightGreen(Text("Som other diff", 30));
        text.EoL();
    }

    string Text(string text, int width)
    {
        if (text.Length <= width)
        {
            return text + new string(' ', width - text.Length);
        }

        return text.Substring(0, width);
    }
}
