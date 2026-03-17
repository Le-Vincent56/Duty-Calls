#nullable enable

using System;
using DutyCalls.Simulation.Core;

namespace DutyCalls.Simulation.Features.BootProbe
{
    /// <summary>
    /// Tiny deterministic simulation used to validate fixed-step + Inject-before-Awake wiring.
    /// </summary>
    public sealed class TickCounterSimulation : ISimulationStep, ITickCounterQueries, ITickCounterEvents
    {
        private static readonly Action<int> Noop = delegate { };
        private int _tickIndex;

        public int SceneSeed { get; }
        public int TickIndex => _tickIndex;
        public event Action<int> TickAdvanced = Noop;
        
        public TickCounterSimulation(int sceneSeed)
        {
            // Scene seed is accepted to enforce the per-scene seed path early.
            // This probe does not currently use randomness.
            _tickIndex = 0;
            SceneSeed = sceneSeed;
        }

        public void Step()
        {
            _tickIndex++;
            TickAdvanced.Invoke(_tickIndex);
        }
    }
}