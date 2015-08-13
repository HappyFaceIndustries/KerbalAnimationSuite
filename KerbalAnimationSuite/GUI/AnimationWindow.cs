using System;
using System.Collections.Generic;
using UnityEngine;

namespace KerbalAnimation
{
	public class AnimationWindow : Window
	{
		//contructor
		public AnimationWindow()
		{
			SetupGUIStyles ();
			WindowRect = new Rect(0f, Screen.height - 300f, 600, 300f);
			WindowTitle = "Animation";

			TimelineArrow = GameDatabase.Instance.GetTexture ("KerbalAnimationSuite/Icons/timeline_arrow", false);
			if (TimelineArrow == null)
				TimelineArrow = Texture2D.whiteTexture;
		}

		//animation
		public EditableAnimationClip animationClip
		{get{return Suite.AnimationClip;}}
		private KerbalAnimationClip.KerbalKeyframe currentKeyframe;

		//textures
		public Texture2D TimelineArrow;

		//gui styles
		private GUIStyle centeredText;
		private void SetupGUIStyles()
		{
			centeredText = new GUIStyle (skin.label);
			centeredText.alignment = TextAnchor.MiddleCenter;
		}

		private ScreenMessage errorMessage = new ScreenMessage ("", 20000f, ScreenMessageStyle.UPPER_CENTER);

		protected override void DrawWindow ()
		{
			//Timeline
			GUILayout.Label ("<b>Timeline</b>", centeredText);
			GUILayout.Space (25f);
		}
		public override void Update ()
		{

		}
	}
}

