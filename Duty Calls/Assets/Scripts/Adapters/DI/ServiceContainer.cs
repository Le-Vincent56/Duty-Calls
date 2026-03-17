#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DutyCalls.Adapters.DI
{
    /// <summary>
    /// Minimal DI container used only by composition roots + injector.
    /// Not for gameplay/presenter code to pull from.
    /// </summary>
    public sealed class ServiceContainer : IServiceRegistry, IServiceResolver
    {
        private readonly Dictionary<Type, object> _services;
        private readonly List<IDisposable> _trackedDisposables;
        private readonly ServiceContainer? _parent;

        public bool IsSealed { get; private set; }

        public bool IsDisposed { get; private set; }

        public ServiceContainer(ServiceContainer? parent = null)
        {
            _parent = parent;
            _trackedDisposables = new List<IDisposable>(16);
            _services = new Dictionary<Type, object>(32);
        }

        /// <summary>
        /// Registers a service instance for a specific service type.
        /// </summary>
        /// <param name="serviceType">The type of the service to be registered.</param>
        /// <param name="instance">The instance of the service to be registered.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="serviceType"/> or <paramref name="instance"/> is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if a service of the specified type has already been registered.
        /// </exception>
        public void Register(Type serviceType, object instance)
        {
            ThrowIfDisposed();
            ThrowIfSealed();
            
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            // Exit case - registration was successful
            if (_services.TryAdd(serviceType, instance))
            {
                TrackDisposable(instance);
                return;
            }
            
            throw new InvalidOperationException("Service already registered: " + serviceType.FullName);
        }

        /// <summary>
        /// Registers a service instance for a specific service type using a generic parameter.
        /// </summary>
        /// <param name="instance">The instance of the service to be registered.</param>
        /// <typeparam name="TService">The type of the service being registered.</typeparam>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="instance"/> is null.
        /// </exception>
        public void Register<TService>(TService instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            Register(typeof(TService), instance);
        }

        /// <summary>
        /// Attempts to resolve a service instance for the specified service type.
        /// </summary>
        /// <param name="serviceType">The type of the service to be resolved.</param>
        /// <param name="instance">
        /// When this method returns, contains the resolved service instance if the resolution is successful; otherwise, null.
        /// </param>
        /// <returns>
        /// True if a service instance of the specified type is successfully resolved; otherwise, false.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="serviceType"/> is null.
        /// </exception>
        public bool TryResolve(Type serviceType, out object? instance)
        {
            ThrowIfDisposed();
            
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));

            // Exit case - successfully retrieved the service
            if (_services.TryGetValue(serviceType, out object found))
            {
                instance = found;
                return true;
            }

            // Attempt to get the service from a parent container
            ServiceContainer? parent = _parent;
            if (parent != null) return parent.TryResolve(serviceType, out instance);

            instance = null;
            return false;
        }

        /// <summary>
        /// Resolves an instance of the specified service type.
        /// </summary>
        /// <param name="serviceType">The type of the service to resolve.</param>
        /// <returns>The resolved instance of the specified service type.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="serviceType"/> is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if no instance is registered for the specified service type.
        /// </exception>
        public object Resolve(Type serviceType)
        {
            // Exit case - successfully retrieved the service
            if (TryResolve(serviceType, out object? instance) && instance != null)
                return instance;

            throw new InvalidOperationException("No service registered for: " + serviceType.FullName);
        }

        /// <summary>
        /// Resolves an instance of the specified service type.
        /// </summary>
        /// <param name="serviceType">The type of the service to resolve.</param>
        /// <returns>The resolved instance of the specified service type.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="serviceType"/> is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if no instance is registered for the specified service type.
        /// </exception>
        public TService Resolve<TService>()
        {
            object instance = Resolve(typeof(TService));
            return (TService)instance;
        }

        /// <summary>
        /// Creates a new child service container with the current container as its parent.
        /// </summary>
        /// <returns>
        /// A new instance of <see cref="ServiceContainer"/> that acts as a child container.
        /// </returns>
        public ServiceContainer CreateChild()
        {
            ThrowIfDisposed();
            return new ServiceContainer(this);
        }

        /// <summary>
        /// Determines whether the specified service type is registered in the current container instance.
        /// </summary>
        /// <param name="serviceType">The type of the service to check for registration.</param>
        /// <returns>
        /// True if the specified service type is registered in the current container instance; otherwise, false.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="serviceType"/> is null.
        /// </exception>
        internal bool ContainsLocal(Type serviceType)
        {
            ThrowIfDisposed();

            // Exit case - service type not provided
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            
            return _services.ContainsKey(serviceType);
        }

        /// <summary>
        /// Seals the service container, preventing any further registrations.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the service container has been disposed prior to calling this method.
        /// </exception>
        public void Seal()
        {
            ThrowIfDisposed();
            IsSealed = true;
        }

        /// <summary>
        /// Releases all resources used by the ServiceContainer and disposes of any tracked disposable objects.
        /// </summary>
        /// <remarks>
        /// This method ensures that all IDisposable objects registered with the ServiceContainer are properly disposed
        /// and any internal collections are cleared. After calling this method, the ServiceContainer is considered disposed
        /// and cannot be used further.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the ServiceContainer has already been disposed.
        /// </exception>
        public void Dispose()
        {
            // Exit case - already disposed
            if (IsDisposed) return;
            
            IsDisposed = true;
            for (int i = _trackedDisposables.Count - 1; i >= 0; i--)
            {
                IDisposable disposable = _trackedDisposables[i];
                
                // Attempt to dispose
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
            
            _trackedDisposables.Clear();
            _services.Clear();
        }

        /// <summary>
        /// Tracks a disposable instance to ensure proper disposal at the container's lifecycle end.
        /// </summary>
        /// <param name="instance">The object to be tracked for disposal if it implements <see cref="IDisposable"/>.</param>
        private void TrackDisposable(object instance)
        {
            IDisposable? disposable = instance as IDisposable;
            
            // Exit case - the instance is not disposable
            if (disposable == null) return;
            
            // Exit case - the instance is a Unity object
            if (instance is UnityEngine.Object) return;
            
            for (int i = 0; i < _trackedDisposables.Count; i++)
            {
                // Skip if the references are not equal
                if (!ReferenceEquals(_trackedDisposables[i], disposable)) continue;
                
                return;
            }
            
            _trackedDisposables.Add(disposable);
        }

        /// <summary>
        /// Ensures that the ServiceContainer instance has not been disposed.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the ServiceContainer instance has been disposed.
        /// </exception>
        private void ThrowIfDisposed()
        {
            // Exit case - not disposed
            if (!IsDisposed) return;

            throw new InvalidOperationException("ServiceContainer is disposed.");
        }

        /// <summary>
        /// Ensures that the ServiceContainer is not sealed before performing an operation.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the ServiceContainer is sealed and modifications are not allowed.
        /// </exception>
        private void ThrowIfSealed()
        {
            // Exit case - not sealed
            if (!IsSealed) return;
            
            throw new InvalidOperationException("ServiceContainer is sealed.");
        }
    }
}