using Signals;

var signalProvider = new SignalProvider();

var source = signalProvider.CreateSource(4);

signalProvider.Watch(() => Console.WriteLine($"signal is {source.Value}"));

await Task.Delay(400);

source.Value = 7;
