#nullable enable
using DutyCalls.Adapters.DI;
using DutyCalls.Simulation.Features.BootProbe;
using UnityEngine;

namespace DutyCalls.Adapters.Bootstrapping.Installers.Implementations
{
    public sealed class TickCounterAdditiveSceneInstaller : MonoBehaviour, IAdditiveSceneInstaller
    {
        [SerializeField] private int order;
        public int Order => order;

        public void Install(in AdditiveSceneInstallContext context, IServiceRegistry services)
        {
            ITickCounterQueries queries = context.OwnerServices.Resolve<ITickCounterQueries>();
            ITickCounterEvents events = context.OwnerServices.Resolve<ITickCounterEvents>();
            
            services.Register<ITickCounterQueries>(queries);
            services.Register<ITickCounterEvents>(events);
        }
    }
}