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
        public StateMachine(StateMachineRunner runner, MonoBehaviour monoComponent)
        {
            runner_ = runner;
            monoComponent_ = monoComponent;

            // Cache state layout lookup for specified Component's type.
            var layoutLookup = StateMappingLayoutCache.Get(monoComponent.GetType(), typeof(T));

            stateLookup_ = new Dictionary<object, StateMapping>(layoutLookup.Lookup.Count);

            foreach (var layoutPair in layoutLookup.Lookup)
            {
                var layout = layoutPair.Value;

                var mapping = new StateMapping(layout.state, layout);
                stateLookup_.Add(mapping.state, mapping);

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
            currentState_ = new StateMapping(null, StateMappingLayout.Null);

            stateChangedAt_ = Time.time;
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
            if (stateLookup_ == null)
            {
                throw new Exception("States have not been configured, please call initialized before trying to set state");
            }

            StateMapping nextState = null;
            if (!stateLookup_.TryGetValue(newState, out nextState))
            {
                throw new Exception("No state with the name " + newState.ToString() + " can be found. Please make sure you are called the correct type the statemachine was initialized with");
            }

            // Self transition.
            if ((options & TransitionOptions.AllowSelfTransition) == 0)
            {
                if (currentState_ == nextState)
                {
                    return;
                }
            }

            // Cancel any queued changes.
            if (queuedChange_ != null)
            {
                runner_.StopCoroutine(queuedChange_);
                queuedChange_ = null;
            }

            if ((options & TransitionOptions.Overwrite) == 0)
            {
                if (isInTransition_)
                {
                    // We are already exiting current state on our way to our previous target state
                    if (exitCoroutine_ != null)
                    {
                        // 최종적으로 변경한 상태로 전환되도록 함. 이게 바람직한건지?
                        // 만약, 의도치 않게 다른 상태로 전환되는 경우를 체크해야한다면...???

                        //경고를 날려주는게 좋을까?
                        //조금더 자연스럽게 처리하는 방법이 없으려나??

                        // Overwrite with our new target
                        destinationState_ = nextState;
                        return;
                    }

                    // We are already entering our previous target state.
                    // Need to wait for that to finish and call the exit routine.
                    if (enterCoroutine_ != null)
                    {
                        // Damn, I need to test this hard
                        queuedChange_ = CoWaitForPreviousTranstionAndTransitToNext(nextState);
                        runner_.StartCoroutine(queuedChange_);
                        return;
                    }
                }
            }
            else
            {
                if (currentTransition_ != null)
                {
                    runner_.StopCoroutine(currentTransition_);
                }

                if (exitCoroutine_ != null)
                {
                    runner_.StopCoroutine(exitCoroutine_);
                }

                if (enterCoroutine_ != null)
                {
                    runner_.StopCoroutine(enterCoroutine_);
                }
            }

            if ((currentState_ != null && currentState_.layout.hasExitCoroutine) || nextState.layout.hasEnterCoroutine)
            {
                isInTransition_ = true;
                currentTransition_ = CoTransitToNewState(nextState, options);
                runner_.StartCoroutine(currentTransition_);
            }
            else //Same frame transition, no coroutines are present
            {
                if (currentState_ != null)
                {
                    currentState_.exitCall();
                    currentState_.finallyCall();
                }

                lastState_ = currentState_;
                currentState_ = nextState;
                stateChangedAt_ = Time.time;

                if (currentState_ != null)
                {
                    currentState_.enterCall();

                    //TODO State.None을 정의하는게 좋지 않을까??
                    Changed?.Invoke(lastState_.state != null ? (T)lastState_.state : default(T), (T)currentState_.state);
                }

                isInTransition_ = false;
            }
        }

        private IEnumerator CoTransitToNewState(StateMapping newState, TransitionOptions options)
        {
            // Cache this so that we can overwrite it and hijack a transition.
            // Exit coroutine에서 변경될수도 있으므로, 이게 최종은 아닐 수 있음.
            destinationState_ = newState;

            if (currentState_ != null)
            {
                if (currentState_.layout.hasExitCoroutine)
                {
                    exitCoroutine_ = currentState_.exitCoroutine();

                    // Don't wait for exit if we are overwriting
                    if (exitCoroutine_ != null && (options & TransitionOptions.Overwrite) == 0)
                    {
                        yield return runner_.StartCoroutine(exitCoroutine_);
                    }

                    exitCoroutine_ = null;
                }
                else
                {
                    currentState_.exitCall();
                }

                currentState_.finallyCall();
            }

            lastState_ = currentState_;
            currentState_ = destinationState_;
            stateChangedAt_ = Time.time;

            if (currentState_ != null)
            {
                if (currentState_.layout.hasEnterCoroutine)
                {
                    enterCoroutine_ = currentState_.enterCoroutine();

                    if (enterCoroutine_ != null)
                    {
                        yield return runner_.StartCoroutine(enterCoroutine_);
                    }

                    enterCoroutine_ = null;
                }
                else
                {
                    currentState_.enterCall();
                }

                // Broadcast change only after enter transition has begun.
                //TODO State.None을 정의하는게 좋지 않을까??
                Changed?.Invoke(lastState_.state != null ? (T)lastState_.state : default(T), (T)currentState_.state);
            }

            isInTransition_ = false;
        }

        private IEnumerator CoWaitForPreviousTranstionAndTransitToNext(StateMapping nextState)
        {
            // Waiting for previous transition is completed.
            while (isInTransition_)
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
        public T LastState
        {
            get { return lastState_ != null ? (T)lastState_.state : default(T); }
        }

        /// <summary>
        /// Gets the current state ID.
        /// </summary>
        public T State
        {
            get { return currentState_.state != null ? (T)currentState_.state : default(T); }
        }

        /// <summary>
        /// Whether currently is in transition or not.
        /// </summary>
        public bool IsInTransition
        {
            get { return isInTransition_; }
        }

        /// <summary>
        /// Gets the current state map.
        /// </summary>
        public StateMapping CurrentStateMap
        {
            get { return currentState_; }
        }

        /// <summary>
        /// Gets the owner mono behaviour component.
        /// </summary>
        public MonoBehaviour Component
        {
            get { return monoComponent_; }
        }

        /// <summary>
        /// Gets the elapsed time(in seconds) since last state changed.
        /// </summary>
        public float ElapsedTimeSinceStateChanged
        {
            get { return Time.time - stateChangedAt_; }
        }



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


        //
        // Member variables
        //

        /// <summary>
        /// Event called when state changed.
        /// First parameter is old state ID.  (from)
        /// Second parameter is new state ID. (to)
        /// </summary>
        public event Action<T, T> Changed;

        /// <summary>
        /// Internal runner.
        /// </summary>
        private readonly StateMachineRunner runner_;

        /// <summary>
        /// Owner mono behaviour component
        /// </summary>
        private readonly MonoBehaviour monoComponent_;

        /// <summary>
        /// Last(previous) state mapping
        /// </summary>
        private StateMapping lastState_;
        /// <summary>
        /// Current state mapping
        /// </summary>
        private StateMapping currentState_;
        /// <summary>
        /// Destination state mapping
        /// </summary>
        private StateMapping destinationState_;

        /// <summary>
        /// Time point at state was changed.
        /// </summary>
        private float stateChangedAt_;

        private readonly Dictionary<object, StateMapping> stateLookup_;

        private bool isInTransition_ = false;
        private IEnumerator currentTransition_;
        private IEnumerator exitCoroutine_;
        private IEnumerator enterCoroutine_;
        private IEnumerator queuedChange_;
    }
}
