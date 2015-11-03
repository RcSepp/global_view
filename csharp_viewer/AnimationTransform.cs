using System;

using OpenTK;

namespace csharp_viewer
{
	[Serializable]
	public class AnimationTransform : ImageTransform
	{
		public override int GetIndex(int i) {switch(i) {case 0: return idx; default: return -1;}}
		public override int SetIndex(int i, int index)
		{
			switch(i)
			{
			case 0:
				numframes = arguments[index].values.Length;
				return idx = index;
			default:
				return -1;
			}
		}
		public override void SetArguments(Cinema.CinemaArgument[] arguments)
		{
			this.arguments = arguments;
			numframes = arguments[idx].values.Length;
		}

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

