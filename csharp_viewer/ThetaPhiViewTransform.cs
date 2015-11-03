using System;

using OpenTK;

namespace csharp_viewer
{
	[Serializable]
	public class _ThetaPhiViewTransform : ImageTransform
	{
		public override int GetIndex(int i) {switch(i) {case 0: return idx.theta; case 1: return idx.phi; default: return -1;}}
		public override int SetIndex(int i, int index)
		{
			switch(i)
			{
			case 0:
				idx.theta = index;
				OnChangeIndex();
				return index;
			case 1:
				idx.phi = index;
				OnChangeIndex();
				return index;
			default:
				return -1;
			}
		}
		public override void SetArguments(Cinema.CinemaArgument[] arguments)
		{
			this.arguments = arguments;
			OnChangeIndex();
		}

		private struct Selection<T>
		{
			public T theta, phi;
		}
		private Selection<int> idx;
		Cinema.CinemaArgument[] arguments;
		private class ImageAndAngle
		{
			public float angle;
			public TransformedImage image;
		}
		private struct ImageAndAngleArray
		{
			private ImageAndAngle[] arr;
			private int[] lengths;
			private int skipidx0, skipidx1;
			public void Alloc(Cinema.CinemaArgument[] arguments, int skipidx0, int skipidx1)
			{
				this.skipidx0 = skipidx0;
				this.skipidx1 = skipidx1;

				lengths = new int[arguments.Length - 2];
				int totallength = 1;
				for(int i = 0, j = 0; i < lengths.Length;++j)
					if(j != skipidx0 && j != skipidx1)
						totallength *= (lengths[i++] = arguments[j].values.Length);
				arr = new ImageAndAngle[totallength];
				for(int i = 0; i < totallength; ++i)
					arr[i] = new ImageAndAngle();
			}
			public void Clear()
			{
				for(int i = 0; i < arr.Length; ++i)
				{
					arr[i].image = null;
					arr[i].angle = float.MaxValue;
				}
			}
			public ImageAndAngle Get(Cinema.CinemaArgument[] arguments, int[] idx)
			{
				int totallength = 1, finalidx = 0;
				for(int j = 0; j < arguments.Length; ++j)
					if(j != skipidx0 && j != skipidx1)
					{
						finalidx += totallength * idx[j];
						totallength *= arguments[j].values.Length;
					}
				return arr[finalidx];
			}
		}
		ImageAndAngleArray bestImages = new ImageAndAngleArray();
		Vector3 viewpos;

		public _ThetaPhiViewTransform()
		{
			SkipImageInterval = UpdateInterval.Dynamic;
		}

		private void OnChangeIndex()
		{
			bestImages.Alloc(arguments, idx.theta, idx.phi);
		}

public static string foo = "";
		public override bool SkipImage(int[] imagekey, TransformedImage image)
		{
			//return imagekey[idx.theta] != active.theta || imagekey[idx.phi] != active.phi;

			ImageAndAngle bestImage = bestImages.Get(arguments, imagekey);
//if(image == bestImage.image)
//	foo = imagekey[idx.theta].ToString() + " " + imagekey[idx.phi].ToString();
			return image != bestImage.image;
		}

		public override void OnRender(float dt, ImageCloud.FreeView freeview)
		{
			viewpos = freeview.viewpos;
			bestImages.Clear();
		}
		public override void PrepareImage(int[] imagekey, TransformedImage image)
		{
			Vector3 viewdir = (image.pos - viewpos).Normalized();

			Selection<float> viewangle;
			//viewangle.phi = (float)-Math.Atan2(viewdir.X, viewdir.Z);
			//viewangle.theta = MathHelper.PiOver2 - (float)Math.Atan2(viewdir.Z * Math.Cos(viewangle.phi) - viewdir.X * Math.Sin(viewangle.phi), -viewdir.Y);

			viewangle.theta = (float)Math.Atan2(viewdir.X, viewdir.Z) + MathHelper.Pi;
			viewangle.phi = MathHelper.Pi - (float)Math.Atan2((float)Math.Sqrt(viewdir.X*viewdir.X + viewdir.Z*viewdir.Z), viewdir.Y);
			foo = viewangle.phi.ToString();

			float totalangle = Math.Abs(viewangle.theta - (float)image.values[idx.theta] / 180.0f * MathHelper.Pi) + Math.Abs(viewangle.phi - (float)image.values[idx.phi] / 180.0f * MathHelper.Pi);
			ImageAndAngle bestImage = bestImages.Get(arguments, imagekey);
			if(totalangle < bestImage.angle /*&& (float)image.values[idx.phi] == 0*/)
			{
				bestImage.angle = totalangle;
				bestImage.image = image;
			}
		}
	}

	[Serializable]
	public class ThetaPhiViewTransform : ImageTransform
	{
		public override int GetIndex(int i) {switch(i) {case 0: return idx.theta; case 1: return idx.phi; default: return -1;}}
		public override int SetIndex(int i, int index)
		{
			switch(i)
			{
			case 0:
				idx.theta = index;
				OnChangeIndex();
				return index;
			case 1:
				idx.phi = index;
				OnChangeIndex();
				return index;
			default:
				return -1;
			}
		}
		public override void SetArguments(Cinema.CinemaArgument[] arguments)
		{
			this.arguments = arguments;
			OnChangeIndex();
		}

		private struct Selection<T>
		{
			public T theta, phi;
		}
		private Selection<int> idx;
		Cinema.CinemaArgument[] arguments;
		private class ImageAndAngle
		{
			public float angle;
			public TransformedImage image;
		}
		private struct ImageAndAngleArray
		{
			private ImageAndAngle[] arr;
			private int[] lengths;
			private System.Collections.Generic.HashSet<int> skipindices;
			public void Alloc(Cinema.CinemaArgument[] arguments, System.Collections.Generic.HashSet<int> skipindices)
			{
				this.skipindices = skipindices;

				lengths = new int[arguments.Length - 2];
				int totallength = 1;
				for(int i = 0, j = 0; i < lengths.Length;++j)
					if(!skipindices.Contains(j))
						totallength *= (lengths[i++] = arguments[j].values.Length);
				arr = new ImageAndAngle[totallength];
				for(int i = 0; i < totallength; ++i)
					arr[i] = new ImageAndAngle();
			}
			public void Clear()
			{
				for(int i = 0; i < arr.Length; ++i)
				{
					arr[i].image = null;
					arr[i].angle = float.MaxValue;
				}
			}
			public ImageAndAngle Get(Cinema.CinemaArgument[] arguments, int[] idx)
			{
				int totallength = 1, finalidx = 0;
				for(int j = 0; j < arguments.Length; ++j)
					if(!skipindices.Contains(j))
					{
						finalidx += totallength * idx[j];
						totallength *= arguments[j].values.Length;
					}
				return arr[finalidx];
			}
		}
		ImageAndAngleArray bestImages = new ImageAndAngleArray();
		Vector3 viewpos;

		public ThetaPhiViewTransform()
		{
			SkipImageInterval = UpdateInterval.Dynamic;
		}

		private void OnChangeIndex()
		{
			bestImages.Alloc(arguments, new System.Collections.Generic.HashSet<int>() { idx.theta, idx.phi });
		}

		public static string foo = "";
		public override bool SkipImage(int[] imagekey, TransformedImage image)
		{
			ImageAndAngle bestImage = bestImages.Get(arguments, imagekey);
			return image != bestImage.image;
		}

		public override void OnRender(float dt, ImageCloud.FreeView freeview)
		{
			viewpos = freeview.viewpos;
			bestImages.Clear();
		}
		public override void PrepareImage(int[] imagekey, TransformedImage image)
		{
			Vector3 viewdir = (image.pos - viewpos).Normalized();

			Selection<float> viewangle;
			//viewangle.phi = (float)-Math.Atan2(viewdir.X, viewdir.Z);
			//viewangle.theta = MathHelper.PiOver2 - (float)Math.Atan2(viewdir.Z * Math.Cos(viewangle.phi) - viewdir.X * Math.Sin(viewangle.phi), -viewdir.Y);

			viewangle.theta = (float)Math.Atan2(viewdir.X, viewdir.Z) + MathHelper.Pi;
			viewangle.phi = MathHelper.Pi - (float)Math.Atan2((float)Math.Sqrt(viewdir.X*viewdir.X + viewdir.Z*viewdir.Z), viewdir.Y);
			foo = viewangle.phi.ToString();

			float totalangle = Math.Abs(viewangle.theta - (float)image.values[idx.theta] / 180.0f * MathHelper.Pi) + Math.Abs(viewangle.phi - (float)image.values[idx.phi] / 180.0f * MathHelper.Pi);
			ImageAndAngle bestImage = bestImages.Get(arguments, imagekey);
			if(totalangle < bestImage.angle)
			{
				bestImage.angle = totalangle;
				bestImage.image = image;
			}
		}
	}
}

