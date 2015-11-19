using System;

using OpenTK;

namespace csharp_viewer
{
	//[Serializable]
	public class AnimationTransform : ImageTransform
	{
		private int idx, numframes, currentframe;
		float t;
		Cinema.CinemaArgument[] arguments;

		public float animationSpeed = 10.0f;

		public AnimationTransform()
		{
			SkipImageInterval = UpdateInterval.Dynamic;
		}

		public override void OnRender(float dt, ImageCloud.FreeView freeview)
		{
			t += animationSpeed * dt;
			t -= (float)(((int)t / numframes) * numframes);
			currentframe = (int)t;
		}

		public override bool SkipImage(int[] imagekey, TransformedImage image)
		{
			return imagekey[idx] != currentframe;
		}
	}
}

