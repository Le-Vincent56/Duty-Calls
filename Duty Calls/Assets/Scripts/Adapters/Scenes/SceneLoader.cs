#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using DutyCalls.Adapters.Utilities;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace DutyCalls.Adapters.Scenes
{
    /// <summary>
    /// Handles the process of loading scenes asynchronously in a Unity environment;
    /// Implements the <see cref="ISceneLoader"/> interface for scene management operations.
    /// </summary>
    public sealed class SceneLoader : MonoBehaviour, ISceneLoader
    {
        /// <summary>
        /// Represents a handle to a loaded scene, encapsulating information about the scene's load operation,
        /// its persistence, and its associated type. Used internally by <see cref="SceneLoader"/> for scene management.
        /// </summary>
        private sealed class LoadedSceneHandle
        {
            public AsyncOperationHandle<SceneInstance> Handle { get; }
            public bool IsPersistent { get; }
            public SceneType SceneType { get; }

            public LoadedSceneHandle(AsyncOperationHandle<SceneInstance> handle, bool isPersistent, SceneType sceneType)
            {
                Handle = handle;
                IsPersistent = isPersistent;
                SceneType = sceneType;
            }
        }

        private readonly Dictionary<SceneGroup, List<LoadedSceneHandle>> _loadedGroups = new Dictionary<SceneGroup, List<LoadedSceneHandle>>();

        private readonly Observable<LoadProgress> _progress = new Observable<LoadProgress>();
        private bool _isLoading;

        public Utilities.IObservable<LoadProgress> Progress => _progress;
        
        private void OnDestroy()
        {
            foreach (KeyValuePair<SceneGroup, List<LoadedSceneHandle>> group in _loadedGroups)
            {
                for (int i = 0; i < group.Value.Count; i++)
                {
                    AsyncOperationHandle<SceneInstance> handle = group.Value[i].Handle;

                    // Skip if the handle is invalid
                    if (!handle.IsValid()) continue;

                    Addressables.UnloadSceneAsync(handle);
                }
            }

            // Clear the cache
            _loadedGroups.Clear();
        }

        /// <summary>
        /// Asynchronously loads scenes based on the specified scene load request.
        /// </summary>
        /// <param name="request">The scene load request containing details about the scenes to be loaded.</param>
        /// <returns>A task representing the asynchronous operation, which resolves to a <see cref="SceneLoadResult"/> containing the outcome of the load operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if a scene load operation is already in progress.</exception>
        public async Task<SceneLoadResult> LoadAsync(SceneLoadRequest request)
        {
            // Exit case - the Scene Loader is already loading scenes
            if (_isLoading)
            {
                throw new InvalidOperationException(
                    "Scene load is already in progress. Wait for the current load to complete before starting a new one"
                );
            }

            // Set loading to true
            _isLoading = true;

            // Reset progress observable for new load
            _progress.Reset();

            // Track loading times using Stopwatch
            Stopwatch stopwatch = Stopwatch.StartNew();

            // Initialize the result
            SceneLoadResult result = new SceneLoadResult
            {
                LoadedGroup = request.SceneGroup,
                LoadedScenes = new List<string>(),
                SkippedScenes = new List<string>(),
                Errors = new List<Exception>(),
                RangeTimings = new Dictionary<string, float>()
            };

            try
            {
                await ExecutePreTransitionAsync(request);
                await ExecuteUnloadPhaseAsync(result);
                await ExecuteLoadAndActivatePhaseAsync(request, result);
                await ExecutePostTransitionAsync(request);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Scene load failed: {ex}");
                result.Errors.Add(ex);
                throw;
            }
            finally
            {
                // Store the total load time
                stopwatch.Stop();
                result.TotalLoadTime = (float)stopwatch.Elapsed.TotalSeconds;

                // Stop loading
                _isLoading = false;

                // Clean up progress and subscribers
                _progress.Complete();
            }

            return result;
        }

        /// <summary>
        /// Executes the pre-transition phase of the scene loading process asynchronously if a pre-transition is defined in the request.
        /// </summary>
        /// <param name="request">The scene load request containing details about the scenes to be loaded and the pre-transition to be executed.</param>
        /// <returns>A task representing the asynchronous operation for executing the pre-transition phase.</returns>
        private async Task ExecutePreTransitionAsync(SceneLoadRequest request)
        {
            // Exit case - no pre-transition is defined
            if (request.PreTransition == null) return;

            await request.PreTransition.PlayAsync(_progress);
        }

        /// <summary>
        /// Executes the unloading phase of the scene transition process, releasing previously loaded scenes and updating progress.
        /// </summary>
        /// <param name="result">An instance of <see cref="SceneLoadResult"/> that captures the results and timings of the unload process.</param>
        /// <returns>A task representing the asynchronous unload operation.</returns>
        private async Task ExecuteUnloadPhaseAsync(SceneLoadResult result)
        {
            Stopwatch rangeStopwatch = Stopwatch.StartNew();
            List<SceneGroup> groupsToUnload = new List<SceneGroup>(_loadedGroups.Keys);

            for (int i = 0; i < groupsToUnload.Count; i++)
            {
                SceneGroup group = groupsToUnload[i];

                // Skip if the handles cannot be retrieved
                if (!_loadedGroups.TryGetValue(group, out List<LoadedSceneHandle> handles)) continue;

                List<LoadedSceneHandle> persistent = new List<LoadedSceneHandle>();
                for (int j = 0; j < handles.Count; j++)
                {
                    LoadedSceneHandle record = handles[j];

                    // Track if the handle is persistent
                    if (record.IsPersistent)
                    {
                        persistent.Add(record);
                        continue;
                    }

                    AsyncOperationHandle<SceneInstance> handle = record.Handle;

                    // Skip if the handle is invalid
                    if (!handle.IsValid()) continue;

                    try
                    {
                        await Addressables.UnloadSceneAsync(handle).Task;
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError("Failed to unload scene: " + ex);
                        result.Errors.Add(ex);
                    }
                }

                if (persistent.Count > 0) _loadedGroups[group] = persistent;
                else _loadedGroups.Remove(group);
            }

            _progress.OnNext(new LoadProgress
            {
                NormalizedProgress = 0.25f,
                CurrentRange = "Unload",
                RangeProgress = 1.0f,
                CurrentOperation = "Unloaded Previous Scenes"
            });

            rangeStopwatch.Stop();
            result.RangeTimings["Unload"] = (float)rangeStopwatch.Elapsed.TotalSeconds;
        }

        /// <summary>
        /// Asynchronously handles the loading and activation of scenes based on the specified request and updates the load progress.
        /// </summary>
        /// <param name="request">The scene load request containing the group of scenes to be loaded and associated settings.</param>
        /// <param name="result">The result object to be updated with details of the executed scene load operation.</param>
        /// <returns>A task representing the asynchronous operation of loading and activating scenes.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the scene group in the request is empty or invalid.</exception>
        private async Task ExecuteLoadAndActivatePhaseAsync(SceneLoadRequest request, SceneLoadResult result)
        {
            SceneGroup.SceneEntry[] entries = request.SceneGroup.Scenes;
            int sceneCount = entries.Length;
            
            if (sceneCount <= 0)
            {
                _progress.OnNext(new LoadProgress
                {
                    NormalizedProgress = 0.90f,
                    CurrentRange = "Load",
                    RangeProgress = 1.0f,
                    CurrentOperation = "No Scenes To Load"
                });
                return;
            }

            // Find the owner (gameplay) scene
            bool requireOwner = !request.MarkScenesAsPersistent;
            int ownerIndex = requireOwner ? FindOwnerSceneIndex(entries) : -1;
            
            // Build the activation order
            List<int> loadOrder = BuildActivationOrder(entries, ownerIndex, requireOwner);
            List<LoadedSceneHandle> records = new List<LoadedSceneHandle>(sceneCount);
            _loadedGroups[request.SceneGroup] = records;
            
            for (int i = 0; i < loadOrder.Count; i++)
            {
                int index = loadOrder[i];
                SceneGroup.SceneEntry entry = entries[index];
                
                // Exit case - if the entry's scene reference is null
                if (entry.SceneReference == null)
                {
                    throw new InvalidOperationException(
                        "SceneEntry.SceneReference is null at index " +
                        index +
                        " in SceneGroup '" +
                        request.SceneGroup.name +
                        "'."
                    );
                }

                float startProgress = i / (float)sceneCount;
                
                _progress.OnNext(new LoadProgress
                {
                    NormalizedProgress = 0.25f + (startProgress * 0.70f),
                    CurrentRange = "Load",
                    RangeProgress = startProgress,
                    CurrentOperation = "Loading Scene " + (i + 1) + " of " + sceneCount
                });
                
                // Set the load scene handle
                AsyncOperationHandle<SceneInstance> handle = Addressables.LoadSceneAsync(
                    entry.SceneReference,
                    LoadSceneMode.Additive,
                    true
                );
                
                await handle.Task;
                
                SceneInstance instance = handle.Result;
                records.Add(new LoadedSceneHandle(handle, request.MarkScenesAsPersistent, entry.SceneType));
                result.LoadedScenes.Add(instance.Scene.name);
                
                if (requireOwner && index == ownerIndex)
                {
                    Scene ownerScene = instance.Scene;
                    
                    // Exit case - the owner scene is not valid or not loaded
                    if (!ownerScene.IsValid() || !ownerScene.isLoaded)
                        throw new InvalidOperationException("Owner (SceneType.Gameplay) scene failed to load.");

                    SceneManager.SetActiveScene(ownerScene);
                }

                float completedProgress = (i + 1) / (float)sceneCount;
                
                _progress.OnNext(new LoadProgress
                {
                    NormalizedProgress = 0.25f + (completedProgress * 0.70f),
                    CurrentRange = "Load",
                    RangeProgress = completedProgress,
                    CurrentOperation = "Loaded " + instance.Scene.name
                });
            }

            _progress.OnNext(new LoadProgress
            {
                NormalizedProgress = 1.0f,
                CurrentRange = "Complete",
                RangeProgress = 1.0f,
                CurrentOperation = "Scene Load Complete"
            });
        }

        /// <summary>
        /// Executes the post-transition phase of the scene loading process if defined in the provided request
        /// </summary>
        /// <param name="request">The scene load request containing the details of the post-transition phase to execute</param>
        /// <returns>A task that represents the asynchronous operation of executing the post-transition phase</returns>
        private async Task ExecutePostTransitionAsync(SceneLoadRequest request)
        {
            // Exit case - no post-transition is defined
            if (request.PostTransition == null) return;

            await request.PostTransition.PlayAsync(_progress);
        }

        /// <summary>
        /// Determines the index of the owner scene within the array of scene entries.
        /// </summary>
        /// <param name="entries">The array of scene entries to evaluate.</param>
        /// <returns>The index of the owner scene, which is the scene marked as <see cref="SceneType.Gameplay"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if there is not exactly one scene marked as <see cref="SceneType.Gameplay"/> in the provided entries.
        /// </exception>
        private static int FindOwnerSceneIndex(SceneGroup.SceneEntry[] entries)
        {
            int ownerIndex = -1;
            int ownerCount = 0;

            for (int i = 0; i < entries.Length; i++)
            {
                // Exit case - the entry is not a gameplay scene
                if (entries[i].SceneType != SceneType.Gameplay) continue;

                ownerIndex = i;
                ownerCount++;
            }

            // Exit case - more than one owner scene
            if (ownerCount != 1)
            {
                throw new InvalidOperationException(
                    $"SceneGroup must contain exactly one SceneType.Gameplay entry (owner). Found: {ownerCount}" 
                );
            }

            return ownerIndex;
        }

        /// <summary>
        /// Constructs an activation order for scenes based on the specified entries, prioritizing certain scene types and optionally including an owner scene.
        /// </summary>
        /// <param name="entries">The array of scene entries from which the activation order is determined.</param>
        /// <param name="ownerIndex">The index of the owner scene in the entries array.</param>
        /// <param name="includeOwner">A boolean indicating whether the owner scene should be included in the activation order.</param>
        /// <returns>A list of integers representing the indices of scenes in the specified activation order.</returns>
        private static List<int> BuildActivationOrder(
            SceneGroup.SceneEntry[] entries,
            int ownerIndex,
            bool includeOwner
        )
        {
            List<int> order = new List<int>(entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                // Skip if the entry is not a system scene
                if (entries[i].SceneType != SceneType.Systems) continue;
                
                order.Add(i);
            }

            if (includeOwner) order.Add(ownerIndex);

            for (int i = 0; i < entries.Length; i++)
            {
                // Skip if including the owner and the entry is the owner scene (already recorded)
                if (includeOwner && i == ownerIndex) continue;

                // Skip if the entry is a system scene (already recorded)
                if (entries[i].SceneType == SceneType.Systems) continue;

                order.Add(i);
            }

            return order;
        }
    }
}