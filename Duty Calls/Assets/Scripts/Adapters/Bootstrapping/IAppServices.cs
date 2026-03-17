#nullable enable
using DutyCalls.Simulation.Core;

namespace DutyCalls.Adapters.Bootstrapping
{
    /// <summary>
    /// App-lifetime configuration available to bootstrappers only (seed, tick rate).
    /// </summary>
    public interface IAppServices
    {
        /// <summary>
        /// Gets the seed value for initializing simulations or other application-lifetime operations.
        /// </summary>
        /// <remarks>
        /// The value of RunSeed is used to ensure deterministic behavior in simulations or processes
        /// that rely on randomization to maintain consistent results throughout the application lifetime.
        /// </remarks>
        int RunSeed { get; }

        /// <summary>
        /// Gets the fixed simulation tick rate, expressed in hertz (Hz), used to control the pacing of simulation updates.
        /// </summary>
        /// <remarks>
        /// The TickRate property defines the frequency at which the simulation processes updates, ensuring consistent timing
        /// for simulation events and operations. This value plays a critical role in maintaining deterministic simulation behavior.
        /// </remarks>
        SimulationTickRate TickRate { get; }

        /// <summary>
        /// Retrieves the seed associated with the specified scene key.
        /// </summary>
        /// <param name="sceneKey">The key of the scene for which the seed is to be retrieved.</param>
        /// <returns>The seed value for the specified scene key.</returns>
        int GetSceneSeed(string sceneKey);
    }
}