using System;

using OpenTK;
using OpenTK.Graphics;

namespace csharp_viewer
{
	[Serializable]
	public abstract class ImageTransform
	{
		public struct Description
		{
			public int numIndices;

			public Description(int numIndices)
			{
				this.numIndices = numIndices;
			}
		}
		public abstract Description GetDescription();
		//public abstract void SetIndices(int[] indices);
		public abstract int GetIndex(int i);
		public abstract int SetIndex(int i, int index);
		public virtual void SetArguments(Cinema.CinemaArgument[] arguments) {}

		[NonSerializedAttribute]
		private AABB transformAabb = new AABB();

		private System.Collections.Generic.HashSet<TransformedImage> updateImageSet = new System.Collections.Generic.HashSet<TransformedImage>();

		public void OnAddTransform(int[] imagekey, TransformedImage img)
		{
			if(transformAabb.IncludeAndCheckChanged(GetImageBounds(imagekey, img)))
				foreach(TransformedImage updateImage in updateImageSet)
					updateImage.ComputeLocation();
		}
		public AABB GetTransformBounds(TransformedImage img)
		{
			updateImageSet.Add(img);
			return transformAabb;
		}

		public virtual void PrepareImage(int[] imagekey, TransformedImage image) {}
		public virtual void LocationTransform(int[] imagekey, TransformedImage image, out Vector3 pos, ref Quaternion rot, ref Vector3 scl) {pos = Vector3.Zero;}
		public virtual AABB GetImageBounds(int[] imagekey, TransformedImage image) {return new AABB();}
		public virtual bool SkipImage(int[] imagekey, TransformedImage image) {return false;}
		public virtual void RenderImage(int[] imagekey, TransformedImage image, ImageCloud.FreeView freeview) {}

		public enum UpdateInterval
		{
			Never, Static, Dynamic
		};
		public UpdateInterval locationTransformInterval, SkipImageInterval;

		public virtual void OnRender(float dt, ImageCloud.FreeView freeview) {}
		public virtual void OnCameraMoved(ImageCloud.FreeView freeview) {}
	}
}

