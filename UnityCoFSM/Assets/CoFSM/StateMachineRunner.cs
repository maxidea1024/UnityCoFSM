using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CoFSM
{
	public class StateMachineRunner : MonoBehaviour
	{
		//
		// Public methods
		//

		public StateMachine<T> Initialize<T>(MonoBehaviour monoComponent) where T : struct, IConvertible, IComparable
		{
			var fsm = new StateMachine<T>(this, monoComponent);

			stateMachineList_.Add(fsm);

			return fsm;
		}

		public StateMachine<T> Initialize<T>(MonoBehaviour monoComponent, T startState) where T : struct, IConvertible, IComparable
		{
			var fsm = Initialize<T>(monoComponent);

			fsm.Transit(startState);

			return fsm;
		}


		//
		// Unity callback methods
		//

		void FixedUpdate()
		{
			for (int i = 0; i < stateMachineList_.Count; ++i)
			{
				var fsm = stateMachineList_[i];

				if (!fsm.IsInTransition && fsm.Component.enabled)
				{
					fsm.CurrentStateMap.fixedUpdateCall();
				}
			}
		}

		void Update()
		{
			for (int i = 0; i < stateMachineList_.Count; ++i)
			{
				var fsm = stateMachineList_[i];

				if (!fsm.IsInTransition && fsm.Component.enabled)
				{
					fsm.CurrentStateMap.updateCall();
				}
			}
		}

		void LateUpdate()
		{
			for (int i = 0; i < stateMachineList_.Count; ++i)
			{
				var fsm = stateMachineList_[i];

				if (!fsm.IsInTransition && fsm.Component.enabled)
				{
					fsm.CurrentStateMap.lateUpdateCall();
				}
			}
		}

		//void OnCollisionEnter(Collision collision)
		//{
		//	if(currentState != null && !IsInTransition)
		//	{
		//		currentState.onCollisionEnterCall(collision);
		//	}
		//}


		public static void DoNothing()
		{
			// DO NOTHING..
		}

		public static void DoNothingCollider(Collider other)
		{
			// DO NOTHING..
		}

		public static void DoNothingCollision(Collision other)
		{
			// DO NOTHING..
		}

		public static IEnumerator DoNothingCoroutine()
		{
			// DO NOTHING..
			yield break;
		}


		//
		// Member variables
		//

		private readonly List<IStateMachine> stateMachineList_ = new List<IStateMachine>();
	}


	public class StateMapping
	{
		//
		// Constructors
		//

		public StateMapping(object state, StateMappingLayout layout)
		{
			this.state = state;
			this.layout = layout;
		}


		//
		// Member variables
		//

		public object state;

		public StateMappingLayout layout;

		public Action enterCall = StateMachineRunner.DoNothing;
		public Func<IEnumerator> enterCoroutine = StateMachineRunner.DoNothingCoroutine;

		public Action exitCall = StateMachineRunner.DoNothing;
		public Func<IEnumerator> exitCoroutine = StateMachineRunner.DoNothingCoroutine;

		public Action finallyCall = StateMachineRunner.DoNothing;

		public Action updateCall = StateMachineRunner.DoNothing;
		public Action lateUpdateCall = StateMachineRunner.DoNothing;
		public Action fixedUpdateCall = StateMachineRunner.DoNothing;

		public Action<Collision> onCollisionEnterCall = StateMachineRunner.DoNothingCollision;
	}
}
