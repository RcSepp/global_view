#define USE_DEPTH_SORTING

using System;
using System.Windows.Forms;
using System.Drawing;
using System.Collections;
using System.Collections.Generic;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;

namespace csharp_viewer
{
	public static class IMAGE_CLOUD_SHADER
	{
		public const string VS_DEFAULT = @"
			attribute vec3 vpos;
			attribute vec2 vtexcoord;
			uniform mat4 World;
			varying vec2 uv;
			varying float alpha;

			void main()
			{
				gl_Position = World * vec4(vpos, 1.0);
				uv = vtexcoord;
				alpha = 1.0;
			}
		";
		public const string VS_DEPTHIMAGE = @"
			attribute vec3 vpos;
			attribute vec2 vtexcoord;
			uniform sampler2D Texture2;
			uniform mat4 World;
			uniform mat4 matImageView; // View matrix of original image
			varying vec2 uv;
			varying float alpha;

			void main()
			{
				vec3 pos = vpos;
				uv = vtexcoord;

				float depth = texture2D(Texture2, uv).r;

				pos.x = uv.x * 2.0 - 1.0;
				pos.y = 1.0 - uv.y * 2.0;
				pos.z = 1.0;
				pos.xy *= cos(1.0472);
				pos.xyz *= depth;

				pos = (matImageView * vec4(pos, 1.0)).xyz;

				alpha = depth < 1e20 ? 1.0 : 0.0;

				gl_Position = World * vec4(pos, 1.0);
			}
		";
		/*public const string VS_USING_GS = @"
			attribute vec3 vpos;

			void main()
			{
				gl_Position = vec4(vpos, 1.0);
			}
		";
		public const string GL = @"
			#extension GL_EXT_geometry_shader4 : require
			//#version 150
			//#extension GL_ARB_explicit_attrib_location : require
			//#extension GL_ARB_explicit_uniform_location : require

			//layout(GL_POINTS​) in;
			//layout(GL_TRIANGLE_STRIP, 4);

			//layout(points) in;
			//layout(triangle_strip, max_vertices = 4) out;

			varying out vec2 uv;

			uniform mat4 World;

			void main()
			{
				gl_Position = World * (gl_PositionIn[0] + vec4(0.0, 1.0, 0.0, 0.0));
				uv = vec2(0.0, 0.0);
				EmitVertex();
				gl_Position = World * (gl_PositionIn[0] + vec4(0.0, 0.0, 0.0, 0.0));
				uv = vec2(0.0, 1.0);
				EmitVertex();
				gl_Position = World * (gl_PositionIn[0] + vec4(1.0, 1.0, 0.0, 0.0));
				uv = vec2(1.0, 0.0);
				EmitVertex();
				gl_Position = World * (gl_PositionIn[0] + vec4(1.0, 0.0, 0.0, 0.0));
				uv = vec2(1.0, 1.0);
				EmitVertex();
				EndPrimitive();
			}
		";*/
		public const string FS = @"
			varying vec2 uv;
			uniform sampler2D Texture;
			uniform vec4 Color;
			varying float alpha;

			vec4 shade(sampler2D sampler, in vec2 uv);

			/*vec3 hsv2rgb(vec3 c)
			{
				// Optimized HSV to RGB function
				// Source: http://lolengine.net/blog/2013/07/27/rgb-to-hsv-in-glsl

				vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
				vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
				return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
			}*/

			void main()
			{
				gl_FragColor = Color * shade(Texture, uv) * vec4(1.0, 1.0, 1.0, alpha);//vec4(0.8, 0.8, 0.8, alpha);
			}
		";
		public const string FS_DEFAULT_DECODER = @"
			vec4 shade(sampler2D sampler, in vec2 uv)
			{
//return texture2D(Texture, uv).r > 1e20 ? vec4(0.0, 0.0, 0.0, 0.0) : texture2D(Texture, uv);
				return texture2D(Texture, uv);
			}
		";
		public const string FS_COLORTABLE_DECODER = @"
			uniform sampler1D InnerColorTable, OuterColorTable;
			uniform int HasOuterColorTable;
			uniform float MinValue, MaxValue;
			uniform vec3 NanColor;

			vec4 shade(sampler2D sampler, in vec2 uv)
			{
				//const vec3 COLOR_NAN = vec3(65.0, 68.0, 91.0) / 255.0;
				const float MIN = 0.0;//0.35;
				const float MAX = 1.0;//0.85;

				// Get float value (valueS) from RGB data
				vec3 rgb = texture2D(Texture, uv).rgb;
				float alpha = texture2D(Texture, uv).a;
				int valueI = int(rgb.r * 255.0) * 0x10000 + int(rgb.g * 255.0) * 0x100 + int(rgb.b * 255.0);
				if(valueI == 0)
					return vec4(NanColor, alpha);
				float valueS = float(valueI - 0x1) / float(0xfffffe); // 0 is reserved as 'nothing'
				valueS = clamp((valueS - MIN) / (MAX - MIN) + MIN, 0.0, 1.0);

				// Sample color table based on valueS
				if(valueS < MinValue)
					return HasOuterColorTable != 0 ? vec4(texture1D(OuterColorTable, valueS).rgb, alpha) : vec4(texture1D(InnerColorTable, 0.0).rgb, alpha);
				else if(valueS > MaxValue)
					return HasOuterColorTable != 0 ? vec4(texture1D(OuterColorTable, valueS).rgb, alpha) : vec4(texture1D(InnerColorTable, 1.0 - 1e-5).rgb, alpha);
				else
					return vec4(texture1D(InnerColorTable, (valueS - MinValue) / (MaxValue - MinValue)).rgb, alpha);
			}
		";
	}
	public static class AABB_SHADER
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
				void main()
				{
					gl_FragColor = vec4(0.5, 1.0, 0.5, 1.0);
				}
			";
	}

	public delegate float ValueCast(object value);

	public class ImageCloud
	{
		private const float FOV_Y = 75.0f * MathHelper.Pi / 180.0f; //90.0f
		private const float Z_NEAR = 0.1f;
		private const float Z_FAR = 1000.0f;

		private Dictionary<int[], TransformedImage> images;
		private Cinema.CinemaArgument[] arguments;

		//public List<_ImageTransform> transforms = new List<_ImageTransform>();
		public List<ImageTransform> transforms = new List<ImageTransform>();

		public event Selection.ChangedDelegate SelectionChanged;
		public event DimensionMapper.TransformDelegate TransformAdded;

		GLShader sdrTextured, sdrAabb;
		int sdrTextured_colorParam;
		GLMesh meshVertex;

		private GLControl glcontrol;
		private Size backbuffersize;

		public class FreeView
		{
			private Vector3 pos, animationTargetPos = new Vector3(float.NaN);
			private Vector2 rot;
			private Frustum viewfrustum;
			private Matrix4 _viewmatrix = Matrix4.Identity, _projmatrix = Matrix4.Identity, _viewprojmatrix = Matrix4.Identity;
			public Matrix4 viewmatrix {get { return _viewmatrix;}}
			public Matrix4 projmatrix {get { return _projmatrix;}}
			public Matrix4 viewprojmatrix {get { return _viewprojmatrix;}}
			public Vector3 viewpos
			{
				get { return pos;}
				set
				{
					animationTargetPos.X = float.NaN; // Stop animation
					ActionManager.Do(MoveAction, new object[] {value, rot});
				}
			}
			private Action MoveAction, AnimatePositionAction;

			public FreeView()
			{
				MoveAction = ActionManager.CreateAction("Move View", this, "Move");
				AnimatePositionAction = ActionManager.CreateAction("Animate View Position", this, "AnimateCam");
				ActionManager.Do(MoveAction, new object[] {new Vector3(0.0f, 0.0f, 1.0f), rot});
			}

			public void AnimatePosition(float target_x, float target_y, float target_z)
			{
				ActionManager.Do(AnimatePositionAction, new object[] {target_x, target_y, target_z});
			}
			public void AnimatePosition(Vector3 target)
			{
				ActionManager.Do(AnimatePositionAction, new object[] {target.X, target.Y, target.Z});
			}
			private void AnimateCam(float x, float y, float z)
			{
				animationTargetPos = new Vector3(x, y, z); // Stop animation
			}

			public void Translate(Vector3 deltapos)
			{
				pos.Z -= deltapos.X * (float)Math.Sin(rot.Y) + deltapos.Z * (float)(Math.Cos(rot.X) * Math.Cos(rot.Y)) - deltapos.Y * (float)(Math.Sin(rot.X) * Math.Cos(rot.Y));
				pos.X -= deltapos.X * (float)Math.Cos(rot.Y) - deltapos.Z * (float)(Math.Cos(rot.X) * Math.Sin(rot.Y)) + deltapos.Y * (float)(Math.Sin(rot.X) * Math.Sin(rot.Y));
				pos.Y -= deltapos.Y * (float)Math.Cos(rot.X) + deltapos.Z * (float)Math.Sin(rot.X);

				ActionManager.Do(MoveAction, new object[] {pos, rot});
			}
			public void Rotate(Vector2 deltarot)
			{
				rot.X += deltarot.X;
				if(rot.X > MathHelper.PiOver2)
					rot.X = MathHelper.PiOver2;
				else if(rot.X < -MathHelper.PiOver2)
					rot.X = -MathHelper.PiOver2;
				rot.Y += deltarot.Y;

				ActionManager.Do(MoveAction, new object[] {pos, rot});
			}

			public void RotateAround(Vector2 deltarot, Vector3 center)
			{
				pos = Vector3.TransformPosition(pos, Matrix4.CreateTranslation(-center) * Matrix4.CreateRotationY(-deltarot.Y) * Matrix4.CreateRotationX(-deltarot.X * (float)Math.Cos(rot.Y)) * Matrix4.CreateRotationZ(-deltarot.X * (float)Math.Sin(rot.Y)) * Matrix4.CreateTranslation(center));

				rot.X += deltarot.X;
				rot.Y += deltarot.Y;

				ActionManager.Do(MoveAction, new object[] {pos, rot});
			}

			private void Move(Vector3 pos, Vector2 rot)
			{
				animationTargetPos.X = float.NaN; // Stop animation

				this.pos = pos;
				this.rot = rot;
				OnViewMatrixChanged();
			}

			public Vector3 GetViewDirection() //TODO: Untested
			{
				return new Vector3(_viewmatrix.M13, _viewmatrix.M23, _viewmatrix.M33);
			}

			public void OnSizeChanged(float aspectRatio)
			{
				_projmatrix = Matrix4.CreatePerspectiveFieldOfView(FOV_Y, aspectRatio, Z_NEAR, Z_FAR);
				_viewprojmatrix = _viewmatrix * _projmatrix;
			}

			public void Update(float dt)
			{
				if(!float.IsNaN(animationTargetPos.X))
				{
					Common.AnimateTransition(ref pos, animationTargetPos, dt);
					OnViewMatrixChanged();
				}
			}

			private void OnViewMatrixChanged()
			{
				_viewmatrix = Matrix4.CreateTranslation(-pos.X, -pos.Y, -pos.Z) * Matrix4.CreateRotationY(rot.Y) * Matrix4.CreateRotationX(rot.X);
				_viewprojmatrix = _viewmatrix * _projmatrix;

				// >>> Rebuild view frustum

				// Left plane
				viewfrustum.pleft.a = _viewprojmatrix.M14 + _viewprojmatrix.M11;
				viewfrustum.pleft.b = _viewprojmatrix.M24 + _viewprojmatrix.M21;
				viewfrustum.pleft.c = _viewprojmatrix.M34 + _viewprojmatrix.M31;
				viewfrustum.pleft.d = _viewprojmatrix.M44 + _viewprojmatrix.M41;
				viewfrustum.pleft.Normalize();

				// Right plane
				viewfrustum.pright.a = _viewprojmatrix.M14 - _viewprojmatrix.M11;
				viewfrustum.pright.b = _viewprojmatrix.M24 - _viewprojmatrix.M21;
				viewfrustum.pright.c = _viewprojmatrix.M34 - _viewprojmatrix.M31;
				viewfrustum.pright.d = _viewprojmatrix.M44 - _viewprojmatrix.M41;
				viewfrustum.pright.Normalize();

				// Top plane
				viewfrustum.ptop.a = _viewprojmatrix.M14 - _viewprojmatrix.M12;
				viewfrustum.ptop.b = _viewprojmatrix.M24 - _viewprojmatrix.M22;
				viewfrustum.ptop.c = _viewprojmatrix.M34 - _viewprojmatrix.M32;
				viewfrustum.ptop.d = _viewprojmatrix.M44 - _viewprojmatrix.M42;
				viewfrustum.ptop.Normalize();

				// Bottom plane
				viewfrustum.pbottom.a = _viewprojmatrix.M14 + _viewprojmatrix.M12;
				viewfrustum.pbottom.b = _viewprojmatrix.M24 + _viewprojmatrix.M22;
				viewfrustum.pbottom.c = _viewprojmatrix.M34 + _viewprojmatrix.M32;
				viewfrustum.pbottom.d = _viewprojmatrix.M44 + _viewprojmatrix.M42;
				viewfrustum.pbottom.Normalize();

				// Near plane
				viewfrustum.pnear.a = _viewprojmatrix.M13;
				viewfrustum.pnear.b = _viewprojmatrix.M23;
				viewfrustum.pnear.c = _viewprojmatrix.M33;
				viewfrustum.pnear.d = _viewprojmatrix.M43;
				viewfrustum.pnear.Normalize();

				// Far plane
				viewfrustum.pfar.a = _viewprojmatrix.M14 - _viewprojmatrix.M13;
				viewfrustum.pfar.b = _viewprojmatrix.M24 - _viewprojmatrix.M23;
				viewfrustum.pfar.c = _viewprojmatrix.M34 - _viewprojmatrix.M33;
				viewfrustum.pfar.d = _viewprojmatrix.M44 - _viewprojmatrix.M43;
				viewfrustum.pfar.Normalize();
			}

			public bool DoFrustumCulling(Matrix4 worldmatrix, Matrix4 scalingmatrix, Matrix4 rotationmatrix, Vector3 center, Vector3 radius)
			{
				return viewfrustum.DoFrustumCulling(worldmatrix, scalingmatrix, rotationmatrix, center, radius);
			}
		}
		FreeView freeview = new FreeView();
		private float aspectRatio;
		private AABB selectionAabb = null;
		private class MouseRect
		{
			public Vector2 min, max;

			public Matrix4 GetTransform()
			{
				Vector2 mid = (min + max) / 2.0f, halfsize = (max - min) / 2.0f;
				return /*Matrix4.CreateTranslation(-1.0f, -1.0f, 0.0f) * */ Matrix4.CreateScale(halfsize.X, halfsize.Y, 1.0f) * Matrix4.CreateTranslation(mid.X, mid.Y, 0.0f);
			}
		}
		private MouseRect mouseRect = null;

		string status_str = "";
		float status_timer = 0.0f;

		private enum ViewControl
		{
			ViewCentric, CoordinateSystemCentric
		}
		private ViewControl viewControl = ViewControl.ViewCentric;

		GLTexture2D texdot;

		ColorTableManager colorTableMgr = null;
		GLTextureStream texstream;
		CoordinateSystem coordsys;
		ImageContextMenu ContextMenu;
		ImageContextMenu.MenuGroup cmImage;

		private ArraySelection selection;

		GLTexture1D tex1, tex2; //TODO: Bad style (tex1 and tex2 are activated at the beginning of the draw call)

		// Actions
		private Action FocusSelectionAction, MoveSelectionAction;
		private Action SetViewControlAction;

		public void Init(GLControl glcontrol)
		{
			this.glcontrol = glcontrol;

			// Define actions
			FocusSelectionAction = ActionManager.CreateAction("Focus Selection", this, "FocusSelection");
			MoveSelectionAction = ActionManager.CreateAction("Move Selection", this, "MoveSelection");
			SetViewControlAction = ActionManager.CreateAction("Set View Control", this, "SetViewControl");

			// Load shaders
			sdrAabb = new GLShader(new string[] {AABB_SHADER.VS}, new string[] {AABB_SHADER.FS});

			texdot = GLTexture2D.FromFile("dot.png", true);

			coordsys = new CoordinateSystem();

			ContextMenu = new ImageContextMenu();
		}

		public void Load(FlowLayoutPanel pnlImageControls, Cinema.CinemaArgument[] arguments, Dictionary<int[], TransformedImage> images, Size imageSize, bool floatimages = false, bool depthimages = false)
		{
			int i;

			this.images = images;
			this.arguments = arguments;

			selection = new ArraySelection(images);

			sdrTextured = new GLShader(new string[] {depthimages ? IMAGE_CLOUD_SHADER.VS_DEPTHIMAGE : IMAGE_CLOUD_SHADER.VS_DEFAULT},
									   new string[] {IMAGE_CLOUD_SHADER.FS, floatimages ? IMAGE_CLOUD_SHADER.FS_COLORTABLE_DECODER : IMAGE_CLOUD_SHADER.FS_DEFAULT_DECODER},
									   null/*new string[] {IMAGE_CLOUD_SHADER.GL}*/);
			sdrTextured_colorParam = sdrTextured.GetUniformLocation("Color");

			if(floatimages)
			{
				colorTableMgr = new ColorTableManager(new Rectangle(100, 10, 15, 128), Common.meshQuad, pnlImageControls, OnColorTableSettingsChanged);
				colorTableMgr.Reset();
			}

			//texstream = new GLTextureStream(256, 963, 770, depthimages);
			//texstream = new GLTextureStream(512, 458, 517, depthimages); //1024
			//texstream = new GLTextureStream(256, 2266, 1100, depthimages);
			texstream = new GLTextureStream(-1, imageSize.Width, imageSize.Height, depthimages);
			//texstream = new GLTextureStream(256, imageSize.Width, imageSize.Height, depthimages);

			if(depthimages)
			{
				Vector3[] positions = new Vector3[imageSize.Width * imageSize.Height];
				Vector2[] texcoords = new Vector2[imageSize.Width * imageSize.Height];
				i = 0;
				for(int y = 0; y < imageSize.Height; ++y)
					for(int x = 0; x < imageSize.Width; ++x)
					{
						positions[i] = new Vector3((float)x / (float)imageSize.Width, (float)y / (float)imageSize.Height, 0.0f);
						texcoords[i] = new Vector2((float)x / (float)imageSize.Width, (float)(imageSize.Height - y - 1) / (float)imageSize.Height);
						++i;
					}

				int[] indices = new int[6 * (imageSize.Width - 1) * (imageSize.Height - 1)];
				i = 0;
				for(int y = 1; y < imageSize.Height; ++y)
					for(int x = 1; x < imageSize.Width; ++x)
					{
						indices[i++] = (x - 1) + imageSize.Width * (y - 1);
						indices[i++] = (x - 0) + imageSize.Width * (y - 1);
						indices[i++] = (x - 1) + imageSize.Width * (y - 0);

						indices[i++] = (x - 1) + imageSize.Width * (y - 0);
						indices[i++] = (x - 0) + imageSize.Width * (y - 1);
						indices[i++] = (x - 0) + imageSize.Width * (y - 0);
					}

				meshVertex = new GLMesh(positions, null, null, null, texcoords, indices);
			}
			else
				//meshVertex = new GLMesh(new Vector3[] {new Vector3(0.0f, 0.0f, 0.0f)}, null, null, null, null, null, PrimitiveType.Points); // Use this when rendering geometry shader quads
				meshVertex = Common.meshQuad;

			cmImage = new ImageContextMenu.MenuGroup("");
			i = 0;
			foreach(Cinema.CinemaArgument arg in arguments)
			{
				ImageContextMenu.MenuButton button = new ImageContextMenu.MenuButton(arg.label, cmdAlign_Click);
				button.tag = (object)i++;
				cmImage.controls.Add(button);
			}
			cmImage.ComputeSize();
		}

		public void Unload()
		{
			images = null;
			arguments = null;
			selection = null;
			selectionAabb = null;
			if(texstream != null)
			{
				texstream.Free();
				texstream = null;
			}
			sdrTextured = null;
			if(colorTableMgr != null)
			{
				colorTableMgr.Free();
				colorTableMgr = null;
			}
			cmImage = null;

			if(meshVertex != null && meshVertex != Common.meshLineQuad)
				meshVertex.Free();
			meshVertex = null;
		}

		private void OnColorTableSettingsChanged(ColorTableManager.ColorTableSettings settings)
		{
			sdrTextured.Bind();

			GL.Uniform1(sdrTextured.GetUniformLocation("HasOuterColorTable"), settings.hasOuterColorTable ? 1 : 0);
			GL.Uniform1(sdrTextured.GetUniformLocation("MinValue"), settings.minValue);
			GL.Uniform1(sdrTextured.GetUniformLocation("MaxValue"), settings.maxValue);
			GL.Uniform3(sdrTextured.GetUniformLocation("NanColor"), settings.nanColor);

			GL.ActiveTexture(TextureUnit.Texture1);
			settings.innerColorTable.Bind();
			GL.Uniform1(sdrTextured.GetUniformLocation("InnerColorTable"), 1);
			tex1 = settings.innerColorTable;

			if(settings.hasOuterColorTable)
			{
				GL.ActiveTexture(TextureUnit.Texture2);
				settings.outerColorTable.Bind();
				GL.Uniform1(sdrTextured.GetUniformLocation("OuterColorTable"), 2);
				tex2 = settings.outerColorTable;
			}
			else
			{
				GL.ActiveTexture(TextureUnit.Texture2);
				GL.Uniform1(sdrTextured.GetUniformLocation("OuterColorTable"), 2);
				tex2 = null;
			}

			GL.ActiveTexture(TextureUnit.Texture0);
		}

		private void FocusAABB(AABB aabb)
		{
			Vector3 minposX = new Vector3(float.MaxValue, 0.0f, 0.0f), minposY = new Vector3(0.0f, float.MaxValue, 0.0f);
			Vector3 maxposX = new Vector3(float.MinValue, 0.0f, 0.0f), maxposY = new Vector3(0.0f, float.MinValue, 0.0f), maxposZ = new Vector3(0.0f, 0.0f, float.MinValue);

			Matrix4 vieworient = freeview.viewmatrix;
			vieworient.M41 = vieworient.M42 = vieworient.M43 = 0.0f;

			float tanY = (float)Math.Tan(FOV_Y / 2.0f), tanX = tanY * aspectRatio;

			if(aabb.min.X == float.MaxValue)
				return;

			// Define corners of aabb to be the vertices to focus
			Vector3[] aabbvtxpos = new Vector3[] {aabb.min, aabb.max,
												  new Vector3(aabb.min.X, aabb.min.Y, aabb.max.Z),
												  new Vector3(aabb.min.X, aabb.max.Y, aabb.min.Z),
												  new Vector3(aabb.min.X, aabb.max.Y, aabb.max.Z),
												  new Vector3(aabb.max.X, aabb.min.Y, aabb.min.Z),
												  new Vector3(aabb.max.X, aabb.min.Y, aabb.max.Z),
												  new Vector3(aabb.max.X, aabb.max.Y, aabb.min.Z)};

			// Transform vertices to view space and store boundary vertices. Include projection when seeking those vertices
			foreach(Vector3 vtxpos in aabbvtxpos)
			{
				Vector3 viewvtxpos = Vector3.TransformPosition(vtxpos, vieworient);

				if(viewvtxpos.X - viewvtxpos.Z * tanX < minposX.X - minposX.Z * tanX)
					minposX = viewvtxpos;
				if(viewvtxpos.Y - viewvtxpos.Z * tanY < minposY.Y - minposY.Z * tanY)
					minposY = viewvtxpos;

				if(viewvtxpos.X + viewvtxpos.Z * tanX > maxposX.X + maxposX.Z * tanX)
					maxposX = viewvtxpos;
				if(viewvtxpos.Y + viewvtxpos.Z * tanY > maxposY.Y + maxposY.Z * tanY)
					maxposY = viewvtxpos;
				if(viewvtxpos.Z > maxposZ.Z)
					maxposZ = viewvtxpos;
			}

			// Find minimum view distance to view the outermost vertices. Do this for X and Y direction separately and choose the furthest distance to include all vertices
			Vector3 V = new Vector3();
			float V_Z_1 = (maxposX.X - minposX.X + (minposX.Z + maxposX.Z) * tanX) / (2.0f * tanX), V_Z_2 = (maxposY.Y - minposY.Y + (maxposY.Z + minposY.Z) * tanY) / (2.0f * tanY);
			V.Z = Math.Max(V_Z_1, V_Z_2);

			// Compute view position in x- and y direction to center vertices
			V.X = V_Z_1 == V.Z ? (tanX * V_Z_1 + minposX.X - minposX.Z * tanX) : ((minposX.X - minposX.Z * tanX + maxposX.X + maxposX.Z * tanX) / 2.0f);
			V.Y = V_Z_2 == V.Z ? (tanY * V_Z_2 + minposY.Y - minposY.Z * tanY) : ((minposY.Y - minposY.Z * tanY + maxposY.Y + maxposY.Z * tanY) / 2.0f);

			// Transform new view position back to global space
			vieworient.Transpose();
			freeview.AnimatePosition(Vector3.TransformPosition(V, vieworient));
		}

		public void OnSelectionChanged(Selection _selection)
		{
			status_str = "Selection changed: " + (_selection == null ? "null" : _selection.Count.ToString());
			status_timer = 1.0f;
			
			if(_selection == null || images == null)
				return;

			// Unselect all images
			foreach(KeyValuePair<int[], TransformedImage> image in images)
				image.Value.selected = false;

			// Select images and lay selectionAabb around selected images
			if(_selection.IsEmpty)
				selectionAabb = null;
			else
			{
				selectionAabb = new AABB();
				foreach(KeyValuePair<int[], TransformedImage> selectedimage in _selection)
				{
					selectedimage.Value.selected = true;
					selectionAabb.Include(selectedimage.Value.GetBounds());
				}
			}
		}

		public void OnSizeChanged(Size backbuffersize)
		{
			this.backbuffersize = backbuffersize;
			freeview.OnSizeChanged(aspectRatio = (float)backbuffersize.Width / (float)backbuffersize.Height);
		}

		struct TransformedImageAndMatrix
		{
			public TransformedImage image;
			public Matrix4 matrix;

			public TransformedImageAndMatrix(TransformedImage image, Matrix4 matrix)
			{
				this.image = image;
				this.matrix = matrix;
			}
		}
		public void Draw(float dt)
		{
			// >>> Update free-view matrix

			if(glcontrol.Focused)
			{
				Vector3 freeViewTranslation = new Vector3((InputDevices.kbstate.IsKeyDown(Key.A) ? 1.0f : 0.0f) - (InputDevices.kbstate.IsKeyDown(Key.D) ? 1.0f : 0.0f),
					                              (InputDevices.kbstate.IsKeyDown(Key.Space) ? 1.0f : 0.0f) - (InputDevices.kbstate.IsKeyDown(Key.LShift) ? 1.0f : 0.0f),
					                              (InputDevices.kbstate.IsKeyDown(Key.W) ? 1.0f : 0.0f) - (InputDevices.kbstate.IsKeyDown(Key.S) ? 1.0f : 0.0f));
				freeViewTranslation.Z += 0.01f * InputDevices.mdz;
				bool viewChanged = InputDevices.mdz != 0.0f;
				if(freeViewTranslation.X != 0.0f || freeViewTranslation.Y != 0.0f || freeViewTranslation.Z != 0.0f)
				{
					freeview.Translate(Vector3.Multiply(freeViewTranslation, 10.0f * dt));
					viewChanged = true;
				}
				if(InputDevices.mstate.IsButtonDown(MouseButton.Middle))
				{
					if(viewControl == ViewControl.ViewCentric)
						freeview.Rotate(new Vector2(0.01f * InputDevices.mdy, 0.01f * InputDevices.mdx));
					else // viewControl == ViewControl.CoordinateSystemCentric
					{
						if(selectionAabb != null)
							freeview.RotateAround(new Vector2(0.01f * InputDevices.mdy, 0.01f * InputDevices.mdx), (selectionAabb.min + selectionAabb.max) / 2.0f);
						else
							freeview.RotateAround(new Vector2(0.01f * InputDevices.mdy, 0.01f * InputDevices.mdx), Vector3.Zero);
					}
					viewChanged = true;
				}
				freeview.Update(dt);
				if(viewChanged)
					foreach(ImageTransform transform in transforms)
						transform.OnCameraMoved(freeview);
			}
			else
				freeview.Update(dt);

			if(texstream != null)
				texstream.Update();

			// >>> Render

			if(tex2 != null)
			{
				GL.ActiveTexture(TextureUnit.Texture2);
				tex2.Bind();
			}
			if(tex1 != null)
			{
				GL.ActiveTexture(TextureUnit.Texture1);
				tex1.Bind();
			}
			GL.ActiveTexture(TextureUnit.Texture0);

			foreach(ImageTransform transform in transforms)
				transform.OnRender(dt, freeview);

			if(images != null)
				foreach(TransformedImage image in images.Values)
					image.PrepareRender();

			Matrix4 vieworient = freeview.viewmatrix, invvieworient;
			vieworient.M41 = vieworient.M42 = vieworient.M43 = 0.0f;
			invvieworient = vieworient;
			invvieworient.Transpose();

			if(selectionAabb != null)
			{
				sdrAabb.Bind(selectionAabb.GetTransform() * freeview.viewprojmatrix);
				Common.meshLineCube.Bind(sdrAabb, null);
				Common.meshLineCube.Draw();
			}
			else
				coordsys.Draw(Vector3.Zero, freeview.viewprojmatrix, vieworient, freeview.viewpos, FOV_Y * backbuffersize.Width / backbuffersize.Height, backbuffersize);

			if(images != null)
			{
#if USE_DEPTH_SORTING
			SortedList<TransformedImageAndMatrix> renderlist = new SortedList<TransformedImageAndMatrix>();
#endif
				foreach(KeyValuePair<int[], TransformedImage> iter in images)
				{
					// Make sure texture is loaded
					iter.Value.LoadTexture(texstream);

					/*bool skip = false;
				foreach(_ImageTransform transform in transforms)
					if(transform.SkipImage(iter.Key, iter.Value))
					{
						skip = true;
						break;
					}
				if(skip)
					continue;

				Matrix4 worldmatrix = Matrix4.Identity;
				worldmatrix *= Matrix4.CreateTranslation(-0.5f, -0.5f, 0.0f);
				worldmatrix *= Matrix4.CreateScale((float)iter.Value.img.Width / (float)iter.Value.img.Height, 1.0f, 1.0f);
				worldmatrix *= Matrix4.CreateScale(2.0f, 2.0f, 1.0f);
				foreach(_ImageTransform transform in transforms)
					worldmatrix *= transform.LocationTransform(iter.Key, iter.Value);
				//worldmatrix *= Matrix4.CreateScale((float)iter.Value.img.Width / (float)iter.Value.img.Height, 1.0f, 1.0f);*/

					/*Color4 clr = new Color4(1.0f, 1.0f, 1.0f, 1.0f);
				foreach(_ImageTransform transform in transforms)
					transform.ColorTransform(iter.Key, iter.Value, ref clr);*/

					/*sdrTextured.Bind(worldmatrix * viewprojmatrix);
				GL.Uniform4(sdrTextured_colorParam, clr);*/
					sdrTextured.Bind();
					iter.Value.Update(dt);
					Matrix4 transform;
					if(iter.Value.IsVisible(freeview, invvieworient, out transform))
					{
#if USE_DEPTH_SORTING
					float dist = Vector3.TransformPerspective(Vector3.Zero, transform).Z;
					if(dist >= Z_NEAR && dist <= Z_FAR)
					{
						dist = -dist;
						renderlist.Add(dist, new TransformedImageAndMatrix(iter.Value, transform));
					}
#else
						iter.Value.Render(meshVertex, sdrTextured, sdrTextured_colorParam, freeview, transform);
#endif
					}
				}

#if USE_DEPTH_SORTING
			foreach(TransformedImageAndMatrix iter in renderlist)
				iter.image.Render(meshVertex, sdrTextured, sdrTextured_colorParam, freeview, iter.matrix);
#endif
			}

			if(selectionAabb != null)
			{
				Vector3 selectionAabbCenter = (selectionAabb.min + selectionAabb.max) / 2.0f;
				coordsys.Draw(selectionAabbCenter, freeview.viewprojmatrix, vieworient, freeview.viewpos, FOV_Y * backbuffersize.Width / backbuffersize.Height, backbuffersize);
			}

			if(defineAlignmentStage != DefineAlignmentStage.None)
			{
				coordsys.Draw(defineAlignmentOrigin, freeview.viewprojmatrix, vieworient, freeview.viewpos, FOV_Y * backbuffersize.Width / backbuffersize.Height, backbuffersize);
				if((int)defineAlignmentStage >= (int)DefineAlignmentStage.DefineOffset)
				{
					Vector3 line = defineAlignmentOrigin - defineAlignmentOffset;
					float linelen = line.Length;
					Matrix4 linetransform = Matrix4.CreateScale(linelen);
					linetransform *= Common.Matrix4_CreateRotationDir(line.Normalized(), new Vector3(0.0f, 0.0f, 1.0f));
					linetransform *= Matrix4.CreateTranslation(defineAlignmentOrigin);
					Common.sdrDashedLine.Bind(linetransform * freeview.viewprojmatrix);
					GL.Uniform1(Common.sdrDashedLine_lengthUniform, linelen);
					Common.meshLine.Bind(Common.sdrDashedLine, null);
					Common.meshLine.Draw();

					if((int)defineAlignmentStage >= (int)DefineAlignmentStage.DefineDelta)
					{
						Matrix4 dottransform = Matrix4.CreateTranslation(-0.5f, -0.5f, 0.0f);
						float zoom = 0.5f * Vector3.TransformPosition(freeview.viewpos - defineAlignmentOffset, vieworient).Z * (FOV_Y * backbuffersize.Width / backbuffersize.Height) * texdot.width / backbuffersize.Width;
						dottransform *= Matrix4.CreateScale(zoom, zoom * (float)texdot.height / (float)texdot.width, zoom);
						dottransform *= invvieworient;
//Matrix4 _dottransform = dottransform;
						dottransform *= Matrix4.CreateTranslation(defineAlignmentOffset);

						Common.sdrTextured.Bind(dottransform * freeview.viewprojmatrix);
						Common.meshQuad.Bind(Common.sdrTextured, texdot);

						for(int i = 0; i < arguments[defineAlignmentIndex].values.Length; ++i)
						{
							Common.meshQuad.Draw();

							dottransform *= Matrix4.CreateTranslation(defineAlignmentDelta);
							Common.sdrTextured.Bind(dottransform * freeview.viewprojmatrix);
						}
						/*foreach(KeyValuePair<int[], TransformedImage> selectedimage in selection)
						{
							Common.meshQuad.Draw();

							_dottransform *= Matrix4.CreateTranslation(selectedimage.Value.pos + defineAlignmentDelta * (float)selectedimage.Key[defineAlignmentIndex]);
							Common.sdrTextured.Bind(_dottransform * freeview.viewprojmatrix);
						}*/
					}
				}
			}

			GL.Disable(EnableCap.DepthTest);

			if(colorTableMgr != null)
				colorTableMgr.Draw(backbuffersize);

			//Common.fontText.DrawString(0.0f, 0.0f, freeview.viewpos.ToString(), backbuffersize);
			//Common.fontText.DrawString(0.0f, 20.0f, freeview.GetViewDirection().ToString(), backbuffersize);

			Common.fontText.DrawString(0.0f, 40.0f, GLTextureStream.foo.ToString(), backbuffersize);
			Common.fontText.DrawString(0.0f, 40.0f, foo.ToString(), backbuffersize);

			if(status_timer > 0.0f)
			{
				status_timer -= dt;
				Common.fontText.DrawString(0.0f, backbuffersize.Height - 20.0f, status_str, backbuffersize);
			}

			if(mouseRect != null)
			{
				Common.sdrSolidColor.Bind(mouseRect.GetTransform());

				Common.meshLineQuad.Bind(Common.sdrSolidColor, null);
				Common.meshLineQuad.Draw();
			}

			ContextMenu.Draw(dt, backbuffersize);

			GL.Enable(EnableCap.DepthTest);
		}

		private enum DefineAlignmentStage
		{
			None, DefineOrigin, DefineOffset, DefineDelta
		}
		private DefineAlignmentStage defineAlignmentStage = DefineAlignmentStage.None;
		private int defineAlignmentIndex;
		private Plane defineAlignmentPlane;
		private Vector3 defineAlignmentOrigin, defineAlignmentOffset, defineAlignmentDelta;
		private void cmdAlign_Click(ImageContextMenu.MenuButton sender)
		{
			if(selectionAabb == null)
				return;

			defineAlignmentStage = DefineAlignmentStage.DefineOrigin;
			defineAlignmentIndex = (int)sender.tag;

			Vector3 selectionAabbCenter = (selectionAabb.min + selectionAabb.max) / 2.0f;
			Matrix4 invviewprojmatrix = freeview.viewprojmatrix.Inverted();
			Vector3 vsnear = Vector3.TransformPerspective(new Vector3(0.0f, 0.0f, 0.0f), invviewprojmatrix);
			Vector3 vsfar = Vector3.TransformPerspective(new Vector3(0.0f, 0.0f, 1.0f), invviewprojmatrix);
			Vector3 vsdir = (vsfar - vsnear).Normalized();
			defineAlignmentPlane = new Plane(selectionAabbCenter, vsdir);

			defineAlignmentOrigin = selectionAabbCenter;

			Point mouseLocation = glcontrol.PointToClient(Control.MousePosition);
			Vector2 mousePos = new Vector2(2.0f * mouseLocation.X / backbuffersize.Width - 1.0f, 1.0f - 2.0f * mouseLocation.Y / backbuffersize.Height);
			Vector3 vnear = new Vector3(mousePos.X, mousePos.Y, 0.0f);
			Vector3 vfar = new Vector3(vnear.X, vnear.Y, 1.0f);
			vnear = Vector3.TransformPerspective(vnear, invviewprojmatrix);
			vfar = Vector3.TransformPerspective(vfar, invviewprojmatrix);
			Vector3 vdir = (vfar - vnear).Normalized();
			defineAlignmentPlane.IntersectLine(vnear, vdir, out defineAlignmentOrigin);
		}

		private TransformedImage dragImage;
		private Plane dragImagePlane;
		private Vector2 mouseDownPos;
		private Point mouseDownLocation;
		public void MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
		{
			if(ContextMenu.MouseDown(sender, e, backbuffersize))
				return;
			
			if(colorTableMgr != null && colorTableMgr.MouseDown(e))
				return;

			if(images == null)
				return;

			mouseDownPos = new Vector2(2.0f * e.X / backbuffersize.Width - 1.0f, 1.0f - 2.0f * e.Y / backbuffersize.Height);
			mouseDownLocation = e.Location;

			if((e.Button & MouseButtons.Left) != 0)
			{
				if(defineAlignmentStage != DefineAlignmentStage.None)
				{
					Vector3 vnear = new Vector3(mouseDownPos.X, mouseDownPos.Y, 0.0f);
					Vector3 vfar = new Vector3(vnear.X, vnear.Y, 1.0f);
					Matrix4 invviewprojmatrix = freeview.viewprojmatrix.Inverted();
					vnear = Vector3.TransformPerspective(vnear, invviewprojmatrix);
					vfar = Vector3.TransformPerspective(vfar, invviewprojmatrix);
					Vector3 vdir = (vfar - vnear).Normalized();

					switch(defineAlignmentStage)
					{
					case DefineAlignmentStage.DefineOrigin:
						defineAlignmentPlane.IntersectLine(vnear, vdir, out defineAlignmentOrigin);
						defineAlignmentStage = DefineAlignmentStage.DefineOffset;
						break;

					case DefineAlignmentStage.DefineOffset:
						defineAlignmentPlane.IntersectLine(vnear, vdir, out defineAlignmentOffset);
						defineAlignmentStage = DefineAlignmentStage.DefineDelta;
						break;

					case DefineAlignmentStage.DefineDelta:
						defineAlignmentPlane.IntersectLine(vnear, vdir, out defineAlignmentDelta);
						defineAlignmentDelta -= defineAlignmentOffset;

						defineAlignmentStage = DefineAlignmentStage.None;
						if(TransformAdded != null)
						{
							TranslationTransform newtransform = new TranslationTransform(defineAlignmentOffset - defineAlignmentOrigin, defineAlignmentDelta);
							//newtransform.SetArguments(arguments);
							newtransform.SetIndex(0, defineAlignmentIndex);
							TransformAdded(newtransform);
						}
						break;
					}
				}
				else
				{
					Matrix4 invvieworient = freeview.viewmatrix;
					invvieworient.M41 = invvieworient.M42 = invvieworient.M43 = 0.0f;
					invvieworient.Transpose();

					Vector3 vnear = new Vector3(mouseDownPos.X, mouseDownPos.Y, 0.0f);
					Vector3 vfar = new Vector3(vnear.X, vnear.Y, 1.0f);
					Matrix4 invviewprojmatrix = freeview.viewprojmatrix.Inverted();
					vnear = Vector3.TransformPerspective(vnear, invviewprojmatrix);
					vfar = Vector3.TransformPerspective(vfar, invviewprojmatrix);
					Vector3 vdir = (vfar - vnear).Normalized();

					float dist, closest_dist = float.MaxValue;
					KeyValuePair<int[], TransformedImage> closest_pair = default(KeyValuePair<int[], TransformedImage>);
					foreach(KeyValuePair<int[], TransformedImage> pair in images)
						if(pair.Value != null && (dist = pair.Value.CastRay(vnear, vdir, invvieworient)) < closest_dist)
						{
							closest_dist = dist;
							closest_pair = pair;
						}

					if(closest_dist < float.MaxValue)
					{
						dragImage = closest_pair.Value;

						// dragImagePlane = plane parallel to screen, going through point of intersection
						Vector3 vsnear = Vector3.TransformPerspective(new Vector3(0.0f, 0.0f, 0.0f), invviewprojmatrix);
						Vector3 vsfar = Vector3.TransformPerspective(new Vector3(0.0f, 0.0f, 1.0f), invviewprojmatrix);
						Vector3 vsdir = (vsfar - vsnear).Normalized();
						dragImagePlane = new Plane(vnear + vdir * closest_dist, vsdir);

						if(!selection.Contains(closest_pair.Key))
						{
							// Clear selection if Windows key (command key on Mac) isn't pressed
							if(InputDevices.kbstate.IsKeyUp(OpenTK.Input.Key.LWin) && !selection.IsEmpty)
								selection.Clear();

							selection.Add(closest_pair.Key);
							SelectionChanged(selection);
						}
					} else
					{
						dragImage = null;

						// Clear selection if Windows key (command key on Mac) isn't pressed
						if(InputDevices.kbstate.IsKeyUp(OpenTK.Input.Key.LWin) && !selection.IsEmpty)
						{
							selection.Clear();
							SelectionChanged(selection);
						}
					}
				}
			}
		}
		public void MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
		{
			dragImage = null;
			mouseRect = null;

			if((e.Button & MouseButtons.Right) != 0 && Math.Abs(mouseDownLocation.X - e.Location.X) + Math.Abs(mouseDownLocation.Y - e.Location.Y) < 4)
			{
				ContextMenu.Show(cmImage, e.Location, backbuffersize);
			}
			else if(ContextMenu.MouseUp(sender, e, backbuffersize))
				return;
			else if(colorTableMgr != null)
				colorTableMgr.MouseUp(e);
		}

		private void MoveSelection(Vector3 deltapos)
		{
			foreach(KeyValuePair<int[], TransformedImage> selectedimage in selection)
			{
				selectedimage.Value.pos += deltapos;
				selectedimage.Value.skipPosAnimation();
			}
			SelectionChanged(selection);
		}

string foo = "";
		public void MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
		{
			if(ContextMenu.MouseMove(sender, e, backbuffersize))
				return;

			if(colorTableMgr != null && colorTableMgr.MouseMove(e))
				return;

			if(images == null)
				return;

			Vector2 mousePos = new Vector2(2.0f * e.X / backbuffersize.Width - 1.0f, 1.0f - 2.0f * e.Y / backbuffersize.Height);

			if(dragImage != null)
			{
				Vector3 vnear = new Vector3(mousePos.X, mousePos.Y, 0.0f);
				Vector3 vfar = new Vector3(vnear.X, vnear.Y, 1.0f);
				Matrix4 invviewprojmatrix = freeview.viewprojmatrix.Inverted();
				vnear = Vector3.TransformPerspective(vnear, invviewprojmatrix);
				vfar = Vector3.TransformPerspective(vfar, invviewprojmatrix);
				Vector3 vdir = (vfar - vnear).Normalized();

				// Set newpos to intersection of mouse ray and dragImagePlane
				Vector3 newpos;
				dragImagePlane.IntersectLine(vnear, vdir, out newpos);
				ActionManager.Do(MoveSelectionAction, new object[] { newpos - dragImage.pos });
			}
			else if(defineAlignmentStage != DefineAlignmentStage.None)
			{
				Vector3 vnear = new Vector3(mousePos.X, mousePos.Y, 0.0f);
				Vector3 vfar = new Vector3(vnear.X, vnear.Y, 1.0f);
				Matrix4 invviewprojmatrix = freeview.viewprojmatrix.Inverted();
				vnear = Vector3.TransformPerspective(vnear, invviewprojmatrix);
				vfar = Vector3.TransformPerspective(vfar, invviewprojmatrix);
				Vector3 vdir = (vfar - vnear).Normalized();

				switch(defineAlignmentStage)
				{
				case DefineAlignmentStage.DefineOrigin:
					defineAlignmentPlane.IntersectLine(vnear, vdir, out defineAlignmentOrigin);
					break;

				case DefineAlignmentStage.DefineOffset:
					defineAlignmentPlane.IntersectLine(vnear, vdir, out defineAlignmentOffset);
					break;

				case DefineAlignmentStage.DefineDelta:
					defineAlignmentPlane.IntersectLine(vnear, vdir, out defineAlignmentDelta);
					defineAlignmentDelta -= defineAlignmentOffset;
					break;
				}
			}
			else if((e.Button & MouseButtons.Left) != 0 && Math.Abs(mouseDownLocation.X - e.Location.X) + Math.Abs(mouseDownLocation.Y - e.Location.Y) > 20)
			{
				mouseDownLocation = new Point(-100, -100); // Make sure mouse rect movement stays enabled

				// Update mouse rect
				if(mouseRect == null)
					mouseRect = new MouseRect();
				mouseRect.min.X = Math.Min(mouseDownPos.X, mousePos.X);
				mouseRect.min.Y = Math.Min(mouseDownPos.Y, mousePos.Y);
				mouseRect.max.X = Math.Max(mouseDownPos.X, mousePos.X);
				mouseRect.max.Y = Math.Max(mouseDownPos.Y, mousePos.Y);

				// >>> Perform frustum intersection with all images

				Frustum mouseRectFrustum;
				//Vector3 pmin = new Vector3(mouseRect.min.X, mouseRect.min.Y, 0.0f); // A point on the bottom left edge of the mouse rect frustum
				//Vector3 pmax = new Vector3(mouseRect.max.X, mouseRect.max.Y, 0.0f); // A point on the top right edge of the mouse rect frustum
				Matrix4 invviewprojmatrix = freeview.viewprojmatrix.Inverted();
				Vector3 ptl = Vector3.TransformPerspective(new Vector3(mouseRect.min.X, mouseRect.max.Y, 0.0f), invviewprojmatrix);
				Vector3 ptl_far = Vector3.TransformPerspective(new Vector3(mouseRect.min.X, mouseRect.max.Y, Z_NEAR), invviewprojmatrix);
				Vector3 ptr = Vector3.TransformPerspective(new Vector3(mouseRect.max.X, mouseRect.max.Y, 0.0f), invviewprojmatrix);
				Vector3 ptr_far = Vector3.TransformPerspective(new Vector3(mouseRect.max.X, mouseRect.max.Y, Z_NEAR), invviewprojmatrix);
				Vector3 pbl = Vector3.TransformPerspective(new Vector3(mouseRect.min.X, mouseRect.min.Y, 0.0f), invviewprojmatrix);
				Vector3 pbl_far = Vector3.TransformPerspective(new Vector3(mouseRect.min.X, mouseRect.min.Y, Z_NEAR), invviewprojmatrix);
				Vector3 pbr = Vector3.TransformPerspective(new Vector3(mouseRect.max.X, mouseRect.min.Y, 0.0f), invviewprojmatrix);

				// Left plane
				mouseRectFrustum.pleft = new Plane(ptl, ptl_far, pbl);

				// Right plane
				mouseRectFrustum.pright = new Plane(ptr, pbr, ptr_far);

				// Top plane
				mouseRectFrustum.ptop = new Plane(ptl, ptr, ptl_far);

				// Bottom plane
				mouseRectFrustum.pbottom = new Plane(pbl, pbl_far, pbr);

				// Near plane
				mouseRectFrustum.pnear.a = freeview.viewprojmatrix.M13;
				mouseRectFrustum.pnear.b = freeview.viewprojmatrix.M23;
				mouseRectFrustum.pnear.c = freeview.viewprojmatrix.M33;
				mouseRectFrustum.pnear.d = freeview.viewprojmatrix.M43;
				mouseRectFrustum.pnear.Normalize();

				// Far plane
				mouseRectFrustum.pfar.a = freeview.viewprojmatrix.M14 - freeview.viewprojmatrix.M13;
				mouseRectFrustum.pfar.b = freeview.viewprojmatrix.M24 - freeview.viewprojmatrix.M23;
				mouseRectFrustum.pfar.c = freeview.viewprojmatrix.M34 - freeview.viewprojmatrix.M33;
				mouseRectFrustum.pfar.d = freeview.viewprojmatrix.M44 - freeview.viewprojmatrix.M43;
				mouseRectFrustum.pfar.Normalize();

				Matrix4 invvieworient = freeview.viewmatrix;
				invvieworient.M41 = invvieworient.M42 = invvieworient.M43 = 0.0f;
				invvieworient.Transpose();
				foreach(KeyValuePair<int[], TransformedImage> pair in images)
					if(mouseRectFrustum.DoFrustumCulling(pair.Value.GetWorldMatrix(invvieworient), Matrix4.Identity, Matrix4.Identity, new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.5f, 0.5f, 0.5f)))
						selection.Add(pair.Key);
				SelectionChanged(selection);
			}
		}

		public void KeyDown(object sender, KeyEventArgs e)
		{
			switch(e.KeyCode)
			{
			case Keys.C:
				ViewControl[] values = (ViewControl[])Enum.GetValues(typeof(ViewControl));
				ActionManager.Do(SetViewControlAction, new object[] {(int)viewControl + 1 == values.Length ? values[0] : values[(int)viewControl + 1]});
				break;

			case Keys.F:
				//ActionManager.Do(FocusSelectionAction);
				FocusSelectionAction.Do();
				break;
			}
		}

		public Bitmap BackbufferToBitmap()
		{
			Bitmap bmp = new Bitmap(backbuffersize.Width, backbuffersize.Height);
			System.Drawing.Imaging.BitmapData data = bmp.LockBits(new Rectangle(Point.Empty, backbuffersize), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
			GL.ReadPixels(0, 0, backbuffersize.Width, backbuffersize.Height, PixelFormat.Bgr, PixelType.UnsignedByte, data.Scan0);
			bmp.UnlockBits(data);

			bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);
			return bmp;
		}

		public void FocusSelection()
		{
			// Focus selectionAabb
			if(selectionAabb != null)
			{
				FocusAABB(selectionAabb);
				status_str = "Focus selection";
				status_timer = 1.0f;
			}
		}
		private void SetViewControl(ViewControl vc)
		{
			// Set view mode
			viewControl = vc;
			status_str = "View control: " + viewControl.ToString();
			status_timer = 1.0f;
		}

		public ImageTransform AddTransform(ImageTransform transform)
		{
			transforms.Add(transform);
			transform.OnCameraMoved(freeview);
			return transform;
		}
		public ImageTransform RemoveTransform(ImageTransform transform)
		{
			transforms.Remove(transform);
			return transform;
		}
		public void ClearTransforms()
		{
			transforms.Clear();
		}
	}
}

