#nullable enable
using System;
using System.Collections.Generic;

namespace DutyCalls.Adapters.DI
{
    /// <summary>
    /// Represents a batch process for registering services in a service container.
    /// This batch allows for staging multiple service registrations before applying them
    /// to a <see cref="ServiceContainer"/>. Once applied, no further modifications can be made.
    /// </summary>
    /// <remarks>
    /// This class is designed to handle deferred registration of services, ensuring that
    /// dependencies and configurations can be added in groups rather than individually.
    /// It prevents duplicate registrations and enforces immutability after application.
    /// </remarks>
    internal sealed class ServiceRegistrationBatch : IServiceRegistry
    {
        private readonly Dictionary<Type, object> _registrations;
        private bool _isApplied;
        public int Count => _registrations.Count;

        public ServiceRegistrationBatch()
        {
            _registrations = new Dictionary<Type, object>(32);
        }

        /// <summary>
        /// Registers an instance of the specified service type.
        /// </summary>
        /// <param name="serviceType">The type of service being registered.</param>
        /// <param name="instance">The instance of the service to register.</param>
        /// <exception cref="InvalidOperationException">Thrown if the batch is already applied or the service is already staged.</exception>
        /// <exception cref="ArgumentNullException">Thrown if the provided service type or instance is null.</exception>
        public void Register(Type serviceType, object instance)
        {
            // Exit case - already applied
            if (_isApplied) throw new InvalidOperationException("ServiceRegistrationBatch has already been applied.");

            // Exit case - no service type provided
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));

            // Exit case - no instance given
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            // Exit case - registration was successful
            if (_registrations.TryAdd(serviceType, instance)) return;
            
            throw new InvalidOperationException("Service already staged: " + serviceType.FullName);
        }

        /// <summary>
        /// Registers an instance of the specified service type.
        /// </summary>
        /// <typeparam name="TService">The type of service being registered.</typeparam>
        /// <param name="instance">The instance of the service to register.</param>
        /// <exception cref="ArgumentNullException">Thrown if the provided instance is null.</exception>
        public void Register<TService>(TService instance)
        {
            // Exit case - no instance given
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            Register(typeof(TService), instance);
        }

        /// <summary>
        /// Applies the service registrations stored in the batch to the specified service container.
        /// </summary>
        /// <param name="container">
        /// The <see cref="ServiceContainer"/> to which the service registrations will be applied.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the batch has already been applied or if a service type in the batch is already registered in the target container.
        /// </exception>
        /// <exception cref="ArgumentNullException">Thrown if the provided container is null.</exception>
        public void ApplyTo(ServiceContainer container)
        {
            // Exit case - already applied
            if (_isApplied)
                throw new InvalidOperationException("ServiceRegistrationBatch has already been applied.");

            // Exit case - no container given
            if (container == null)
                throw new ArgumentNullException(nameof(container));

            foreach (KeyValuePair<Type, object> registration in _registrations)
            {
                if (container.ContainsLocal(registration.Key))
                {
                    throw new InvalidOperationException(
                        "Service already registered in target container: " + registration.Key.FullName
                    );
                }
            }

            foreach (KeyValuePair<Type, object> registration in _registrations)
            {
                container.Register(registration.Key, registration.Value);
            }

            _isApplied = true;
        }
    }
}