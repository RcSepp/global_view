using System;
using System.Collections.Generic;

using OpenTK;

namespace csharp_viewer
{
	[Serializable]
	public class XTransform : ImageTransform
	{
		public override Description GetDescription() {return new Description(1);}
		public override int GetIndex(int i) {switch(i) {case 0: return idx; default: return -1;}}
		public override int SetIndex(int i, int index) {switch(i) {case 0: return idx = index; default: return -1;}}

		private int idx;

		public override void LocationTransform(int[] imagekey, TransformedImage image, out Vector3 pos, ref Quaternion rot, ref Vector3 scl)
		{
			float x = (float)imagekey[idx];
			pos = new Vector3(-x * 1.2f, 0.0f, 0.0f);
		}

		public override AABB GetImageBounds(int[] imagekey, TransformedImage image)
		{
			float x = (float)imagekey[idx];
			return new AABB(new Vector3(-x * 1.2f - 0.5f, -0.5f, -0.5f), new Vector3(-x * 1.2f + 0.5f, 0.5f, 0.5f));
		}
	}

	[Serializable]
	public class YTransform : ImageTransform
	{
		public override Description GetDescription() {return new Description(1);}
		public override int GetIndex(int i) {switch(i) {case 0: return idx; default: return -1;}}
		public override int SetIndex(int i, int index) {switch(i) {case 0: return idx = index; default: return -1;}}

		private int idx;

		public override void LocationTransform(int[] imagekey, TransformedImage image, out Vector3 pos, ref Quaternion rot, ref Vector3 scl)
		{
			float y = (float)imagekey[idx];
			pos = new Vector3(0.0f, y * 1.2f, 0.0f);
		}

		public override AABB GetImageBounds(int[] imagekey, TransformedImage image)
		{
			float y = (float)imagekey[idx];
			return new AABB(new Vector3(-0.5f, y * 1.2f - 0.5f, -0.5f), new Vector3(0.5f, y * 1.2f + 0.5f, 0.5f));
		}
	}

	[Serializable]
	public class ZTransform : ImageTransform
	{
		public override Description GetDescription() {return new Description(1);}
		public override int GetIndex(int i) {switch(i) {case 0: return idx; default: return -1;}}
		public override int SetIndex(int i, int index) {switch(i) {case 0: return idx = index; default: return -1;}}

		private int idx;

		public override void LocationTransform(int[] imagekey, TransformedImage image, out Vector3 pos, ref Quaternion rot, ref Vector3 scl)
		{
			float z = (float)imagekey[idx];
			pos = new Vector3(0.0f, 0.0f, -z * 1.2f);
		}

		public override AABB GetImageBounds(int[] imagekey, TransformedImage image)
		{
			float z = (float)imagekey[idx];
			return new AABB(new Vector3(-0.5f, -0.5f, -z * 1.2f - 0.5f), new Vector3(0.5f, 0.5f, -z * 1.2f + 0.5f));
		}
	}

	[Serializable]
	public class TranslationTransform : ImageTransform
	{
		public override Description GetDescription() {return new Description(1);}
		public override int GetIndex(int i) {switch(i) {case 0: return idx; default: return -1;}}
		public override int SetIndex(int i, int index) {switch(i) {case 0: return idx = index; default: return -1;}}

		private int idx;
		private Vector3 origin, delta;

		public TranslationTransform()
		{
			origin = Vector3.Zero;
			delta = Vector3.UnitX;
		}
		public TranslationTransform(Vector3 origin, Vector3 delta)
		{
			this.origin = origin;
			this.delta = delta;
		}

		public override void LocationTransform(int[] imagekey, TransformedImage image, out Vector3 pos, ref Quaternion rot, ref Vector3 scl)
		{
			pos = origin + delta * (float)imagekey[idx];
		}

		public override AABB GetImageBounds(int[] imagekey, TransformedImage image)
		{
			Vector3 pos = origin + delta * (float)imagekey[idx];
			return new AABB(pos - new Vector3(0.5f, 0.5f, 0.5f), pos + new Vector3(0.5f, 0.5f, 0.5f));
		}
	}


	public static class CompiledTransform
	{
		public static ImageTransform CompileTranslationTransform(string x, string y, string z, bool useTime, ref string warnings)
		{
			string timeCode = useTime ? @"
		public TranslationTransform()
		{{
			locationTransformInterval = UpdateInterval.Dynamic;
		}}" : "";

			string source = string.Format(@"
using System;
using System.Collections;
using System.Collections.Generic;

using OpenTK;

namespace csharp_viewer
{{
	public class TranslationTransform : ImageTransform
	{{
		public override Description GetDescription() {{return new Description(1);}}
		public override int GetIndex(int i) {{switch(i) {{case 0: return idx; default: return -1;}}}}
		public override int SetIndex(int i, int index) {{switch(i) {{case 0: return idx = index; default: return -1;}}}}

		private int idx;

		public override void LocationTransform(int[] imagekey, TransformedImage image, out Vector3 pos, ref Quaternion rot, ref Vector3 scl)
		{{
			float x = (float)({0}), y = (float)({1}), z = (float)({2});
			pos = new Vector3(x, y, z);
		}}

		public override AABB GetImageBounds(int[] imagekey, TransformedImage image)
		{{
			float x = (float)({0}), y = (float)({1}), z = (float)({2});
			return new AABB(new Vector3(x - 0.5f, y - 0.5f, z - 0.5f), new Vector3(x + 0.5f, y + 0.5f, z + 0.5f));
		}}
		
		{3}
	}}
}}", x, y, z, timeCode);

			return (ImageTransform)ISQL.Compiler.CompileCSharpClass(source, "csharp_viewer", "TranslationTransform", ref warnings);
		}
	}
}

