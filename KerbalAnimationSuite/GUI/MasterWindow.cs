using System;
using UnityEngine;

namespace KerbalAnimation
{
	public class MasterWindow : Window
	{
		public MasterWindow()
		{
			AllowDrag = false;
			ExpandHeight = true;
			WindowRect = new Rect (Screen.width - 250f, 50f, 250f, 0f); //height is zero because it expands
			WindowTitle = "Kerbal Animation Suite";
		}

		public bool AnimationOpen = true;
		public bool ManipulationOpen = true;
		public bool HierarchyOpen = true;

		protected override void DrawWindow ()
		{
			GUILayout.BeginVertical (skin.scrollView);

			AnimationOpen = GUILayout.Toggle (AnimationOpen, "Animation", skin.button);
			if (!Suite.Kerbal.IsAnimationPlaying)
				HierarchyOpen = GUILayout.Toggle (HierarchyOpen, "Bone Hierarchy", skin.button);
			else
				GUILayout.Toggle (false, "Bone Hierarchy", skin.button);
			if (!Suite.Kerbal.IsAnimationPlaying)
				ManipulationOpen = GUILayout.Toggle (ManipulationOpen, "Manipulation", skin.button);
			else
				GUILayout.Toggle (false, "Manipulation", skin.button);

			GUILayout.EndVertical ();
		}
		public override void Update()
		{

		}
	}
}

