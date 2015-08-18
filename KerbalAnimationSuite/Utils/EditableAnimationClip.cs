﻿using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace KerbalAnimation
{
	public class EditableAnimationClip : KerbalAnimationClip
	{
		public SelectedKerbalEVA Kerbal
		{get; private set;}

		private string tempNewName = null;
		public new string Name
		{
			get{if (tempNewName == null) tempNewName = base.name; return tempNewName;}
			set{tempNewName = value;}
		}
		public new int Layer
		{
			get{return layer;}
			set{layer = value;}
		}
		public new float Duration
		{
			get{return duration;}
			set{duration = value;}
		}

		private WrapMode wrapMode;
		public WrapMode WrapMode
		{
			get{return wrapMode;}
			set{wrapMode = value;}
		}

		public EditableAnimationClip(SelectedKerbalEVA eva)
		{
			Kerbal = eva;
			name = "CustomClip_" + Guid.NewGuid ().ToString ();

			BuildAnimationClip ();
			Initialize ();
		}
		public EditableAnimationClip(SelectedKerbalEVA eva, string url)
		{
			Kerbal = eva;
			name = "CustomClip_" + Guid.NewGuid ().ToString ();

			if(!url.EndsWith(".anim"))
				url += ".anim";
			ConfigNode node = ConfigNode.Load (KSPUtil.ApplicationRootPath + "GameData/" + url);
			base.Load (node);

			BuildAnimationClip ();
			Initialize ();
		}

		public new void AddMixingTransform(string mixingTransformName)
		{
			base.AddMixingTransform (mixingTransformName);
		}
		public new void RemoveMixingTransform(string mixingTransformName)
		{
			base.RemoveMixingTransform (mixingTransformName);
		}
		public new List<string> MixingTransforms
		{
			get{return base.MixingTransforms;}
		}

		public void Initialize()
		{
			base.Initialize (Kerbal.animation, Kerbal.transform, tempNewName);
		}
		public new AnimationClip BuildAnimationClip()
		{
			var clip = base.BuildAnimationClip ();
			clip.wrapMode = WrapMode;
			return clip;
		}

		public new List<KerbalKeyframe> Keyframes
		{
			get{return base.Keyframes;}
		}
		public KerbalKeyframe CreateKeyframe(float normalizedTime)
		{
			KerbalKeyframe keyframe = new KerbalKeyframe (this);
			Keyframes.Add (keyframe);
			return keyframe;
		}
		public KerbalKeyframe GetKeyframe(float normalizedTime)
		{
			foreach (var keyframe in Keyframes)
			{
				if (keyframe.NormalizedTime == normalizedTime)
					return keyframe;
			}
			return null;
		}
		public void RemoveKeyframe(KerbalKeyframe keyframe)
		{
			if(Keyframes.Contains(keyframe))
				Keyframes.Remove (keyframe);
		}

		public void SetAnimationTime(float normalizedTime)
		{
			if(Kerbal.IsAnimationPlaying)
				Kerbal.animation [name].normalizedTime = normalizedTime;
			else
			{
				Kerbal.animation.Play (name);
				Kerbal.animation [name].normalizedTime = normalizedTime;
				Kerbal.animation.Sample ();
				Kerbal.animation.Stop ();
			}
		}
		public float GetAnimationTime(bool clamped = true)
		{
			if(!clamped)
				return Kerbal.animation [name].normalizedTime;
			else
			{
				float time = Kerbal.animation [name].normalizedTime;
				float floor = Mathf.Floor (time);
				return time - floor;
			}
		}

		public void Play()
		{
			Kerbal.animation.Play (name, PlayMode.StopAll);
		}
		public void Stop()
		{
			Kerbal.animation.Stop (name);
		}

		//saving and loading
		//TODO: move loading/saving code into a static class
		public bool Load(string url)
		{
			if (!url.EndsWith (".anim"))
				url += ".anim";
			var fullPath = KSPUtil.ApplicationRootPath + "GameData/" + url;
			var node = ConfigNode.Load (fullPath);
			if (node == null)
			{
				Debug.LogError ("ConfigNode not found at " + url);
				return false;
			}
			base.Load (node);
			return true;
		}
		public void Save(string url)
		{
			string folderPath = KSPUtil.ApplicationRootPath + "GameData/" + url + "/";
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
			node.AddValue ("Name", Name);
			node.AddValue ("Duration", Duration);

			ConfigNode mtNode = new ConfigNode ("MIXING_TRANSFORMS");
			foreach (var mt in MixingTransforms)
			{
				mtNode.AddValue ("MixingTransform", mt);
			}
			node.AddNode (mtNode);

			ConfigNode keyframesNode = new ConfigNode ("KEYFRAMES");
			foreach(var keyframe in Keyframes)
			{
				ConfigNode keyframeNode = new ConfigNode ("KEYFRAME");
				keyframeNode.AddValue ("NormalizedTime", keyframe.NormalizedTime);

				foreach (string animationName in KerbalAnimationSuite.Instance.AnimationNames.Values)
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
	}
}

