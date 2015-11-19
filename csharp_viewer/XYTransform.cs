using System;

using OpenTK;

namespace csharp_viewer
{
	//[Serializable]
	public class XYTransform : ImageTransform
	{
		private struct Point<T>
		{
			public T x, y;
		}
		private Point<int> idx;

		public override void LocationTransform(int[] imagekey, TransformedImage image, out Vector3 pos, ref Quaternion rot, ref Vector3 scl)
		{
			float x = (float)imagekey[idx.x] - 2.1f;
			float y = (float)imagekey[idx.y] - 2.1f;

			pos = new Vector3(x * 2.1f, y * 2.1f, 0.0f);
		}
	}
}

