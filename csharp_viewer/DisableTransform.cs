using System;

using OpenTK;

namespace csharp_viewer
{
	//[Serializable]
	public class DisableTransform : ImageTransform
	{
		public DisableTransform()
		{
			skipImageInterval = UpdateInterval.Static;
		}

		public override bool SkipImage(int[] imagekey, TransformedImage image)
		{
			return true;
		}
	}
}

