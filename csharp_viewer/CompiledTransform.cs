using System;

namespace csharp_viewer
{
	public static class CompiledTransform
	{
		public static ImageTransform CompileTranslationTransform(string x, string y, string z, string skip, bool useTime, ref string warnings)
		{
			string timeCode = useTime ? @"
		public TranslationTransform()
		{
			locationTransformInterval = UpdateInterval.Dynamic;
		}" : "";

			string source = string.Format(@"
using System;
using System.Collections;
using System.Collections.Generic;

using OpenTK;

namespace csharp_viewer
{{
	public class TranslationTransform : ImageTransform
	{{
		public override void LocationTransform(int[] imagekey, TransformedImage image, out Vector3 pos, ref Quaternion rot, ref Vector3 scl)
		{{
			if({3})
			{{
				pos = Vector3.Zero;
				return;
			}}
			float x = (float)({0}), y = (float)({1}), z = (float)({2});
			pos = new Vector3(x, y, z);
		}}
		
		public override AABB GetImageBounds(int[] imagekey, TransformedImage image)
		{{
			if({3})
				return new AABB();
			float x = (float)({0}), y = (float)({1}), z = (float)({2});
			return new AABB(new Vector3(x - 0.5f, y - 0.5f, z - 0.5f), new Vector3(x + 0.5f, y + 0.5f, z + 0.5f));
		}}
		
		/*Random rand = new Random((int)((Global.time * 1000.0f) % (float)int.MaxValue));
		public override void OnRender(float dt, ImageCloud.FreeView freeview)
		{{
			rand = new Random((int)((Global.time * 1000.0f) % (float)int.MaxValue));
		}}*/
		
		{4}
	}}
}}", x, y, z, skip, timeCode);

			return (ImageTransform)ISQL.Compiler.CompileCSharpClass(source, "csharp_viewer", "TranslationTransform", ref warnings);
		}

		public static ImageTransform CompilePolarTransform(string tpr/*string theta, string phi, string radius*/, bool useTime, ref string warnings)
		{
			string timeCode = useTime ? @"
		public PolarTransform()
		{
			locationTransformInterval = UpdateInterval.Dynamic;
		}" : "";

			string source = string.Format(@"
using System;
using System.Collections;
using System.Collections.Generic;

using OpenTK;

namespace csharp_viewer
{{
	public class PolarTransform : ImageTransform
	{{
		public override void LocationTransform(int[] imagekey, TransformedImage image, out Vector3 pos, ref Quaternion rot, ref Vector3 scl)
		{{
			Vector3 tpr = new Vector3({0});

			pos = new Vector3(tpr.Z * (float)(Math.Sin(tpr.Y) * Math.Sin(tpr.X)), tpr.Z * (float)Math.Cos(tpr.Y), tpr.Z * (float)(Math.Sin(tpr.Y) * Math.Cos(tpr.X)));
		}}

		public override AABB GetImageBounds(int[] imagekey, TransformedImage image)
		{{
			Vector3 tpr = new Vector3({0});

			Vector3 pos = new Vector3(tpr.Z * (float)(Math.Sin(tpr.Y) * Math.Sin(tpr.X)), tpr.Z * (float)Math.Cos(tpr.Y), tpr.Z * (float)(Math.Sin(tpr.Y) * Math.Cos(tpr.X)));

			return new AABB(pos - new Vector3(0.5f, 0.5f, 0.5f), pos + new Vector3(0.5f, 0.5f, 0.5f));
		}}
		
		{1}
	}}
}}", tpr, timeCode);

			return (ImageTransform)ISQL.Compiler.CompileCSharpClass(source, "csharp_viewer", "PolarTransform", ref warnings);
		}

		public static ImageTransform CompileSkipTransform(string skip, bool useTime, ref string warnings)
		{
			string source = string.Format(@"
using System;
using System.Collections;
using System.Collections.Generic;

using OpenTK;

namespace csharp_viewer
{{
	public class SkipTransform : ImageTransform
	{{
		public override bool SkipImage(int[] imagekey, TransformedImage image)
		{{
			return {0};
		}}
		
		public SkipTransform()
		{{
			locationTransformInterval = UpdateInterval.Never;
			SkipImageInterval = UpdateInterval.{1};
		}}
	}}
}}", skip, useTime ? "Dynamic" : "Static");

			return (ImageTransform)ISQL.Compiler.CompileCSharpClass(source, "csharp_viewer", "SkipTransform", ref warnings);
		}

		public static ImageTransform CreateTransformLookAt(string tp, string indices, ref string warnings)
		{
			string source = string.Format(@"
using System;
using System.Collections;
using System.Collections.Generic;

using OpenTK;

namespace csharp_viewer
{{
	public class LookAtTransform : ImageTransform
	{{
		public override void OnArgumentsChanged()
		{{
			OnChangeIndex();
		}}

		private struct Selection<T>
		{{
			public T theta, phi;
		}}
		private class ImageAndAngle
		{{
			public float angle;
			public TransformedImage image;
		}}
		private struct ImageAndAngleArray
		{{
			private ImageAndAngle[] arr;
			private int[] lengths;
			private System.Collections.Generic.HashSet<int> skipindices;
			public void Alloc(Cinema.CinemaArgument[] arguments, System.Collections.Generic.HashSet<int> skipindices)
			{{
				this.skipindices = skipindices;

				lengths = new int[arguments.Length - 2];
				int totallength = 1;
				for(int i = 0, j = 0; i < lengths.Length;++j)
					if(!skipindices.Contains(j))
						totallength *= (lengths[i++] = arguments[j].values.Length);
				arr = new ImageAndAngle[totallength];
				for(int i = 0; i < totallength; ++i)
					arr[i] = new ImageAndAngle();
			}}
			public void Clear()
			{{
				for(int i = 0; i < arr.Length; ++i)
				{{
					arr[i].image = null;
					arr[i].angle = float.MaxValue;
				}}
			}}
			public ImageAndAngle Get(Cinema.CinemaArgument[] arguments, int[] idx)
			{{
				int totallength = 1, finalidx = 0;
				for(int j = 0; j < arguments.Length; ++j)
					if(!skipindices.Contains(j))
					{{
						finalidx += totallength * idx[j];
						totallength *= arguments[j].values.Length;
					}}
				return arr[finalidx];
			}}
		}}
		ImageAndAngleArray bestImages = new ImageAndAngleArray();
		Vector3 viewpos;

		public LookAtTransform()
		{{
			SkipImageInterval = UpdateInterval.Dynamic;
		}}

		private void OnChangeIndex()
		{{
			bestImages.Alloc(Global.arguments, new System.Collections.Generic.HashSet<int>() {{ {0} }});
		}}

		public override bool SkipImage(int[] imagekey, TransformedImage image)
		{{
			ImageAndAngle bestImage = bestImages.Get(Global.arguments, imagekey);
			return image != bestImage.image;
		}}

		public override void OnRender(float dt, ImageCloud.FreeView freeview)
		{{
			viewpos = freeview.viewpos;
			bestImages.Clear();
		}}
		public override void PrepareImage(int[] imagekey, TransformedImage image)
		{{
			Vector3 viewdir = (image.pos - viewpos).Normalized();

			Selection<float> viewangle;

			viewangle.theta = (float)Math.Atan2(viewdir.X, viewdir.Z) + MathHelper.Pi;
			viewangle.phi = MathHelper.Pi - (float)Math.Atan2((float)Math.Sqrt(viewdir.X*viewdir.X + viewdir.Z*viewdir.Z), viewdir.Y);

			float[] tp = {{ {1} }};
			tp[0] += 10.0f * (float)Math.PI;
			tp[0] %= 2.0f * (float)Math.PI;
			tp[1] += 10.0f * (float)Math.PI;
			tp[1] %= 2.0f * (float)Math.PI;
			float totalangle = AngularDistance(viewangle.theta, tp[0])/*Math.Abs(viewangle.theta - tp[0])*/ + Math.Abs(viewangle.phi - tp[1]);
			ImageAndAngle bestImage = bestImages.Get(Global.arguments, imagekey);
			if(totalangle < bestImage.angle)
			{{
				bestImage.angle = totalangle;
				bestImage.image = image;
			}}
		}}
		private static float AngularDistance(float a, float b)
		{{
			return Math.Min(Math.Abs(a - b), Math.Abs(((a + (float)Math.PI) % (2.0f * (float)Math.PI)) - ((b + (float)Math.PI) % (2.0f * (float)Math.PI))));
		}}
	}}
}}", indices, tp);

			return (ImageTransform)ISQL.Compiler.CompileCSharpClass(source, "csharp_viewer", "LookAtTransform", ref warnings);
		}

		public static ImageTransform CompileStarTransform(string star, string skip, bool useTime, ref string warnings)
		{
			string timeCode = useTime ? @"
		public StarTransform()
		{
			locationTransformInterval = UpdateInterval.Dynamic;
		}" : "";

			string source = string.Format(@"
using System;
using System.Collections;
using System.Collections.Generic;

using OpenTK;

namespace csharp_viewer
{{
	public class StarTransform : ImageTransform
	{{
		private static float HALF_SQRT_2 = (float)(Math.Sqrt(2.0) / 2.0);
		private static Vector3[][] WEIGHTS = {{
			/* 0 dimensions */ new Vector3[] {{
			}},
			/* 1 dimension */ new Vector3[] {{
				new Vector3(1.0f, 0.0f, 0.0f)
			}},
			/* 2 dimensions */ new Vector3[] {{
				new Vector3(1.0f, 0.0f, 0.0f),
				new Vector3(0.0f, 1.0f, 0.0f)
			}},
			/* 3 dimensions */ new Vector3[] {{
				new Vector3(1.0f, 0.0f, 0.0f),
				new Vector3(0.0f, 1.0f, 0.0f),
				new Vector3(0.0f, 0.0f, 1.0f)
			}},
			/* 4 dimensions */ new Vector3[] {{
				new Vector3( 0.75f, -0.5f * (float)Math.Sqrt(0.75), -1.0f / 3.0f),
				new Vector3(  0.0f,  1.0f * (float)Math.Sqrt(0.75), -1.0f / 3.0f),
				new Vector3(-0.75f, -0.5f * (float)Math.Sqrt(0.75), -1.0f / 3.0f),
				new Vector3(  0.0f,  0.0f,                           1.0f)
			}},
			/* 5 dimensions */ new Vector3[] {{
				new Vector3( (float)Math.Sqrt(0.75), -0.5f, 0.0f),
				new Vector3( 0.0f,                    1.0f, 0.0f),
				new Vector3(-(float)Math.Sqrt(0.75), -0.5f, 0.0f),
				new Vector3(  0.0f,  0.0f,  (float)Math.Sqrt(0.75)),
				new Vector3( 0.0f,   0.0f, -(float)Math.Sqrt(0.75))
			}},
			/* 6 dimensions new Vector3[] {{
				new Vector3( (float)Math.Sqrt(0.75),  0.5f,                  -0.5f),
				new Vector3( 0.0f,                    (float)Math.Sqrt(0.5),  1.0f),
				new Vector3(-(float)Math.Sqrt(0.75),  0.5f,                  -0.5f),
				new Vector3(-0.5f,                   -0.5f,                   (float)Math.Sqrt(0.75)),
				new Vector3( 1.0f,                   -(float)Math.Sqrt(0.5),  0.0f),
				new Vector3(-0.5f,                   -0.5f,                  -(float)Math.Sqrt(0.75))
			}}*/
			/* 6 dimensions */ new Vector3[] {{ // <- EDIT: works except for signs!
				new Vector3(-(float)Math.Cos(  54.0 * Math.PI / 180.0) * HALF_SQRT_2, -(float)Math.Sin(  54.0 * Math.PI / 180.0) * HALF_SQRT_2, HALF_SQRT_2),
				new Vector3((float)Math.Cos( 126.0 * Math.PI / 180.0) * HALF_SQRT_2, (float)Math.Sin( 126.0 * Math.PI / 180.0) * HALF_SQRT_2, -HALF_SQRT_2),
				new Vector3(-(float)Math.Cos(-162.0 * Math.PI / 180.0) * HALF_SQRT_2, -(float)Math.Sin(-162.0 * Math.PI / 180.0) * HALF_SQRT_2, HALF_SQRT_2),
				new Vector3((float)Math.Cos( -90.0 * Math.PI / 180.0) * HALF_SQRT_2, (float)Math.Sin( -90.0 * Math.PI / 180.0) * HALF_SQRT_2, -HALF_SQRT_2),
				new Vector3(-(float)Math.Cos( -18.0 * Math.PI / 180.0) * HALF_SQRT_2, -(float)Math.Sin( -18.0 * Math.PI / 180.0) * HALF_SQRT_2, HALF_SQRT_2),
				new Vector3(0.0f, 0.0f, -1.0f)
			}}
		}};
		
		/* 3 dimensions 2D: new Vector3[] {{
			new Vector3( (float)Math.Sqrt(0.75), -0.5f, 0.0f),
			new Vector3( 0.0f,         1.0f,        0.0f),
			new Vector3(-(float)Math.Sqrt(0.75), -0.5f, 0.0f)
		}}*/
		
		public override void LocationTransform(int[] imagekey, TransformedImage image, out Vector3 pos, ref Quaternion rot, ref Vector3 scl)
		{{
			if({1})
			{{
				pos = Vector3.Zero;
				return;
			}}

			float[] star = {{ {0} }};
			
			pos = new Vector3(0.0f, 0.0f, 0.0f);

			int numdims = star.Length;
			if(numdims < WEIGHTS.Length)
			{{
				Vector3[] weights = WEIGHTS[numdims];
				for(int d = 0; d < numdims; ++d)
				{{
					float factor = star[d];
				pos += weights[d] /** new Vector3(2.0f, 1.5f, 1.0f)*/ * factor; //TODO: delete ""new Vector3(2.0f, 1.0f, 1.0f)""
				}}
			}}
			else
			{{
				// 2D version
				for(int d = 0; d < numdims; ++d)
				{{
					float angle = (float)d * MathHelper.TwoPi / (float)numdims;
					float factor = star[d];

					pos.X += 50.0f * (float)Math.Cos(angle) * factor;
					pos.Y += 50.0f * (float)Math.Sin(angle) * factor;
				}}
			}}
		}}
		
		{2}
	}}
}}", star, skip, timeCode);

			return (ImageTransform)ISQL.Compiler.CompileCSharpClass(source, "csharp_viewer", "StarTransform", ref warnings);
		}
	}
}

