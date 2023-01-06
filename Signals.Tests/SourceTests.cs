using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

namespace Signals.Tests;

[TestClass]
public class SourceTests
{
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

    [TestMethod]
    public void Subscribe_SourceChanges_Updates()
    {
        int notifyCount = 0;

        var source = _signals.Source(0);
        var mock = new Mock<IObserver<int>>();

        _ = mock
            .Setup(o => o.OnNext(It.IsAny<int>()))
            .Callback(() => notifyCount++);

        using var unsubscribe = source.Subscribe(mock.Object);

        source.Value = 1;

        Assert.AreEqual(2, notifyCount);
    }

    [TestMethod]
    public void Subscribe_DisposeCall_Unsubscribes()
    {
        int notifyCount = 0;

        var source = _signals.Source(0);
        var mock = new Mock<IObserver<int>>();

        _ = mock
            .Setup(o => o.OnNext(It.IsAny<int>()))
            .Callback(() => notifyCount++);

        var unsubscribe = source.Subscribe(mock.Object);

        unsubscribe.Dispose();
        source.Value = 1;

        Assert.AreEqual(1, notifyCount);
    }

    [TestMethod]
    public void Subscribe_MultipleDisposeCalls_NoThrow()
    {
        int notifyCount = 0;

        var source = _signals.Source(0);
        var mock = new Mock<IObserver<int>>();

        _ = mock
            .Setup(o => o.OnNext(It.IsAny<int>()))
            .Callback(() => notifyCount++);

        var unsubscribe = source.Subscribe(mock.Object);

        unsubscribe.Dispose();
        unsubscribe.Dispose();

        source.Value = 1;

        Assert.AreEqual(1, notifyCount);
    }
}
