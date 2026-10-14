namespace gmdTest.Utils;

[TestClass]
public class TimingTest
{
    // How a time reads in the log: milliseconds alone while under a second, then seconds and
    // milliseconds, and hours, minutes, seconds and milliseconds from a minute on
    [TestMethod]
    public void TestText()
    {
        Assert.AreEqual("0ms", Timing.Text(TimeSpan.Zero));
        Assert.AreEqual("999ms", Timing.Text(TimeSpan.FromMilliseconds(999.9)), "Whole milliseconds, rounded down");
        Assert.AreEqual("1s, 0ms", Timing.Text(TimeSpan.FromSeconds(1)));
        Assert.AreEqual("59s, 250ms", Timing.Text(TimeSpan.FromMilliseconds(59_250)));
        Assert.AreEqual("0:1:0:0", Timing.Text(TimeSpan.FromMinutes(1)));
        Assert.AreEqual("2:3:4:5", Timing.Text(new TimeSpan(0, 2, 3, 4, 5)));
    }

    [TestMethod]
    public void TestToStringIsTheTimeSoFarInParenthesis()
    {
        StringAssert.Matches(Timing.Start().ToString(), new System.Text.RegularExpressions.Regex(@"^\(\d+ms\)$"));
    }
}
