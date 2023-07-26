
using gmd.Cui.Common;
using Terminal.Gui;
using Clipboard = gmd.Utils.Clipboard;

interface IUnicodeSetsDlg
{
    void Show();
}

class UnicodeSetsDlg : IUnicodeSetsDlg
{
    record Set(string Name, int start, int end);
    IReadOnlyList<Text> content = null!;
    ContentView contentView = null!;

    public void Show()
    {
        var dlg = new UIDialog("Unicode Character Sets", 110, 30);

        content = GetContent();
        contentView = dlg.AddContentView(0, 0, Dim.Fill(), Dim.Fill() - 2, content);
        contentView.IsShowCursor = false;
        contentView.IsScrollMode = true;
        contentView.RegisterKeyHandler(Key.Esc, () => dlg.Close());

        dlg.AddDlgClose();
        contentView.RegisterKeyHandler(Key.C | Key.CtrlMask, OnCopy);
        dlg.Show(contentView);
    }

    void OnCopy()
    {
        var text = contentView.CopySelectedText();
        Log.Info($"Copy: '{text}'");
        Clipboard.Set(text);
    }

    // Returns a list of texts of all the characters in each set in batches 
    // Not all sets are included, only those most likely to be useful
    IReadOnlyList<Text> GetContent()
    {
        var sets = new List<Set>(){
            new Set("Basic Latin", 0x0020, 0x007E),
            new Set("Box Drawing", 0x2500, 0x257F),
            new Set("Block Elements", 0x2580, 0x259F),
            new Set("Geometric Shapes", 0x25A0, 0x25FF),
            new Set("Miscellaneous Symbols", 0x2600, 0x26FF),
            new Set("Symbols for Legacy Computing", 0x1FB00, 0x1FBFF),
            new Set("Dingbats", 0x2700, 0x27BF),
            new Set("Miscellaneous Mathematical Symbols-A", 0x27C0, 0x27EF),
            new Set("Supplemental Arrows-A", 0x27F0, 0x27FF),
            new Set("Supplemental Arrows-B", 0x2900, 0x297F),
            new Set("Miscellaneous Mathematical Symbols-B", 0x2980, 0x29FF),
            new Set("Supplemental Mathematical Operators", 0x2A00, 0x2AFF),
            new Set("Miscellaneous Symbols and Arrows", 0x2B00, 0x2BFF),

            new Set("CJK Compatibility Forms", 0xFE30, 0xFE4F),
            new Set("CJK Symbols and Punctuation", 0x3000, 0x303F),
            new Set("Alphabetic Presentation Forms", 0xFB00, 0xFB4F),
            new Set("Alphabetic Presentation Forms", 0xFB00, 0xFB4F),
            new Set("Alchemical Symbols", 0x1F700, 0x1F77F),

            new Set("Latin-1 Supplement", 0x00A0, 0x00FF),
            new Set("Latin Extended-A", 0x0100, 0x017F),
            new Set("Latin Extended-B", 0x0180, 0x024F),
            new Set("Latin Extended Additional", 0x1E00, 0x1EFF),
            new Set("Latin Extended-C", 0x2C60, 0x2C7F),
            new Set("Latin Extended-D", 0xA720, 0xA7FF),
            new Set("Latin Extended-E", 0xAB30, 0xAB6F),
            new Set("Latin Extended-F", 0x2DE0, 0x2DFF),
            new Set("Latin Extended-G", 0xA7F2, 0xA7FF),
            new Set("IPA Extensions", 0x0250, 0x02AF),
            new Set("Spacing Modifier Letters", 0x02B0, 0x02FF),

            new Set("Greek and Coptic", 0x0370, 0x03FF),
            new Set("Greek Extended", 0x1F00, 0x1FFF),
            new Set("Cyrillic", 0x0400, 0x04FF),
            new Set("Cyrillic Supplement", 0x0500, 0x052F),
            new Set("Cyrillic Extended-A", 0x2DE0, 0x2DFF),
            new Set("Cyrillic Extended-B", 0xA640, 0xA69F),

            new Set("Unified Canadian Aboriginal Syllabics Extended", 0x18B0, 0x18FF),
            new Set("Unified Canadian Aboriginal Syllabics", 0x1400, 0x167F),
            new Set("Ogham", 0x1680, 0x169F),
            new Set("Runic", 0x16A0, 0x16FF),
            new Set("Phonetic Extensions", 0x1D00, 0x1D7F),
            new Set("Unicode symbols", 0x2013, 0x204A),
            new Set("General Punctuation", 0x2000, 0x206F),
            new Set("Superscripts and Subscripts", 0x2070, 0x209F),
            new Set("Currency Symbols", 0x20A0, 0x20CF),
            new Set("Letterlike Symbols", 0x2100, 0x214F),
            new Set("Number Forms", 0x2150, 0x218F),
            new Set("Arrows", 0x2190, 0x21FF),
            new Set("Mathematical Operators", 0x2200, 0x22FF),
            new Set("Miscellaneous Technical", 0x2300, 0x23FF),
            new Set("Control Pictures", 0x2400, 0x243F),
            new Set("Optical Character Recognition", 0x2440, 0x245F),
            new Set("Enclosed Alphanumerics", 0x2460, 0x24FF),
            new Set("Braille Patterns", 0x2800, 0x28FF),
           

            // Strange characters
            // new Set("Armenian", 0x0530, 0x058F),
            // new Set("Hebrew", 0x0590, 0x05FF),
            // new Set("Arabic", 0x0600, 0x06FF),
            // new Set("Syriac", 0x0700, 0x074F),
            // new Set("Arabic Supplement", 0x0750, 0x077F),
            // new Set("Thaana", 0x0780, 0x07BF),
            // new Set("NKo", 0x07C0, 0x07FF),
            // new Set("Samaritan", 0x0800, 0x083F),
            // new Set("Mandaic", 0x0840, 0x085F),
            // new Set("Arabic Extended-A", 0x08A0, 0x08FF),
            // new Set("Devanagari", 0x0900, 0x097F),
            // new Set("Bengali", 0x0980, 0x09FF),
            // new Set("Gurmukhi", 0x0A00, 0x0A7F),
            // new Set("Gujarati", 0x0A80, 0x0AFF),
            // new Set("Oriya", 0x0B00, 0x0B7F),
            // new Set("Tamil", 0x0B80, 0x0BFF),
            // new Set("Telugu", 0x0C00, 0x0C7F),
            // new Set("Kannada", 0x0C80, 0x0CFF),
            // new Set("Malayalam", 0x0D00, 0x0D7F),
            // new Set("Sinhala", 0x0D80, 0x0DFF),
            // new Set("Thai", 0x0E00, 0x0E7F),
            // new Set("Lao", 0x0E80, 0x0EFF),
            // new Set("Tibetan", 0x0F00, 0x0FFF),
            // new Set("Myanmar", 0x1000, 0x109F),
            // new Set("Georgian", 0x10A0, 0x10FF),
            // new Set("Hangul Jamo", 0x1100, 0x11FF),
            // new Set("Ethiopic", 0x1200, 0x137F),
            // new Set("Ethiopic Supplement", 0x1380, 0x139F),
            // new Set("Cherokee", 0x13A0, 0x13FF),
            // new Set("Tagalog", 0x1700, 0x171F),
            // new Set("Hanunoo", 0x1720, 0x173F),
            // new Set("Buhid", 0x1740, 0x175F),
            // new Set("Tagbanwa", 0x1760, 0x177F),
            // new Set("Khmer", 0x1780, 0x17FF),
            // new Set("Mongolian", 0x1800, 0x18AF),
            // new Set("Limbu", 0x1900, 0x194F),
            // new Set("Tai Le", 0x1950, 0x197F),
            // new Set("New Tai Lue", 0x1980, 0x19DF),
            // new Set("Khmer Symbols", 0x19E0, 0x19FF),
            // new Set("Buginese", 0x1A00, 0x1A1F),
            // new Set("Tai Tham", 0x1A20, 0x1AAF),
            // new Set("Combining Diacritical Marks Extended", 0x1AB0, 0x1AFF),
            // new Set("Balinese", 0x1B00, 0x1B7F),
            // new Set("Sundanese", 0x1B80, 0x1BBF),
            // new Set("Batak", 0x1BC0, 0x1BFF),
            // new Set("Lepcha", 0x1C00, 0x1C4F),
            // new Set("Glagolitic", 0x2C00, 0x2C5F),
            // new Set("CJK Compatibility Ideographs", 0xF900, 0xFAFF),
            // new Set("CJK Compatibility Ideographs Supplement", 0x2F800, 0x2FA1F),
            // new Set("CJK Radicals Supplement", 0x2E80, 0x2EFF),
            // new Set("Kangxi Radicals", 0x2F00, 0x2FDF),
            // new Set("Ideographic Description Characters", 0x2FF0, 0x2FFF),
            // new Set("Hiragana", 0x3040, 0x309F),
            // new Set("Combining Diacritical Marks for Symbols", 0x20D0, 0x20FF),
        };

        // Create a list of texts of all the characters in each set in batches of 32 chars per row
        // But do include set title before each set
        int charsPerRow = 32;
        var borderLine = new string('─', charsPerRow * 2 + 5);
        var allRows = new List<Text>();

        // Add all sets in box with title and character codes in hex and rows of characters
        foreach (var set in sets)
        {
            // Add Title
            allRows.Add(Text.Empty);
            allRows.Add(Text.Cyan($"  {set.Name}").Dark($"  {set.start:X4} - {set.end:X4}"));

            // Top border
            allRows.Add(Text.Dark(" ┌─").Dark(borderLine).Dark("┐"));

            // Add all rows in the set
            for (int i = set.start; i <= set.end; i += charsPerRow)
            {
                // Add a row of characters
                var rowChars = new List<char>();
                for (int j = i; j < i + charsPerRow; j++)
                {
                    var c = j <= set.end ? (char)j : ' ';  // Fill out the end of the row with spaces
                    rowChars.Add(c);
                    rowChars.Add(' ');
                }

                // Add the row of characters with border sides
                var codeText = Text.Dark($"{i:X4}").Dark("│ ");
                allRows.Add(Text.Dark(" │").Add(codeText).White(string.Join("", rowChars.ToArray())).Dark("│"));
                if (i + charsPerRow <= set.end) allRows.Add(Text.Dark(" ├─").Dark(borderLine).Dark("┤"));
            }

            // Bottom border
            allRows.Add(Text.Dark(" └─").Dark(borderLine).Dark("┘"));
        }

        return allRows;
    }
}

