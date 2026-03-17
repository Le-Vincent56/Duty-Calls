#nullable enable
using System;

namespace DutyCalls.Adapters.Bootstrapping.Installers
{
    /// <summary>
    /// Represents a context for installation processes that require persistent application services.
    /// </summary>
    public readonly struct PersistentInstallContext
    {
        public IAppServices AppServices { get; }

        public PersistentInstallContext(IAppServices appServices)
        {
            AppServices = appServices ?? throw new ArgumentNullException(nameof(appServices));
        }
    }
}