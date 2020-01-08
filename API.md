# UnityCoFSM API

### TransitionOptions
* TransitionOptions.Safe
* TransitionOptions.AllowSelfTransition
* TransitionOptions.Overwrite


### StateMachine < T >
* StateMachine< T >.Transit(T newState)
* StateMachine< T >.TransitSelf(T newState)
* StateMachine< T >.TransitOverwrite(T newState)
* StateMachine< T >.TransitForce(T newState)
* StateMachine< T >.Transit(T newState, TransitionOptions options)
* T StateMachine< T >.LastState
* T StateMachine< T >.State
* bool StateMachine< T >.IsInTransition
* StateMachine< T >.StateMapping StateMachine< T >.CurrentStateMap
* MonoBehaviour StateMachine< T >.Component
* float StateMachine< T >.ElapsedTimeSinceStateChanged
* StateMachine<T> StateMachine< T >.Initialize(MonoBehaviour monoComponent)
* StateMachine<T> StateMachine< T >.Initialize(MonoBehaviour monoComponent, T startState)

### StateMachineRunner

