#nullable enable
using DutyCalls.Adapters.DI;
using DutyCalls.Simulation.Core;
using DutyCalls.Simulation.Features.BootProbe;
using UnityEngine;

namespace DutyCalls.Adapters.Bootstrapping.Installers.Implementations
{
    /// <summary>
    /// A MonoBehaviour that implements the installation of tick counter-related services
    /// for a scene with a specific lifetime. This class registers all required services
    /// for tick counting and simulation, ensuring proper integration with the dependency
    /// injection container used by the application.
    /// </summary>
    public sealed class TickCounterOwnerSceneInstaller : MonoBehaviour, IOwnerSceneInstaller
    {
        [SerializeField] private int order;
        public int Order => order;

        /// <summary>
        /// Installs tick counter-related services into the application context for a scene
        /// using the provided dependency injection container.
        /// </summary>
        /// <param name="context">
        /// The installation context containing scene-specific data
        /// such as scene seed and application services.
        /// </param>
        /// <param name="services">
        /// The service registry to which the tick counter services
        /// will be registered.
        /// </param>
        public void Install(in OwnerSceneInstallContext context, IServiceRegistry services)
        {
            TickCounterSimulation simulation = new TickCounterSimulation(context.SceneSeed);
            services.Register<ISimulationStep>(simulation);
            services.Register<ITickCounterQueries>(simulation);
            services.Register<ITickCounterEvents>(simulation);
        }
    }
}