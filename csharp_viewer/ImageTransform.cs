using System;

using OpenTK;
using OpenTK.Graphics;

namespace csharp_viewer
{
	//[Serializable]
	public abstract class ImageTransform
	{
		public delegate void OnDisposeDelegate();
		public event OnDisposeDelegate OnDispose;

		public enum MouseButtons
		{
			None = 0,
			Left = 1048576,
			Right = 2097152,
			Middle = 4194304,
			XButton1 = 8388608,
			XButton2 = 16777216
		}

		public struct Id : IComparable<Id>, IEquatable<Id>
		{
			//private static int nextFreeId = 1;
			private static System.Collections.Generic.Dictionary<int, ImageTransform> usedIds = new System.Collections.Generic.Dictionary<int, ImageTransform>();
			private int id;

			public static Id Generate(ImageTransform transform)
			{
				//return new Id(nextFreeId++);
				int newid = 0;
				while(usedIds.ContainsKey(++newid)) { }
				usedIds.Add(newid, transform);
				return new Id(newid);
			}
			private Id(int id)
			{
				this.id = id;
			}
			public void Remove()
			{
				usedIds.Remove(id);
			}
			public static ImageTransform GetTransformById(Id id)
			{
				ImageTransform transform;
				usedIds.TryGetValue(id.id, out transform);
				return transform;
			}

			public override string ToString()
			{
				return "T" + id;
			}

			public static bool TryParse(string str, out Id id)
			{
				int _id;
				if(str.StartsWith("T") && int.TryParse(str.Substring("T".Length), out _id))
				{
					id = new Id(_id);
					return true;
				}
				else
				{
					id = new Id(-1);
					return false;
				}
			}

			int IComparable<Id>.CompareTo(Id other) { return this.id - other.id; }
			public bool Equals(Id other) { return this.id == other.id; }
			public static bool operator==(Id a, Id b) { return a.id == b.id; }
			public static bool operator!=(Id a, Id b) { return a.id != b.id; }
			public override bool Equals(object other) { return this.id == ((Id)other).id; }
			public override int GetHashCode() { return id.GetHashCode(); }
		}
		public Id id;
		public string description = "";

		public virtual void OnArgumentsChanged() {}
		public virtual void OnImageMouseDown(MouseButtons button, TransformedImage image, Vector2 uv, out bool allowDrag) { allowDrag = true; }
		public virtual void OnImageMouseMove(MouseButtons button, TransformedImage image, Vector2 uv) {}
		public virtual void SetTransformParameter(object tparam) {}

		//[NonSerializedAttribute]
		private AABB transformAabb = new AABB();

		private System.Collections.Generic.HashSet<TransformedImage> updateImageSet = new System.Collections.Generic.HashSet<TransformedImage>();

		public ImageTransform()
		{
			id = Id.Generate(this);
		}
		public void Dispose()
		{
			if(OnDispose != null)
				OnDispose();
			
			id.Remove();
		}
		public static ImageTransform GetTransformById(Id id)
		{
			return Id.GetTransformById(id);
		}

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
		public virtual void ColorTransform(int[] imagekey, TransformedImage image, out Color4 mul, out Color4 add) {mul = new Color4(1.0f, 1.0f, 1.0f, 1.0f); add = new Color4(0.0f, 0.0f, 0.0f, 0.0f);}
		public virtual AABB GetImageBounds(int[] imagekey, TransformedImage image) {return new AABB();}
		public virtual bool SkipImage(int[] imagekey, TransformedImage image) {return false;}
		public virtual void RenderImage(int[] imagekey, TransformedImage image, ImageCloud.FreeView freeview) {}

		public enum UpdateInterval
		{
			/// <summary> The behaviour isn't used (never checked) </summary>
			Never,

			/// <summary> The behaviour never changes (checked only once) </summary>
			Static,

			/// <summary> The behaviour changes in user-defined intervals (checked when ...Triggered == true) </summary>
			Triggered,

			/// <summary> The behaviour changes every frame (not checked during temporal prefetching) </summary>
			Dynamic,

			/// <summary> The behaviour changes with time (checked during temporal prefetching) </summary>
			Temporal
		};
		public UpdateInterval locationTransformInterval = UpdateInterval.Never, skipImageInterval = UpdateInterval.Never, colorTransformInterval = UpdateInterval.Never;
		public bool locationTransformTriggered = false, skipImageTriggered = false, colorTransformTriggered = false; // Triggers for manually triggering behavior checks

		public virtual void OnRender(float dt, ImageCloud.FreeView freeview) {}
		public virtual void OnCameraMoved(ImageCloud.FreeView freeview) {}
	}
}

