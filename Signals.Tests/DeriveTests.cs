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
        ISignal<int>? a = null;
        ISignal<int>? b = null;
        ISignal<int>? c = null;
        ISignal<int>? d = null;

        a = _signals.Derive(() => b?.Value ?? 0);
        b = _signals.Derive(() => c?.Value ?? 0);
        c = _signals.Derive(() => d?.Value ?? 0);
        d = _signals.Derive(() => a?.Value ?? 0);

        _ = Assert.ThrowsException<InvalidOperationException>(GetValue);

        void GetValue() => _ = a.Value;
    }

    [TestMethod]
    public void Peek_DependencyCycle_Throws()
    {
        ISignal<int>? a = null;
        ISignal<int>? b = null;
        ISignal<int>? c = null;
        ISignal<int>? d = null;

        a = _signals.Derive(() => b?.Value ?? 0);
        b = _signals.Derive(() => c?.Value ?? 0);
        c = _signals.Derive(() => d?.Value ?? 0);
        d = _signals.Derive(() => a?.Value ?? 0);

        _ = Assert.ThrowsException<InvalidOperationException>(GetValue);

        void GetValue() => _ = a.Peek;
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
        int callCount = 0;

        var a = _signals.Source("a");
        var b = _signals.Source("b");

        var condition = _signals.Source(true);

        var derived = _signals.Derive(() =>
        {
            callCount++;
            return condition.Value ? a.Value : b.Value;
        });

        _ = derived.Value;

        a.Value = "aa";
        _ = derived.Value;

        b.Value = "bb";
        _ = derived.Value;

        Assert.AreEqual("aa", derived.Value);
        Assert.AreEqual(2, callCount);
    }

    [TestMethod]
    public void Value_ThrowsNoCall_NoThrow()
    {
        var source = _signals.Source(0);

        var derivedError = _signals.Derive(() =>
        {
            if (source.Value == 0)
            {
                throw new InvalidOperationException("Error");
            }

            return source.Value * 2;
        });

        Assert.AreEqual(0, source.Value);
    }

    [TestMethod]
    public void Value_Throws_ThrowsOnGet()
    {
        var source = _signals.Source(0);

        var error = _signals.Derive(() =>
        {
            if (source.Value == 0)
            {
                throw new InvalidOperationException("Error");
            }

            return source.Value * 2;
        });

        Assert.AreEqual(0, source.Value);

        _ = Assert.ThrowsException<InvalidOperationException>(GetError);

        void GetError() => _ = error.Value;
    }

    [TestMethod]
    public void Value_OnlyDeriveThrows_ThrowContained()
    {
        var source = _signals.Source(0);

        var error = _signals.Derive(() =>
        {
            if (source.Value == 0)
            {
                throw new InvalidOperationException("Error");
            }

            return source.Value * 2;
        });

        var contained = _signals.Derive(() =>
        {
            try
            {
                return error.Value;
            }
            catch (InvalidOperationException)
            {
                return 2;
            }
        });

        Assert.AreEqual(0, source.Value);

        _ = Assert.ThrowsException<InvalidOperationException>(GetError);

        Assert.AreEqual(2, contained.Value);

        void GetError() => _ = error.Value;
    }
}
