using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Signals.Tests;

[TestClass]
public class SourceTests
{
    // TODO: figure out how to count effect calls
    // possible option: Moq

    private readonly SignalProvider _signals;

    public SourceTests()
    {
        _signals = new SignalProvider();
    }

    [TestMethod]
    public void Value_Initial_Equals()
    {
        const int initial = 5;

        var source = _signals.Source(initial);

        Assert.AreEqual(initial, source.Value);
    }

    [TestMethod]
    public void Value_Modified_EqualsChange()
    {
        const int initial = 5;
        const int update = 3;

        var source = _signals.Source(initial);

        source.Value = update;

        Assert.AreEqual(update, source.Value);
    }

    [TestMethod]
    public void Peek_Initial_Equals()
    {
        const int initial = 3;

        var source = _signals.Source(initial);

        Assert.AreEqual(initial, source.Peek);
    }

    [TestMethod]
    public void Peek_Modified_EqualsChange()
    {
        const int initial = 5;
        const int update = 3;

        var source = _signals.Source(initial);

        source.Value = update;

        Assert.AreEqual(update, source.Peek);
    }

    // [TestMethod]
    // public void Subscribe_MultipleDisposeCalls_NoThrow()
    // {
    // }
}
