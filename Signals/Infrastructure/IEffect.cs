using System;

namespace Signals.Infrastructure;

internal interface IEffect : ITarget, IDisposable
{
    IEffect? Run(Messenger messenger);
}
