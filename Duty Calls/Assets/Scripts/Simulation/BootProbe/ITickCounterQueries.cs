#nullable enable

namespace DutyCalls.Simulation.Features.BootProbe
{
    /// <summary>
    /// Minimal read surface to prove Simulation->Presentation wiring.
    /// </summary>
    public interface ITickCounterQueries
    {
        int TickIndex { get; }
    }
}