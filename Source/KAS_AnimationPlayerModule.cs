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
			AddModule ("kerbalEVA", typeof(KAS_AnimationPlayerModule).Name);
			AddModule ("kerbalEVAfemale", typeof(KAS_AnimationPlayerModule).Name);
		}


		void AddModule(string partName, string moduleName)
		{
			foreach (var aPart in PartLoader.LoadedPartsList)
			{
				if (aPart.name != partName)
					continue;

				try
				{
					aPart.partPrefab.AddModule (moduleName);
				}
				catch {}
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

		public override void OnUpdate()
		{
			if (KerbalAnimationSuite.Instance != null)
			{
				bool isAnimating = KerbalAnimationSuite.IsAnimating;
				Events ["PlayAnimation"].active = !isAnimating && animationsLoaded;
				Events ["ReloadAnimations"].active = !isAnimating;
				Fields ["SelectedAnimation"].guiActive = !isAnimating && animationsLoaded;
			}
			else
			{
				Events ["PlayAnimation"].active = false;
				Events ["ReloadAnimations"].active = false;
				Fields ["SelectedAnimation"].guiActive = false;
			}
		}

		[KSPEvent(guiName = "Play Animation", guiActive = false, guiActiveUnfocused = false, externalToEVAOnly = false)]
		public void PlayAnimation()
		{
			if (animation [SelectedAnimationName] != null)
			{
				StartCoroutine (PlayAnimationOnce (SelectedAnimationName));
			}
			else
			{
				ScreenMessages.PostScreenMessage ("<color=red>Animation " + SelectedAnimationName + " does not exist</color>");
			}
		}
		IEnumerator<YieldInstruction> PlayAnimationOnce(string name)
		{
			var state = animation [SelectedAnimationName];
			state.layer = 5;
			animation.CrossFade (SelectedAnimationName, 0.22f * state.length, PlayMode.StopSameLayer);
			state.normalizedTime = 0f;

			yield return new WaitForSeconds (state.length);

			animation.Stop (name);
		}

		[KSPEvent(guiName = "Load Animations", guiActive = false, guiActiveUnfocused = false, externalToEVAOnly = false)]
		public void ReloadAnimations()
		{
			Animations.Clear ();

			foreach(var file in Directory.GetFiles(KSPUtil.ApplicationRootPath + "GameData" + Path.DirectorySeparatorChar, "*.anim", SearchOption.AllDirectories))
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

			Events["ReloadAnimations"].guiName = "Reload Animations";
		}
	}
}

