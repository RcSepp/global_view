using System;

using OpenTK;

namespace csharp_viewer
{
	[Serializable]
	public class DisableTransform : ImageTransform
	{
		public override int GetIndex(int i) {return -1;}
		public override int SetIndex(int i, int index) {return -1;}

		public DisableTransform()
		{
			SkipImageInterval = UpdateInterval.Static;
		}

		public override bool SkipImage(int[] imagekey, TransformedImage image)
		{
			return true;
		}
	}
}

