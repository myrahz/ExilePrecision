using System;

namespace ExilePrecision.Core.Combat.State
{
    public class StateCoordinator
    {
        private RoutineState _currentState;
        private Exception _lastError;
        private DateTime _lastStateChange;

        public RoutineState CurrentState => _currentState;
        public Exception LastError => _lastError;
        public DateTime LastStateChange => _lastStateChange;
        public TimeSpan TimeInCurrentState => DateTime.Now - _lastStateChange;

        public StateCoordinator()
        {
            Reset();
        }

        public void SetState(RoutineState newState)
        {
            if (_currentState == newState)
                return;

            var oldState = _currentState;
            _currentState = newState;
            _lastStateChange = DateTime.Now;

            OnStateChanged(oldState, newState);
        }

        public void SetError(Exception error)
        {
            _lastError = error;
            SetState(RoutineState.Error);
        }

        public void Reset()
        {
            _currentState = RoutineState.Inactive;
            _lastError = null;
            _lastStateChange = DateTime.Now;
        }

        public bool IsInState(params RoutineState[] states)
        {
            foreach (var state in states)
            {
                if (_currentState == state)
                    return true;
            }
            return false;
        }

        public bool CanTransitionTo(RoutineState newState)
        {
            return _currentState switch
            {
                RoutineState.Inactive => newState == RoutineState.Idle,
                RoutineState.Idle => newState == RoutineState.Active || newState == RoutineState.Inactive,
                RoutineState.Active => newState == RoutineState.Paused || newState == RoutineState.Idle || newState == RoutineState.Inactive,
                RoutineState.Paused => newState == RoutineState.Active || newState == RoutineState.Idle || newState == RoutineState.Inactive,
                RoutineState.Error => newState == RoutineState.Inactive || newState == RoutineState.Idle,
                _ => false
            };
        }

        public bool TryTransition(RoutineState newState)
        {
            if (!CanTransitionTo(newState))
                return false;

            SetState(newState);
            return true;
        }

        protected virtual void OnStateChanged(RoutineState oldState, RoutineState newState)
        {
            // Override in derived classes if state change notifications are needed
        }
    }
}