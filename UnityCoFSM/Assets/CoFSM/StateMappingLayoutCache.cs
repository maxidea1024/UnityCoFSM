using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine.Assertions;

namespace CoFSM
{
	public class StateMappingLayoutCache
	{
        static Dictionary<System.Type, StateMappingLayoutLookup> _cache = new Dictionary<System.Type, StateMappingLayoutLookup>();

		static public StateMappingLayoutLookup Get(Type componentType, Type stateEnumType)
		{
			if (_cache.TryGetValue(componentType, out StateMappingLayoutLookup lookup))
			{
				return lookup;
			}

			return BuildLayoutCache(componentType, stateEnumType);
		}

		static public StateMappingLayoutLookup BuildLayoutCache(Type componentType, Type stateEnumType)
		{
			var values = Enum.GetValues(stateEnumType);
			if (values.Length < 1)
			{
				throw new ArgumentException("Enum provided to Initialize must have at least 1 visible definition");
			}

			StateMappingLayoutLookup lookup = new StateMappingLayoutLookup();
			_cache.Add(componentType, lookup);

			for (int i = 0; i < values.Length; ++i)
			{
				var info = new StateMappingLayout((Enum)values.GetValue(i));
				lookup.Add(info.state, info);
			}

			//var methods = componentType.GetMethods(BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic);
			var methods = componentType.GetMethods(BindingFlags.Instance | /*BindingFlags.DeclaredOnly | */BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
			var separator = "_".ToCharArray();
			for (int i = 0; i < methods.Length; ++i)
			{
                var methodInfo = methods[i];

				if (methodInfo.GetCustomAttributes(typeof(CompilerGeneratedAttribute), true).Length != 0)
				{
					continue;
				}

				var names = methodInfo.Name.Split(separator);

				// Ignore functions without an underscore
				if (names.Length <= 1)
				{
					continue;
				}

                Enum key;
                if (!TryParseEnum(stateEnumType, names[0], out key))
                {
                    // Not an method as listed in the state enum
                    continue;
                }


				//TODO 인자 갯수를 매칭해주어야함. 괜시리 느려지기만 하려나??
				var targetInfo = lookup.Get(key);
                bool specified = false;

				switch (names[1])
				{
					case "Enter":
                        if (targetInfo.enterMethod == null)
                        {
                            if (methodInfo.ReturnType == typeof(IEnumerator))
                            {
                                targetInfo.hasEnterCoroutine = true;
                            }
                            else
                            {
                                Assert.IsTrue(methodInfo.ReturnType == typeof(void));
                                targetInfo.hasEnterCoroutine = false;
                            }
                            Assert.IsTrue(methodInfo.GetParameters().Length == 0);

                            targetInfo.enterMethod = methods[i];
                        }
                        specified = true;
                        break;

					case "Exit":
                        if (targetInfo.exitMethod == null)
                        {
                            if (methodInfo.ReturnType == typeof(IEnumerator))
                            {
                                targetInfo.hasExitCoroutine = true;
                            }
                            else
                            {
                                Assert.IsTrue(methodInfo.ReturnType == typeof(void));
                                targetInfo.hasExitCoroutine = false;
                            }
                            Assert.IsTrue(methodInfo.GetParameters().Length == 0);

                            targetInfo.exitMethod = methods[i];
                        }
                        specified = true;
                        break;

					case "Finally":
                        if (targetInfo.finallyMethod == null)
                        {
                            Assert.IsTrue(methodInfo.ReturnType == typeof(void));
                            Assert.IsTrue(methodInfo.GetParameters().Length == 0);
                            targetInfo.finallyMethod = methods[i];
                        }
                        specified = true;
                        break;

					case "Update":
                        if (targetInfo.updateMethod == null)
                        {
                            Assert.IsTrue(methodInfo.ReturnType == typeof(void));
                            Assert.IsTrue(methodInfo.GetParameters().Length == 0);
                            targetInfo.updateMethod = methods[i];
                        }
                        specified = true;
                        break;

					case "LateUpdate":
                        if (targetInfo.lateUpdateMethod == null)
                        {
                            Assert.IsTrue(methodInfo.ReturnType == typeof(void));
                            Assert.IsTrue(methodInfo.GetParameters().Length == 0);
                            targetInfo.lateUpdateMethod = methods[i];
                        }
                        specified = true;
                        break;

					case "FixedUpdate":
                        if (targetInfo.fixedUpdateMethod == null)
                        {
                            Assert.IsTrue(methodInfo.ReturnType == typeof(void));
                            Assert.IsTrue(methodInfo.GetParameters().Length == 0);
                            targetInfo.fixedUpdateMethod = methods[i];
                        }
                        specified = true;
                        break;

					case "OnCollisionEnter":
                        if (targetInfo.onCollisionEnterMethod == null)
                        {
                            Assert.IsTrue(methodInfo.ReturnType == typeof(void));
                            Assert.IsTrue(methodInfo.GetParameters().Length == 1);
                            Assert.IsTrue(methodInfo.GetParameters()[0].ParameterType == typeof(UnityEngine.Collision));
                            targetInfo.onCollisionEnterMethod = methods[i];
                        }
                        specified = true;
                        break;
				}

                //if (specified)
                //{
                //    UnityEngine.Debug.LogFormat("FSM : {0}.{1}_{2}", componentType.Name, names[0], names[1]);
                //}
            }

            return lookup;
		}

        static bool TryParseEnum(Type enumType, string name, out Enum parsed)
        {
            parsed = default(Enum);

            try
            {
                parsed = (Enum)Enum.Parse(enumType, name);
            }
            catch (ArgumentException)
            {
                // Not an method as listed in the state enum
                return false;
            }

            return true;
        }
	}
}
