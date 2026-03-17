#nullable enable
using System;
using DutyCalls.Adapters.DI;

namespace DutyCalls.Adapters.Bootstrapping.Installers
{
    /// <summary>
    /// Represents the context used to install services for an additive scene.
    /// Provides access to app-lifetime services and configuration.
    /// </summary>
    public readonly struct AdditiveSceneInstallContext
    {
        public IAppServices AppServices { get; }
        public IServiceResolver OwnerServices { get; }

        public AdditiveSceneInstallContext(IAppServices appServices, IServiceResolver ownerServices)
        {
            AppServices = appServices ?? throw new ArgumentNullException(nameof(appServices));
            OwnerServices = ownerServices ?? throw new ArgumentNullException(nameof(ownerServices));
        }
    }
}