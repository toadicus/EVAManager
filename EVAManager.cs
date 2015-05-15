// EVAManager
//
// EVAManager.cs
//
// Copyright © 2014, toadicus
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without modification,
// are permitted provided that the following conditions are met:
//
// 1. Redistributions of source code must retain the above copyright notice,
//    this list of conditions and the following disclaimer.
//
// 2. Redistributions in binary form must reproduce the above copyright notice,
//    this list of conditions and the following disclaimer in the documentation and/or other
//    materials provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES,
// INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
// SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
// WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using KSP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ToadicusTools;
using UnityEngine;

namespace EVAManager
{
	[KSPAddon(KSPAddon.Startup.MainMenu, true)]
	public class EVAManager : MonoBehaviour
	{
		private const string patchPattern = @"^((DELETE|EDIT)_)?EVA_([a-zA-Z_]+)(\[(.+)\])?";
		private const int operatorIdx = 2;
		private const int classIdx = 3;
		private const int nameIdx = 5;

		private const string empty = "";

		private const string MODULE = "MODULE";
		private const string RESOURCE = "RESOURCE";

		private List<ConfigAction> evaConfigs;
		private List<ConfigAction> passQueue;

		private List<Part> evaParts;

		private Pass pass;

		public void Awake()
		{
			pass = Pass.Collect;
			this.passQueue = new List<ConfigAction>();
			this.evaParts = new List<Part>();
		}

		public virtual void Update()
		{
			if (!PartLoader.Instance.IsReady() || PartResourceLibrary.Instance == null)
			{
				return;
			}

			#if DEBUG
			Tools.DebugLogger log;
			#endif

			if (this.passQueue.Count > 0 && this.evaConfigs != null)
			{
				this.evaConfigs.AddRange(this.passQueue);

				this.passQueue.Clear();
			}

			ConfigAction action;

			switch (pass)
			{
				case Pass.Collect:
					AvailablePart loadedPart;
					for (int idx = 0; idx < PartLoader.LoadedPartsList.Count; idx++)
					{
						loadedPart = PartLoader.LoadedPartsList[operatorIdx];
						string lowerName = loadedPart.name.ToLower();

						if (lowerName == "kerbaleva" || lowerName == "kerbalevafemale")
						{
							this.LogDebug("Found {0}", loadedPart.name);

							evaParts.Add(loadedPart.partPrefab);

							#if DEBUG
							log = Tools.DebugLogger.New(this);

							log.AppendLine("Modules before run:");

							foreach (var m in evaPart.GetComponents<PartModule>())
							{
								log.Append('\t');
								log.Append(m.GetType().Name);
								log.Append('\n');
							}

							log.AppendLine("Resources before run:");

							foreach (var r in evaPart.GetComponents<PartResource>())
							{
								log.Append('\t');
								log.AppendFormat("Name: {0}, amount: {1}, maxAmount: {2}",
									r.resourceName, r.amount, r.maxAmount);
								log.Append('\n');
							}

							log.Print();
							#endif

							if (this.evaParts.Count == 2)
							{
								break;
							}
						}
					}

					evaConfigs = new List<ConfigAction>();

					Regex rgx = new Regex(patchPattern);

					UrlDir.UrlConfig urlConfig;
					IEnumerator<UrlDir.UrlConfig> enumerator = GameDatabase.Instance.root.AllConfigs.GetEnumerator();
					while (enumerator.MoveNext())
					{
						urlConfig = enumerator.Current;
						Tools.PostDebugMessage(
							this,
							"Checking urlconfig; name: {0}, type: {1}, config.name: {2}",
							urlConfig.name,
							urlConfig.type,
							urlConfig.config.name);

						Match match = rgx.Match(urlConfig.type);

						this.LogDebug("Found {0}match for {1}{2}",
							!match.Success ? "no " : "",
							urlConfig.type,
							!match.Success ? "" : string.Format(
								"\nOp: {0}, Class: {1}, Name: {2}",
								match.Groups[operatorIdx],
								match.Groups[classIdx],
								match.Groups[nameIdx])
						);

						if (match.Success)
						{
							string op = match.Groups[operatorIdx].Value;
							string classType = match.Groups[classIdx].Value;
							string matchName = match.Groups[nameIdx].Value;

							evaConfigs.Add(new ConfigAction(op, classType, matchName, urlConfig.config));
						}
					}

					pass = Pass.Delete;
					break;
				case Pass.Delete:
					for (int idx = 0; idx < this.evaConfigs.Count; idx++)
					{
						action = this.evaConfigs[idx];
						if (action.Operator == "DELETE")
						{
							this.LogDebug("Trying delete action on {0}", action);

							if (action.MatchName == string.Empty)
							{
								this.LogWarning("Match name required for 'delete' action but not present; ignoring.");
								continue;
							}

							switch (action.ClassType)
							{
								case MODULE:
									this.delModuleByName(evaParts[0], action.MatchName);
									this.delModuleByName(evaParts[1], action.MatchName);
									break;
								case RESOURCE:
									this.delResourceByName(evaParts[0], action.MatchName);
									this.delResourceByName(evaParts[1], action.MatchName);
									break;
								default:
									this.LogWarning("Class type '{0}' not implemented for 'delete' action.",
										action.ClassType);
									continue;
							}
						}
					}

					pass = Pass.Edit;
					break;
				case Pass.Edit:
					for (int idx = 0; idx < this.evaConfigs.Count; idx++)
					{
						action = this.evaConfigs[idx];
						if (action.Operator == "EDIT")
						{
							this.LogDebug("Trying edit action on {0}", action);

							if (action.MatchName == string.Empty)
							{
								this.LogWarning("Match name required for 'edit' action but not present; ignoring.");
								continue;
							}

							switch (action.ClassType)
							{
								case MODULE:
									this.editModuleByNameFromConfig(evaParts[0], action.MatchName, action.Node);
									this.editModuleByNameFromConfig(evaParts[1], action.MatchName, action.Node);
									break;
								case RESOURCE:
									this.editResourceByNameFromConfig(evaParts[0], action.MatchName, action.Node);
									this.editResourceByNameFromConfig(evaParts[1], action.MatchName, action.Node);
									break;
								default:
									this.LogWarning("Class type '{0}' not implemented for 'delete' action.",
										action.ClassType);
									continue;
							}
						}
					}
					pass = Pass.Insert;
					break;
				case Pass.Insert:
					for (int idx = 0; idx < this.evaConfigs.Count; idx++)
					{
						action = this.evaConfigs[idx];
						if (action.Operator == empty)
						{
							if (action.MatchName != string.Empty)
							{
								this.LogWarning("match name ('{0}') not used for 'add' action; ignoring.",
									action.MatchName);
							}

							switch (action.ClassType)
							{
								case MODULE:
									this.addModuleFromConfig(evaParts[0], action.Node);
									this.addModuleFromConfig(evaParts[1], action.Node);
									break;
								case RESOURCE:
									this.addResourceFromConfig(evaParts[0], action.Node);
									this.addResourceFromConfig(evaParts[1], action.Node);
									break;
								default:
									this.LogWarning("Class type '{0}' not implemented for 'add' action.",
										action.ClassType);
									continue;
							}
						}
					}
					pass = Pass.Done;
					break;
				case Pass.Done:
					#if DEBUG
					log = Tools.DebugLogger.New(this);

					log.AppendLine("Modules after run:");

					foreach (var m in evaPart.GetComponents<PartModule>())
					{
						log.Append('\t');
						log.Append(m.GetType().Name);
						log.Append('\n');
					}

					log.AppendLine("Resources after run:");

					foreach (var r in evaPart.GetComponents<PartResource>())
					{
						log.Append('\t');
						log.AppendFormat("Name: {0}, amount: {1}, maxAmount: {2}",
							r.resourceName, r.amount, r.maxAmount);
						log.Append('\n');
					}

					log.Print();
					#endif

					GameObject.Destroy(this);

					this.LogDebug("Destruction Requested.");
					break;
			}
		}

		private void addModuleFromConfig(Part evaPart, ConfigNode evaModuleNode)
		{
			string moduleName;

			if (evaModuleNode.TryGetValue("name", out moduleName))
			{
				if (evaPart.GetComponents<PartModule>().Any(m => m.GetType().Name == moduleName))
				{
					this.LogWarning("Skipping module {1}: already present in kerbalEVA", moduleName);
					return;
				}

				Type moduleClass = AssemblyLoader.GetClassByName(typeof(PartModule), moduleName);

				if (moduleClass == null)
				{
					this.LogWarning("Skipping module {0}: class not found in loaded assemblies.", moduleName);
					return;
				}

				try
				{
					PartModule evaModule = evaPart.gameObject.AddComponent(moduleClass)
						as PartModule;

					var awakeMethod = typeof(PartModule).GetMethod("Awake",
						System.Reflection.BindingFlags.NonPublic |
						System.Reflection.BindingFlags.Instance
					);

					awakeMethod.Invoke(evaModule, new object[] {});

					evaModule.Load(evaModuleNode);
				}
				catch (Exception ex)
				{
					this.LogError("Handled exception {0} while adding modules to kerbalEVA.", ex.GetType().Name);

					#if DEBUG
					Debug.LogException(ex);
					#endif
				}

				if (evaPart.GetComponents<PartModule>().Any(m => m.GetType().Name == moduleName))
				{
					this.Log("Added module {0} to kerbalEVA part.",	moduleName);
				}
				else
				{
					this.LogWarning("Failed to add {0} to kerbalEVA part.", moduleName);
				}
			}
			else
			{
				this.LogWarning("Skipping malformed EVA_MODULE node: missing 'name' field.");
				return;
			}
		}

		private void addResourceFromConfig(Part evaPart, ConfigNode evaResourceNode)
		{
			string resourceName;

			if (evaResourceNode.TryGetValue("name", out resourceName))
			{
				this.LogDebug("Adding resource '{0}'", resourceName);

				PartResourceDefinition resourceInfo =
					PartResourceLibrary.Instance.GetDefinition(resourceName);

				if (resourceInfo == null)
				{
					this.LogWarning("Skipping resource {0}: definition not present in library.", resourceName);

					return;
				}

				this.LogDebug("Resource '{0}' is in library.", resourceName);

				if (evaPart.GetComponents<PartResource>().Any(r => r.resourceName == resourceName))
				{
					this.LogWarning("Skipping resource {0}: already present in kerbalEVA.", resourceName);

					return;
				}

				this.LogDebug("Resource '{0}' is not present.", resourceName);

				PartResource resource = evaPart.gameObject.AddComponent<EVAPartResource>();

				this.LogDebug("Resource '{0}' component built.", resourceName);

				resource.SetInfo(resourceInfo);
				((EVAPartResource)resource).Load(evaResourceNode);

				this.Log("Added resource {0} to kerbalEVA part.", resource.resourceName);

				this.LogDebug("Resource '{0}' loaded.", resourceName);
			}
			else
			{
				this.Log("Skipping malformed EVA_RESOURCE node: missing 'name' field.");
				return;
			}
		}

		private void delModuleByName(Part evaPart, string matchName)
		{
			PartModule module = this.matchFirstModuleByName(evaPart, matchName);

			if (module != null)
			{
				GameObject.Destroy(module);
			}
		}

		private void delResourceByName(Part evaPart, string matchName)
		{
			PartResource resource = this.matchFirstResourceByName(evaPart, matchName);

			if (resource != null)
			{
				GameObject.Destroy(resource);

				Tools.PostDebugMessage(
					this,
					"EVA resource {0} marked for destruction.",
					resource.resourceName);
			}
		}

		private void editModuleByNameFromConfig(Part evaPart, string matchName, ConfigNode config)
		{
			PartModule module = this.matchFirstModuleByName(evaPart, matchName);

			if (module != null)
			{
				if (config.HasValue("name"))
				{
					config = this.mergeConfigs(module, config);

					GameObject.Destroy(module);

					this.LogDebug("EVA module {0} marked for destruction.", module.GetType().Name);

					ConfigAction copyAction = new ConfigAction(empty, MODULE, empty, config);

					this.passQueue.Add(copyAction);

					Tools.PostDebugMessage(
						this,
						"EVA module {0} marked for insertion\n(action: {1})",
						config.GetValue("name"),
						copyAction
					);
				}
				else
				{
					this.assignFieldsFromConfig(module, config);
				}
			}
		}

		private void editResourceByNameFromConfig(Part evaPart, string matchName, ConfigNode config)
		{
			PartResource resource = this.matchFirstResourceByName(evaPart, matchName);

			if (resource != null)
			{
				if (config.HasValue("name"))
				{
					config = this.mergeConfigs(resource, config);

					GameObject.Destroy(resource);

					this.LogDebug("EVA resource {0} marked for destruction.", resource.resourceName);

					ConfigAction copyAction = new ConfigAction(empty, RESOURCE, empty, config);

					this.passQueue.Add(copyAction);

					Tools.PostDebugMessage(
						this,
						"EVA resource {0} marked for insertion\n(action: {1})",
						config.GetValue("name"),
						copyAction
					);
				}
				else
				{
					this.assignFieldsFromConfig(resource, config);
				}
			}
		}

		private PartModule matchFirstModuleByName(Part evaPart, string matchName)
		{
			Regex rgx = new Regex(@matchName);

			PartModule[] modules = evaPart.GetComponents<PartModule>();
			PartModule module;
			for (int idx = 0; idx < modules.Length; idx++)
			{
				module = modules[idx];
				Match match = rgx.Match(module.GetType().Name);

				if (match.Success)
				{
					return module;
				}
			}

			return null;
		}

		private PartResource matchFirstResourceByName(Part evaPart, string matchName)
		{
			Regex rgx = new Regex(@matchName);

			PartResource[] resources = evaPart.GetComponents<PartResource>();
			PartResource resource;
			for (int idx = 0; idx < resources.Length; idx++)
			{
				resource = resources[idx];
				Match match = rgx.Match(resource.resourceName);

				Tools.PostDebugMessage(
					this,
					"EVA resource {0} is {1}a match for action.",
					resource.resourceName,
					match.Success ? "" : "not ");

				if (match.Success)
				{
					return resource;
				}
			}

			return null;
		}

		private bool assignFieldsFromConfig(object obj, ConfigNode config)
		{
			bool success = true;

			ConfigNode.Value cfgValue;
			for (int idx = 0; idx < config.values.Count; idx++)
			{
				cfgValue = config.values[idx];
				try
				{
					var namedField = obj.GetType().GetField(cfgValue.name,
						System.Reflection.BindingFlags.Public |
						System.Reflection.BindingFlags.NonPublic |
						System.Reflection.BindingFlags.Instance |
						System.Reflection.BindingFlags.FlattenHierarchy
					);

					if (namedField != null)
					{
						var fieldType = namedField.FieldType;

						object convertedValue = Convert.ChangeType(cfgValue.value, fieldType);

						namedField.SetValue(obj, convertedValue);

						success &= true;

						this.LogDebug("Assigned field '{0}' with new value '{1}'.", namedField.Name, convertedValue);
					}
					else
					{
						this.LogWarning("Failed assigning value '{0}': field not found in class '{1}'",
							cfgValue.name,
							obj.GetType().Name
						);

						success &= false;
					}
				}
				catch (Exception ex)
				{
					this.LogWarning("Failed assigning value '{0}': {1}", cfgValue.name,	ex.Message);

					success &= false;

					#if DEBUG
					Debug.LogException(ex);
					#endif
				}
			}

			return success;
		}

		private ConfigNode mergeConfigs(ConfigNode source, ConfigNode target)
		{
			ConfigNode.Value value;
			for (int idx = 0; idx < target.values.Count; idx++)
			{
				value = target.values[idx];
				if (source.HasValue(value.name))
				{
					source.RemoveValue(value.name);
				}
			}

			source.CopyTo(target);

			return target;
		}

		private ConfigNode mergeConfigs(PartResource resource, ConfigNode target)
		{
			ConfigNode source = new ConfigNode();

			if (resource is EVAPartResource)
			{
				((EVAPartResource)resource).Save(source);
			}
			else
			{
				resource.Save(source);
			}

			return this.mergeConfigs(source, target);
		}

		private ConfigNode mergeConfigs(PartModule module, ConfigNode target)
		{
			ConfigNode source = new ConfigNode();

			source.AddValue("name", module.GetType().Name);

			System.Reflection.FieldInfo[] fieldInfos = module.GetType().GetFields(
				System.Reflection.BindingFlags.Public |
				System.Reflection.BindingFlags.NonPublic |
				System.Reflection.BindingFlags.Instance
			);
			System.Reflection.FieldInfo field;
			for (int fIdx = 0; fIdx < fieldInfos.Length; fIdx++)
			{
				field = fieldInfos[fIdx];

				object[] attrs = field.GetCustomAttributes(true);
				object attr;
				for (int aIdx = 0; aIdx < attrs.Length; aIdx++)
				{
					attr = attrs[aIdx];

					if (attr is KSPField)
					{
						source.AddValue(field.Name, field.GetValue(module));

						break;
					}
				}
			}

			return this.mergeConfigs(source, target);
		}

		#if DEBUG
		public void OnDestroy()
		{
			this.LogDebug("Destroyed.");
		}
		#endif

		private enum Pass
		{
			Collect,
			Delete,
			Edit,
			Insert,
			Done
		}

		internal class ConfigAction
		{
			internal string Operator
			{
				get;
				private set;
			}

			internal string ClassType
			{
				get;
				private set;
			}

			internal string MatchName
			{
				get;
				private set;
			}

			internal ConfigNode Node
			{
				get;
				private set;
			}

			private ConfigAction() {}

			internal ConfigAction(string op, string classType, string matchName)
			{
				this.Operator = op;
				this.ClassType = classType;
				this.MatchName = matchName;
			}

			internal ConfigAction(string op, string classType, string matchName, ConfigNode node)
				: this(op, classType, matchName)
			{
				this.Node = node;
			}

			public override string ToString()
			{
				return string.Format(
					"[ConfigAction: Operator: {0}, ClassType: {1}, MatchName: {2}, Node: {3}]",
					this.Operator ?? "null",
					this.ClassType ?? "null",
					this.MatchName ?? "null",
					this.Node);
			}
		}
	}
}

