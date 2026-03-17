#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DutyCalls.Adapters.DI
{
    /// <summary>
    /// Injects scene/prefab objects by invoking the single method marked with <see cref="InjectAttribute"/>.
    /// Reflection is cached per concrete type; invocation does not allocate per object (buffers reused).
    /// </summary>
    internal sealed class SceneInjector
    {
        /// <summary>
        /// Represents a plan for injecting dependencies into components by storing the method information
        /// and parameter types of the inject method. Enables efficient invocation of the injection process
        /// without the need to repeatedly analyze the target type.
        /// </summary>
        private sealed class InjectionPlan
        {
            private readonly MethodInfo _injectMethod;
            private readonly Type[] _parameterTypes;

            public InjectionPlan(MethodInfo injectMethod, Type[] parameterTypes)
            {
                _injectMethod = injectMethod;
                _parameterTypes = parameterTypes;
            }

            public MethodInfo InjectMethod => _injectMethod;
            public Type[] ParameterTypes => _parameterTypes;
        }

        private readonly Dictionary<Type, InjectionPlan?> _planCache;
        
        private readonly List<GameObject> _rootBuffer;
        private readonly List<MonoBehaviour> _behaviourBuffer;
        
        private readonly object?[] _args1;
        private readonly object?[] _args2;
        private readonly object?[] _args3;
        private readonly object?[] _args4;
        private readonly object?[] _args5;
        private readonly object?[] _args6;

        public SceneInjector()
        {
            _planCache = new Dictionary<Type, InjectionPlan?>(256);
            _rootBuffer = new List<GameObject>(128);
            _behaviourBuffer = new List<MonoBehaviour>(512);
            
            _args1 = new object?[1];
            _args2 = new object?[2];
            _args3 = new object?[3];
            _args4 = new object?[4];
            _args5 = new object?[5];
            _args6 = new object?[6];
        }

        /// <summary>
        /// Injects dependencies into all GameObjects in the specified scene.
        /// </summary>
        /// <param name="scene">The target scene whose GameObjects will be injected. Must be valid and loaded.</param>
        /// <param name="resolver">The resolver used to supply dependencies for the injection process.</param>
        /// <exception cref="InvalidOperationException">Thrown when the provided scene is invalid.</exception>
        public void InjectScene(Scene scene, IServiceResolver resolver)
        {
            // Exit case - the scene is invalid
            if (!scene.IsValid())
            {
                throw new InvalidOperationException("Cannot inject an invalid scene.");
            }

            // Prepare the buffer
            _rootBuffer.Clear();
            
            // Inject all game objects
            scene.GetRootGameObjects(_rootBuffer);
            for (int i = 0; i < _rootBuffer.Count; i++)
            {
                InjectGameObject(_rootBuffer[i], resolver);
            }
        }

        public void InjectGameObject(GameObject root, IServiceResolver resolver)
        {
            // Exit case - no game object root given
            if (!root) throw new ArgumentNullException(nameof(root));

            // Prepare the buffer
            _behaviourBuffer.Clear();
            
            // Inject all children
            root.GetComponentsInChildren(true, _behaviourBuffer);
            for (int i = 0; i < _behaviourBuffer.Count; i++)
            {
                MonoBehaviour behaviour = _behaviourBuffer[i];
                
                // Skip if the child is inactive or null
                if (!behaviour) continue;

                Type concreteType = behaviour.GetType();
                InjectionPlan? plan = GetOrBuildPlan(concreteType);
                
                // Skip if the plan couldn't be built
                if (plan == null)  continue;

                InvokeInject(behaviour, plan, resolver);
            }
        }

        /// <summary>
        /// Retrieves an existing injection plan for the specified type from the cache,
        /// or builds and caches a new plan if none exists.
        /// </summary>
        /// <param name="concreteType">The type of the object for which the injection plan is required. Must not be null.</param>
        /// <returns>The injection plan for the specified type, or null if no valid inject method is found.</returns>
        private InjectionPlan? GetOrBuildPlan(Type concreteType)
        {
            // Exit case - could get the cached plan
            if (_planCache.TryGetValue(concreteType, out InjectionPlan? cached))
                return cached;

            // Build the plan and cache it
            InjectionPlan? built = BuildPlan(concreteType);
            _planCache.Add(concreteType, built);
            return built;
        }

        /// <summary>
        /// Constructs an injection plan for the specified concrete type by analyzing its methods
        /// to determine the appropriate injection logic.
        /// </summary>
        /// <param name="concreteType">The concrete type to analyze for constructing the injection plan. Must not be null.</param>
        /// <returns>An <see cref="InjectionPlan"/> instance representing the injection logic for the type, or null if no suitable injection method is found.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the [Inject] method on the provided type violates any requirements, such as:
        /// being static, returning a non-void type, being generic, or having more than 6 parameters.
        /// </exception>
        private static InjectionPlan? BuildPlan(Type concreteType)
        {
            MethodInfo? injectMethod = null;
            Type? scan = concreteType;
            
            while (scan != null)
            {
                // Get all method information
                MethodInfo[] methods = scan.GetMethods(
                    BindingFlags.Instance | 
                    BindingFlags.Public |
                    BindingFlags.NonPublic | 
                    BindingFlags.DeclaredOnly
                );
                
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    
                    // Skip if the method is not marked with the [Inject] attribute
                    if (!method.IsDefined(typeof(InjectAttribute), false)) continue;

                    // Exit case - there is already an inject method (multiple inject attributes)
                    if (injectMethod != null)
                        throw new InvalidOperationException($"Multiple [Inject] methods found on: {concreteType.FullName}");

                    // Set the inject method
                    injectMethod = method;
                }

                scan = scan.BaseType;
            }

            // Exit case - no inject method found
            if (injectMethod == null) return null;

            // Exit case - the inject method is static
            if (injectMethod.IsStatic)
                throw new InvalidOperationException($"[Inject] method must be instance method on: {concreteType.FullName}");

            // Exit case - the inject method does not return void
            if (injectMethod.ReturnType != typeof(void))
                throw new InvalidOperationException("[Inject] method must return void on: " + concreteType.FullName);

            // Exit case - the inject method contains generic parameters
            if (injectMethod.ContainsGenericParameters)
                throw new InvalidOperationException("[Inject] method must not be generic on: " + concreteType.FullName);

            ParameterInfo[] parameters = injectMethod.GetParameters();
            
            // Exit case - there are more than six parameters (max allowed)
            if (parameters.Length > 6)
                throw new InvalidOperationException("[Inject] supports up to 6 parameters. Offender: " + concreteType.FullName);

            Type[] parameterTypes = new Type[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo p = parameters[i];
                
                // Exit case - the parameter is ref/out
                if (p.IsOut || p.ParameterType.IsByRef)
                    throw new InvalidOperationException($"[Inject] parameters cannot be ref/out on: {concreteType.FullName}");

                // Exit case - the parameter is optional
                if (p.IsOptional)
                    throw new InvalidOperationException($"[Inject] parameters cannot be optional on: {concreteType.FullName}");

                ValidateInjectParameterType(p.ParameterType, concreteType);
                parameterTypes[i] = p.ParameterType;
            }

            return new InjectionPlan(injectMethod, parameterTypes);
        }

        /// <summary>
        /// Invokes the injection method on a specified MonoBehaviour using the provided injection plan and service resolver.
        /// </summary>
        /// <param name="target">The MonoBehaviour instance on which the injection method will be invoked. Must not be null.</param>
        /// <param name="plan">The injection plan containing the method information and parameter types required for injection.</param>
        /// <param name="resolver">The service resolver used to resolve the required dependencies for the injection process.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the target, plan, or resolver is null.
        /// </exception>
        /// <exception cref="TargetInvocationException">
        /// Thrown if the injection method invocation fails.
        /// </exception>
        private void InvokeInject(MonoBehaviour target, InjectionPlan plan, IServiceResolver resolver)
        {
            Type[] types = plan.ParameterTypes;
            int count = types.Length;
            object?[] args = GetArgsBuffer(count);
            
            try
            {
                // Try to resolve arguments
                for (int i = 0; i < count; i++)
                {
                    Type dependencyType = types[i];
                    try
                    {
                        args[i] = resolver.Resolve(dependencyType);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Failed to resolve dependency '{dependencyType.FullName}' for [Inject] on {target.GetType().FullName} at '" +
                            $"{GetHierarchyPath(target.transform)} ' in scene '{target.gameObject.scene.name}'.",
                            ex
                        );
                    }
                }
                
                // Try to invoke the injection method
                try
                {
                    plan.InjectMethod.Invoke(target, args);
                }
                catch (TargetInvocationException ex) when (ex.InnerException != null)
                {
                    throw new InvalidOperationException(
                        $"[Inject] method threw on {target.GetType().FullName} at '{GetHierarchyPath(target.transform)}' in scene '" +
                        $"{target.gameObject.scene.name}'.",
                        ex.InnerException
                    );
                }
            }
            finally
            {
                for (int i = 0; i < count; i++)
                {
                    args[i] = null;
                }
            }
        }

        /// <summary>
        /// Retrieves a pre-allocated buffer of arguments based on the specified count. Buffers are reused to minimize allocations.
        /// </summary>
        /// <param name="count">The number of arguments required. Must be between 0 and 6, inclusive.</param>
        /// <returns>A buffer of the requested size that can hold argument values.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the specified count is outside the valid range of 0 to 6.
        /// </exception>
        private object?[] GetArgsBuffer(int count)
        {
            switch (count)
            {
                case 0: return Array.Empty<object?>();
                case 1: return _args1;
                case 2: return _args2;
                case 3: return _args3;
                case 4: return _args4;
                case 5: return _args5;
                case 6: return _args6;
                default:
                    throw new ArgumentOutOfRangeException(nameof(count));
            }
        }

        /// <summary>
        /// Constructs the hierarchy path of a transform by concatenating the names of the transform and its parents.
        /// </summary>
        /// <param name="transform">The transform whose hierarchy path is to be created. Cannot be null.</param>
        /// <returns>A string representing the full hierarchy path of the specified transform, starting from the root.</returns>
        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            Transform? current = transform.parent;
            
            while (current)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            
            return path;
        }

        /// <summary>
        /// Validates whether the specified parameter type is allowed for dependency injection.
        /// Throws an exception if the parameter type is classified as dependency injection infrastructure,
        /// such as resolver, registry, or container-related types.
        /// </summary>
        /// <param name="parameterType">The type of the parameter to be validated.</param>
        /// <param name="concreteType">The concrete type containing the method requesting the parameter.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the parameter type is classified as a dependency injection infrastructure type,
        /// such as IServiceResolver, IServiceRegistry, ServiceContainer, or SceneInjector.
        /// </exception>
        private static void ValidateInjectParameterType(Type parameterType, Type concreteType)
        {
            bool isDiInfrastructure =
                typeof(IServiceResolver).IsAssignableFrom(parameterType) ||
                typeof(IServiceRegistry).IsAssignableFrom(parameterType) ||
                typeof(ServiceContainer).IsAssignableFrom(parameterType) ||
                typeof(SceneInjector).IsAssignableFrom(parameterType);
            if (!isDiInfrastructure)
            {
                return;
            }
            throw new InvalidOperationException(
                $"[Inject] cannot request DI infrastructure type {parameterType.FullName} on: {concreteType.FullName}"
            );
        }
    }
}
