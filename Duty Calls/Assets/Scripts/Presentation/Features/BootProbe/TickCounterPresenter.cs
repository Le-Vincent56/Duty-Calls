#nullable enable
using System;
using DutyCalls.Adapters.DI;
using DutyCalls.Simulation.Features.BootProbe;
using UnityEngine;

namespace DutyCalls.Presentation.Features.BootProbe
{
    /// <summary>
    /// Minimal presenter that proves "inject before Awake" and Simulation->Presentation flow.
    /// </summary>
    public sealed class TickCounterPresenter : MonoBehaviour
    {
        [SerializeField] private int currentTick;
        
        private ITickCounterQueries? _queries;
        private ITickCounterEvents? _events;
        private bool _isInjected;

        [Inject]
        public void Inject(ITickCounterQueries queries, ITickCounterEvents events)
        {
            if (_isInjected) throw new InvalidOperationException("TickCounterPresenter injected more than once.");
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _isInjected = true;
        }

        private void Awake()
        {
            // Exit case - not injected
            if (!_isInjected) throw new InvalidOperationException("TickCounterPresenter must be injected before Awake.");
        }

        private void OnEnable()
        {
            ITickCounterQueries queries = _queries ?? throw new InvalidOperationException("Queries missing.");
            ITickCounterEvents events = _events ?? throw new InvalidOperationException("Events missing.");
            
            currentTick = queries.TickIndex;
            events.TickAdvanced += OnTickAdvanced;
        }

        private void OnDisable()
        {
            ITickCounterEvents? events = _events;
            
            // Exit case - no events to unsubscribe from
            if (events == null) return;
            
            events.TickAdvanced -= OnTickAdvanced;
        }

        private void OnTickAdvanced(int tickIndex) => currentTick = tickIndex;
    }
}