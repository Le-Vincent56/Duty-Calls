#nullable enable
using DutyCalls.Adapters.DI;

namespace DutyCalls.Adapters.Bootstrapping.Installers
{
    /// <summary>
    /// Installs services into the owner-scene container (scene-lifetime).
    /// </summary>
    public interface IOwnerSceneInstaller : IOrderedInstaller
    {
        void Install(in OwnerSceneInstallContext context, IServiceRegistry services);
    }
}