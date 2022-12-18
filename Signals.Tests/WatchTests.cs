using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Signals.Tests;

[TestClass]
public class WatchTests
{
    private readonly SignalProvider _signals;

    public WatchTests()
    {
        _signals = new SignalProvider();
    }
}
