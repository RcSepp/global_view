using System;

namespace csharp_viewer
{
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

		public static ImageTransform CompilePolarTransform(string tpr/*string theta, string phi, string radius*/, bool useTime, ref string warnings)
		{
			string timeCode = useTime ? @"
		public PolarTransform()
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
	public class PolarTransform : ImageTransform
	{{
		public override Description GetDescription() {{return new Description(1);}}
		public override int GetIndex(int i) {{switch(i) {{case 0: return idx; default: return -1;}}}}
		public override int SetIndex(int i, int index) {{switch(i) {{case 0: return idx = index; default: return -1;}}}}

		private int idx;

		public override void LocationTransform(int[] imagekey, TransformedImage image, out Vector3 pos, ref Quaternion rot, ref Vector3 scl)
		{{
			Vector3 tpr = new Vector3({0});

			//pos = new Vector3((float)(Math.Cos(tpr.X) * Math.Sin(tpr.Y)) * 4.1f, (float)Math.Sin(tpr.X) * 4.1f, (float)(Math.Cos(tpr.X) * Math.Cos(tpr.Y)) * 4.1f);
			pos = new Vector3(tpr.Z * (float)(Math.Sin(tpr.Y) * Math.Cos(tpr.X)), tpr.Z * (float)Math.Cos(tpr.Y), tpr.Z * (float)(Math.Sin(tpr.Y) * Math.Sin(tpr.X)));
		}}

		public override AABB GetImageBounds(int[] imagekey, TransformedImage image)
		{{
			Vector3 tpr = new Vector3({0});

			//Vector3 pos = new Vector3((float)(Math.Cos(tpr.X) * Math.Sin(tpr.Y)) * 4.1f, (float)Math.Sin(tpr.X) * 4.1f, (float)(Math.Cos(tpr.X) * Math.Cos(tpr.Y)) * 4.1f);
			Vector3 pos = new Vector3(tpr.Z * (float)(Math.Sin(tpr.Y) * Math.Cos(tpr.X)), tpr.Z * (float)Math.Cos(tpr.Y), tpr.Z * (float)(Math.Sin(tpr.Y) * Math.Sin(tpr.X)));

			return new AABB(pos - new Vector3(0.5f, 0.5f, 0.5f), pos + new Vector3(0.5f, 0.5f, 0.5f));
		}}
		
		{1}
	}}
}}", tpr, timeCode);

			return (ImageTransform)ISQL.Compiler.CompileCSharpClass(source, "csharp_viewer", "PolarTransform", ref warnings);
		}

		public static ImageTransform CompileSkipTransform(string skip, bool useTime, ref string warnings)
		{
			string timeCode = useTime ? @"
		public SkipTransform()
		{{
			SkipImageInterval = UpdateInterval.Dynamic;
		}}" : "";

			string source = string.Format(@"
using System;
using System.Collections;
using System.Collections.Generic;

using OpenTK;

namespace csharp_viewer
{{
	public class SkipTransform : ImageTransform
	{{
		public override Description GetDescription() {{return new Description(1);}}
		public override int GetIndex(int i) {{switch(i) {{case 0: return idx; default: return -1;}}}}
		public override int SetIndex(int i, int index) {{switch(i) {{case 0: return idx = index; default: return -1;}}}}
		
		private int idx;
		
		public override bool SkipImage(int[] imagekey, TransformedImage image)
		{{
			return {0};
		}}
		
		{1}
	}}
}}", skip, timeCode);

			return (ImageTransform)ISQL.Compiler.CompileCSharpClass(source, "csharp_viewer", "SkipTransform", ref warnings);
		}
	}
}

