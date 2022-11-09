using Terminal.Gui;
using Color = Terminal.Gui.Attribute;

namespace gmd.Cui;





record Fragment(string Text, Color Color);


class Content
{
    List<IReadOnlyList<Fragment>> rows = new List<IReadOnlyList<Fragment>>();
    List<Fragment> currentRow = new List<Fragment>();

    internal int RowCount => rows.Count;

    public void Clear() => rows = new List<IReadOnlyList<Fragment>>();

    internal void Red(string text) => Add(text, Colors.Red);
    internal void Blue(string text) => Add(text, Colors.Blue);
    internal void White(string text) => Add(text, Colors.White);
    internal void Magenta(string text) => Add(text, Colors.Magenta);
    internal void BrightBlue(string text) => Add(text, Colors.BrightBlue);
    internal void BrightCyan(string text) => Add(text, Colors.BrightCyan);
    internal void BrightGreen(string text) => Add(text, Colors.BrightGreen);
    internal void BrightMagenta(string text) => Add(text, Colors.BrightMagenta);
    internal void BrightRed(string text) => Add(text, Colors.BrightRed);
    internal void BrightYellow(string text) => Add(text, Colors.BrightYellow);
    internal void Cyan(string text) => Add(text, Colors.Cyan);
    internal void DarkGray(string text) => Add(text, Colors.DarkGray);
    internal void Gray(string text) => Add(text, Colors.Gray);
    internal void Green(string text) => Add(text, Colors.Green);
    internal void Yellow(string text) => Add(text, Colors.Yellow);
    internal void Black(string text) => Add(text, Colors.Black);

    internal void Add(string text, Color color) =>
          currentRow.Add(new Fragment(text, color));


    internal void EoL()
    {
        rows.Add(currentRow);
        currentRow = new List<Fragment>();
    }

    internal void Draw(View view, Rect viewRect, Rect contentRect)
    {
        int firstRow = Math.Min(contentRect.Y, rows.Count);
        int rowCount = Math.Min(viewRect.Height, Math.Min(contentRect.Height, rows.Count - firstRow));

        int rowWidth = Math.Min(contentRect.Width, viewRect.Width);
        int rowX = contentRect.X;

        if (rowCount == 0 || viewRect.Width == 0)
        {
            return;
        }

        DrawRows(view, viewRect.X, viewRect.Y, firstRow, rowCount, rowX, rowWidth);
    }

    internal void DrawRows(View view, int x, int y, int firstRow, int rowCount, int rowX, int rowWidth)
    {
        rows.Skip(firstRow).Take(rowCount).ForEach(row =>
        {
            view.Move(x, y);
            DrawRow(row, rowX, rowWidth);
            y++;
        });
    }

    internal void DrawRow(IReadOnlyList<Fragment> row, int rowX, int rowWidth)
    {
        int x = 0;
        foreach (var fragment in row)
        {
            if (x >= rowWidth)
            {
                // Reached beyond last text to show
                return;
            }

            string text = fragment.Text;
            int end = x + text.Length;
            if (end < rowX)
            {
                // Text left of rowX 
                x += text.Length;
                continue;
            }

            if (x < rowX)
            {
                text = text.Substring(rowX - x);
            }

            x += (rowX - x);

            if (x + text.Length >= (rowX + rowWidth))
            {
                text = text.Substring(0, ((rowX + rowWidth) - x));
            }

            if (text == "")
            {
                continue;
            }

            View.Driver.SetAttribute(fragment.Color);
            View.Driver.AddStr(text);
            x += text.Length;
        }
    }
}
