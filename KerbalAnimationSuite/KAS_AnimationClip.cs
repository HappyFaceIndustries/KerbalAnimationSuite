using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KerbalAnimation
{
	/// <summary>
	/// KerbalAnimationSuite animation clip.
	/// </summary>
	public class KAS_AnimationClip
	{
		public static implicit operator string (KAS_AnimationClip clip)
		{
			return clip.Name;
		}
		public static implicit operator AnimationClip (KAS_AnimationClip clip)
		{
			return clip.clip;
		}

		public string Name = "CustomClip";

		public int Layer = 0;

		public AnimationClip clip;

		public float Duration
		{
			get{return tgtDuration;}
			set{tgtDuration = value;}
		}
		float tgtDuration = 1f;

		Dictionary<string, AnimationCurve> RotationWCurves = new Dictionary<string, AnimationCurve>();
		Dictionary<string, AnimationCurve> RotationXCurves = new Dictionary<string, AnimationCurve>();
		Dictionary<string, AnimationCurve> RotationYCurves = new Dictionary<string, AnimationCurve>();
		Dictionary<string, AnimationCurve> RotationZCurves = new Dictionary<string, AnimationCurve>();

		Dictionary<string, AnimationCurve> PositionXCurves = new Dictionary<string, AnimationCurve>();
		Dictionary<string, AnimationCurve> PositionYCurves = new Dictionary<string, AnimationCurve>();
		Dictionary<string, AnimationCurve> PositionZCurves = new Dictionary<string, AnimationCurve>();

		public List<KAS_Keyframe> Keyframes = new List<KAS_Keyframe>();

		public KAS_Keyframe LastKeyframe
		{
			get{return Keyframes [Keyframes.Count - 1];}
		}
		public KAS_Keyframe FirstKeyframe
		{
			get{return Keyframes [0];}
		}
		public int KeyframesCount
		{
			get{return Keyframes.Count;}
		}

		public List<string> MixingTransforms = new List<string>();
		public void AddMixingTransform(string name)
		{
			MixingTransforms.Add (name);
		}
		public void RemoveMixingTransform(string name)
		{
			if (MixingTransforms.Contains (name))
				MixingTransforms.Remove (name);
		}

		public void CreateKeyframe(Transform transform, float time)
		{
			var keyframe = new KAS_Keyframe (this);
			keyframe.Write (transform, time);
			Keyframes.Add (keyframe);
			Debug.Log ("[assembly: " + Assembly.GetExecutingAssembly ().GetName().Name + "]:" + "Created Keyframe! " + time);
		}
		public void ReorderKeyframes()
		{
			Keyframes.OrderBy(k => k.NormalizedTime);
		}

		public AnimationClip BuildAnimationClip()
		{
			clip = new AnimationClip ();
			clip.wrapMode = WrapMode.Loop;

			//populate dictionaries with curves
			RotationWCurves.Clear ();
			RotationXCurves.Clear ();
			RotationYCurves.Clear ();
			RotationZCurves.Clear ();
			PositionXCurves.Clear ();
			PositionYCurves.Clear ();
			PositionZCurves.Clear ();
			foreach (string animationName in KerbalAnimationSuite.AnimationNames.Values)
			{
				RotationWCurves.Add (animationName, new AnimationCurve ());
				RotationXCurves.Add (animationName, new AnimationCurve ());
				RotationYCurves.Add (animationName, new AnimationCurve ());
				RotationZCurves.Add (animationName, new AnimationCurve ());

				PositionXCurves.Add (animationName, new AnimationCurve ());
				PositionYCurves.Add (animationName, new AnimationCurve ());
				PositionZCurves.Add (animationName, new AnimationCurve ());
			}

			//populate curves with keyframe values
			ReorderKeyframes ();
			foreach (var keyframe in Keyframes)
			{
				foreach (string animationName in KerbalAnimationSuite.AnimationNames.Values)
				{
					RotationWCurves [animationName].AddKey (keyframe.Time, keyframe.GetValue (animationName, KAS_ValueType.RotW));
					RotationXCurves [animationName].AddKey (keyframe.Time, keyframe.GetValue (animationName, KAS_ValueType.RotX));
					RotationYCurves [animationName].AddKey (keyframe.Time, keyframe.GetValue (animationName, KAS_ValueType.RotY));
					RotationZCurves [animationName].AddKey (keyframe.Time, keyframe.GetValue (animationName, KAS_ValueType.RotZ));
					PositionXCurves [animationName].AddKey (keyframe.Time, keyframe.GetValue (animationName, KAS_ValueType.PosX));
					PositionYCurves [animationName].AddKey (keyframe.Time, keyframe.GetValue (animationName, KAS_ValueType.PosY));
					PositionZCurves [animationName].AddKey (keyframe.Time, keyframe.GetValue (animationName, KAS_ValueType.PosZ));
				}
			}

			//set curves to clip
			foreach (string animationName in KerbalAnimationSuite.AnimationNames.Values)
			{
				clip.SetCurve (animationName, typeof(Transform), "localRotation.w", RotationWCurves [animationName]);
				clip.SetCurve (animationName, typeof(Transform), "localRotation.x", RotationXCurves [animationName]);
				clip.SetCurve (animationName, typeof(Transform), "localRotation.y", RotationYCurves [animationName]);
				clip.SetCurve (animationName, typeof(Transform), "localRotation.z", RotationZCurves [animationName]);

				clip.SetCurve (animationName, typeof(Transform), "localPosition.x", PositionXCurves [animationName]);
				clip.SetCurve (animationName, typeof(Transform), "localPosition.y", PositionYCurves [animationName]);
				clip.SetCurve (animationName, typeof(Transform), "localPosition.z", PositionZCurves [animationName]);
			}

			clip.EnsureQuaternionContinuity ();
			return clip;
		}

		/// <summary>
		/// Initialize the animation clip in the kerbal part.
		/// </summary>
		/// <param name="animation">The Animation object to be used. Should be the animation property of any PartModule attached to a kerbalEVA/kerbalEVAfemale part</param>
		/// <param name="transform">The Transform object to be used as the skeleton. Should be the transform property of any PartModule attached to a kerbalEVA/kerbalEVAfemale part</param>
		public void Initialize(Animation animation, Transform transform)
		{
			Debug.Log ("[assembly: " + Assembly.GetExecutingAssembly ().GetName().Name + "]:" + "Rebuilding clip");
			var clip = BuildAnimationClip ();
			animation.RemoveClip (Name);
			animation.AddClip (clip, Name);
			animation [Name].layer = Layer;
			foreach(var mt in MixingTransforms)
			{
				if (/*KerbalAnimationSuite_Loader.AnimationNames.ContainsKey(mt) && transform.Find (KerbalAnimationSuite_Loader.AnimationNames [mt]) != null*/transform != null)
					animation [Name].AddMixingTransform (transform.Find (KerbalAnimationSuite.AnimationNames [mt]));
				else
					Debug.LogError ("[assembly: " + Assembly.GetExecutingAssembly ().GetName().Name + "]: animation mixing transform " + mt + " from animation " + Name + " does not exist, or could not be found.");
			}
		}


		#region IO
		public const int FileTypeVersion = 1;

		/// <summary>
		/// Save the animation clip to a .anim file
		/// </summary>
		/// <param name="savePath">Path to save animation file relative to GameData folder</param>
		public void Save(string savePath = "")
		{
			string folderPath = KSPUtil.ApplicationRootPath + "GameData/" + savePath + "/";
			string fileName = Name;

			int v = 0;
			while (File.Exists (folderPath + fileName + ".anim"))
			{
				File.Move (folderPath + fileName + ".anim", folderPath + fileName + v.ToString () + ".anim.old");
				v++;
			}

			string path = folderPath + fileName + ".anim";

			Debug.Log ("[assembly: " + Assembly.GetExecutingAssembly ().GetName().Name + "]:" + "Saving animation " + Name + " to " + path);

			ConfigNode node = new ConfigNode ("KAS_Animation");
			node.AddValue ("FileTypeVersion", FileTypeVersion);
			node.AddValue ("Name", Name);
			node.AddValue ("Duration", Duration);

			ConfigNode mtNode = new ConfigNode ("MIXING_TRANSFORMS");
			foreach (var mt in MixingTransforms)
			{
				mtNode.AddValue ("MixingTransform", mt);
			}
			node.AddNode (mtNode);

			ConfigNode keyframesNode = new ConfigNode ("KEYFRAMES");
			ReorderKeyframes ();
			foreach(var keyframe in Keyframes)
			{
				ConfigNode keyframeNode = new ConfigNode ("KEYFRAME");
				keyframeNode.AddValue ("NormalizedTime", keyframe.NormalizedTime);

				foreach (string animationName in KerbalAnimationSuite.AnimationNames.Values)
				{
					string rotW = keyframe.GetValue (animationName, KAS_ValueType.RotW).ToString();
					string rotX = keyframe.GetValue (animationName, KAS_ValueType.RotX).ToString();
					string rotY = keyframe.GetValue (animationName, KAS_ValueType.RotY).ToString();
					string rotZ = keyframe.GetValue (animationName, KAS_ValueType.RotZ).ToString();
					string posX = keyframe.GetValue (animationName, KAS_ValueType.PosX).ToString();
					string posY = keyframe.GetValue (animationName, KAS_ValueType.PosY).ToString();
					string posZ = keyframe.GetValue (animationName, KAS_ValueType.PosZ).ToString();
					string value = rotW + " " + rotX + " " + rotY + " " + rotZ + " " + posX + " " + posY + " " + posZ;

					keyframeNode.AddValue(animationName, value);
				}

				//OptimizeKeyframeNode (keyframeNode);
				keyframesNode.AddNode (keyframeNode);
			}
			node.AddNode (keyframesNode);

			node.Save (path, "Saved " + DateTime.Now.ToString());
		}
//		private void OptimizeKeyframeNode(ConfigNode keyframe)
//		{
//			foreach (string animationName in KerbalAnimationSuite.AnimationNames.Values)
//			{
//				bool isMixed = false;
//				foreach (var mt in MixingTransforms)
//				{
//					if (animationName.Contains (mt))
//						isMixed = true;
//				}
//
//				if (!isMixed)
//				{
//					keyframe.RemoveValue (animationName);
//				}
//			}
//		}

		/// <summary>
		/// Loads an animation file from a URL relative to the /GameData/ folder. Does not require an extension in the url.
		/// </summary>
		/// <param name="url">The URL to load from</param>
		public void LoadURL(string url)
		{
			Debug.Log ("[assembly: " + Assembly.GetExecutingAssembly ().GetName().Name + "]:" + "Loading animation from URL " + url);
			this.Load (KSPUtil.ApplicationRootPath + "GameData/" + url);
		}
		/// <summary>
		/// Loads an animation using it's full path.
		/// </summary>
		/// <param name="fullPath">The full path of the file.</param>
		public void Load(string fullPath)
		{
			Debug.Log ("[assembly: " + Assembly.GetExecutingAssembly ().GetName().Name + "]:" + "Loading animation from " + fullPath);
			try
			{
				ConfigNode node = ConfigNode.Load (fullPath +  (fullPath.EndsWith(".anim") ? "" : ".anim") );
				Name = node.GetValue ("Name");
				Duration = float.Parse(node.GetValue ("Duration"));

				ConfigNode mtNode = node.GetNode ("MIXING_TRANSFORMS");
				foreach (var mt in mtNode.GetValues("MixingTransform"))
				{
					AddMixingTransform (mt);
				}

				ConfigNode keyframesNode = node.GetNode ("KEYFRAMES");
				foreach (var keyframeNode in keyframesNode.GetNodes("KEYFRAME"))
				{
					KAS_Keyframe keyframe = new KAS_Keyframe (this);
					keyframe.NormalizedTime = float.Parse (keyframeNode.GetValue ("NormalizedTime"));

					foreach (string animationName in KerbalAnimationSuite.AnimationNames.Values)
					{
						if (!keyframeNode.HasValue (animationName))
							continue;

						string allValues = keyframeNode.GetValue (animationName);
						string[] values = allValues.Split (' ');

						float rotW = float.Parse (values [0]);
						float rotX = float.Parse (values [1]);
						float rotY = float.Parse (values [2]);
						float rotZ = float.Parse (values [3]);
						float posX = float.Parse (values [4]);
						float posY = float.Parse (values [5]);
						float posZ = float.Parse (values [6]);

						keyframe.SetValue (rotW, animationName, KAS_ValueType.RotW);
						keyframe.SetValue (rotX, animationName, KAS_ValueType.RotX);
						keyframe.SetValue (rotY, animationName, KAS_ValueType.RotY);
						keyframe.SetValue (rotZ, animationName, KAS_ValueType.RotZ);
						keyframe.SetValue (posX, animationName, KAS_ValueType.PosX);
						keyframe.SetValue (posY, animationName, KAS_ValueType.PosY);
						keyframe.SetValue (posZ, animationName, KAS_ValueType.PosZ);
					}

					Keyframes.Add (keyframe);
				}
			}
			catch(KeyNotFoundException e)
			{
				Debug.LogError ("ERROR ENCOUNTERED LOADING ANIMATION");
				Debug.LogException (e);
			}
			finally
			{
				Debug.Log ("finally was hit <-- this is not a bad message");
			}
		}
		#endregion
	}

	/// <summary>
	/// Keyframe of a KerbalAnimationSuite animation clip (KAS_AnimationClip)
	/// </summary>
	public class KAS_Keyframe
	{
		public KAS_Keyframe(KAS_AnimationClip animClip)
		{
			this.clip = animClip;
		}

		/// <summary>
		/// The animation clip this keyframe is attached to.
		/// </summary>
		KAS_AnimationClip clip;
		/// <summary>
		/// The normalized time of this keyframe.
		/// </summary>
		public float NormalizedTime = 0f;
		/// <summary>
		/// Gets the time in seconds that this keyframe happens at.
		/// </summary>
		public float Time
		{
			get{return NormalizedTime * clip.Duration;}
		}

		Dictionary<string, float> RotationW = new Dictionary<string, float>();
		Dictionary<string, float> RotationX = new Dictionary<string, float>();
		Dictionary<string, float> RotationY = new Dictionary<string, float>();
		Dictionary<string, float> RotationZ = new Dictionary<string, float>();

		Dictionary<string, float> PositionX = new Dictionary<string, float>();
		Dictionary<string, float> PositionY = new Dictionary<string, float>();
		Dictionary<string, float> PositionZ = new Dictionary<string, float>();

		public void Write(Transform transform, float time)
		{
			this.Clear ();
			this.NormalizedTime = time;
			foreach (string name in KerbalAnimationSuite.AnimationNames.Values)
			{
				Transform t = transform.Find (name);
				if (t == null)
					Debug.LogError ("[assembly: " + Assembly.GetExecutingAssembly ().GetName().Name + "]:" + "t is null at " + name);
				Quaternion quatRot = t.localRotation;
				RotationW.Add(name, quatRot.w);
				RotationX.Add(name, quatRot.x);
				RotationY.Add(name, quatRot.y);
				RotationZ.Add(name, quatRot.z);

				Vector3 localPos = t.localPosition;
				PositionX.Add(name, localPos.x);
				PositionY.Add(name, localPos.y);
				PositionZ.Add(name, localPos.z);
			}
		}
		/// <summary>
		/// Clears this keyframe.
		/// </summary>
		public void Clear()
		{
			RotationW.Clear ();
			RotationX.Clear ();
			RotationY.Clear ();
			RotationZ.Clear ();
			PositionX.Clear ();
			PositionY.Clear ();
			PositionZ.Clear ();
			this.NormalizedTime = 0f;
		}
		/// <summary>
		/// Sets the value.
		/// </summary>
		/// <param name="value">The float value to set it to.</param>
		/// <param name="animationName">the animation name of the bone you wish to edit.</param>
		/// <param name="type">Which value you wish to set.</param>
		public void SetValue(float value, string animationName, KAS_ValueType type)
		{
			switch (type)
			{
			case KAS_ValueType.RotW:
				RotationW [animationName] = value;
				break;
			case KAS_ValueType.RotX:
				RotationX [animationName] = value;
				break;
			case KAS_ValueType.RotY:
				RotationY [animationName] = value;
				break;
			case KAS_ValueType.RotZ:
				RotationZ [animationName] = value;
				break;
			case KAS_ValueType.PosX:
				PositionX [animationName] = value;
				break;
			case KAS_ValueType.PosY:
				PositionY [animationName] = value;
				break;
			case KAS_ValueType.PosZ:
				PositionZ [animationName] = value;
				break;
			default:
				break;
			}
		}

		/// <summary>
		/// Gets a value of the bone animationName. Similar to SetValue
		/// </summary>
		/// <returns>The value.</returns>
		/// <param name="animationName">Animation name.</param>
		/// <param name="type">Type.</param>
		public float GetValue(string animationName, KAS_ValueType type)
		{
			try
			{
				switch (type)
				{
				case KAS_ValueType.RotW:
					return RotationW [animationName];
				case KAS_ValueType.RotX:
					return RotationX [animationName];
				case KAS_ValueType.RotY:
					return RotationY [animationName];
				case KAS_ValueType.RotZ:
					return RotationZ [animationName];
				case KAS_ValueType.PosX:
					return PositionX [animationName];
				case KAS_ValueType.PosY:
					return PositionY [animationName];
				case KAS_ValueType.PosZ:
					return PositionZ [animationName];
				default:
					return 0f;
				}
			}
			catch(KeyNotFoundException e)
			{
				//ignore colliders
				if (animationName.ToLower ().Contains ("collider"))
					return 0f;

				Debug.LogError ("key not found: " + animationName);
				Debug.LogException (e);
			}
			return 0f;
		}
	}

	/// <summary>
	/// An enumeration of values for use with KAS_Keyframes
	/// </summary>
	public enum KAS_ValueType
	{
		RotW, RotX, RotY, RotZ, PosX, PosY, PosZ
	}
}

