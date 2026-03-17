#nullable enable

namespace DutyCalls.Simulation.Core
{
    /// <summary>
    /// Unity-free simulation stepping contract. Called exactly once per simulation tick
    /// </summary>
    public interface ISimulationStep
    {
        /// <summary>
        /// Advances the simulation by one fixed tick
        /// </summary>
        void Step();
    }
}
