using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Signals.Tests;

[TestClass]
public class WatchTests
{
    private const int CycleLimit = 200;

    private readonly SignalProvider _signals;

    public WatchTests()
    {
        _signals = new SignalProvider();
    }

    [TestMethod]
    public void Source_Value_Cascades()
    {
        int callCount = 0;
        var source = _signals.Source(0);

        using var watch = _signals.Watch(() =>
        {
            _ = source.Value;
            callCount++;
        });

        source.Value = 1;

        Assert.AreEqual(2, callCount);
    }

    [TestMethod]
    public void Source_Peek_NoCascade()
    {
        int callCount = 0;
        var source = _signals.Source(1);

        using var watch = _signals.Watch(() =>
        {
            _ = source.Peek;
            callCount++;
        });

        source.Value = 2;

        Assert.AreEqual(1, callCount);
    }

    [TestMethod]
    public void Source_MultipleValues_Cascades()
    {
        int callCount = 0;

        var sourceA = _signals.Source(1);
        var sourceB = _signals.Source("b");

        using var watch = _signals.Watch(() =>
        {
            _ = sourceA.Value;
            _ = sourceB.Value;
            callCount++;
        });

        sourceA.Value = 2;
        sourceB.Value = "B";

        Assert.AreEqual(3, callCount);
    }

    [TestMethod]
    public void Derive_Value_Cascades()
    {
        int callCount = 0;

        var source = _signals.Source(1);
        var timesTwo = _signals.Derive(() => source.Value * 2);

        using var watch = _signals.Watch(() =>
        {
            _ = timesTwo.Value;
            callCount++;
        });

        source.Value = 2;

        Assert.AreEqual(2, callCount);
    }

    [TestMethod]
    public void Derive_Peek_NoCascade()
    {
        int callCount = 0;

        var source = _signals.Source(1);
        var timesTwo = _signals.Derive(() => source.Value * 2);

        using var watch = _signals.Watch(() =>
        {
            _ = timesTwo.Peek;
            callCount++;
        });

        source.Value = 2;

        Assert.AreEqual(1, callCount);
    }

    [TestMethod]
    public void Source_SetValue_DetectsCycle()
    {
        var source = _signals.Source(0);

        _ = Assert.ThrowsException<InvalidOperationException>(TestWatch);

        void TestWatch()
        {
            int iteration = 0;

            using var watch = _signals.Watch(() =>
            {
                if (iteration++ > CycleLimit)
                {
                    throw new AssertFailedException("Cycle not detected");
                }

                _ = source.Value++;
            });
        }
    }

    [TestMethod]
    public void Dispose_SingleCall_Unsubscribes()
    {
        int callCount = 0;
        var source = _signals.Source(0);

        var watch = _signals.Watch(() =>
        {
            _ = source.Value;
            callCount++;
        });

        watch.Dispose();
        source.Value = 1;

        Assert.AreEqual(1, callCount);
    }

    [TestMethod]
    public void Dispose_MultipleCalls_NoThrow()
    {
        int callCount = 0;
        var source = _signals.Source(0);

        var watch = _signals.Watch(() =>
        {
            _ = source.Value;
            callCount++;
        });

        watch.Dispose();
        watch.Dispose();

        source.Value = 1;

        Assert.AreEqual(1, callCount);
    }

    [TestMethod]
    public void Source_ConditionalValue_PartialCascade()
    {
        int callCount = 0;

        var a = _signals.Source("a");
        var b = _signals.Source("b");

        var condition = _signals.Source(true);

        using var watch = _signals.Watch(() =>
        {
            _ = condition.Value ? a.Value : b.Value;
            callCount++;
        });

        a.Value = "aa";
        b.Value = "bb";

        Assert.AreEqual(2, callCount);
    }

    [TestMethod]
    public void Dispose_WithCleanup_RunsCleanup()
    {
        int callCount = 0;

        var watch = _signals.Watch(() =>
        {
            return () =>
            {
                callCount++;
            };
        });

        watch.Dispose();

        Assert.AreEqual(1, callCount);
    }

    [TestMethod]
    public void Derive_NoChange_NoCascade()
    {
        int callCount = 0;

        var source = _signals.Source(0);

        var noChange = _signals.Derive(() =>
        {
            _ = source.Value;
            return 0;
        });

        using var watch = _signals.Watch(() =>
        {
            _ = noChange.Value;
            callCount++;
        });

        source.Value = 1;

        Assert.AreEqual(1, callCount);
    }

    [TestMethod]
    public void Source_ThrowsInWatch_NoCascade()
    {
        int callCount = 0;
        var source = _signals.Source(0);

        IDisposable? watch = null;

        try
        {
            watch = _signals.Watch(() =>
            {
                callCount++;
                _ = source.Value;
                throw new InvalidOperationException("Failure");
            });
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            source.Value = 1;
            watch?.Dispose();
        }

        Assert.AreEqual(1, callCount);
    }

    [TestMethod]
    public void Source_ThrowsWithCleanup_IgnoresCleanup()
    {
        int callCount = 0;
        var source = _signals.Source(0);

        IDisposable? watch = null;

        try
        {
            watch = _signals.Watch(() =>
            {
                if (source.Value == 0)
                {
                    throw new InvalidOperationException("Failure");
                }

                return () => callCount++;
            });
        }
        catch (InvalidOperationException)
        { }
        finally
        {
            source.Value = 1;
            watch?.Dispose();
        }

        Assert.AreEqual(0, callCount);
    }

    [TestMethod]
    public void Source_ThrowInCleanup_Disposes()
    {
        int callCount = 0;
        var source = _signals.Source(0);

        var watch = _signals.Watch(() =>
        {
            _ = source.Value;
            callCount++;

            return () =>
            {
                throw new InvalidOperationException("Failure");
            };
        });

        try
        {
            watch.Dispose();
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            source.Value = 1;
            watch.Dispose();
        }

        Assert.AreEqual(1, callCount);
    }
}
