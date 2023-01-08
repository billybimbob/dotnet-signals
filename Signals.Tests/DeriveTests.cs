using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

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

    [TestMethod]
    public void Peek_InitCombinedSources_Equals()
    {
        var aSource = _signals.Source("a");
        var bSource = _signals.Source("b");

        var combined = _signals.Derive(() => aSource.Value + bSource.Value);

        Assert.AreEqual("ab", combined.Peek);
    }

    [TestMethod]
    public void Peek_UpdateCombinedSources_Equals()
    {
        var aSource = _signals.Source("a");
        var bSource = _signals.Source("b");

        var combined = _signals.Derive(() => aSource.Value + bSource.Value);

        aSource.Value = "aa";

        Assert.AreEqual("aab", combined.Peek);
    }

    [TestMethod]
    public void Subscribe_SourceChanges_Updates()
    {
        int notifyCount = 0;

        var source = _signals.Source(0);
        var timesTwo = _signals.Derive(() => source.Value * 2);

        var mock = new Mock<IObserver<int>>();

        _ = mock
            .Setup(o => o.OnNext(It.IsAny<int>()))
            .Callback(() => notifyCount++);

        using var unsubscribe = timesTwo.Subscribe(mock.Object);

        source.Value = 1;

        Assert.AreEqual(2, notifyCount);
    }

    [TestMethod]
    public void Subscribe_DisposeCall_Unsubscribes()
    {
        int notifyCount = 0;

        var source = _signals.Source(0);
        var timesTwo = _signals.Derive(() => source.Value * 2);

        var mock = new Mock<IObserver<int>>();

        _ = mock
            .Setup(o => o.OnNext(It.IsAny<int>()))
            .Callback(() => notifyCount++);

        var unsubscribe = timesTwo.Subscribe(mock.Object);

        unsubscribe.Dispose();
        source.Value = 1;

        Assert.AreEqual(1, notifyCount);
    }

    [TestMethod]
    public void Subscribe_MultipleDisposeCalls_NoThrow()
    {
        int notifyCount = 0;

        var source = _signals.Source(0);
        var timesTwo = _signals.Derive(() => source.Value * 2);

        var mock = new Mock<IObserver<int>>();

        _ = mock
            .Setup(o => o.OnNext(It.IsAny<int>()))
            .Callback(() => notifyCount++);

        var unsubscribe = timesTwo.Subscribe(mock.Object);

        unsubscribe.Dispose();
        unsubscribe.Dispose();

        source.Value = 1;

        Assert.AreEqual(1, notifyCount);
    }

    [TestMethod]
    public void Source_SetValue_Throws()
    {
        var source = _signals.Source(0);

        var modify = _signals.Derive(() =>
        {
            source.Value--;
            return 0;
        });

        void GetValue() => _ = modify.Value;

        _ = Assert.ThrowsException<InvalidOperationException>(GetValue);
    }

    [TestMethod]
    public void Source_DependencyCycle_Throws()
    {
        Assert.Inconclusive();
    }

    [TestMethod]
    public void Peek_DependencyCycle_Throws()
    {
        Assert.Inconclusive();
    }

    [TestMethod]
    public void Value_NoCall_IsLazy()
    {
        int callCount = 0;
        var source = _signals.Source(0);

        var timesTwo = _signals.Derive(() =>
        {
            callCount++;
            return source.Value * 2;
        });

        Assert.AreEqual(0, callCount);
    }

    [TestMethod]
    public void Value_Called_IsEvaluated()
    {
        int callCount = 0;
        var source = _signals.Source(0);

        var timesTwo = _signals.Derive(() =>
        {
            callCount++;
            return source.Value * 2;
        });

        _ = timesTwo.Value;

        Assert.AreEqual(1, callCount);
    }

    [TestMethod]
    public void Value_MultipleCalls_SingleEvaluation()
    {
        int callCount = 0;
        var source = _signals.Source(0);

        var timesTwo = _signals.Derive(() =>
        {
            callCount++;
            return source.Value * 2;
        });

        _ = timesTwo.Value;
        _ = timesTwo.Value;
        _ = timesTwo.Value;

        Assert.AreEqual(1, callCount);
    }

    [TestMethod]
    public void Value_ConditionalSource_PartialCascade()
    {
        Assert.Inconclusive();
    }

    [TestMethod]
    public void Value_ThrowsNoCall_NoThrow()
    {
        Assert.Inconclusive();
    }

    [TestMethod]
    public void Value_Throws_ThrowsOnGet()
    {
        Assert.Inconclusive();
    }

    [TestMethod]
    public void Value_OnlyDeriveThrows_ThrowContained()
    {
        Assert.Inconclusive();
    }
}
