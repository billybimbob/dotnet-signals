using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Signals.Tests;

[TestClass]
public class DeriveTests
{
    private readonly SignalProvider _signals;

    public DeriveTests()
    {
        _signals = new SignalProvider();
    }

    [TestMethod]
    public void Value_InitCombinedSources_Equals()
    {
        var aSource = _signals.Source("a");
        var bSource = _signals.Source("b");

        var combined = _signals.Derive(() => aSource.Value + bSource.Value);

        Assert.AreEqual("ab", combined.Value);
    }

    [TestMethod]
    public void Value_UpdateCombinedSources_Equals()
    {
        var aSource = _signals.Source("a");
        var bSource = _signals.Source("b");

        var combined = _signals.Derive(() => aSource.Value + bSource.Value);

        aSource.Value = "aa";

        Assert.AreEqual("aab", combined.Value);
    }

    // [TestMethod]
    // public void Subscribe_MultipleDisposeCalls_NoThrow()
    // {
    // }
}
