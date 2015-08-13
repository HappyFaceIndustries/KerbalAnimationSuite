using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace KerbalAnimation
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class KerbalAnimationSuite : MonoBehaviour
	{
		public static KerbalAnimationSuite Instance;

		public bool IsAnimating
		{
			get{return Kerbal != null ? Instance.Kerbal.IsAnimating : false;}
		}

		public GUISkin skin = HighLogic.Skin;

		public Dictionary<string, string> ReadableNames = new Dictionary<string, string>();
		public Dictionary<string, string> AnimationNames = new Dictionary<string, string>();

		public EditableAnimationClip AnimationClip;

		public SelectedKerbalEVA Kerbal;
		public SelectedBone CurrentBone;

		//GUI
		//Windows
		public MasterWindow Master;
		public HierarchyWindow Hierarchy;
		public ManipulationWindow Manipulation;
		public AnimationWindow Animation;

		//Button
		private static bool ButtonAdded = false;
		public static ApplicationLauncherButton Button;

		public bool ShowUI //set by pressing F2
		{get; private set;}

		private void Awake()
		{
			Instance = this;

			//set defaults
			ShowUI = true;

			//instantiate windows
			Master = new MasterWindow ();
			Hierarchy = new HierarchyWindow ();
			Manipulation = new ManipulationWindow ();
			Animation = new AnimationWindow ();

			AddApplicationLauncherButton ();
		}

		private void Start()
		{
			//load animation data
			ConfigurationUtils.LoadAnimationNames ();
			ConfigurationUtils.LoadReadableNames ();
		}
		private void OnDestroy()
		{
			GameEvents.onShowUI.Remove (OnShowUI);
			GameEvents.onHideUI.Remove (OnHideUI);
		}
		private void OnShowUI()
		{
			ShowUI = true;
		}
		private void OnHideUI()
		{
			ShowUI = false;
		}

		private void AddApplicationLauncherButton ()
		{
			if (ButtonAdded)
				return;

			var buttonTexture = GameDatabase.Instance.GetTexture ("KerbalAnimationSuite/Icons/button", false);
			Button = ApplicationLauncher.Instance.AddModApplication (EnableAnimationSuite, DisableAnimationSuite, null, null, null, null, ApplicationLauncher.AppScenes.FLIGHT, buttonTexture);
			ButtonAdded = true;
		}

		public void EnableAnimationSuite()
		{
			var vessel = FlightGlobals.ActiveVessel;
			if (vessel.evaController == null)
			{
				ScreenMessages.PostScreenMessage (new ScreenMessage ("<color=" + Colors.DefaultMessageColor + ">Active vessel must be an EVA to use the Animation Suite</color>", 3f, ScreenMessageStyle.UPPER_CENTER));
				return;
			}

			Kerbal = new SelectedKerbalEVA (vessel.evaController);
			AnimationClip = new EditableAnimationClip (Kerbal);

			if (!Kerbal.EnterAnimationMode ())
			{
				return;
			}
		}
		public void DisableAnimationSuite()
		{
			Kerbal.ExitAnimationMode ();

			Kerbal = null;
			AnimationClip = null;
		}


		void Update()
		{
			if (Kerbal != null && Kerbal.IsAnimating)
			{
				Master.Update ();
				Hierarchy.Update ();
				Manipulation.Update ();
				Animation.Update ();
			}
		}

		void OnGUI()
		{
			if (!ShowUI)
				return;
			if (Kerbal == null || !Kerbal.IsAnimating)
				return;

			GUI.skin = skin;

			Master.Draw ();
			if (Master.HierarchyOpen)
				Hierarchy.Draw ();
			if (Master.ManipulationOpen)
				Manipulation.Draw ();
			if (Master.AnimationOpen)
				Animation.Draw ();
		}
	}
}

