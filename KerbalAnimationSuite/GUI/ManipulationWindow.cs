using System;
using System.Collections.Generic;
using UnityEngine;

namespace KerbalAnimation
{
	public class ManipulationWindow : Window
	{
		public ManipulationWindow()
		{
			WindowRect = new Rect (Screen.width - 500f, Screen.height - 500f, 500f, 300f);
			WindowTitle = "Manipulation";
		}

		public SelectedBone Bone
		{
			get{return Suite.CurrentBone;}
			set{Suite.CurrentBone = value;}
		}

		public Vector3 Rotation;
		public Vector3 Position;

		//private GUI values
		private Vector2 manipulationScroll;
		private Dictionary<string, string> textBoxValues = new Dictionary<string, string>();
		private Dictionary<string, Color> textBoxColors = new Dictionary<string, Color>();

		public override void Update()
		{
			if(Suite.CurrentBone != null)
			{
				Suite.CurrentBone.Position = Position;
				Suite.CurrentBone.Rotation = Rotation;
			}
		}

		protected override void DrawWindow()
		{
			manipulationScroll = GUILayout.BeginScrollView (manipulationScroll);

			GUILayout.Label ("Rotation");
			Rotation.x = DrawManipulationSlider ("RX", "X", Rotation.x, 0f, 360f);
			Rotation.y = DrawManipulationSlider ("RY", "Y", Rotation.y, 0f, 360f);
			Rotation.z = DrawManipulationSlider ("RZ", "Z", Rotation.z, 0f, 360f);
			GUILayout.Space (20f);

			GUILayout.Label ("Relative Position");
			Position.x = DrawManipulationSlider ("PX", "X", Position.x, -0.5f, 0.5f);
			Position.y = DrawManipulationSlider ("PY", "Y", Position.y, -0.5f, 0.5f);
			Position.z = DrawManipulationSlider ("PZ", "Z", Position.z, -0.5f, 0.5f);

			GUILayout.Space (10f);
			if (GUILayout.Button ("Toggle Helmet"))
			{
				Suite.Kerbal.ToggleHelmet();
			}

			GUILayout.EndScrollView ();

			GUI.DragWindow ();
		}

		private float DrawManipulationSlider(string uniqueName, string name, float value, float min, float max)
		{
			value = Mathf.Clamp (value, min, max);
			if (!textBoxValues.ContainsKey (uniqueName))
				textBoxValues.Add (uniqueName, value.ToString ());
			if (!textBoxColors.ContainsKey (uniqueName))
				textBoxColors.Add (uniqueName, Color.white);

			GUILayout.BeginHorizontal ();

			GUILayout.Label ("<b><color=" + Colors.Orange + ">" + name + ":</color></b>");

			float finalValue = value;

			textBoxValues [uniqueName] = GUILayout.TextField (textBoxValues [uniqueName]);
			float parsedTextBoxFloat;
			bool parsedTextBox = true;
			if (!float.TryParse (textBoxValues [uniqueName], out parsedTextBoxFloat))
			{
				textBoxColors [uniqueName] = Color.red;
				parsedTextBox = false;
			}
			if (parsedTextBox && (parsedTextBoxFloat < min || parsedTextBoxFloat > max))
			{
				textBoxColors [uniqueName] = Color.red;
				parsedTextBox = false;
			}

			float sliderValue = value;
			if (parsedTextBox)
			{
				sliderValue = parsedTextBoxFloat;
			}

			finalValue = GUILayout.HorizontalSlider (sliderValue, min, max);

			GUILayout.Label (finalValue.ToString("000.00"));

			GUILayout.EndHorizontal ();

			return finalValue;
		}
	}
}

