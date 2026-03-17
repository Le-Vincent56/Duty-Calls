#nullable enable

using System;

namespace DutyCalls.Simulation.Features.BootProbe
{
    /// <summary>
    /// Minimal event surface to avoid per-frame polling.
    /// </summary>
    public interface ITickCounterEvents
    {
        event Action<int> TickAdvanced;
    }
}