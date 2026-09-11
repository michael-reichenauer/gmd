namespace gmd.Cui.Common;

// The bottom edge of the frame around a spell checked input. While any word is misspelled it says
// how many and how to get at the suggestions, and otherwise it is the plain edge, so it takes no
// row of its own and shows up the moment a word turns red, which is when what the red means is
// asked. No view, so it is unit testable; UIDialog draws it over the border's own edge.
static class SpellHint
{
    public const string Suggestions = "F7 or right-click for suggestions";

    // The edge of a frame 'width' wide, with the hint for 'misspelled' words let into it. A frame
    // too narrow for the hint gets the count alone, and one too narrow for that the plain edge.
    public static Text Edge(int misspelled, int width, Color color)
    {
        var line = new string('─', Math.Max(0, width - 2));
        if (misspelled == 0)
            return Text.Color(color, $"└{line}┘").ToText();

        var words = misspelled == 1 ? "1 misspelled word" : $"{misspelled} misspelled words";
        var room = width - 3; // "└─" and "┘"
        var rest = $", {Suggestions} ";
        if (words.Length + 2 + rest.Length > room)
            rest = " ";
        if (words.Length + 2 + rest.Length > room)
            return Text.Color(color, $"└{line}┘").ToText();

        var edge = new string('─', room - words.Length - 1 - rest.Length);
        return Text.Color(color, "└─").BrightRed($" {words}").Color(color, $"{rest}{edge}┘").ToText();
    }
}
