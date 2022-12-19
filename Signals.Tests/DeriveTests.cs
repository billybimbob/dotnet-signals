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

    // [TestMethod]
    // public void Subscribe_MultipleDisposeCalls_NoThrow()
    // {
    // }
}
