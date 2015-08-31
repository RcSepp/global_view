using System;

using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace csharp_viewer
{
	public static class Common
	{
		public static class TEXTURED_SHADER
		{
			public const string VS = @"
				attribute vec3 vpos;
				attribute vec2 vtexcoord;
				uniform mat4 World;
				varying vec2 uv;

				void main()
				{
					gl_Position = World * vec4(vpos, 1.0);
					uv = vtexcoord;
				}
			";
			public const string FS = @"
				uniform vec4 Color;
				uniform sampler2D Texture;
				varying vec2 uv;

				void main()
				{
					gl_FragColor = texture2D(Texture, uv) * Color;
				}
			";
		}
		public static class SOLID_COLOR_SHADER
		{
			public const string VS = @"
				attribute vec3 vpos;
				uniform mat4 World;

				void main()
				{
					gl_Position = World * vec4(vpos, 1.0);
				}
			";
			public const string FS = @"
				uniform vec4 Color;

				void main()
				{
					gl_FragColor = Color;
				}
			";
		}
		public static class DASHED_LINE_SHADER
		{
			public const string VS = @"
				attribute vec3 vpos;
				attribute vec2 vtexcoord;
				uniform mat4 World;
				uniform float Length;
				varying float value;

				void main()
				{
					gl_Position = World * vec4(vpos, 1.0);
					value = vtexcoord.y * Length;
				}
			";
			public const string FS = @"
				uniform vec4 Color;
				varying float value;

				void main()
				{
					gl_FragColor = mod(value, 0.1) > 0.05 ? Color : vec4(0.0, 0.0, 0.0, 0.0);
				}
			";
		}

		public static GLMesh meshQuad;
		public static GLMesh meshLine;
		public static GLMesh meshLineQuad, meshLineQuad2;
		public static GLMesh meshLineCube;

		public static GLTextFont fontText;

		public static GLShader sdrTextured, sdrSolidColor, sdrDashedLine;
		public static int sdrSolidColor_colorUniform, sdrDashedLine_colorUniform, sdrDashedLine_lengthUniform, sdrTextured_colorUniform;

		public static void CreateCommonMeshes()
		{
			Vector3[] positions;
			Vector2[] texcoords;

			// Create a simple line mesh
			positions = new Vector3[] {
				new Vector3(0.0f, 0.0f, 0.0f),
				new Vector3(0.0f, 0.0f, 1.0f)
			};
			meshLine = new GLMesh(positions, null, null, null, null, null, PrimitiveType.Lines);

			// Create a 2D quad mesh
			positions = new Vector3[] {
				new Vector3(0.0f, 1.0f, 0.0f),
				new Vector3(0.0f, 0.0f, 0.0f),
				new Vector3(1.0f, 1.0f, 0.0f),
				new Vector3(1.0f, 0.0f, 0.0f),
			};
			texcoords = new Vector2[] {
				new Vector2(0.0f, 0.0f),
				new Vector2(0.0f, 1.0f),
				new Vector2(1.0f, 0.0f),
				new Vector2(1.0f, 1.0f)
			};
			meshQuad = new GLMesh(positions, null, null, null, texcoords);

			// Create a 2D quad outline mesh
			positions = new Vector3[] {
				new Vector3(-1.0f, -1.0f, 0.0f),
				new Vector3(-1.0f,  1.0f, 0.0f),
				new Vector3( 1.0f,  1.0f, 0.0f),
				new Vector3( 1.0f, -1.0f, 0.0f),
			};
			meshLineQuad = new GLMesh(positions, null, null, null, null, null, PrimitiveType.LineLoop);
			positions = new Vector3[] {
				new Vector3(0.0f, 0.0f, 0.0f),
				new Vector3(0.0f, 1.0f, 0.0f),
				new Vector3(1.0f, 1.0f, 0.0f),
				new Vector3(1.0f, 0.0f, 0.0f),
			};
			meshLineQuad2 = new GLMesh(positions, null, null, null, null, null, PrimitiveType.LineLoop);

			// Create a 3D cube outline mesh
			positions = new Vector3[] {
				new Vector3(-1.0f, -1.0f, -1.0f),
				new Vector3( 1.0f, -1.0f, -1.0f),
				new Vector3(-1.0f,  1.0f, -1.0f),
				new Vector3( 1.0f,  1.0f, -1.0f),
				new Vector3(-1.0f, -1.0f, -1.0f),
				new Vector3(-1.0f,  1.0f, -1.0f),
				new Vector3( 1.0f, -1.0f, -1.0f),
				new Vector3( 1.0f,  1.0f, -1.0f),

				new Vector3(-1.0f, -1.0f,  1.0f),
				new Vector3( 1.0f, -1.0f,  1.0f),
				new Vector3(-1.0f,  1.0f,  1.0f),
				new Vector3( 1.0f,  1.0f,  1.0f),
				new Vector3(-1.0f, -1.0f,  1.0f),
				new Vector3(-1.0f,  1.0f,  1.0f),
				new Vector3( 1.0f, -1.0f,  1.0f),
				new Vector3( 1.0f,  1.0f,  1.0f),

				new Vector3(-1.0f, -1.0f, -1.0f),
				new Vector3(-1.0f, -1.0f,  1.0f),
				new Vector3( 1.0f, -1.0f, -1.0f),
				new Vector3( 1.0f, -1.0f,  1.0f),
				new Vector3(-1.0f,  1.0f, -1.0f),
				new Vector3(-1.0f,  1.0f,  1.0f),
				new Vector3( 1.0f,  1.0f, -1.0f),
				new Vector3( 1.0f,  1.0f,  1.0f)
			};
			meshLineCube = new GLMesh(positions, null, null, null, null, null, PrimitiveType.Lines);
		}

		public static void CreateCommonFonts()
		{
			//fontText = new GLTextFont("HelveticaNeueText_12.png", new Vector2(17.0f, 23.0f), meshQuad);
			fontText = new GLTextFont("HelveticaNeueText_8.png", new Vector2(10.0f, 17.0f), meshQuad);
		}

		public static void CreateCommonShaders()
		{
			sdrTextured = new GLShader(new string[] { TEXTURED_SHADER.VS }, new string[] { TEXTURED_SHADER.FS });
			sdrTextured.Bind();
			sdrTextured_colorUniform = sdrTextured.GetUniformLocation("Color");
			GL.Uniform4(sdrTextured_colorUniform, OpenTK.Graphics.Color4.White);

			sdrSolidColor = new GLShader(new string[] { SOLID_COLOR_SHADER.VS }, new string[] { SOLID_COLOR_SHADER.FS });
			sdrSolidColor.Bind();
			sdrSolidColor_colorUniform = sdrSolidColor.GetUniformLocation("Color");
			GL.Uniform4(sdrSolidColor_colorUniform, OpenTK.Graphics.Color4.White);

			sdrDashedLine = new GLShader(new string[] { DASHED_LINE_SHADER.VS }, new string[] { DASHED_LINE_SHADER.FS });
			sdrDashedLine.Bind();
			sdrDashedLine_colorUniform = sdrDashedLine.GetUniformLocation("Color");
			sdrDashedLine_lengthUniform = sdrDashedLine.GetUniformLocation("Length");
			GL.Uniform4(sdrDashedLine_colorUniform, OpenTK.Graphics.Color4.White);
		}


		#region "Value Animation"
		public static void AnimateTransition(ref float current, float target, float dt)
		{
			float delta = target - current;
			if(delta < -1e-2f || delta > 1e-2f)
			{
				current += (float)Math.Pow(Math.Abs(delta), 0.5) * Math.Sign(delta) * 4.0f * dt;

				// Avoid overshoot (current passing target)
				if(Math.Sign(target - current) != Math.Sign(delta))
					current = target;
			}
			else
				current = target;
		}
		public static void AnimateTransition(ref Vector2 current, Vector2 target, float dt)
		{
			float dist = (current - target).Length;
			AnimateTransition(ref current.X, target.X, dt * Math.Abs(target.X - current.X) / dist);
			AnimateTransition(ref current.Y, target.Y, dt * Math.Abs(target.Y - current.Y) / dist);
		}
		public static void AnimateTransition(ref Vector3 current, Vector3 target, float dt)
		{
			float dist = (current - target).Length;
			AnimateTransition(ref current.X, target.X, dt * Math.Abs(target.X - current.X) / dist);
			AnimateTransition(ref current.Y, target.Y, dt * Math.Abs(target.Y - current.Y) / dist);
			AnimateTransition(ref current.Z, target.Z, dt * Math.Abs(target.Z - current.Z) / dist);
		}
		#endregion

		public static Matrix4 Matrix4_CreateRotationDir(Vector3 vfrom, Vector3 vto)
		{
			float dot = Vector3.Dot(vfrom, vto);
			if(dot <= -1.0f + -1e5f || dot >= 1.0f - -1e5f)
				return Matrix4.Identity;
			Vector3 vcross = Vector3.Cross(vfrom, vto);
			vfrom.Z = -vfrom.Z;
			return Matrix4.CreateFromAxisAngle(vcross, (float)Math.Acos(Vector3.Dot(vfrom, vto)));
		}
	}

	public class AABB
	{
		public Vector3 min, max;

		public AABB()
		{
			min = new Vector3(float.MaxValue);
			max = new Vector3(float.MinValue);
		}
		public AABB(Vector3 min, Vector3 max)
		{
			this.min = min;
			this.max = max;
		}

		public Matrix4 GetTransform()
		{
			if(min.X == float.MaxValue || min.Y == float.MaxValue || min.Z == float.MaxValue)
				return Matrix4.Identity;
			Vector3 mid = (min + max) / 2.0f, halfsize = (max - min) / 2.0f;
			return Matrix4.CreateScale(halfsize) * Matrix4.CreateTranslation(mid);
		}

		public void Include(AABB aabb)
		{
			min = Vector3.ComponentMin(min, aabb.min);
			max = Vector3.ComponentMax(max, aabb.max);
		}
		public bool IncludeAndCheckChanged(AABB aabb)
		{
			bool changed = false;
			if(aabb.min.X < min.X) {min.X = aabb.min.X; changed = true;}
			if(aabb.min.Y < min.Y) {min.Y = aabb.min.Y; changed = true;}
			if(aabb.min.Z < min.Z) {min.Z = aabb.min.Z; changed = true;}
			if(aabb.max.X > max.X) {max.X = aabb.max.X; changed = true;}
			if(aabb.max.Y > max.Y) {max.Y = aabb.max.Y; changed = true;}
			if(aabb.max.Z > max.Z) {max.Z = aabb.max.Z; changed = true;}
			return changed;
		}
		public void Include(Vector3 v)
		{
			min = Vector3.ComponentMin(min, v);
			max = Vector3.ComponentMax(max, v);
		}
	}

	public struct Plane
	{
		public float a, b, c, d;
		public Plane(float a, float b, float c, float d)
		{
			this.a = a;
			this.b = b;
			this.c = c;
			this.d = d;
		}
		public Plane(Vector3 pos, Vector3 nml)
		{
			this.a = nml.X;
			this.b = nml.Y;
			this.c = nml.Z;
			this.d = Vector3.Dot(pos, nml);
		}
		public Plane(Vector3 p0, Vector3 p1, Vector3 p2)
		{
			Vector3 nml = Vector3.Cross(p1 - p0, p2 - p0);
			nml.Normalize();

			this.a = -nml.X;
			this.b = -nml.Y;
			this.c = -nml.Z;
			this.d = Vector3.Dot(p0, nml);
		}

		public void Normalize()
		{
			float len = (float)Math.Sqrt(a*a + b*b + c*c);
			a /= len;
			b /= len;
			c /= len;
			d /= len;
		}

		public float DotCoord(Vector3 pos)
		{
			return a * pos.X + b * pos.Y + c * pos.Z + d;
		}

		public bool IntersectLine(Vector3 lpos, Vector3 ldir, out Vector3 intersection)
		{
			float dot = Vector3.Dot(ldir, new Vector3(a, b, c));
			if(dot > -1e-6f && dot < 1e-6f)
			{
				intersection = Vector3.Zero;
				return false; // Line is parallel
			}

			//intersection = lpos + ldir * (d - Vector3.Dot(lpos, new Vector3(a, b, c)));

			float y = d - Vector3.Dot(lpos, new Vector3(a, b, c));
			Vector3 vy = new Vector3(a, b, c) * y;
			float ldy = Vector3.Dot(ldir, vy.Normalized());
			intersection = lpos + ldir * (y / ldy);

			return true;
		}

		public void Draw(GLShader sdr, Matrix4 viewprojmatrix)
		{
			//GL.Enable(EnableCap.CullFace);
			Matrix4 transform = Matrix4.CreateTranslation(-0.5f, -0.5f, 0.0f) * Common.Matrix4_CreateRotationDir(new Vector3(a, b, c), new Vector3(0.0f, 0.0f, 1.0f)) * Matrix4.CreateTranslation(new Vector3(a, b, c) * -d);
			sdr.Bind(transform * viewprojmatrix);
			Common.meshQuad.Bind(sdr, null);
			Common.meshQuad.Draw();
			//GL.Disable(EnableCap.CullFace);
		}

		public override string ToString()
		{
			return "nml: " + new Vector3(a, b, c).ToString() + ", dist: " + d.ToString();
		}
	}
	public struct Frustum
	{
		public Plane pleft, pright, ptop, pbottom, pnear, pfar;

		public Plane this[int index]  
		{  
			get {
				switch(index) {
				case 0: return pleft;
				case 1: return pright;
				case 2: return ptop;
				case 3: return pbottom;
				case 4: return pnear;
				case 5: return pfar;
				default: throw new IndexOutOfRangeException();
				}
			}  
		} 

		private static Vector3 Vector3_TransformNormal(Vector3 v, Matrix4 m)
		{
			return new Vector3(v.X * m.M11 + v.Y * m.M21 + v.Z * m.M31,
							   v.X * m.M12 + v.Y * m.M22 + v.Z * m.M32,
							   v.X * m.M13 + v.Y * m.M23 + v.Z * m.M33);
		}
		public bool DoFrustumCulling(Matrix4 worldmatrix, Matrix4 scalingmatrix, Matrix4 rotationmatrix, Vector3 center, Vector3 radius)
		{
			Vector3 vPlaneNormalWorld;

			Vector3 vBoundCenterWorld = Vector3.TransformPosition(center, worldmatrix);
			Matrix4 mScaleBounds = Matrix4.CreateScale(radius);
			Matrix4 mScaleRot = scalingmatrix * mScaleBounds * rotationmatrix;

			//Common.fontText.DrawString(0.0f, 00.0f, pleft.DotCoord(vBoundCenterWorld).ToString(), new Size(800, 600));
			//vPlaneNormalWorld = Vector3_TransformNormal(new Vector3(pleft.a, pleft.b, pleft.c), mScaleRot);
			//Common.fontText.DrawString(0.0f, 40.0f, vPlaneNormalWorld.Length.ToString(), new Size(800, 600));

			for(int i = 0; i < 6; ++i)
			{
				Plane p = this[i];
				vPlaneNormalWorld = Vector3_TransformNormal(new Vector3(p.a, p.b, p.c), mScaleRot);

				if(-p.DotCoord(vBoundCenterWorld) > vPlaneNormalWorld.Length)//if(p.DotCoord(vBoundCenterWorld) < vPlaneNormalWorld.Length)
					return false;
			}

			return true;
		}
	}

	class SortedList<Value> : System.Collections.Generic.IEnumerable<Value>
	{
		private class Element
		{
			public float key;
			public Value value;
			public Element next;
		}
		Element first = null;

		public void Add(float key, Value value)
		{
			Element e = new Element();
			e.key = key;
			e.value = value;

			Element prev = null, current = first;
			while(current != null && current.key < key)
			{
				prev = current;
				current = current.next;
			}

			e.next = current;
			if(prev != null)
				prev.next = e;
			else
				first = e;
		}

		public System.Collections.Generic.IEnumerator<Value> GetEnumerator()
		{
			for(Element current = first; current != null; current = current.next)
				yield return current.value;
		}
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}
	}
}

