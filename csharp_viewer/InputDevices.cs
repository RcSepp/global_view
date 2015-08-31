using System;

using OpenTK.Input;

namespace csharp_viewer
{
	public static class InputDevices
	{
		public static KeyboardState kbstate;
		public static MouseState mstate;
		public static float mdx, mdy, mdz;

		private static float oldx = float.NaN, oldy = float.NaN, oldz = float.NaN;
		public static void Update()
		{
			kbstate = Keyboard.GetState();
			mstate = Mouse.GetState();

			if(float.IsNaN(oldx))
				mdx = mdy = 0.0f;
			else
			{
				mdx = mstate.X - oldx;
				mdy = mstate.Y - oldy;
				mdz = mstate.WheelPrecise - oldz;
			}

			oldx = mstate.X;
			oldy = mstate.Y;
			oldz = mstate.WheelPrecise;
		}
	}
}

