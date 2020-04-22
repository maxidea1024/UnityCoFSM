using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = System.Object;

namespace CoFSM
{
    /// <summary>
    /// Transition options.
    /// </summary>
    [System.Flags]
    public enum TransitionOptions
    {
        /// <summary>
        /// No self and no overwrite pending transition.
        /// </summary>
        Safe = 0,

        /// <summary>
        /// Allow self transition.
        /// </summary>
        AllowSelfTransition = 0x01,

        /// <summary>
        /// Overwrite pending transition.
        /// </summary>
        Overwrite = 0x02,
    }

    public interface IStateMachine
    {
        MonoBehaviour Component { get; }

        StateMapping CurrentStateMap { get; }

        bool IsInTransition { get; }
    }

    public class StateMachine<T> : IStateMachine where T : struct, IConvertible, IComparable
    {
        /// <summary>
        /// Event called when state changed.
        /// First parameter is old state ID.  (from)
        /// Second parameter is new state ID. (to)
        /// </summary>
        public event Action<T, T> Changed;

        /// <summary>
        /// Internal runner.
        /// </summary>
        private readonly StateMachineRunner _runner;

        /// <summary>
        /// Owner mono behaviour component
        /// </summary>
        private readonly MonoBehaviour _monoComponent;

        /// <summary>
        /// Last(previous) state mapping
        /// </summary>
        private StateMapping _lastState;
        /// <summary>
        /// Current state mapping
        /// </summary>
        private StateMapping _currentState;
        /// <summary>
        /// Destination state mapping
        /// </summary>
        private StateMapping _destinationState;

        /// <summary>
        /// Time point at state was changed.
        /// </summary>
        private float _stateChangedAt;

        private readonly Dictionary<object, StateMapping> _stateLookup;

        private bool _isInTransition = false;
        private IEnumerator _currentTransition;
        private IEnumerator _exitCoroutine;
        private IEnumerator _enterCoroutine;
        private IEnumerator _queuedChange;

        public StateMachine(StateMachineRunner runner, MonoBehaviour monoComponent)
        {
            _runner = runner;
            _monoComponent = monoComponent;

            // Cache state layout lookup for specified Component's type.
            var layoutLookup = StateMappingLayoutCache.Get(monoComponent.GetType(), typeof(T));

            _stateLookup = new Dictionary<object, StateMapping>(layoutLookup.Lookup.Count);

            foreach (var layoutPair in layoutLookup.Lookup)
            {
                var layout = layoutPair.Value;

                var mapping = new StateMapping(layout.state, layout);
                _stateLookup.Add(mapping.state, mapping);

                // *_Enter callback
                if (layout.enterMethod != null)
                {
                    if (layout.hasEnterCoroutine)
                    {
                        mapping.enterCoroutine = CreateDelegate<Func<IEnumerator>>(layout.enterMethod, monoComponent);
                    }
                    else
                    {
                        mapping.enterCall = CreateDelegate<Action>(layout.enterMethod, monoComponent);
                    }
                }

                // *_Exit callback
                if (layout.exitMethod != null)
                {
                    if (layout.hasExitCoroutine)
                    {
                        mapping.exitCoroutine = CreateDelegate<Func<IEnumerator>>(layout.exitMethod, monoComponent);
                    }
                    else
                    {
                        mapping.exitCall = CreateDelegate<Action>(layout.exitMethod, monoComponent);
                    }
                }

                // *_Finally callback
                if (layout.finallyMethod != null)
                {
                    mapping.finallyCall = CreateDelegate<Action>(layout.finallyMethod, monoComponent);
                }

                // *_Update callback
                if (layout.updateMethod != null)
                {
                    mapping.updateCall = CreateDelegate<Action>(layout.updateMethod, monoComponent);
                }

                // *_LateUpdate callback
                if (layout.lateUpdateMethod != null)
                {
                    mapping.lateUpdateCall = CreateDelegate<Action>(layout.lateUpdateMethod, monoComponent);
                }

                // *_FixedUpdate callback
                if (layout.fixedUpdateMethod != null)
                {
                    mapping.fixedUpdateCall = CreateDelegate<Action>(layout.fixedUpdateMethod, monoComponent);
                }

                // *_OnCollision callback
                //if (layout.onCollisionEnterMethod != null)
                //{
                //	mapping.onCollisionEnterCall = CreateDelegate<Action>(layout.onCollisionEnterMethod, monoComponent);
                //}
            }

            // Create nil state mapping
            _currentState = new StateMapping(null, StateMappingLayout.Null);

            _stateChangedAt = Time.time;
        }

        private V CreateDelegate<V>(MethodInfo method, Object target) where V : class
        {
            var ret = (Delegate.CreateDelegate(typeof(V), target, method) as V);

            if (ret == null)
            {
                throw new ArgumentException("Unabled to create delegate for method called " + method.Name);
            }

            return ret;
        }

        public void Transit(T newState)
        {
            Transit(newState, TransitionOptions.Safe);
        }

        public void TransitSelf(T newState)
        {
            Transit(newState, TransitionOptions.AllowSelfTransition);
        }

        public void TransitOverwrite(T newState)
        {
            Transit(newState, TransitionOptions.Overwrite);
        }

        public void TransitForce(T newState)
        {
            Transit(newState, TransitionOptions.Overwrite | TransitionOptions.AllowSelfTransition);
        }

        public void Transit(T newState, TransitionOptions options)
        {
            if (_stateLookup == null)
            {
                throw new Exception("States have not been configured, please call initialized before trying to set state");
            }

            StateMapping nextState = null;
            if (!_stateLookup.TryGetValue(newState, out nextState))
            {
                throw new Exception("No state with the name " + newState.ToString() + " can be found. Please make sure you are called the correct type the statemachine was initialized with");
            }

            // Self transition.
            if ((options & TransitionOptions.AllowSelfTransition) == 0)
            {
                if (_currentState == nextState)
                {
                    return;
                }
            }

            // Cancel any queued changes.
            if (_queuedChange != null)
            {
                _runner.StopCoroutine(_queuedChange);
                _queuedChange = null;
            }

            if ((options & TransitionOptions.Overwrite) == 0)
            {
                if (_isInTransition)
                {
                    // We are already exiting current state on our way to our previous target state
                    if (_exitCoroutine != null)
                    {
                        // Overwrite with our new target
                        _destinationState = nextState;
                        return;
                    }

                    // We are already entering our previous target state.
                    // Need to wait for that to finish and call the exit routine.
                    if (_enterCoroutine != null)
                    {
                        // Damn, I need to test this hard
                        _queuedChange = CoWaitForPreviousTranstionAndTransitToNext(nextState);
                        _runner.StartCoroutine(_queuedChange);
                        return;
                    }
                }
            }
            else
            {
                if (_currentTransition != null)
                {
                    _runner.StopCoroutine(_currentTransition);
                }

                if (_exitCoroutine != null)
                {
                    _runner.StopCoroutine(_exitCoroutine);
                }

                if (_enterCoroutine != null)
                {
                    _runner.StopCoroutine(_enterCoroutine);
                }
            }

            if ((_currentState != null && _currentState.layout.hasExitCoroutine) || nextState.layout.hasEnterCoroutine)
            {
                _isInTransition = true;
                _currentTransition = CoTransitToNewState(nextState, options);
                _runner.StartCoroutine(_currentTransition);
            }
            else //Same frame transition, no coroutines are present
            {
                if (_currentState != null)
                {
                    _currentState.exitCall();
                    _currentState.finallyCall();
                }

                _lastState = _currentState;
                _currentState = nextState;
                _stateChangedAt = Time.time;

                if (_currentState != null)
                {
                    _currentState.enterCall();

                    Changed?.Invoke(_lastState.state != null ? (T)_lastState.state : default(T), (T)_currentState.state);
                }

                _isInTransition = false;
            }
        }

        private IEnumerator CoTransitToNewState(StateMapping newState, TransitionOptions options)
        {
            // Cache this so that we can overwrite it and hijack a transition.
            _destinationState = newState;

            if (_currentState != null)
            {
                if (_currentState.layout.hasExitCoroutine)
                {
                    _exitCoroutine = _currentState.exitCoroutine();

                    // Don't wait for exit if we are overwriting
                    if (_exitCoroutine != null && (options & TransitionOptions.Overwrite) == 0)
                    {
                        yield return _runner.StartCoroutine(_exitCoroutine);
                    }

                    _exitCoroutine = null;
                }
                else
                {
                    _currentState.exitCall();
                }

                _currentState.finallyCall();
            }

            _lastState = _currentState;
            _currentState = _destinationState;
            _stateChangedAt = Time.time;

            if (_currentState != null)
            {
                if (_currentState.layout.hasEnterCoroutine)
                {
                    _enterCoroutine = _currentState.enterCoroutine();

                    if (_enterCoroutine != null)
                    {
                        yield return _runner.StartCoroutine(_enterCoroutine);
                    }

                    _enterCoroutine = null;
                }
                else
                {
                    _currentState.enterCall();
                }

                // Broadcast change only after enter transition has begun.
                Changed?.Invoke(_lastState.state != null ? (T)_lastState.state : default(T), (T)_currentState.state);
            }

            _isInTransition = false;
        }

        private IEnumerator CoWaitForPreviousTranstionAndTransitToNext(StateMapping nextState)
        {
            // Waiting for previous transition is completed.
            while (_isInTransition)
            {
                yield return null;
            }

            // Workaround for unwanted state tansition churns.
            while (Time.timeScale <= 0f)
            {
                yield return null;
            }

            Transit((T)nextState.state);
        }


        //
        // Properties
        //

        /// <summary>
        /// Gets the last state ID.
        /// </summary>
        public T LastState => _lastState != null ? (T)_lastState.state : default(T);

        /// <summary>
        /// Gets the current state ID.
        /// </summary>
        public T State => _currentState.state != null ? (T)_currentState.state : default(T);77

        /// <summary>
        /// Whether currently is in transition or not.
        /// </summary>
        public bool IsInTransition => _isInTransition;

        /// <summary>
        /// Gets the current state map.
        /// </summary>
        public StateMapping CurrentStateMap => _currentState;

        /// <summary>
        /// Gets the owner mono behaviour component.
        /// </summary>
        public MonoBehaviour Component => _monoComponent;

        /// <summary>
        /// Gets the elapsed time(in seconds) since last state changed.
        /// </summary>
        public float ElapsedTimeSinceStateChanged => Time.time - _stateChangedAt;


        //
        // Static Methods
        //

        /// <summary>
        /// Initialize this state-machine.
        /// </summary>
        /// <param name="monoComponent">The owner mono behaviour component</param>
        /// <returns>Created state-machine</returns>
        public static StateMachine<T> Initialize(MonoBehaviour monoComponent)
        {
            var runner = monoComponent.GetComponent<StateMachineRunner>();
            if (runner == null)
            {
                runner = monoComponent.gameObject.AddComponent<StateMachineRunner>();
            }

            return runner.Initialize<T>(monoComponent);
        }

        /// <summary>
        /// Initialize this state-machine with initial state.
        /// </summary>
        /// <param name="monoComponent">The owner mono behaviour component</param>
        /// <param name="startState">The initial state</param>
        /// <returns>Created state-machine</returns>
        public static StateMachine<T> Initialize(MonoBehaviour monoComponent, T startState)
        {
            var runner = monoComponent.GetComponent<StateMachineRunner>();
            if (runner == null)
            {
                runner = monoComponent.gameObject.AddComponent<StateMachineRunner>();
            }

            return runner.Initialize<T>(monoComponent, startState);
        }
    }
}
