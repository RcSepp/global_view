using System;

using OpenTK;

namespace csharp_viewer
{
	[Serializable]
	public class ThetaPhiTransform : ImageTransform
	{
		public override int GetIndex(int i) {switch(i) {case 0: return idx.theta; case 1: return idx.phi; default: return -1;}}
		public override int SetIndex(int i, int index) {switch(i) {case 0: return idx.theta = index; case 1: return idx.phi = index; default: return -1;}}

		private struct Selection<T>
		{
			public T theta, phi;
		}
		private Selection<int> idx;

		public override void LocationTransform(int[] imagekey, TransformedImage image, out Vector3 pos, ref Quaternion rot, ref Vector3 scl)
		{
			float theta = (float)image.values[idx.theta] / 180.0f * MathHelper.Pi;
			float phi = -(float)image.values[idx.phi] / 180.0f * MathHelper.Pi;

			//pos = new Vector3((float)(Math.Cos(theta) * Math.Sin(phi)) * 4.1f, (float)Math.Sin(theta) * 4.1f, (float)(Math.Cos(theta) * Math.Cos(phi)) * 4.1f);
			pos = new Vector3(10.0f * (float)(Math.Sin(phi) * Math.Cos(theta)), 10.0f * (float)Math.Cos(phi), 10.0f * (float)(Math.Sin(phi) * Math.Sin(theta)));
		}

		public override AABB GetImageBounds(int[] imagekey, TransformedImage image)
		{
			float theta = (float)image.values[idx.theta] / 180.0f * MathHelper.Pi;
			float phi = -(float)image.values[idx.phi] / 180.0f * MathHelper.Pi;

			//Vector3 pos = new Vector3((float)(Math.Cos(theta) * Math.Sin(phi)) * 4.1f, (float)Math.Sin(theta) * 4.1f, (float)(Math.Cos(theta) * Math.Cos(phi)) * 4.1f);
			Vector3 pos = new Vector3(10.0f * (float)(Math.Sin(phi) * Math.Cos(theta)), 10.0f * (float)Math.Cos(phi), 10.0f * (float)(Math.Sin(phi) * Math.Sin(theta)));

			return new AABB(pos - new Vector3(0.5f, 0.5f, 0.5f), pos + new Vector3(0.5f, 0.5f, 0.5f));
		}

		public override bool SkipImage(int[] imagekey, TransformedImage image)
		{
			if(((float)image.values[idx.theta] < -90.0f || (float)image.values[idx.theta] > 90.0f) ||
				(((float)image.values[idx.theta] == -90.0f || (float)image.values[idx.theta] == 90.0f) && (float)image.values[idx.phi] != 0.0f))
				return true;
			return false;
		}
	}

	[Serializable]
	public class ThetaTransform : ImageTransform
	{
		public override int GetIndex(int i) {switch(i) {case 0: return idx; default: return -1;}}
		public override int SetIndex(int i, int index) {switch(i) {case 0: return idx = index; default: return -1;}}

		private int idx;

		public override void LocationTransform(int[] imagekey, TransformedImage image, out Vector3 pos, ref Quaternion rot, ref Vector3 scl)
		{
			float theta = (float)image.values[idx] / 180.0f * MathHelper.Pi;
			pos = new Vector3(0.0f, (float)Math.Cos(theta) * 2.1f, (float)Math.Sin(theta) * 2.1f);
		}
	}

	[Serializable]
	public class PhiTransform : ImageTransform
	{
		public override int GetIndex(int i) {switch(i) {case 0: return idx; default: return -1;}}
		public override int SetIndex(int i, int index) {switch(i) {case 0: return idx = index; default: return -1;}}

		private int idx;

		public override void LocationTransform(int[] imagekey, TransformedImage image, out Vector3 pos, ref Quaternion rot, ref Vector3 scl)
		{
			float phi = (float)image.values[idx] / 180.0f * MathHelper.Pi;
			pos = new Vector3((float)Math.Sin(phi) * 2.1f, 0.0f, (float)Math.Cos(phi) * 2.1f);
		}
	}
}

