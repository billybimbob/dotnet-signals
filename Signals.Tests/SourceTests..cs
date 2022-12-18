using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Signals.Tests;

[TestClass]
public class SourceTests
{
    private readonly SignalProvider _signals;

    public SourceTests()
    {
        _signals = new SignalProvider();
    }
}
