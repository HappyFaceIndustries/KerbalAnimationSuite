using System;
using UnityEngine;

namespace KerbalAnimation
{
	public abstract class Window
	{
		protected Rect WindowRect;
		protected string WindowTitle = "";
		protected bool AllowDrag = true;
		protected bool ExpandWidth = false;
		protected bool ExpandHeight = false;

		protected abstract void DrawWindow ();
		public abstract void Update();

		protected int ID = Guid.NewGuid().ToString().GetHashCode();
		public void Draw()
		{
			WindowRect = GUILayout.Window (ID, WindowRect, WindowDelegate, WindowTitle, GUILayout.ExpandWidth (ExpandWidth), GUILayout.ExpandHeight (ExpandHeight));
		}
		private void WindowDelegate(int id)
		{
			DrawWindow ();
			if (AllowDrag)
				GUI.DragWindow ();
		}

		public KerbalAnimationSuite Suite
		{
			get{return KerbalAnimationSuite.Instance;}
		}
		public GUISkin skin
		{
			get{return Suite.skin;}
		}
	}
}

