#nullable enable
using DutyCalls.Adapters.DI;

namespace DutyCalls.Adapters.Bootstrapping.Installers
{
    /// <summary>
    /// Installs services into the app container from a Persistent Systems scene.
    /// </summary>
    public interface IPersistentServicesInstaller : IOrderedInstaller
    {
        void Install(in PersistentInstallContext context, IServiceRegistry services);
    }
}