#nullable enable

using System;

namespace DutyCalls.Simulation.Core
{
    /// <summary>
    /// Fixed simulation tick rate (Hz). Simulation time is ticks-only.
    /// </summary>
    public readonly struct SimulationTickRate
    {
        /// <summary>
        /// Gets the fixed simulation tick rate in Hertz (Hz). Determines the number of simulation ticks per second,
        /// enforcing a consistent and discrete simulation update frequency.
        /// </summary>
        public int TickRateHz { get; }

        public SimulationTickRate(int tickRateHz)
        {
            if (tickRateHz <= 0) throw new ArgumentOutOfRangeException(nameof(tickRateHz));

            TickRateHz = tickRateHz;
        }
    }
}