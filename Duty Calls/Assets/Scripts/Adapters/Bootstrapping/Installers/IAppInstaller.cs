#nullable enable
using DutyCalls.Adapters.DI;

namespace DutyCalls.Adapters.Bootstrapping.Installers
{
    public interface IAppInstaller : IOrderedInstaller
    {
        void Install(in AppInstallContext context, IServiceRegistry services);
    }
}