#nullable enable

using System;
using DutyCalls.Adapters.DI;
using DutyCalls.Simulation.Core;
using UnityEngine;

namespace DutyCalls.Adapters.Stepping
{
    /// <summary>
    /// Unity-facing stepping driver. Uses Update() + accumulator, not FixedUpdate().
    /// Scene-loaded instances must be injected before Awake.
    /// </summary>
    public sealed class SimulationStepRunner : MonoBehaviour
    {
        [SerializeField] private int maxStepsPerFrame = 8;
        
        private ISimulationStep? _simulationStep;
        private FixedStepScheduler? _scheduler;
        private bool _isInjected;

        /// <summary>
        /// Injects dependencies into the SimulationStepRunner. This method must be called only once per instance;
        /// repeated injections are not allowed. It initializes the simulation stepping components with a specific
        /// simulation step implementation and tick rate.
        /// </summary>
        /// <param name="simulationStep">An implementation of the ISimulationStep interface, representing the core simulation logic to execute per tick. Cannot be null.</param>
        /// <param name="tickRate">The desired fixed tick rate of the simulation. Determines how frequently simulation steps are executed. Tick rate must be valid.</param>
        /// <exception cref="InvalidOperationException">Thrown if the method is called more than once or if maxStepsPerFrame is less than or equal to zero.</exception>
        /// <exception cref="ArgumentNullException">Thrown if the simulationStep parameter is null.</exception>
        [Inject]
        public void Inject(ISimulationStep simulationStep, SimulationTickRate tickRate)
        {
            if (_isInjected) throw new InvalidOperationException("SimulationStepRunner injected more than once.");
            if (maxStepsPerFrame <= 0) throw new InvalidOperationException("SimulationStepRunner maxStepsPerFrame must be > 0.");

            _simulationStep = simulationStep ?? throw new ArgumentNullException(nameof(simulationStep));
            _scheduler = new FixedStepScheduler(tickRate, maxStepsPerFrame);
            _isInjected = true;
        }
        
        private void Awake()
        {
            // Exit case - already injected
            if (_isInjected) return;
            
            throw new InvalidOperationException("SimulationStepRunner must be injected before Awake.");
        }

        private void Update()
        {
            FixedStepScheduler scheduler = _scheduler ?? throw new InvalidOperationException("Scheduler missing.");
            ISimulationStep simulationStep = _simulationStep ?? throw new InvalidOperationException("Simulation step missing.");
            
            int steps = scheduler.AddTime(Time.deltaTime);
            
            for (int i = 0; i < steps; i++)
            {
                simulationStep.Step();
            }
        }
    }
}