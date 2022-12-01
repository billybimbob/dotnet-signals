using Signals;

var signals = new SignalProvider();

var source = signals.CreateSource(4);

signals.Watch(() => Console.WriteLine($"signal is {source.Value}"));

int[] intervals = { 400, 100, 200, };

foreach (int delay in intervals)
{
    await Task.Delay(delay);

    source.Value += 1;
}

source.Value = 7;
source.Value = 7;
