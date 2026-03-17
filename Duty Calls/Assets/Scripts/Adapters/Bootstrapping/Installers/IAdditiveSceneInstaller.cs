#nullable enable
using DutyCalls.Adapters.DI;

namespace DutyCalls.Adapters.Bootstrapping.Installers
{
    /// <summary>
    /// Installs services into an additive-scene child container (local to that additive scene).
    /// </summary>
    public interface IAdditiveSceneInstaller : IOrderedInstaller
    {
        void Install(in AdditiveSceneInstallContext context, IServiceRegistry services);
    }
}