using System.Collections.Generic;
using System.Collections.ObjectModel; // ReadOnlyDictionary
using System.Reflection;

namespace CoFSM
{
	public class StateMappingLayoutLookup
	{
		private Dictionary<object, StateMappingLayout> lookup_ = new Dictionary<object, StateMappingLayout>();

		public StateMappingLayoutLookup()
		{
		}

		public void Add(object state, StateMappingLayout layout)
		{
			lookup_.Add(state, layout);
		}

		public StateMappingLayout Get(object state)
		{
			if (lookup_.TryGetValue(state, out StateMappingLayout layout))
			{
				return layout;
			}

			return null;
		}

		public ReadOnlyDictionary<object, StateMappingLayout> Lookup
		{
			get
			{
				return new ReadOnlyDictionary<object, StateMappingLayout>(lookup_);
			}
		}
	}

	public class StateMappingLayout
	{
		static public StateMappingLayout Null = new StateMappingLayout(null);

		public object state;

		public bool hasEnterCoroutine;
		public MethodInfo enterMethod;

		public bool hasExitCoroutine;
		public MethodInfo exitMethod;

		public MethodInfo finallyMethod;

		public MethodInfo updateMethod;
		public MethodInfo lateUpdateMethod;
		public MethodInfo fixedUpdateMethod;

		public MethodInfo onCollisionEnterMethod;

		public StateMappingLayout(object state)
		{
			this.state = state;
		}
	}
}
