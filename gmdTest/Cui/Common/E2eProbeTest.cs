using gmd.Cui.Common;

namespace gmdTest.Cui.Common;

// What the end-to-end tests are told of gmd's state, as the terminal's title
[TestClass]
public class E2eProbeTest
{
    [TestMethod]
    public void TestTheTitleSaysIdleOrBusyAndCountsTheKeys()
    {
        var probe = new E2eProbe();
        Assert.AreEqual("gmd-e2e idle 0 0", probe.Title(isBusy: false));

        probe.KeyReceived(isDropped: false);
        probe.KeyReceived(isDropped: true);

        Assert.AreEqual("gmd-e2e busy 2 1", probe.Title(isBusy: true));
    }

    // An idle gmd writes nothing to the terminal iteration after iteration
    [TestMethod]
    public void TestTheTitleIsWrittenOnlyWhenItChanges()
    {
        var probe = new E2eProbe();

        Assert.AreEqual("gmd-e2e busy 0 0", probe.TitleIfChanged(isBusy: true));
        Assert.IsNull(probe.TitleIfChanged(isBusy: true));
        Assert.AreEqual("gmd-e2e idle 0 0", probe.TitleIfChanged(isBusy: false));

        probe.KeyReceived(isDropped: false);
        Assert.AreEqual("gmd-e2e idle 1 0", probe.TitleIfChanged(isBusy: false));
    }
}
