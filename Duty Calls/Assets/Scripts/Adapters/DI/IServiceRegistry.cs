#nullable enable
using System;

namespace DutyCalls.Adapters.DI
{
    /// <summary>
    /// Write-only service registry used during composition.
    /// </summary>
    public interface IServiceRegistry
    {
        /// <summary>
        /// Registers a service instance with a specific service type.
        /// </summary>
        /// <param name="serviceType">The type of the service being registered.</param>
        /// <param name="instance">The instance of the service to register.</param>
        void Register(Type serviceType, object instance);

        /// <summary>
        /// Registers a service instance with the specified service type.
        /// </summary>
        /// <typeparam name="TService">The type of the service being registered.</typeparam>
        /// <param name="instance">The instance of the service to register.</param>
        void Register<TService>(TService instance);
    }
}