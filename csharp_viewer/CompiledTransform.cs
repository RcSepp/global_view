using System;

namespace csharp_viewer
{
	public static class CompiledTransform
	{
		public static ImageTransform CompileTranslationTransform(string xExpr, string yExpr, string zExpr, string skip, bool useTime, ref string warnings)
		{
			string source = string.Format(@"
using System;
using System.Collections;
using System.Collections.Generic;

using OpenTK;

namespace csharp_viewer
{{
	public class TranslationTransform : ImageTransform
	{{
		public TranslationTransform()
		{{
			locationTransformInterval = UpdateInterval.{4};
		}}

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
	}}
}}", xExpr, yExpr, zExpr, skip, useTime ? "Temporal" : "Static");

			return (ImageTransform)ISQL.Compiler.CompileCSharpClass(source, "csharp_viewer", "TranslationTransform", ref warnings);
		}

		public static ImageTransform CompilePolarTransform(string thetaExpr, string phiExpr, string radiusExpr, bool useTime, ref string warnings)
		{
			string source = string.Format(@"
using System;
using System.Collections;
using System.Collections.Generic;

using OpenTK;

namespace csharp_viewer
{{
	public class PolarTransform : ImageTransform
	{{
		public PolarTransform()
		{{
			locationTransformInterval = UpdateInterval.{3};
		}}

		public override void LocationTransform(int[] imagekey, TransformedImage image, out Vector3 pos, ref Quaternion rot, ref Vector3 scl)
		{{
			float theta = (float)({0}), phi = (float)({1}), radius = (float)({2});

			pos = new Vector3(radius * (float)(Math.Sin(phi) * Math.Sin(theta)), radius * (float)Math.Cos(phi), radius * (float)(Math.Sin(phi) * Math.Cos(theta)));
		}}

		public override AABB GetImageBounds(int[] imagekey, TransformedImage image)
		{{
			float theta = (float)({0}), phi = (float)({1}), radius = (float)({2});

			Vector3 pos = new Vector3(radius * (float)(Math.Sin(phi) * Math.Sin(theta)), radius * (float)Math.Cos(phi), radius * (float)(Math.Sin(phi) * Math.Cos(theta)));

			return new AABB(pos - new Vector3(0.5f, 0.5f, 0.5f), pos + new Vector3(0.5f, 0.5f, 0.5f));
		}}
	}}
}}", thetaExpr, phiExpr, radiusExpr, useTime ? "Temporal" : "Static");

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
		public SkipTransform()
		{{
			locationTransformInterval = UpdateInterval.Never;
			SkipImageInterval = UpdateInterval.{1};
		}}

		public override bool SkipImage(int[] imagekey, TransformedImage image)
		{{
			return {0};
		}}
	}}
}}", skip, useTime ? "Temporal" : "Static");

			return (ImageTransform)ISQL.Compiler.CompileCSharpClass(source, "csharp_viewer", "SkipTransform", ref warnings);
		}

		public static ImageTransform CreateTransformLookAt(string thetaExpr, string phiExpr, string indices, bool useTime, ref string warnings)
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
		public LookAtTransform()
		{{
			SkipImageInterval = UpdateInterval.{3};
		}}

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
		//Vector3 viewpos;
		Vector3 viewdir;

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
			//viewpos = freeview.viewpos;
			viewdir = -freeview.GetViewDirection();
			bestImages.Clear();
		}}
		public override void PrepareImage(int[] imagekey, TransformedImage image)
		{{
			//Vector3 viewdir = (image.pos - viewpos).Normalized();

			Selection<float> viewangle;

			viewangle.theta = (float)Math.Atan2(viewdir.X, viewdir.Z) + MathHelper.Pi;
			viewangle.phi = MathHelper.Pi - (float)Math.Atan2((float)Math.Sqrt(viewdir.X*viewdir.X + viewdir.Z*viewdir.Z), viewdir.Y);

			float t = (float)({1}), p = (float)({2});
			t += 10.0f * (float)Math.PI;
			t %= 2.0f * (float)Math.PI;
			p += 10.0f * (float)Math.PI;
			p %= 2.0f * (float)Math.PI;
			float totalangle = AngularDistance(viewangle.theta, t)/*Math.Abs(viewangle.theta - t)*/ + Math.Abs(viewangle.phi - p);
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
}}", indices, thetaExpr, phiExpr, useTime ? "Temporal" : "Dynamic");

			return (ImageTransform)ISQL.Compiler.CompileCSharpClass(source, "csharp_viewer", "LookAtTransform", ref warnings);
		}

		public static ImageTransform CreateTransformSphericalView(string thetaExpr, string phiExpr, string indices, bool useTime, ref string warnings)
		{
			string source = string.Format(@"
using System;
using System.Collections;
using System.Collections.Generic;

using OpenTK;

namespace csharp_viewer
{{
	public class SphericalViewTransform : ImageTransform
	{{
		public SphericalViewTransform()
		{{
			SkipImageInterval = UpdateInterval.{3};
		}}

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
		Selection<float> viewangle = new Selection<float> {{ theta= 0.0f, phi= (float)Math.PI / 2.0f }};
		Vector2 mdown_uv;
		Selection<float> mdown_viewangle;

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
			bestImages.Clear();
		}}
		public override void OnImageMouseDown(ImageTransform.MouseButtons button, TransformedImage image, Vector2 uv, out bool allowDrag)
		{{
			allowDrag = false;
			mdown_uv = uv;
			mdown_viewangle = new Selection<float> {{ theta= viewangle.theta, phi= viewangle.phi }};
		}}
		public override void OnImageMouseMove(ImageTransform.MouseButtons button, TransformedImage image, Vector2 uv)
		{{
			if(button == MouseButtons.Left)
			{{
				viewangle.theta = mdown_viewangle.theta - (uv.X - mdown_uv.X) * (float)Math.PI;
				viewangle.phi = mdown_viewangle.phi - (uv.Y - mdown_uv.Y) * (float)Math.PI;
			}}
		}}
		public override void PrepareImage(int[] imagekey, TransformedImage image)
		{{
			float t = (float)({1}), p = (float)({2});
			t += 10.0f * (float)Math.PI;
			t %= 2.0f * (float)Math.PI;
			p += 10.0f * (float)Math.PI;
			p %= 2.0f * (float)Math.PI;
			float totalangle = AngularDistance(viewangle.theta, t)/*Math.Abs(viewangle.theta - t)*/ + Math.Abs(viewangle.phi - p);
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
}}", indices, thetaExpr, phiExpr, useTime ? "Temporal" : "Dynamic");

			return (ImageTransform)ISQL.Compiler.CompileCSharpClass(source, "csharp_viewer", "SphericalViewTransform", ref warnings);
		}

		public static ImageTransform CompileStarTransform(string[] starExpr, string skip, bool useTime, ref string warnings)
		{
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

		public StarTransform()
		{{
			locationTransformInterval = UpdateInterval.{2};
		}}
		
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
	}}
}}", string.Join(", ", starExpr), skip, useTime ? "Temporal" : "Static");

			return (ImageTransform)ISQL.Compiler.CompileCSharpClass(source, "csharp_viewer", "StarTransform", ref warnings);
		}
	}
}

