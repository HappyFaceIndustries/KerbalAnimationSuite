using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace KerbalAnimation
{
	[KSPAddon(KSPAddon.Startup.MainMenu, true)]
	public class EVAModuleAdder : MonoBehaviour
	{
		void Start()
		{
			AddModule ("kerbalEVA", "KAS_AnimationPlayerModule");
			AddModule ("kerbalEVAfemale", "KAS_AnimationPlayerModule");
		}


		void AddModule(string partName, string moduleName)
		{
			Debug.Log ("Adding" + moduleName + " to part " + partName);

			foreach (var aPart in PartLoader.LoadedPartsList)
			{
				if (aPart.name != partName)
					continue;

				try
				{
					aPart.partPrefab.AddModule (moduleName);
				}
				catch {}
				Debug.Log ("Added" + moduleName + " to part " + partName + " successfully");
			}
		}
	}

	public class KAS_AnimationPlayerModule : PartModule
	{
		[UI_Cycle(stateNames = new string[]{""})]
		[KSPField(isPersistant = false, guiName = "Selected Animation", guiActive = true)]
		public int SelectedAnimation;
		public string SelectedAnimationName
		{
			get{return AnimationNames [SelectedAnimation];}
		}
		private string[] AnimationNames;

		private bool animationsLoaded = false;

		private List<KAS_AnimationClip> Animations = new List<KAS_AnimationClip> ();

		public override void OnStart (StartState state)
		{
			Debug.Log ("Starting KAS_AnimationPlayerModule: " + state.ToString ());

			ReloadAnimations ();
		}

		public override void OnUpdate()
		{
			if (KerbalAnimationSuite.Instance != null)
			{
				bool isAnimating = KerbalAnimationSuite.Instance.isAnimating;
				Events ["PlayAnimation"].active = !isAnimating && animationsLoaded;
				Events ["ReloadAnimations"].active = !isAnimating;
				Fields ["SelectedAnimation"].guiActive = !isAnimating && animationsLoaded;

				if (animationsLoaded)
				{
					for (var i = 0; i < Animations.Count; i++)
					{
						if (i > 9)
							continue;
						var animName = Animations [i].Name;
						if (Input.GetKeyDown ((i + 1).ToString ()))
						{
							PlayAnimationOnce (animName);
						}
					}
				}
			}
			else
			{
				Events ["PlayAnimation"].active = false;
				Events ["ReloadAnimations"].active = false;
				Fields ["SelectedAnimation"].guiActive = false;
			}
		}

		[KSPEvent(guiName = "Play Animation", guiActive = true, guiActiveUnfocused = false, externalToEVAOnly = false)]
		public void PlayAnimation()
		{
			Debug.Log ("Playing Animation...");
			if (animation [SelectedAnimationName] != null)
			{
				PlayAnimationOnce (SelectedAnimationName);
			}
			else
			{
				ScreenMessages.PostScreenMessage ("<color=red>Animation " + SelectedAnimationName + " does not exist</color>");
			}
		}
		public void PlayAnimationOnce(string name)
		{
			var state = animation [name];
			if (state == null || animation.GetClip(name) == null)
				return;
			state.wrapMode = WrapMode.Once;
			animation.CrossFade (name, 0.22f * state.length, PlayMode.StopSameLayer);
		}

		[KSPEvent(guiName = "Reload Animations", guiActive = true, guiActiveUnfocused = false, externalToEVAOnly = false)]
		public void ReloadAnimations()
		{
			Debug.Log ("Loading Animations...");

			Animations.Clear ();

			foreach(var file in Directory.GetFiles(KSPUtil.ApplicationRootPath + "GameData/", "*.anim", SearchOption.AllDirectories))
			{
				Debug.Log ("Loading " + file);
				KAS_AnimationClip clip = new KAS_AnimationClip ();
				clip.Load (file);
				Animations.Add (clip);
			}
			if (Animations.Count <= 0)
			{
				ScreenMessages.PostScreenMessage ("<color=red>No Animations found</color>");
				return;
			}
			List<string> names = new List<string> ();
			foreach (var anim in Animations)
			{
				anim.Initialize (animation, part.transform);
				animation [anim.Name].layer = 5;
				names.Add (anim.Name);
			}
			if (Fields ["SelectedAnimation"].uiControlFlight is UI_Cycle)
			{
				var cycle = Fields ["SelectedAnimation"].uiControlFlight as UI_Cycle;
				cycle.stateNames = names.ToArray ();
				AnimationNames = names.ToArray ();
				SelectedAnimation = 0;
			}
			animationsLoaded = true;
		}
	}
}

