using gmd.Cui.Common;

namespace gmdTest.Cui.Common;

[TestClass]
public class UIDialogTest
{
    // A border covers the whole rectangle of the view it frames, and a mouse event goes to the last
    // view added at its position, so the border has to go under the view: over it, no click ever
    // reached the message body of the commit dialog.
    [TestMethod]
    public void TestABorderGoesUnderTheViewItFrames()
    {
        var dlg = new UIDialog("Test", 40, 12);
        var text = dlg.AddMultiLineInputView(1, 1, 20, 5, "");
        var list = dlg.AddContentView(1, 8, 20, 2, []);
        var listBorder = dlg.AddBorderView(list, Color.Dark);

        var views = dlg.Views.ToList();
        var textBorder = views.OfType<BorderView>().First();
        Assert.IsTrue(views.IndexOf(textBorder) < views.IndexOf(text), "The text view's border should be under it");
        Assert.IsTrue(views.IndexOf(listBorder) < views.IndexOf(list), "The list's border should be under it");
        Assert.AreEqual(2, views.OfType<BorderView>().Count());
    }
}
