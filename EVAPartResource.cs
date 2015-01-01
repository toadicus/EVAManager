// EVAManager
//
// EVAPartModule.cs
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
using ToadicusTools;
using UnityEngine;

namespace EVAManager
{
	public class EVAPartResource : PartResource
	{
		public bool FillFromPod
		{
			get;
			private set;
		}

		public void Awake()
		{
			try
			{
				var baseAwake = typeof(PartResource).GetMethod(
					"Awake",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
				);

				baseAwake.Invoke(this, null);

				Tools.PostDebugMessage(this, "base Awake.");
			}
			#if DEBUG
			catch (Exception ex)
			#else
			catch
			#endif
			{
				Debug.LogError(string.Format("[{0}] Could not wake up base class.", this.GetType().Name));

				#if DEBUG
				Debug.LogException(ex);
				#endif
			}

			this.FillFromPod = true;

			GameEvents.onCrewOnEva.Add(this.onEvaHandler);
			GameEvents.onCrewBoardVessel.Add(this.onBoardHandler);

			Tools.PostDebugMessage(this, "Awake");
		}

		public void OnDestroy()
		{
			GameEvents.onCrewOnEva.Remove(this.onEvaHandler);
			GameEvents.onCrewBoardVessel.Remove(this.onBoardHandler);
		}

		public new void Load(ConfigNode node)
		{
			base.Load(node);

			this.FillFromPod = node.GetValue("FillFromPod", this.FillFromPod);

			Tools.PostDebugMessage(this, "Loaded.");
		}

		public new void Save(ConfigNode node)
		{
			base.Save(node);

			if (node.HasValue("FillFromPod"))
			{
				node.SetValue("FillFromPod", this.FillFromPod.ToString());
			}
			else
			{
				node.AddValue("FillFromPod", this.FillFromPod.ToString());
			}

			Tools.PostDebugMessage(this, "Saved.");
		}

		private void onEvaHandler(GameEvents.FromToAction<Part, Part> data)
		{
			if (data.to == null || data.from == null)
			{
				return;
			}

			if (data.to == this.part)
			{
				Debug.Log(
					string.Format("[{0}] Caught OnCrewOnEva event to part ({1}) containing this resource ({2})",
						this.GetType().Name,
						this.part.partInfo.title,
						this.resourceName
					));

				if (this.FillFromPod)
				{
					double needAmount = this.maxAmount - this.amount;
					double gotAmount;

					gotAmount = data.from.RequestResource(this.resourceName, needAmount);

					this.amount += gotAmount;

					Tools.PostDebugMessage(this, "Filled {0} {1} from {2}",
						gotAmount,
						this.resourceName,
						data.to.partInfo.title
					);
				}
			}
		}

		private void onBoardHandler(GameEvents.FromToAction<Part, Part> data)
		{
			if (data.to == null || data.from == null)
			{
				return;
			}

			if (data.from == this.part)
			{
				if (this.FillFromPod)
				{
					double returnAmount = this.amount;
					double sentAmount;

					sentAmount = data.to.RequestResource(this.resourceName, -returnAmount);

					this.amount += sentAmount;

					Tools.PostDebugMessage(this, "Returned {0} {1} to {2}",
						-sentAmount,
						this.resourceName,
						data.to.partInfo.title
					);
				}
			}
		}
	}
}

