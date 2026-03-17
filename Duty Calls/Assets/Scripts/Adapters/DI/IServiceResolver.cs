#nullable enable
using System;

namespace DutyCalls.Adapters.DI
{
    /// <summary>
    /// Read-only resolver used by the injector to supply dependencies to Inject(...).
    /// </summary>
    public interface IServiceResolver
    {
        /// <summary>
        /// Tries to resolve an instance of the specified service type.
        /// </summary>
        /// <param name="serviceType">The type of the service to resolve.</param>
        /// <param name="instance">The resolved instance if the method returns true; otherwise, null.</param>
        /// <returns>True if the service could be successfully resolved; otherwise, false.</returns>
        bool TryResolve(Type serviceType, out object? instance);

        /// <summary>
        /// Resolves an instance of the specified service type.
        /// </summary>
        /// <param name="serviceType">The type of the service to resolve.</param>
        /// <returns>The resolved service instance.</returns>
        object Resolve(Type serviceType);

        /// <summary>
        /// Resolves an instance of the specified service type.
        /// </summary>
        /// <typeparam name="TService">The type of the service to resolve.</typeparam>
        /// <returns>The resolved service instance.</returns>
        TService Resolve<TService>();
    }
}