#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DutyCalls.Adapters.Bootstrapping.Installers
{
    /// <summary>
    /// Provides validation for installer types to ensure they remain pure and do not
    /// contain forbidden MonoBehaviour lifecycle methods.
    /// </summary>
    internal sealed class InstallerPurityValidator
    {
        private static readonly string[] _forbiddenLifecycleMethods =
        {
            "Awake",
            "OnEnable",
            "Start",
            "Update",
            "LateUpdate",
            "FixedUpdate",
            "OnDisable",
            "OnDestroy"
        };

        private readonly Dictionary<Type, string?> _violationsByType;

        public InstallerPurityValidator()
        {
            _violationsByType = new Dictionary<Type, string?>(64);
        }

        /// <summary>
        /// Validates the specified installer type to ensure it does not contain
        /// any forbidden MonoBehaviour lifecycle methods.
        /// </summary>
        /// <param name="installerType">The type of the installer to validate.</param>
        /// <exception cref="ArgumentNullException">Thrown if the provided installer type is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the installer type contains a forbidden lifecycle method.
        /// </exception>
        public void Validate(Type installerType)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (installerType == null) throw new ArgumentNullException(nameof(installerType));

            // Attempt to get the violation
            if (!_violationsByType.TryGetValue(installerType, out string? violation))
            {
                violation = FindViolation(installerType);
                _violationsByType.Add(installerType, violation);
            }

            // Exit case - no violations found
            if (violation == null) return;
            
            throw new InvalidOperationException(
                "Installer type " +
                installerType.FullName +
                " must be composition-only. Remove lifecycle method " +
                violation +
                "."
            );
#endif
        }

        /// <summary>
        /// Searches for lifecycle method violations in the given installer type.
        /// </summary>
        /// <param name="installerType">The type to validate for forbidden MonoBehaviour lifecycle methods.</param>
        /// <returns>The name of the first found forbidden lifecycle method if a violation exists; otherwise, null.</returns>
        private static string? FindViolation(Type installerType)
        {
            Type? scan = installerType;
            while (scan != null && scan != typeof(MonoBehaviour))
            {
                // Look for lifecycle methods
                for (int i = 0; i < _forbiddenLifecycleMethods.Length; i++)
                {
                    string methodName = _forbiddenLifecycleMethods[i];
                    MethodInfo? method = scan.GetMethod(
                        methodName,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly
                    );
                    
                    // Exit case - a method was found
                    if (method != null) return methodName;
                }

                scan = scan.BaseType;
            }

            return null;
        }
    }
}