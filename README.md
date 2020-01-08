# UnityCoFSM
State machines are a very effective way to manage game state, either on your main game play object (Game Over, Restart, Continue etc) or on individual actors and NPCs (AI behaviours, Animations, etc). The following is a simple state machine that should work well within any Unity context.

# Designed with simplicity in min
Most state machines come from the world of C# enterprise, and are wonderfully complicated or require a lot of boilerplate code. State Machines however are an incredibly useful pattern in game development, administrative overhead should never be a burden that discourages you from writing good code.

- Simple use of Enums as state definition.
- Minimal initialization - one line of code.
- Incredibly easy to add/remove states
- Uses reflection to avoid boiler plate code - only write the methods you actually need.
- Compatible with Coroutines.
- Tested on iOS and Android.

# Usage
An example project is included (Unity 5.0) to show the State Machine in action.

To use the state machine you need a few simple steps

Include the StateMachine package

```cs
using CoFSM;

public class Monster : MonoBehaviour
{

}
```

Define your states using an Enum

```cs
public enum MonsterState
{
    Idle,
    Play,
    Win,
    Lose
}
```

Create a variable to store a reference to the State Machine

```cs
StateMachine<MonsterState> fsm;
```

This is where all of the magic in the StateMachine happens: in the background it inspects your `MonoBehaviour (this)` and looks for any methods described by the convention shown below.

You can call this at any time, but generally `Awake()` is a safe choice.

You are now ready to manage state by simply calling `Transit()`

```cs
fsm.Transit(MonsterState.Init);
```

State callbacks are defined by underscore convention ( StateName_Method )

```cs
void Init_Enter()
{
	Debug.Log("We are now ready");
}

// Coroutines are supported, simply return IEnumerator
IEnumerator Play_Enter()
{
    Debug.Log("Game Starting in 3");
    yield return new WaitForSeconds(1);
    
    Debug.Log("Game Starting in 2");
    yield return new WaitForSeconds(1);
    
    Debug.Log("Game Starting in 1");
    yield return new WaitForSeconds(1);
    
    Debug.Log("Start");	
}

void Play_Update()
{
	Debug.Log("Game Playing");
}

void Play_Exit()
{
	Debug.Log("Game Over");
}
```

Currently supported methods are:

- Enter
- Exit
- FixedUpdate
- Update
- LateUpdate
- Finally


It should be easy enough to extend the source to include other Unity Methods such as `OnTriggerEnter`, `OnMouseDown` etc

These methods can be private or public. The methods themselves are all optional, so you only need to provide the ones you actually intend on using.

Couroutines are supported on `Enter` and `Exit`, simply return `IEnumerator`. This can be great way to accommodate animations. Note: `FixedUpdate`, `Update` and `LateUpdate` calls won't execute while an `Enter` or `Exit` routine is running.

`Finally` is a special method guaranteed to be called after a state has exited. This is a good place to perform any hygiene operations such as removing event listeners. Note: `Finally` does not support coroutines.

### Transitions
There is simple support for managing asynchronous state changes with long enter or exit coroutines.

```cs
fsm.Transit(States.MyNextState, TransitionOptions.Safe);
```

The default is TransitionOptions.Safe. This will always allows the current state to finish both it's enter and exit functions before transitioning to any new states.

```cs
fsm.Transit(States.MyNextState, TransitionOptions.Overwrite);
```

StateMahcine.Overwrite will cancel any current transitions, and call the next state immediately. This means any code which has yet to run in enter and exit routines will be skipped. If you need to ensure you end with a particular configuration, the finally function will always be called:

```cs
void MyCurrentState_Finally()
{
    //Reset object to desired configuration
}
```

### Dependencies
There are no dependencies, but if you're working with the source files, the tests rely on the UnityTestTools package. These are non-essential, only work in the editor, and can be deleted if you so choose.


# Implementation and Shortcomings
This implementation uses reflection to automatically bind the state methods callbacks for each state. This saves you having to write endless boilerplate and generally makes life a lot more pleasant. But of course reflection is slow, so we try minimize this by only doing it once during the call to Initialize.

For most objects this won't be a problem, but note that if you are spawning many objects during game play it might pay to make use of an object pool, and initialize objects on start up instead. (This is generally good practice anyway).

### Manual Initialization
In performance critical situations (e.g. thousands of instances) you can optimize initialization further but manually configuring the StateMachineRunner component. You will need to manually add this to a `GameObject` and then call:

```cs
StateMachines<MoonsterState> fsm = GetComponent<StateMachineRunner>().Initialize<MonsterState>(componentReference);
```

### Memory Allocation Free?
This is designed to target mobile, as such should be memory allocation free. However the same rules apply as with the rest of unity in regards to using IEnumerator and Coroutines.

### Windows Store Platforms
Due to differences in the Windows Store flavour of .Net, this is currently incompatible. More details available in this [issue](https://github.com/thefuntastic/Unity3d-Finite-State-Machine/issues/4).

## License
[MIT License](LICENSE.md)
