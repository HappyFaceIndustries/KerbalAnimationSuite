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

		public static bool IsAnimating
		{
			get{return Instance.isAnimating;}
		}

		private static bool hasAddedButton = false;
		public GUISkin skin = HighLogic.Skin;

		public Dictionary<string, string> ReadableNames = new Dictionary<string, string>();
		public static Dictionary<string, string> AnimationNames = new Dictionary<string, string>();

		void Awake()
		{
			Instance = this;
		}

		void Start()
		{
			if (!hasAddedButton)
			{
				AddApp ();
				hasAddedButton = true;
			}

			ConfigNode readableNamesNode = ConfigNode.Load (KSPUtil.ApplicationRootPath + "GameData/KerbalAnimationSuite/Config/ReadableNames.cfg");
			LoadReadableNames (readableNamesNode);
			Debug.Log ("ReadableNamesCount: " + ReadableNames != null ? ReadableNames.Count.ToString() : "NaN");

			LoadAnimationNames ("KerbalAnimationSuite/Config/animation_hierarchy");

			GameEvents.onShowUI.Add (ShowUI);
			GameEvents.onHideUI.Add (HideUI);

			timelineArrowTex = GameDatabase.Instance.GetTexture ("KerbalAnimationSuite/Icons/timeline_arrow", false);
		}
		void OnDestroy()
		{
			GameEvents.onShowUI.Remove (ShowUI);
			GameEvents.onHideUI.Remove (HideUI);
		}
		void ShowUI()
		{
			showUI = true;
		}
		void HideUI()
		{
			showUI = false;
		}

		Texture2D buttonTex;
		ApplicationLauncherButton Button;
		void AddApp ()
		{
			buttonTex = GameDatabase.Instance.GetTexture ("KerbalAnimationSuite/Icons/button", false);
			Button = ApplicationLauncher.Instance.AddModApplication (OnTrue, OnFalse, null, null, null, null, ApplicationLauncher.AppScenes.FLIGHT, buttonTex);
		}
		void OnTrue()
		{
			var vessel = FlightGlobals.ActiveVessel;
			if (!vessel.isEVA)
			{
				Button.SetFalse (false);
				return;
			}

			eva = vessel.evaController;
			evaPart = eva.part;
			//PrintFSM ();

			if (eva.isRagdoll)
			{
				Button.SetFalse (false);
				ScreenMessages.PostScreenMessage (new ScreenMessage("Kerbal must be standing on ground to animate", 2.5f, ScreenMessageStyle.UPPER_CENTER), false);
				eva = null;
				evaPart = null;
				return;
			}

			RebuildAnimationClip ();
			States = GetEVAStates ();
			if (States.Find (k => k.name == "KAS_Animation") == null)
			{
				KFSMState state = new KFSMState ("KAS_Animation");
				state.updateMode = KFSMUpdateMode.MANUAL_TRIGGER;
				eva.fsm.AddState (state);

				KFSMEvent enterEvent = new KFSMEvent ("Enter KAS_Animation");
				enterEvent.GoToStateOnEvent = state;
				enterEvent.updateMode = KFSMUpdateMode.MANUAL_TRIGGER;
				var idleGrounded = States.Find (k => k.name == "Idle (Grounded)");
				eva.fsm.AddEvent (enterEvent, idleGrounded);

				KFSMEvent exitEvent = new KFSMEvent ("Exit KAS_Animation");
				exitEvent.GoToStateOnEvent = idleGrounded;
				exitEvent.updateMode = KFSMUpdateMode.MANUAL_TRIGGER;
				eva.fsm.AddEvent (exitEvent, state);
			}
			if (eva.fsm.CurrentState.name == "Idle (Grounded)")
			{
				var enter = eva.fsm.CurrentState.StateEvents.Find (k => k.name == "Enter KAS_Animation");
				if (enter != null)
					eva.fsm.RunEvent (enter);
				else
					Debug.LogError ("failed to run event: Enter KAS_Animation");
			}
			else
			{
				Button.SetFalse (false);
				ScreenMessages.PostScreenMessage (new ScreenMessage("Kerbal must be standing on ground to animate", 2.5f, ScreenMessageStyle.UPPER_CENTER), false);
				eva = null;
				evaPart = null;
				return;
			}
			evaPart.animation.playAutomatically = false;
			evaPart.animation.Stop ();

			evaPart.transform.position += evaPart.transform.up * 10f;
			foreach (var rb in evaPart.GetComponents<Rigidbody>())
			{
				rb.velocity = Vector3.zero;
				rb.constraints = RigidbodyConstraints.FreezeAll;
			}

			joints01 = evaPart.transform.Find("globalMove01/joints01");
			PopulateAnimationNames ();
			SaveAnimationNames ("KerbalAnimationSuite/Config/animation_hierarchy");

			InputLockManager.SetControlLock (ControlTypes.EVA_INPUT | ControlTypes.TIMEWARP | ControlTypes.VESSEL_SWITCHING, "KerbalAnimationSuite_Lock");

			foreach (AnimationState state in evaPart.animation)
			{
				//Debug.Log (state.name + ": Layer: " + state.layer + ", WrapMode: " + state.wrapMode.ToString () + ", BlendMode: " + state.blendMode.ToString () + ", Enabled: " + state.enabled + ", Speed: " + state.speed + ", Length: " + state.length);
			}

			isAnimating = true;
			windowOpen = true;
		}
		void OnFalse()
		{
			PopupDialog.SpawnPopupDialog (new MultiOptionDialog ("Are you sure? You will lose all unsaved work if you do this!", "", skin, new DialogOption ("No, Don't Leave", delegate {
				Button.SetTrue(false);
			}, true), new DialogOption ("Yes, Leave", delegate {
				StopAnimating();
			}, true)), false, skin);
		}
		void StopAnimating()
		{
			if (eva.fsm.CurrentState.name == "KAS_Animation")
			{
				var exit = eva.fsm.CurrentState.StateEvents.Find (k => k.name == "Exit KAS_Animation");
				if (exit != null)
					eva.fsm.RunEvent (exit);
				else
					Debug.LogError ("failed to run event: Exit KAS_Animation");
			}
			evaPart.animation.playAutomatically = true;

			SetHelmet (true);

			evaPart.transform.position -= evaPart.transform.up * 9.75f;
			foreach (var rb in evaPart.GetComponents<Rigidbody>())
			{
				rb.velocity = Vector3.zero;
				rb.constraints = RigidbodyConstraints.None;
			}

			InputLockManager.RemoveControlLock ("KerbalAnimationSuite_Lock");

			rotation = new Vector3 ();
			position = new Vector3 ();

			isAnimating = false;
			windowOpen = false;
			eva = null;
			evaPart = null;
			joints01 = null;
		}


		Part evaPart;
		KerbalEVA eva;
		Transform joints01;
		List<KFSMState> States;

		void Update()
		{
			if (isAnimating)
			{
				if(!isPlayingAnimation)
					UpdateManipulation ();
				UpdateAnimation ();
			}
		}
		bool showUI = true;

		bool windowOpen = false;
		Rect masterRect = new Rect (Screen.width - 250f, 50f, 250f, 140f);
		Rect hierarchyWindowRect = new Rect(0f, 50f, 500f, 525f);
		Rect manipulationWindowRect = new Rect(Screen.width - 500f, Screen.height - 500f, 500f, 300f);
		Rect animationWindowRect = new Rect(0f, Screen.height - 300f, 600, 300f);
		void OnGUI()
		{
			if (!showUI)
				return;

			GUI.skin = skin;

			if (windowOpen)
			{
				masterRect = GUILayout.Window ("KerbalAnimationSuite_Master".GetHashCode (), masterRect, MasterWindow, "Kerbal Animation Suite");
				if(hierarchy && !isPlayingAnimation)
					hierarchyWindowRect = GUILayout.Window ("KerbalAnimationSuite_Hierarchy".GetHashCode (), hierarchyWindowRect, HierarchyWindow, "Bone Hierarchy");
				if(manipulation && !isPlayingAnimation)
					manipulationWindowRect = GUILayout.Window ("KerbalAnimationSuite_Manipulation".GetHashCode (), manipulationWindowRect, ManipulationWindow, "Manipulation");
				if (animationOpen)
					animationWindowRect = GUILayout.Window ("KerbalAnimationSuite_Animation".GetHashCode (), animationWindowRect, AnimationWindow, "Animation");
			}
		}
		public bool isAnimating = false;

		bool hierarchy = true;
		bool manipulation = true;
		bool animationOpen = true;

		string loadPath = "";
		void MasterWindow(int id)
		{
			GUILayout.BeginVertical (skin.scrollView);
			if (!isPlayingAnimation)
				hierarchy = GUILayout.Toggle (hierarchy, "Bone Hierarchy", skin.button);
			else
				GUILayout.Toggle (false, "Bone Hierarchy", skin.button);
			if (!isPlayingAnimation)
				manipulation = GUILayout.Toggle (manipulation, "Manipulation", skin.button);
			else
				GUILayout.Toggle (false, "Manipulation", skin.button);
			animationOpen = GUILayout.Toggle (animationOpen, "Animation", skin.button);

			GUILayout.EndVertical ();
		}


		#region Hierarchy
		//hierarchy
		Vector2 hierarchyScroll = new Vector2 ();
		Vector2 boneSelectionScroll = new Vector2 ();
		bool showRawHierarchy = false;
		void HierarchyWindow(int id)
		{
			if (evaPart != null)
			{
				if (GUILayout.Button (showRawHierarchy ? "Show Bone Selection" : "Show Hierarchy Tree"))
				{
					showRawHierarchy = !showRawHierarchy;
				}

				if (showRawHierarchy)
				{
					hierarchyScroll = GUILayout.BeginScrollView (hierarchyScroll);

					DrawHierarchy (joints01, 0);

					GUILayout.EndScrollView ();

					if (GUILayout.Button ("Print Hierarchy"))
					{
						PrintTransform (evaPart.transform, 0);
					}
				}
				else
				{
					boneSelectionScroll = GUILayout.BeginScrollView (boneSelectionScroll);

					//legs
					GUILayout.Label ("<b>Legs:</b>");
					GUILayout.BeginHorizontal ();

					GUILayout.BeginVertical (skin.box);
					DrawBoneSelection ("bn_l_hip01");
					DrawBoneSelection ("bn_l_knee_b01");
					DrawBoneSelection ("bn_l_foot01");
					DrawBoneSelection ("bn_l_ball01");
					GUILayout.EndVertical ();
					//GUILayout.Space (2f);
					GUILayout.BeginVertical (skin.box);
					DrawBoneSelection ("bn_r_hip01");
					DrawBoneSelection ("bn_r_knee_b01");
					DrawBoneSelection ("bn_r_foot01");
					DrawBoneSelection ("bn_r_ball01");
					GUILayout.EndVertical ();

					GUILayout.EndHorizontal();
					GUILayout.Space (10f);

					//arms
					GUILayout.Label ("<b>Arms:</b>");
					GUILayout.BeginHorizontal ();

					GUILayout.BeginVertical (skin.box);
					DrawBoneSelection ("bn_l_shld01");
					DrawBoneSelection ("bn_l_arm01 1");
					DrawBoneSelection ("bn_l_elbow_a01");
					DrawBoneSelection ("bn_l_elbow_b01");
					DrawBoneSelection ("bn_l_wrist01");
					GUILayout.Space (5f);
					GUILayout.Label ("<color=orange>Left Hand</color>");
					DrawBoneSelection ("bn_l_mid_a01");
					DrawBoneSelection ("bn_l_mid_b01");
					DrawBoneSelection ("bn_l_thumb_a01");
					DrawBoneSelection ("bn_l_thumb_b01");
					DrawBoneSelection ("bn_l_thumb_c01");
					GUILayout.EndVertical ();
					//GUILayout.Space (2f);
					GUILayout.BeginVertical (skin.box);
					DrawBoneSelection ("bn_r_shld01");
					DrawBoneSelection ("bn_r_arm01 1");
					DrawBoneSelection ("bn_r_elbow_a01");
					DrawBoneSelection ("bn_r_elbow_b01");
					DrawBoneSelection ("bn_r_wrist01");
					GUILayout.Space (5f);
					GUILayout.Label ("<color=orange>Right Hand</color>");
					DrawBoneSelection ("bn_r_mid_a01");
					DrawBoneSelection ("bn_r_mid_b01");
					DrawBoneSelection ("bn_r_thumb_a01");
					DrawBoneSelection ("bn_r_thumb_b01");
					DrawBoneSelection ("bn_r_thumb_c01");
					GUILayout.EndVertical ();

					GUILayout.EndHorizontal();
					GUILayout.Space (10f);

					//spine/neck
					GUILayout.Label ("<b>Spine:</b>");
					GUILayout.BeginHorizontal ();

					GUILayout.BeginVertical (skin.box);
					DrawBoneSelection ("bn_spB01");
					DrawBoneSelection ("bn_spc01");
					DrawBoneSelection ("bn_spD01");
					DrawBoneSelection ("bn_neck01");
					DrawBoneSelection ("bn_headPivot_b01");
					DrawBoneSelection ("bn_helmet01");
					GUILayout.EndVertical ();

					GUILayout.EndHorizontal();
					GUILayout.Space (10f);

					//face
					GUILayout.Label ("<b>Face:</b>");
					GUILayout.BeginHorizontal ();

					GUILayout.BeginVertical (skin.box);
					DrawBoneSelection ("bn_lowerJaw01");
					DrawBoneSelection ("bn_upperJaw01");
					GUILayout.Space (5f);
					DrawBoneSelection ("bn_lowerTeeth01");
					DrawBoneSelection ("bn_upperTeet01");
					GUILayout.Space (5f);
					GUILayout.Label ("<color=orange>Eyes</color>");
					GUILayout.BeginHorizontal ();
					DrawBoneSelection ("jntDrv_l_eye01");
					DrawBoneSelection ("jntDrv_r_eye01");
					GUILayout.EndHorizontal ();
					GUILayout.Space (10f);
					GUILayout.Label ("<color=orange>Mouth</color>");
					GUILayout.BeginHorizontal ();
					GUILayout.BeginVertical ();
					GUILayout.Label ("<color=orange>Lower Lip</color>");
					DrawBoneSelection ("bn_l_mouthCorner01");
					DrawBoneSelection ("bn_l_mouthLow_d01");
					DrawBoneSelection ("bn_l_mouthLow_c01");
					DrawBoneSelection ("bn_l_mouthLow_b01");
					DrawBoneSelection ("bn_l_mouthLowMid_a01");
					DrawBoneSelection ("bn_r_mouthLow_b01");
					DrawBoneSelection ("bn_r_mouthLow_c01");
					DrawBoneSelection ("bn_r_mouthLow_d01");
					DrawBoneSelection ("bn_r_mouthCorner01");
					GUILayout.EndVertical ();
					GUILayout.BeginVertical ();
					GUILayout.Label ("<color=orange>Upper Lip</color>");
					DrawBoneSelection ("bn_l_mouthUp_d01");
					DrawBoneSelection ("bn_l_mouthUp_c01");
					DrawBoneSelection ("bn_l_mouthUp_b01");
					DrawBoneSelection ("bn_l_mouthUpMid_a01");
					DrawBoneSelection ("bn_r_mouthUp_b01");
					DrawBoneSelection ("bn_r_mouthUp_c01");
					DrawBoneSelection ("bn_r_mouthUp_d01");
					GUILayout.EndVertical ();
					GUILayout.EndHorizontal ();

					GUILayout.EndVertical ();

					GUILayout.EndHorizontal();
					GUILayout.Space (10f);

					GUILayout.EndScrollView ();
				}
			}
			else
				Debug.Log("error");

			GUI.DragWindow ();
		}
		void DrawHierarchy(Transform t, int level)
		{
			foreach (Transform child in t)
			{
				GUILayout.BeginVertical (skin.box);

				string indent = "";
				for(int i = 0; i < level; i++)
					indent += " |";
				string name = ReadableNames.ContainsKey (child.name) ? ReadableNames [child.name] : null;
				if (AnimationNames.ContainsKey(child.name))
					name = "<color=orange>" + name + "</color>";

				GUILayout.BeginHorizontal ();
				GUILayout.Label (indent + child.name + " (" + name + ")");

				if (animPage == 1 && animationOpen)
				{
					GUI.backgroundColor = Color.green;
					if (GUILayout.Button ("+", GUILayout.Width (30f)))
					{
						if (AnimationNames.ContainsKey (child.name))
						{
							animationClip.AddMixingTransform (child.name);
							addMTErrorText = "";
							tempMTToAdd = "";
						}
						else
						{
							addMTErrorText = "Bone " + child.name + " not found!";
							tempMTToAdd = "";
						}
					}
					GUI.backgroundColor = Color.white;
				}

				GUILayout.EndHorizontal ();
				GUILayout.EndVertical ();

				DrawHierarchy (child, level + 1);
			}
		}
		void PrintTransform(Transform t, int level)
		{
			string indent = "";
			for(int i = 0; i < level; i++)
				indent += "   |";
			Debug.Log (indent + t.name);
			foreach (Transform child in t)
			{
				PrintTransform (child, level + 1);
			}
		}


		//manipulation
		string currentBone = "";
		Transform currentBoneTrns;
		Vector3 rotation;
		Vector3 position;

		void UpdateManipulation()
		{
			if(!tempSelected)
			{
				currentBoneTrns = null;
				currentBone = "";
			}

			if (currentBoneTrns != null)
			{
				currentBoneTrns.localRotation = Quaternion.Euler(rotation);
				currentBoneTrns.localPosition = position;
			}
		}

		Vector2 manipulationScroll = new Vector2();
		void ManipulationWindow(int id)
		{
			manipulationScroll = GUILayout.BeginScrollView (manipulationScroll);

			GUILayout.Label ("Rotation");
			rotation.x = DrawManipulationSlider ("X", rotation.x, 0f, 360f);
			rotation.y = DrawManipulationSlider ("Y", rotation.y, 0f, 360f);
			rotation.z = DrawManipulationSlider ("Z", rotation.z, 0f, 360f);
			GUILayout.Space (20f);

			GUILayout.Label ("Relative Position");
			position.x = DrawManipulationSlider ("X", position.x, -0.5f, 0.5f);
			position.y = DrawManipulationSlider ("Y", position.y, -0.5f, 0.5f);
			position.z = DrawManipulationSlider ("Z", position.z, -0.5f, 0.5f);

			GUILayout.Space (10f);
			if (GUILayout.Button ("Toggle Helmet"))
			{
				SetHelmet (!hasHelmet);
			}

			GUILayout.EndScrollView ();

			GUI.DragWindow ();
		}
		float DrawManipulationSlider(string name, float value, float min, float max)
		{
			GUILayout.BeginHorizontal ();

			GUILayout.Label ("<b><color=orange>" + name + ":</color></b>");
			float v = GUILayout.HorizontalSlider (value, min, max);

			float vD = Mathf.Round (v * 100f) / 100f;
			string vString = vD.ToString ();
			GUILayout.Label (vString);

			GUILayout.EndHorizontal ();

			return v;
		}

		bool tempSelected = true;
		void DrawBoneSelection(string boneName)
		{
			string name = ReadableNames.ContainsKey (boneName) ? ReadableNames [boneName] : boneName;

			if (tempSelected && currentBone == boneName)
			{
				tempSelected = GUILayout.Toggle (tempSelected, "<color=lime>" + name + "</color>", skin.button);
			}
			else if (GUILayout.Button (name))
			{
				currentBone = boneName;
				string animationName = AnimationNames.ContainsKey (boneName) ? AnimationNames [boneName] : "none found :C";
				currentBoneTrns = AnimationNames.ContainsKey (boneName) ? evaPart.transform.Find (AnimationNames [boneName]) : null;
				if (currentBoneTrns == null)
				{
					currentBone = "";
					Debug.LogError ("null bone: " + boneName + " at " + animationName);
				}
				else
				{
					rotation = currentBoneTrns.localEulerAngles;
					position = currentBoneTrns.localPosition;
					tempSelected = true;
					Debug.Log ("bone " + boneName + " selected at " + animationName);
				}
			}
		}
		#endregion


		//animation
		KAS_AnimationClip animationClip = new KAS_AnimationClip();
		KAS_Keyframe currentKeyframe;
		Texture2D timelineArrowTex;

		void UpdateAnimation()
		{

		}
		List<KFSMState> GetEVAStates()
		{
			var fsm = eva.fsm;

			var type = fsm.GetType();
			var statesF = type.GetField ("States", BindingFlags.NonPublic | BindingFlags.Instance);
			List<KFSMState> states = (List<KFSMState>)statesF.GetValue (fsm);
			return states;
		}
		void PrintFSM()
		{
			var fsm = eva.fsm;
			Debug.Log ("CurrentState: " + fsm.CurrentState.name);

			var type = fsm.GetType();
			Debug.Log ("Type: " + type.Name);
			var statesF = type.GetField ("States", BindingFlags.NonPublic | BindingFlags.Instance);
			List<KFSMState> states = (List<KFSMState>)statesF.GetValue (fsm);

			foreach (var state in states)
			{
				if (state == null)
				{
					Debug.LogWarning ("null state found, skipping");
					continue;
				}
				Debug.Log ("State: " + state.name + " : " + state.updateMode.ToString ());
				if (state.StateEvents == null)
				{
					Debug.LogWarning ("No state events list found");
					continue;
				}
				foreach (var evt in state.StateEvents)
				{
					if (evt == null)
					{
						Debug.LogWarning ("null evt found, skipping");
						continue;
					}
					Debug.Log ("----- Event: " + evt.name + ": => " + (evt.GoToStateOnEvent != null ? evt.GoToStateOnEvent.name : "N/A") + " : " + evt.updateMode.ToString ());
				}
			}
		}

		float time = 0.0f;
		bool isPlayingAnimation = false;
		float tempNormalizedTime = 0.95f;

		Vector2 animScroll = new Vector2();
		Vector2 settingScroll = new Vector2();

		string tempMTToAdd = "";
		string addMTErrorText = "";

		string loadAnimErrorText = "";
		string tempSavePath = "";
		string saveAnimErrorText = "";

		int animPage = 0;
		int animPageCount = 2;
		void AnimationWindow(int id)
		{
			//TODO: add tangent controls
			//TODO: add sanity checks for saving/loading

			switch(animPage)
			{
			case 0:
				animScroll = GUILayout.BeginScrollView (animScroll);

				if(!isPlayingAnimation)
				{
					if (GUILayout.Button ("Create Keyframe (" + animationClip.Keyframes.Count + ")"))
					{
						if (animationClip != null)
						{
							animationClip.CreateKeyframe (evaPart.transform, time);
							RebuildAnimationClip ();
							SetAnimationTime (time);
							time = 0.95f;
						}
						else
							Debug.LogError ("AnimationClip is null!");
					}
					if (animationClip.KeyframesCount > 0)
					{
						GUILayout.BeginHorizontal ();
						GUILayout.Label ("<b>Create new at time:</b>");
						time = GUILayout.HorizontalSlider (time, 0f, 1f);
						GUILayout.Label ((Mathf.Round (time * 100f) / 100f).ToString ());
						GUILayout.EndHorizontal ();
						GUILayout.Space (20f);
						

						GUILayout.BeginHorizontal (skin.horizontalSlider);
						GUILayout.EndHorizontal();

						Rect sliderRect = GUILayoutUtility.GetLastRect ();
						float width = sliderRect.width - 20f;
						for(int i = 0; i < animationClip.KeyframesCount; i++)
						{
							var keyframe = animationClip.Keyframes [i];

							if(currentKeyframe == keyframe)
							{
								GUI.backgroundColor = Color.green;
								if (GUI.Button (new Rect (sliderRect.xMin + (tempNormalizedTime * width), sliderRect.yMin, 20f, 20f), i.ToString()))
								{
									Debug.Log ("Time: " + keyframe.Time);
									currentKeyframe = null;
									tempNormalizedTime = 0.95f;
									SetAnimationTime (1f);
								}
							}
							else
							{
								if (keyframe.NormalizedTime == 1f || keyframe.NormalizedTime == 0f)
									GUI.backgroundColor = Color.yellow;
								else
									GUI.backgroundColor = Color.white;
								if (GUI.Button (new Rect (sliderRect.xMin + (keyframe.NormalizedTime * width), sliderRect.yMin, 20f, 12f), i.ToString()))
								{
									Debug.Log ("Time: " + keyframe.Time);
									currentKeyframe = keyframe;
									tempNormalizedTime = currentKeyframe.NormalizedTime;
									SetAnimationTime (currentKeyframe.NormalizedTime);
								}
								GUI.backgroundColor = Color.white;
							}
						}
						GUI.backgroundColor = Color.white;

						GUI.DrawTexture (new Rect (sliderRect.xMin + (GetAnimationTime () * width), sliderRect.yMin - 16f, 16f, 16f), timelineArrowTex);

						if (currentKeyframe != null)
						{
							GUILayout.Space (10f);
							GUILayout.Label ("<b>Time:</b>");
							tempNormalizedTime = GUILayout.HorizontalSlider (tempNormalizedTime, 0f, 1f);

							GUILayout.BeginHorizontal ();
							if (GUILayout.Button ("Delete Keyframe") || Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
							{
								animationClip.Keyframes.Remove (currentKeyframe);
								currentKeyframe = null;
							}
							if (GUILayout.Button ("Save Keyframe"))
							{
								animationClip.Keyframes.Remove (currentKeyframe);
								animationClip.CreateKeyframe (evaPart.transform, tempNormalizedTime);
								currentKeyframe = null;
								tempNormalizedTime = 0.95f;
								RebuildAnimationClip ();
							}

							if (GUILayout.Button ("Cancel"))
							{
								currentKeyframe = null;
								tempNormalizedTime = 0.95f;
							}
							GUILayout.EndHorizontal ();
						}

						GUILayout.Space (20f);
						GUILayout.BeginHorizontal ();
						GUILayout.Label ("Animation Duration: ");
						string duration = animationClip.Duration.ToString();
						duration = GUILayout.TextField (duration);
						animationClip.Duration = float.Parse (duration);
						GUILayout.EndHorizontal ();
					}
					else
					{
						GUILayout.Space (60f);

						GUILayout.BeginHorizontal ();
						GUILayout.Label ("Load from: <color=white>/GameData/</color>");
						loadPath = GUILayout.TextField (loadPath);
						GUILayout.EndHorizontal ();
						if (GUILayout.Button ("Load Animation"))
						{
							try
							{
								KAS_AnimationClip clip = new KAS_AnimationClip();
								clip.LoadURL(loadPath);
								animationClip = clip;
								loadAnimErrorText = "";
							}
							catch
							{
								loadAnimErrorText = "Couldn't find animation at " + loadPath;
							}
						}
						if (loadAnimErrorText != null && loadAnimErrorText != "")
						{
							GUILayout.Label ("<color=red>" + loadAnimErrorText + "</color>");
						}
					}
				}
				else
				{

					GUILayout.Space (20f);

					GUILayout.BeginHorizontal (skin.horizontalSlider);
					GUILayout.EndHorizontal();

					Rect sliderRect = GUILayoutUtility.GetLastRect ();
					float width = sliderRect.width - 20f;

					if (animationClip.KeyframesCount > 0)
					{
						for(int i = 0; i < animationClip.Keyframes.Count; i++)
						{
							var keyframe = animationClip.Keyframes [i];

							if (keyframe.NormalizedTime == 1f || keyframe.NormalizedTime == 0f)
								GUI.backgroundColor = Color.yellow;
							else
								GUI.backgroundColor = Color.white;
							if (GUI.Button (new Rect (sliderRect.xMin + (keyframe.NormalizedTime * width), sliderRect.yMin, 20f, 12f), i.ToString()))
							{
							}
							GUI.backgroundColor = Color.white;
						}
						GUI.backgroundColor = Color.white;
					}

					GUI.DrawTexture (new Rect (sliderRect.xMin + (GetAnimationTime () * width), sliderRect.yMin - 16f, 16f, 16f), timelineArrowTex);
				}

				GUILayout.Space (10f);
				GUILayout.Label ("<color=lime><b>Is Playing:</b></color> <color=white>" + evaPart.animation.isPlaying + "</color>");
				GUILayout.Label ("<color=lime><b>Current State:</b></color> <color=white>" + eva.fsm.CurrentState.name + "</color>");

				GUILayout.EndScrollView();
				break;
			case 1:
				settingScroll = GUILayout.BeginScrollView (settingScroll);

				GUILayout.BeginHorizontal ();

				GUILayout.Label ("<b>Mixing Transforms</b>");
				tempMTToAdd = GUILayout.TextField (tempMTToAdd);
				if (addMTErrorText != null && addMTErrorText != "") {
					GUILayout.Label ("<color=red>" + addMTErrorText + "</color>");
				}

				GUI.backgroundColor = Color.green;
				if (GUILayout.Button ("+", GUILayout.Width (30f))) {
					if (AnimationNames.ContainsKey (tempMTToAdd)) {
						animationClip.AddMixingTransform (tempMTToAdd);
						addMTErrorText = "";
						tempMTToAdd = "";
					} else {
						addMTErrorText = "Bone " + tempMTToAdd + " not found!";
						tempMTToAdd = "";
					}
				}
				GUI.backgroundColor = Color.white;

				GUILayout.EndHorizontal ();

				string toDelete = null;
				foreach (var mt in animationClip.MixingTransforms) {
					GUILayout.BeginHorizontal ();

					GUILayout.Label (mt, skin.textField);
					GUI.backgroundColor = Color.red;
					if (GUILayout.Button ("X", GUILayout.Width (30f))) {
						toDelete = mt;
					}
					GUI.backgroundColor = Color.white;

					GUILayout.EndHorizontal ();
				}
				if (toDelete != null || toDelete != "") {
					animationClip.RemoveMixingTransform (toDelete);
				}

				GUILayout.Space (10f);
				GUILayout.Label ("<b>Animation Settings</b>");

				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Name: ");
				animationClip.Name = GUILayout.TextField (animationClip.Name);
				GUILayout.EndHorizontal ();
				GUILayout.Space (5f);

				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Save to: <color=white>/GameData/</color>");
				tempSavePath = GUILayout.TextField (tempSavePath);
				GUILayout.EndHorizontal ();
				if (GUILayout.Button ("Save Animation"))
				{
					try
					{
						animationClip.Save (tempSavePath);
						saveAnimErrorText = "";
					}
					catch
					{
						saveAnimErrorText = "couldn't save animation to " + tempSavePath;
					}
				}
				if(saveAnimErrorText != null && saveAnimErrorText != "")
				{
					GUILayout.Label ("<color=red>" + saveAnimErrorText + "</color>");
				}

				GUILayout.EndScrollView ();
				break;
			default:
				break;
			}

			GUILayout.BeginHorizontal ();
			if (animPage > 0)
			{
				if (GUILayout.Button ("<", GUILayout.Width (30f)))
				{
					animPage -= 1;
				}
			}

			if (!isPlayingAnimation && GUILayout.Button ("Play Animation"))
			{
				RebuildAnimationClip ();

				isPlayingAnimation = true;
				currentBoneTrns = null;
				currentBone = "";
				rotation = new Vector3 ();
				position = new Vector3 ();

				Debug.Log ("Playing...");
				evaPart.animation.Play ("CustomClip");
			}
			if (isPlayingAnimation && GUILayout.Button ("Stop Animation"))
			{
				evaPart.animation.Stop ();
				SetAnimationTime (1f);
				Debug.Log ("Stopping...");
				isPlayingAnimation = false;
			}

			if (animPage < animPageCount - 1)
			{
				if (GUILayout.Button (">", GUILayout.Width (30f)))
				{
					animPage += 1;
				}
			}

			GUILayout.EndHorizontal ();

			GUI.DragWindow ();
		}

		void RebuildAnimationClip()
		{
			Debug.Log ("Rebuilding clip");
			var clip = animationClip.BuildAnimationClip ();
			evaPart.animation.RemoveClip ("CustomClip");
			evaPart.animation.AddClip (clip, "CustomClip");
			foreach(var mt in animationClip.MixingTransforms)
			{
				evaPart.animation ["CustomClip"].AddMixingTransform (evaPart.transform.Find(AnimationNames [mt]));
			}
		}
		void SetAnimationTime(float normalizedTime, string animationName = "CustomClip")
		{
			if(evaPart.animation.isPlaying)
				evaPart.animation [animationName].normalizedTime = normalizedTime;
			else
			{
				evaPart.animation.Play (animationName);
				evaPart.animation [animationName].normalizedTime = normalizedTime;
				evaPart.animation.Sample ();
				evaPart.animation.Stop ();
			}
		}
		float GetAnimationTime(string animationName = "CustomClip", bool clamped = true)
		{
			if(!clamped)
				return evaPart.animation [animationName].normalizedTime;
			else
			{
				float time = evaPart.animation [animationName].normalizedTime;
				float floor = Mathf.Floor (time);
				return time - floor;
			}
		}


		private const string bone_prefix = "bn";
		private const string bone_ending_prefix = "be";


		void LoadReadableNames(ConfigNode node)
		{
			ReadableNames = new Dictionary<string, string> ();
			foreach (ConfigNode.Value value in node.values)
			{
				//replace + with space
				string name = value.name.Replace ('+', ' ');
				ReadableNames.Add (name, value.value);
			}
		}
		void PopulateAnimationNames()
		{
			if (joints01 == null)
			{
				Debug.Log ("joints01 is null, cannot populate animation names");
				return;
			}

			string prefix = "globalMove01";
			AnimationNames = new Dictionary<string, string> ();

			PopulateAnimationNamesRecursive (joints01, prefix);
		}
		void PopulateAnimationNamesRecursive(Transform t, string prefix)
		{
			prefix += "/" + t.name;
			AnimationNames.Add (t.name, prefix);
			foreach (Transform child in t)
			{
				PopulateAnimationNamesRecursive (child, prefix);
			}
		}

		void SaveAnimationNames(string url)
		{
			ConfigNode node = new ConfigNode ();
			foreach (var name in AnimationNames)
			{
				node.AddValue (name.Key, name.Value);
			}
			node.Save (KSPUtil.ApplicationRootPath + "GameData/" + url + ".dat");
		}
		void LoadAnimationNames(string url)
		{
			ConfigNode node = ConfigNode.Load (KSPUtil.ApplicationRootPath + "GameData/" + url + ".dat");
			AnimationNames.Clear ();
			foreach (ConfigNode.Value value in node.values)
			{
				AnimationNames.Add (value.name, value.value);
			}
		}

		bool hasHelmet = true;
		void SetHelmet(bool value)
		{
			foreach (var rend in eva.GetComponentsInChildren<Renderer>())
			{
				if(rend.name == "helmet" || rend.name == "visor" || rend.name == "flare1" || rend.name == "flare2")
				{
					rend.enabled = value;
				}
			}
			hasHelmet = value;
		}
	}
}

