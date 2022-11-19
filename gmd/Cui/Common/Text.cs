using Terminal.Gui;
using Color = Terminal.Gui.Attribute;


namespace gmd.Cui.Common;


class Text
{
    record Fragment(string Text, Color Color);
    readonly List<Fragment> fragments = new List<Fragment>();
    internal int Length { get; private set; } = 0;

    internal static Text None => new Text();
    internal static Text New => new Text();

    internal Text Red(string text) => Color(TextColor.Red, text);
    internal Text Blue(string text) => Color(TextColor.Blue, text);
    internal Text White(string text) => Color(TextColor.White, text);
    internal Text Magenta(string text) => Color(TextColor.Magenta, text);
    internal Text BrightBlue(string text) => Color(TextColor.BrightBlue, text);
    internal Text BrightCyan(string text) => Color(TextColor.BrightCyan, text);
    internal Text BrightGreen(string text) => Color(TextColor.BrightGreen, text);
    internal Text BrightMagenta(string text) => Color(TextColor.BrightMagenta, text);
    internal Text BrightRed(string text) => Color(TextColor.BrightRed, text);
    internal Text BrightYellow(string text) => Color(TextColor.BrightYellow, text);
    internal Text Cyan(string text) => Color(TextColor.Cyan, text);
    internal Text Dark(string text) => Color(TextColor.Dark, text);
    internal Text Green(string text) => Color(TextColor.Green, text);
    internal Text Yellow(string text) => Color(TextColor.Yellow, text);
    internal Text Black(string text) => Color(TextColor.Black, text);


    internal Text Color(Color color, string text)
    {
        fragments.Add(new Fragment(text, color));
        Length += text.Length;
        return this;
    }

    internal void Draw(View view, int x, int y, int startIndex = 0, int length = int.MaxValue)
    {
        view.Move(x, y);
        Draw(startIndex, length);
    }

    internal void DrawAsLine(View view, int x, int y, int width)
    {
        if (!fragments.Any() || fragments[0].Text == "")
        {
            return;
        }

        view.Move(x, y);
        View.Driver.SetAttribute(fragments[0].Color);
        View.Driver.AddStr(new string(fragments[0].Text[0], width));
    }

    internal void Draw(int startIndex = 0, int length = int.MaxValue)
    {
        int x = 0;
        foreach (var fragment in fragments)
        {
            if (x >= length)
            {
                // Reached beyond last text to show
                return;
            }

            string text = fragment.Text;
            int end = x + text.Length;
            if (end < startIndex)
            {
                // Text left of rowX 
                x += text.Length;
                continue;
            }

            if (x < startIndex)
            {
                text = text.Substring(startIndex - x);
                x += (startIndex - x);
            }

            if (x + text.Length >= (startIndex + length))
            {
                text = text.Substring(0, ((startIndex + length) - x));
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




// class Content
// {
//     List<Text> rows = new List<Text>();
//     List<Fragment> currentRow = new List<Fragment>();

//     internal int RowCount => rows.Count;

//     public void Clear() => rows = new List<Text>();

//     internal void Red(string text) => Add(text, Colors.Red);
//     internal void Blue(string text) => Add(text, Colors.Blue);
//     internal void White(string text) => Add(text, Colors.White);
//     internal void Magenta(string text) => Add(text, Colors.Magenta);
//     internal void BrightBlue(string text) => Add(text, Colors.BrightBlue);
//     internal void BrightCyan(string text) => Add(text, Colors.BrightCyan);
//     internal void BrightGreen(string text) => Add(text, Colors.BrightGreen);
//     internal void BrightMagenta(string text) => Add(text, Colors.BrightMagenta);
//     internal void BrightRed(string text) => Add(text, Colors.BrightRed);
//     internal void BrightYellow(string text) => Add(text, Colors.BrightYellow);
//     internal void Cyan(string text) => Add(text, Colors.Cyan);
//     internal void DarkGray(string text) => Add(text, Colors.DarkGray);
//     internal void Gray(string text) => Add(text, Colors.Gray);
//     internal void Green(string text) => Add(text, Colors.Green);
//     internal void Yellow(string text) => Add(text, Colors.Yellow);
//     internal void Black(string text) => Add(text, Colors.Black);

//     internal void Add(string text, Color color) =>
//           currentRow.Add(new Fragment(text, color));


//     internal void EoL()
//     {
//         rows.Add(new Row(currentRow));
//         currentRow = new List<Fragment>();
//     }

//     internal void Draw(View view, Rect viewRect, Rect contentRect)
//     {
//         int firstRow = Math.Min(contentRect.Y, rows.Count);
//         int rowCount = Math.Min(viewRect.Height, Math.Min(contentRect.Height, rows.Count - firstRow));

//         int rowWidth = Math.Min(contentRect.Width, viewRect.Width);
//         int rowX = contentRect.X;

//         if (rowCount == 0 || viewRect.Width == 0)
//         {
//             return;
//         }

//         DrawRows(view, viewRect.X, viewRect.Y, firstRow, rowCount, rowX, rowWidth);
//     }

//     internal void DrawRow(View view, int x, int y, int rowIndex, int rowX = 0, int rowWidth = int.MaxValue)
//     {
//         view.Move(x, y);
//         rows[rowIndex].Draw(rowX, rowWidth);
//     }


//     void DrawRows(View view, int x, int y, int firstRow, int rowCount, int rowX, int rowWidth)
//     {
//         rows.Skip(firstRow).Take(rowCount).ForEach(row =>
//         {
//             view.Move(x, y);
//             row.Draw(rowX, rowWidth);
//             y++;
//         });
//     }

// }
