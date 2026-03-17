#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DutyCalls.Adapters.Bootstrapping.Installers
{
    /// <summary>
    /// Responsible for collecting and sorting installer objects from game scenes.
    /// Provides functionality to extract specific installers from a scene and sort them
    /// based on their defined order.
    /// </summary>
    internal sealed class InstallerCollector
    {
        private readonly List<GameObject> _roots;
        private readonly List<MonoBehaviour> _behaviours;
        private readonly InstallerPurityValidator _purityValidator;

        public InstallerCollector()
        {
            _roots = new List<GameObject>(128);
            _behaviours = new List<MonoBehaviour>(512);
            _purityValidator = new InstallerPurityValidator();
        }

        public void CollectFromScene<TInstaller>(Scene scene, List<TInstaller> results) where TInstaller : class, IOrderedInstaller
        {
            // Exit case - the scene is invalid
            if (!scene.IsValid())
                throw new InvalidOperationException("Cannot collect installers from an invalid scene.");

            // Prepare the buffer
            results.Clear();
            _roots.Clear();
            
            scene.GetRootGameObjects(_roots);
            
            for (int i = 0; i < _roots.Count; i++)
            {
                GameObject root = _roots[i];
                
                // Prepare the buffer
                _behaviours.Clear();
                
                root.GetComponentsInChildren(true, _behaviours);
                for (int j = 0; j < _behaviours.Count; j++)
                {
                    MonoBehaviour behaviour = _behaviours[j];
                    
                    // Skip the child is inactive or null
                    if (!behaviour) continue;

                    TInstaller? installer = behaviour as TInstaller;
                    
                    // Skip if the installer is null
                    if (installer == null) continue;
                    
                    _purityValidator.Validate(behaviour.GetType());
                    results.Add(installer);
                }
            }
        }

        /// <summary>
        /// Sorts the provided list of installers in ascending order based on their order value.
        /// </summary>
        /// <typeparam name="TInstaller">The type of installer, which must implement <see cref="IOrderedInstaller"/>.</typeparam>
        /// <param name="installers">The list of installers to be sorted. The list will be modified in place.</param>
        public void SortByOrder<TInstaller>(List<TInstaller> installers) where TInstaller : IOrderedInstaller
        {
            // Stable insertion sort (lists are expected to be small).
            for (int i = 1; i < installers.Count; i++)
            {
                TInstaller key = installers[i];
                int keyOrder = key.Order;
                int j = i - 1;
                
                while (j >= 0 && installers[j].Order > keyOrder)
                {
                    installers[j + 1] = installers[j];
                    j--;
                }

                installers[j + 1] = key;
            }
        }
    }
}