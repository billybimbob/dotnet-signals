using Signals;

var signals = new SignalProvider();

var source = signals.Source(4);
var timesTwo = signals.Derive(() => source.Value * 2);

using var watch1 = signals.Watch(() => Console.WriteLine($"signal is {source.Value}"));
using var watch2 = signals.Watch(() => Console.WriteLine($"derived times two is {timesTwo.Value}"));

// TODO: fix bug where ordering of subscription flips

using var subscription1 = source.Subscribe(new SourceObserver());
using var subscription2 = timesTwo.Subscribe(new DerivedObserver());

int[] intervals = { 400, 100, 200, };

foreach (int delay in intervals)
{
    await Task.Delay(delay);

    source.Value += 1;
}

source.Value = 7;
source.Value = 7;

sealed file record Name(string First, string Last);

sealed file class SourceObserver : IObserver<int>
{
    void IObserver<int>.OnCompleted()
        => Console.WriteLine("Completed source observation");

    void IObserver<int>.OnError(Exception error)
        => Console.WriteLine($"Observed source exception {error}");

    void IObserver<int>.OnNext(int value)
        => Console.WriteLine($"Observed source value {value}");
}

sealed file class DerivedObserver : IObserver<int>
{
    void IObserver<int>.OnCompleted()
        => Console.WriteLine("Completed derived observation");

    void IObserver<int>.OnError(Exception error)
        => Console.WriteLine($"Observed derived exception {error}");

    void IObserver<int>.OnNext(int value)
        => Console.WriteLine($"Observed derived value {value}");
}
