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
    public void SetValue_DetectsCycle()
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
    public void SetValue_Indirect_DetectsCycle()
    {
        var source = _signals.Source(0);

        var derivedCycle = _signals.Derive(() => source.Value--);

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

                _ = derivedCycle.Value;
            });
        }
    }

    // [TestMethod]
    // public void MultipleDisposeCalls_NoThrow()
    // {
    // }
}
