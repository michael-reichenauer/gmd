namespace gmd.Cui.Common;

// Asks for the text to search the diff or the blame for, see RowSearch. The log has its own, which
// lists the commits as they are typed (FilterDlg); a file's lines are seen where they are instead.
static class SearchDlg
{
    // 'what' is what is searched, e.g. "diff", which the question names
    public static Result<string> Show(string what)
    {
        var dlg = new UIDialog("Search", 50, 7);
        dlg.AddLabel(1, 0, $"Text to find in the {what}, in any case:");
        var text = dlg.AddTextField(1, 2, 46);

        if (!dlg.ShowOkCancel(text) || text.Text == "")
            return new Error();

        return text.Text;
    }
}
