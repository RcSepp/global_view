#define USE_DEPTH_SORTING
//#define USE_GS_QUAD
#define USE_ARG_IDX

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
			uniform float DepthScale;
			uniform mat4 World;
			//uniform mat4 ImageViewInv; // Inverse view matrix of original image
			uniform vec3 eye, target;
			varying vec2 uv;
			varying float alpha;

#define PI 3.141593

			void main()
			{
				uv = vtexcoord;

				float depth = texture2D(Texture2, uv).r;

				// vdepth equals depth if DepthScale == 1.0 and merges towards vec3(1.0, 1.0, 0.0) when DepthScale goes towards 0.0
				vec3 vdepth;
				vdepth.x = vdepth.y = 1.0 + DepthScale * (depth - 1.0);
				vdepth.z = DepthScale * depth;

				float x = (9.5 * PI / 180.0) * vpos.x; //30.0 28.1
				float y = (9.5 * PI / 180.0) * vpos.y; //30.0 28.1
				vec3 voxelpos = (/*ImageViewInv **/ vec4(vec3(sin(x), sin(y), -cos(x) * cos(y)) * vdepth, 1.0)).xyz;

				alpha = depth < 1e20 ? 1.0 : 0.0;
				gl_Position = World * vec4(voxelpos, 1.0);
			}
		";
		public const string VS_USING_GS = @"
			attribute vec3 vpos;

			void main()
			{
				gl_Position = vec4(vpos, 1.0);
			}
		";
		public const string GS = @"
			#extension GL_EXT_geometry_shader4 : require
			//#version 150
			//#extension GL_ARB_explicit_attrib_location : require
			//#extension GL_ARB_explicit_uniform_location : require

			//layout(GL_POINTS​) in;
			//layout(GL_TRIANGLE_STRIP, 4);

			//layout(points) in;
			//layout(triangle_strip, max_vertices = 4) out;

			varying out vec2 uv;
			varying out float alpha;

			uniform mat4 World;

			void main()
			{
				gl_Position = World * (gl_PositionIn[0] + vec4(-0.5,  0.5, 0.0, 0.0));
				uv = vec2(0.0, 0.0);
				alpha = 1.0;
				EmitVertex();
				gl_Position = World * (gl_PositionIn[0] + vec4(-0.5, -0.5, 0.0, 0.0));
				uv = vec2(0.0, 1.0);
				alpha = 1.0;
				EmitVertex();
				gl_Position = World * (gl_PositionIn[0] + vec4( 0.5,  0.5, 0.0, 0.0));
				uv = vec2(1.0, 0.0);
				alpha = 1.0;
				EmitVertex();
				gl_Position = World * (gl_PositionIn[0] + vec4( 0.5, -0.5, 0.0, 0.0));
				uv = vec2(1.0, 1.0);
				alpha = 1.0;
				EmitVertex();
				EndPrimitive();
			}
		";
		public const string FS = @"
			varying vec2 uv;
			uniform sampler2D Texture;
			uniform vec4 Color;
			uniform int HasTexture;
			varying float alpha;

			vec4 shade(sampler2D sampler, in vec2 uv);

			void main()
			{
				/*gl_FragColor = Color * vec4(1.0, 1.0, 1.0, alpha);
				if(HasTexture != 0)
					gl_FragColor *= shade(Texture, uv);*/
				
				if(HasTexture != 0)
					gl_FragColor = Color * shade(Texture, uv) * vec4(1.0, 1.0, 1.0, alpha);
				else
					gl_FragColor = vec4(0.0, 0.0, 0.0, alpha);
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
			//uniform sampler1D InnerColorTable, OuterColorTable;
			//uniform int HasOuterColorTable;
			//uniform float MinValue, MaxValue;
uniform sampler1D Colormap;
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

				/*// Sample color table based on valueS
				if(valueS < MinValue)
					return HasOuterColorTable != 0 ? vec4(texture1D(OuterColorTable, valueS).rgb, alpha) : vec4(texture1D(InnerColorTable, 0.0).rgb, alpha);
				else if(valueS > MaxValue)
					return HasOuterColorTable != 0 ? vec4(texture1D(OuterColorTable, valueS).rgb, alpha) : vec4(texture1D(InnerColorTable, 1.0 - 1e-5).rgb, alpha);
				else
					return vec4(texture1D(InnerColorTable, (valueS - MinValue) / (MaxValue - MinValue)).rgb, alpha);*/

return vec4(texture1D(Colormap, valueS).rgb, alpha);
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

	public class ImageCloud : GLControl
	{
		private const float FOV_Y = 75.0f * MathHelper.Pi / 180.0f; //90.0f
		private const float Z_NEAR = 0.1f;
		private const float Z_FAR = 1000.0f;

		private TransformedImageCollection images;
		private Cinema.CinemaArgument[] arguments;
		private Selection selection = Viewer.selection;

		//public List<_ImageTransform> transforms = new List<_ImageTransform>();
		public List<ImageTransform> transforms = new List<ImageTransform>();

		public event Selection.ChangedDelegate SelectionChanged;
		public event Selection.MovedDelegate SelectionMoved;
		public delegate void TransformAddedDelegate(ImageTransform newtransform);
		public event TransformAddedDelegate TransformAdded;

		public class RenderShader : GLShader
		{
			int colorParam, hastex, depthScale, imageViewInv;

			public RenderShader(string[] vs, string[] fs, string[] gs = null)
				: base (vs, fs, gs)
			{
				colorParam = GetUniformLocation("Color");
				hastex = GetUniformLocation("HasTexture");
				depthScale = GetUniformLocation("DepthScale");
				imageViewInv = GetUniformLocation("ImageViewInv");
			}

			public void Bind(Matrix4 transform, Color4 clr, bool texloaded, float depthscale = 0.0f, Matrix4 invview = default(Matrix4))
			{
				Bind(transform);
				GL.Uniform4(colorParam, clr);
				GL.Uniform1(hastex, texloaded ? (int)1 : (int)0);
				//if(sdr_imageViewInv != -1)
				//	GL.UniformMatrix4(sdr_imageViewInv, false, ref invview);
				if(depthScale != -1)
					GL.Uniform1(depthScale, depthscale);
			}
		}
		RenderShader sdr2D, sdr3D;
		GLShader sdrAabb;
		GLMesh mesh2D, mesh3D;

		private GLWindow glcontrol;
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
					ActionManager.Do(MoveAction, value, rot);
				}
			}
			private Action MoveAction, AnimatePositionAction;

			public FreeView()
			{
				MoveAction = ActionManager.CreateAction("Move View", this, "Move");
				AnimatePositionAction = ActionManager.CreateAction("Animate View Position", this, "AnimateCam");
				//ActionManager.Do(MoveAction, new object[] {new Vector3(0.0f, 0.0f, 1.0f), rot});
				ActionManager.Do(MoveAction, new Vector3(0.0f, 0.0f, 10.0f), rot);

			}

			public void AnimatePosition(float target_x, float target_y, float target_z)
			{
				ActionManager.Do(AnimatePositionAction, target_x, target_y, target_z);
			}
			public void AnimatePosition(Vector3 target)
			{
				ActionManager.Do(AnimatePositionAction, target.X, target.Y, target.Z);
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

				ActionManager.Do(MoveAction, pos, rot);
			}
			public void Rotate(Vector2 deltarot)
			{
				rot.X += deltarot.X;
				if(rot.X > MathHelper.PiOver2)
					rot.X = MathHelper.PiOver2;
				else if(rot.X < -MathHelper.PiOver2)
					rot.X = -MathHelper.PiOver2;
				rot.Y += deltarot.Y;

				ActionManager.Do(MoveAction, pos, rot);
			}

			public void RotateAround(Vector2 deltarot, Vector3 center)
			{
				pos = Vector3.TransformPosition(pos, Matrix4.CreateTranslation(-center) * Matrix4.CreateRotationY(-deltarot.Y) * Matrix4.CreateRotationX(-deltarot.X * (float)Math.Cos(rot.Y)) * Matrix4.CreateRotationZ(-deltarot.X * (float)Math.Sin(rot.Y)) * Matrix4.CreateTranslation(center));

				rot.X += deltarot.X;
				rot.Y += deltarot.Y;

				ActionManager.Do(MoveAction, pos, rot);
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
				OnViewMatrixChanged();
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
		bool overallAabb_invalid = true;
		float camera_speed = 1.0f;

		static string status_str = "";
		static float status_timer = 0.0f;
		public static void Status(string status)
		{
			status_str = status;
			status_timer = 1.0f;
		}

		public enum ViewControl
		{
			ViewCentric, CoordinateSystemCentric, TwoDimensional
		}
		public ViewControl viewControl = ViewControl.ViewCentric;

		private bool depthRenderingEnabled = true;
		private float depthRenderingEnabled_fade;

		GLTexture2D texdot;

		ColorTableManager colorTableMgr;

		#if USE_ARG_IDX
		ArgumentIndex argIndex = new ArgumentIndex();
		#endif

		bool floatimages;
		GLTextureStream texstream;
		CoordinateSystem coordsys;
		ImageContextMenu ContextMenu;
		ImageContextMenu.MenuGroup cmImage;
		public void ShowContextMenu(ImageContextMenu.MenuGroup cm)
		{
			ContextMenu.Show(cm, glcontrol.PointToClient(Control.MousePosition), backbuffersize);
		}

		// Options

		public bool showCoordinateSystem = true;

		// Actions

		private Action SetViewControlAction;
		private Action EnableDepthRenderingAction, DisableDepthRenderingAction;
		private Action MoveAction;

		public void Init(GLWindow glcontrol)
		{
			this.glcontrol = glcontrol;

			// Define actions
			SetViewControlAction = ActionManager.CreateAction("Set View Control", this, "SetViewControl");
			EnableDepthRenderingAction = ActionManager.CreateAction("Enable Depth Rendering", "enable depth", this, "EnableDepthRendering");
			DisableDepthRenderingAction = ActionManager.CreateAction("Disable Depth Rendering", "disable depth", this, "DisableDepthRendering");
			MoveAction = ActionManager.CreateAction("Move images", this, "Move");
			/*ActionManager.CreateAction("Select all", "all", this, "SelectAll");
			ActionManager.CreateAction("Select and focus all", "focus all", delegate(object[] parameters) {
				this.SelectAll();
				this.FocusSelection();
			});*/

			// Load shaders
			sdrAabb = new GLShader(new string[] {AABB_SHADER.VS}, new string[] {AABB_SHADER.FS});

			// Create mesh for non-depth rendering
#if USE_GS_QUAD
			mesh2D = new GLMesh(new Vector3[] {new Vector3(0.0f, 0.0f, 0.0f)}, null, null, null, null, null, PrimitiveType.Points); // Use this when rendering geometry shader quads
#else
			mesh2D = Common.meshQuad2;
#endif

			texdot = GLTexture2D.FromFile("dot.png", true);

			#if USE_ARG_IDX
			argIndex.Bounds = new Rectangle(30, 10, 600, 16);
			argIndex.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			argIndex.Init();
			this.Controls.Add(argIndex);
			#endif

			colorTableMgr = new ColorTableManager(glcontrol);
			colorTableMgr.Visible = false;
			this.Controls.Add(colorTableMgr);

			coordsys = new CoordinateSystem();

			ContextMenu = new ImageContextMenu();
		}

		public void Load(Cinema.CinemaArgument[] arguments, TransformedImageCollection images, Dictionary<string, HashSet<object>> valuerange, Size imageSize, bool floatimages = false, bool depthimages = false)
		{
			int i;

			this.images = images;
			this.arguments = arguments;
			this.floatimages = floatimages;

			if(floatimages)
			{
				colorTableMgr.Visible = true;
				colorTableMgr.OnSizeChanged(backbuffersize);
				colorTableMgr.Reset();
			}
			else
				colorTableMgr.Visible = false;

#if USE_GS_QUAD
			sdr2D = new RenderShader(new string[] {IMAGE_CLOUD_SHADER.VS_USING_GS}, new string[] {IMAGE_CLOUD_SHADER.FS, floatimages ? IMAGE_CLOUD_SHADER.FS_COLORTABLE_DECODER : IMAGE_CLOUD_SHADER.FS_DEFAULT_DECODER}, new string[] {IMAGE_CLOUD_SHADER.GS});
#else
			sdr2D = new RenderShader(new string[] {IMAGE_CLOUD_SHADER.VS_DEFAULT}, new string[] {IMAGE_CLOUD_SHADER.FS, floatimages ? IMAGE_CLOUD_SHADER.FS_COLORTABLE_DECODER : IMAGE_CLOUD_SHADER.FS_DEFAULT_DECODER}, null);
#endif
			sdr3D = new RenderShader(new string[] {IMAGE_CLOUD_SHADER.VS_DEPTHIMAGE}, new string[] {IMAGE_CLOUD_SHADER.FS, floatimages ? IMAGE_CLOUD_SHADER.FS_COLORTABLE_DECODER : IMAGE_CLOUD_SHADER.FS_DEFAULT_DECODER}, null);

			foreach(GLShader sdr in new GLShader[] {sdr2D, sdr3D})
			{
				sdr.Bind();
				GL.ActiveTexture(TextureUnit.Texture2);
				colorTableMgr.Colormap.Bind();
				GL.Uniform1(sdr.GetUniformLocation("Colormap"), 2);
			}
			GL.ActiveTexture(TextureUnit.Texture0);

			//texstream = new GLTextureStream(images, 256*1024*1024, depthimages); // Optimize for 1GB of VRAM
			texstream = new GLTextureStream(images, 64*1024*1024, depthimages); // Optimize for 256MB of VRAM
			//texstream = new GLTextureStream(images, 8*1024*1024, depthimages); // Optimize for 32MB of VRAM
			//texstream = new GLTextureStream(images, 1024*1024, depthimages); // Optimize for 4MB of VRAM
			//texstream = new GLTextureStream(images, 128*1024, depthimages); // Optimize for 512KB of VRAM

			// Create mesh for depth rendering
			Size depthimagesize = new Size(imageSize.Width, imageSize.Height);
			Vector3[] positions = new Vector3[depthimagesize.Width * depthimagesize.Height];
			Vector2[] texcoords = new Vector2[depthimagesize.Width * depthimagesize.Height];
			i = 0;
			for(int y = 0; y < depthimagesize.Height; ++y)
				for(int x = 0; x < depthimagesize.Width; ++x)
				{
					positions[i] = new Vector3(2.0f * (float)x / (float)(depthimagesize.Width - 1) - 1.0f, 2.0f * (float)y / (float)(depthimagesize.Height - 1) - 1.0f, 1.0f);
					texcoords[i] = new Vector2((float)x / (float)(depthimagesize.Width - 1), 1.0f - (float)y / (float)(depthimagesize.Height - 1));
					++i;
				}
			/*int[] indices = new int[6 * (depthimagesize.Width - 1) * (depthimagesize.Height - 1)];
			i = 0;
			for(int y = 1; y < depthimagesize.Height; ++y)
				for(int x = 1; x < depthimagesize.Width; ++x)
				{
					indices[i++] = (x - 1) + depthimagesize.Width * (y - 1);
					indices[i++] = (x - 0) + depthimagesize.Width * (y - 1);
					indices[i++] = (x - 1) + depthimagesize.Width * (y - 0);

					indices[i++] = (x - 1) + depthimagesize.Width * (y - 0);
					indices[i++] = (x - 0) + depthimagesize.Width * (y - 1);
					indices[i++] = (x - 0) + depthimagesize.Width * (y - 0);
				}
			mesh3D = new GLMesh(positions, null, null, null, texcoords, indices);*/
			mesh3D = new GLMesh(positions, null, null, null, texcoords, null, PrimitiveType.Points);
			//GL.PointSize(2.0f);

			#if USE_ARG_IDX
			argIndex.Load(images, arguments, valuerange);
			/*argIndex.SelectionChanged += CallSelectionChangedHandlers;
			argIndex.ArgumentLabelMouseDown += ArgumentIndex_ArgumentLabelMouseDown;*/
			#endif

			cmImage = new ImageContextMenu.MenuGroup("");
			i = 0;
			foreach(Cinema.CinemaArgument arg in arguments)
			{
				ImageContextMenu.MenuButton button = new ImageContextMenu.MenuButton(arg.label, cmdAlign_Click);
				button.tag = (object)i++;
				cmImage.controls.Add(button);
			}
			cmImage.ComputeSize();

			// Enable depth rendering by default whenever a new scene is loaded
			depthRenderingEnabled = true;
			depthRenderingEnabled_fade = 1.0f;
//depthRenderingEnabled = false;
//depthRenderingEnabled_fade = 0.0f;
		}

		public void Unload()
		{
			images = null;
			arguments = null;
			selectionAabb = null;
			if(texstream != null)
			{
				texstream.Free();
				texstream = null;
			}
			sdr2D = null;
			sdr3D = null;
			if(mesh3D != null)
				mesh3D.Free();
			mesh3D = null;
			/*if(colorTableMgr != null)
			{
				colorTableMgr.Free();
				colorTableMgr = null;
			}*/
			cmImage = null;

			#if USE_ARG_IDX
			argIndex.Unload();
			#endif
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

		public void InvalidateOverallBounds()
		{
			overallAabb_invalid = true;
		}

		public void OnSelectionChanged()
		{
			#if USE_ARG_IDX
			if(argIndex != null)
				argIndex.OnSelectionChanged();
			#endif

			Status("Selection changed: " + (selection == null ? "null" : selection.Count.ToString()));

			if(images == null)
				return;

			// Unselect all images
			foreach(TransformedImage image in images.Values)
				image.selected = false;

			// Select images
			foreach(TransformedImage selectedimage in selection)
				selectedimage.selected = true;

			OnSelectionMoved();
		}
		public void OnSelectionMoved()
		{
			// Select images and lay selectionAabb around selected images
			if(selection == null || selection.IsEmpty)
				selectionAabb = null;
			else
			{
				selectionAabb = new AABB();
				if(viewControl == ViewControl.TwoDimensional)
					foreach(TransformedImage selectedimage in selection)
					{
						AABB selectedimageBounds = selectedimage.GetBounds().Clone();
						selectedimageBounds.min.Z = selectedimageBounds.max.Z = (selectedimageBounds.min.Z + selectedimageBounds.max.Z) / 2.0f;
						selectionAabb.Include(selectedimageBounds);
					}
				else
					foreach(TransformedImage selectedimage in selection)
						selectionAabb.Include(selectedimage.GetBounds());
			}
		}

		public void OnSizeChanged(Size backbuffersize)
		{
			this.backbuffersize = backbuffersize;
			freeview.OnSizeChanged(aspectRatio = (float)backbuffersize.Width / (float)backbuffersize.Height);

			if(floatimages)
				colorTableMgr.OnSizeChanged(backbuffersize);
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
		private Point oldmousepos = Control.MousePosition;
		protected override void Draw(float dt, Matrix4 _transform)
		{
			if(overallAabb_invalid)
			{
				if(images != null)
				{
					overallAabb_invalid = false;
					AABB overallAabb = new AABB();
					foreach(TransformedImage image in images.Values)
						overallAabb.Include(image.GetBounds());
					camera_speed = 0.1f * Math.Max(Math.Max(overallAabb.max.X - overallAabb.min.X, overallAabb.max.Y - overallAabb.min.Y), overallAabb.max.Z - overallAabb.min.Z);
					camera_speed = Math.Max(0.0001f, camera_speed);
					camera_speed = Math.Min(10.0f, camera_speed);
				}
				else
					camera_speed = 1.0f;
			}

			// >>> Update free-view matrix

			//if(glcontrol.ParentForm.Focused)
			if(glcontrol.Focused)
			{
				bool viewChanged = false;
				if(viewControl == ViewControl.TwoDimensional)
				{
					Vector3 freeViewTranslation = new Vector3(0.0f, 0.0f, camera_speed * 1.0f * InputDevices.mdz / dt);

					if(dragImage == null && (InputDevices.mstate.IsButtonDown(MouseButton.Middle) || InputDevices.mstate.IsButtonDown(MouseButton.Right)))
					{
						Vector2 dm = new Vector2(4.0f * (Control.MousePosition.X - oldmousepos.X) / backbuffersize.Width, 4.0f * (oldmousepos.Y - Control.MousePosition.Y) / backbuffersize.Height);
						Vector3 vnear = new Vector3(dm.X, dm.Y, 0.0f);
						Vector3 vfar = new Vector3(vnear.X, vnear.Y, 1.0f);
						Matrix4 invviewprojmatrix = freeview.viewprojmatrix.Inverted();
						vnear = Vector3.TransformPerspective(vnear, invviewprojmatrix);
						vfar = Vector3.TransformPerspective(vfar, invviewprojmatrix);
						Vector3 vdir = (vfar - vnear).Normalized();

						freeViewTranslation.X = 31.0f * vdir.X * freeview.viewpos.Z;//(float)dm.X / (float)backbuffersize.Width * freeview.viewpos.Z / (float)Z_NEAR;
						freeViewTranslation.Y = 31.0f * vdir.Y * freeview.viewpos.Z;//(float)dm.Y / (float)backbuffersize.Height * freeview.viewpos.Z / (float)Z_NEAR;
					}
					oldmousepos = Control.MousePosition;

					if(freeViewTranslation.X != 0.0f || freeViewTranslation.Y != 0.0f || freeViewTranslation.Z != 0.0f)
					{
						freeview.Translate(Vector3.Multiply(freeViewTranslation, dt));
						viewChanged = true;
					}
				}
				else
				{
					Vector3 freeViewTranslation = new Vector3((InputDevices.kbstate.IsKeyDown(Key.A) ? 1.0f : 0.0f) - (InputDevices.kbstate.IsKeyDown(Key.D) ? 1.0f : 0.0f),
						                             (InputDevices.kbstate.IsKeyDown(Key.Space) ? 1.0f : 0.0f) - (InputDevices.kbstate.IsKeyDown(Key.LShift) ? 1.0f : 0.0f),
						                             (InputDevices.kbstate.IsKeyDown(Key.W) ? 1.0f : 0.0f) - (InputDevices.kbstate.IsKeyDown(Key.S) ? 1.0f : 0.0f));
					freeViewTranslation.Z += InputDevices.mdz;
					if(freeViewTranslation.X != 0.0f || freeViewTranslation.Y != 0.0f || freeViewTranslation.Z != 0.0f)
					{
						freeview.Translate(Vector3.Multiply(freeViewTranslation, camera_speed * dt));
						viewChanged = true;
					}
					if(dragImage == null && (InputDevices.mstate.IsButtonDown(MouseButton.Middle) | InputDevices.mstate.IsButtonDown(MouseButton.Right)))
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
				}
				freeview.Update(camera_speed * dt);
				if(viewChanged)
					foreach(ImageTransform transform in transforms)
						transform.OnCameraMoved(freeview);
			}
			else
				freeview.Update(camera_speed * dt);

			// >>> Render

			GL.Enable(EnableCap.DepthTest);

			/*if(tex2 != null)
			{
				GL.ActiveTexture(TextureUnit.Texture2);
				tex2.Bind();
			}
			if(tex3 != null)
			{
				GL.ActiveTexture(TextureUnit.Texture3);
				tex3.Bind();
			}
			GL.ActiveTexture(TextureUnit.Texture0);*/

			/*if(floatimages)
			{
				GL.ActiveTexture(TextureUnit.Texture2);
				colorTableMgr.Colormap.Bind();
				GL.ActiveTexture(TextureUnit.Texture0);
			}*/

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
			else if(showCoordinateSystem)
				coordsys.Draw(Vector3.Zero, freeview.viewprojmatrix, vieworient, freeview.viewpos, FOV_Y * backbuffersize.Width / backbuffersize.Height, backbuffersize);

			if(depthRenderingEnabled)
			{
				depthRenderingEnabled_fade += 1.0f * dt;
				if(depthRenderingEnabled_fade > 1.0f)
					depthRenderingEnabled_fade = 1.0f;
			}
			else
			{
				depthRenderingEnabled_fade -= 1.0f * dt;
				if(depthRenderingEnabled_fade < 0.0f)
					depthRenderingEnabled_fade = 0.0f;
			}

			if(images != null)
			{
#if USE_DEPTH_SORTING
			SortedList<TransformedImageAndMatrix> renderlist = new SortedList<TransformedImageAndMatrix>();
#endif

				float _time = Global.time;
				//Global.time += 0.5f; // Prefetch 0.5 second into the future
				Global.time += 1.1f;
				// If the prefetching intervall is too short, images aren't loaded on time.
				// If the prefetching intervall is too long, too much memory is consumed.
				// Optimally the prefetching intervall should depend on the load time.
				foreach(TransformedImage iter in images.Values)
					iter.PrefetchRenderPriority(freeview, invvieworient, backbuffersize);
				Global.time = _time;

				foreach(TransformedImage iter in images.Values)
				{
					// Make sure texture is loaded
					iter.LoadTexture(texstream);

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
					iter.Update(dt);
					Matrix4 transform;
					if(iter.IsVisible(freeview, invvieworient, backbuffersize, out transform))
					{
#if USE_DEPTH_SORTING
						float dist = Vector3.TransformPerspective(Vector3.Zero, transform).Z;
						if(dist >= Z_NEAR && dist <= Z_FAR)
						{
							//dist = -dist;
							renderlist.Add(dist, new TransformedImageAndMatrix(iter, transform));
						}
#else
						if(depthRenderingEnabled_fade > 0.0 && iter.Value.HasDepthInfo)
						{
							sdr3D.Bind();
							iter.Value.Render(mesh3D, sdr3D, depthRenderingEnabled_fade, freeview, transform);
						}
						else
						{
							sdr2D.Bind();
							iter.Value.Render(mesh2D, sdr2D, 0.0f, freeview, transform);
						}
#endif
					}
				}

#if USE_DEPTH_SORTING
				//renderlist.reversed = true;
				foreach(TransformedImageAndMatrix iter in renderlist)
				{
					if(depthRenderingEnabled_fade > 0.0 && iter.image.HasDepthInfo)
					{
						sdr3D.Bind();
						iter.image.Render(mesh3D, sdr3D, depthRenderingEnabled_fade, freeview, iter.matrix);
					}
					else
					{
						sdr2D.Bind();
						iter.image.Render(mesh2D, sdr2D, 0.0f, freeview, iter.matrix);
					}
				}
#endif
			}

			if( showCoordinateSystem && selectionAabb != null)
			{
				Vector3 selectionAabbCenter = (selectionAabb.min + selectionAabb.max) / 2.0f;
				coordsys.Draw(selectionAabbCenter, freeview.viewprojmatrix, vieworient, freeview.viewpos, FOV_Y * backbuffersize.Width / backbuffersize.Height, backbuffersize);
			}

			if(defineAlignmentStage != DefineAlignmentStage.None)
			{
				if(showCoordinateSystem)
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

			//Common.fontText.DrawString(0.0f, 0.0f, freeview.viewpos.ToString(), backbuffersize);
			//Common.fontText.DrawString(0.0f, 20.0f, freeview.GetViewDirection().ToString(), backbuffersize);

			Common.fontText.DrawString(0.0f, 40.0f, GLTextureStream.foo.ToString(), backbuffersize);
			Common.fontText.DrawString(60.0f, 40.0f, GLTextureStream.foo2, backbuffersize);

			if(texstream != null)
				texstream.DrawDebugInfo(backbuffersize);

			if(status_timer > 0.0f)
			{
				status_timer -= dt;
				Common.fontText.DrawString(10.0f, backbuffersize.Height - 30.0f, status_str, backbuffersize);
			}

			if(floatimages)
				colorTableMgr.Draw(dt);

			if(selection != null)
			{
				int j = 2;
				foreach(TransformedImage selectedimage in selection)
				{
					string desc = "";
					if(selectedimage.args.Length > 0)
						desc = selectedimage.args[0].label + ": " + selectedimage.values[0].ToString();
					for(int i = 1; i < selectedimage.args.Length; ++i)
						desc += "  " + selectedimage.args[0].label + ": " + selectedimage.values[i].ToString();
					Common.fontText.DrawString(0.0f, backbuffersize.Height - 20.0f * j++, desc, backbuffersize);
					if(j >= 10)
						break;
				}
			}

			if(mouseRect != null)
			{
				Common.sdrSolidColor.Bind(mouseRect.GetTransform());

				Common.meshLineQuad.Bind(Common.sdrSolidColor, null);
				Common.meshLineQuad.Draw();
			}

			ContextMenu.Draw(dt, backbuffersize);
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
		private Vector3 dragImageOffset;
		private Plane dragImagePlane;
		private Vector2 mouseDownPos;
		private Point mouseDownLocation;
		private bool mouseDownInsideImageCloud = false;
		public void MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
		{
			mouseDownInsideImageCloud = false;

			if(ContextMenu.MouseDown(sender, e, backbuffersize))
				return;
			
			if(floatimages && colorTableMgr.MouseDown(e))
				return;

			if(images == null)
				return;

			mouseDownPos = new Vector2(2.0f * e.X / backbuffersize.Width - 1.0f, 1.0f - 2.0f * e.Y / backbuffersize.Height);
			mouseDownLocation = e.Location;
			mouseDownInsideImageCloud = true;

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
				TransformedImage closest_image = default(TransformedImage);
				foreach(TransformedImage image in images.ReverseValues)
					if(image != null && (dist = image.CastRay(vnear, vdir, invvieworient)) < closest_dist)
					{
						closest_dist = dist;
						closest_image = image;
					}

				if(closest_dist < float.MaxValue)
				{
					dragImage = closest_image;
					dragImageOffset = closest_image.pos - (vnear + vdir * closest_dist);

					// dragImagePlane = plane parallel to screen, going through point of intersection
					Vector3 vsnear = Vector3.TransformPerspective(new Vector3(0.0f, 0.0f, 0.0f), invviewprojmatrix);
					Vector3 vsfar = Vector3.TransformPerspective(new Vector3(0.0f, 0.0f, 1.0f), invviewprojmatrix);
					Vector3 vsdir = (vsfar - vsnear).Normalized();
					dragImagePlane = new Plane(vnear + vdir * closest_dist, vsdir);

					Viewer.browser.OnImageMouseDown(closest_image);
				}
				else
				{
					dragImage = null;

					// Clear selection if Windows key (command key on Mac) isn't pressed
					if(InputDevices.kbstate.IsKeyUp(OpenTK.Input.Key.LWin) && !selection.IsEmpty)
					{
						selection.Clear();
						SelectionChanged();
					}
				}
			}
		}
		public void MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
		{
			if(Math.Abs(mouseDownLocation.X - e.Location.X) + Math.Abs(mouseDownLocation.Y - e.Location.Y) < 4)
			{
				if(e.Button == MouseButtons.Left)
					Viewer.browser.OnImageClick(dragImage);
				else if(e.Button == MouseButtons.Right)
					Viewer.browser.OnImageRightClick(dragImage);
			}
			else if(!ContextMenu.MouseUp(sender, e, backbuffersize) && floatimages)
				colorTableMgr.MouseUp(e);

			dragImage = null;
			mouseRect = null;
		}

		public void Move(Vector3 deltapos, IEnumerable<TransformedImage> images)
		{
			foreach(TransformedImage image in images)
			{
				image.pos += deltapos;
				image.skipPosAnimation();
			}
			SelectionMoved();
		}
			
		public void MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
		{
			if(!mouseDownInsideImageCloud)
			{
				if(ContextMenu.MouseMove(sender, e, backbuffersize))
					return;

				if(floatimages && colorTableMgr.MouseMove(e))
					return;

				return;
			}

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
				Viewer.browser.OnImageMove(dragImage, newpos - dragImage.pos + dragImageOffset);
				//ActionManager.Do(MoveAction, newpos - dragImage.pos + dragImageOffset, selection);

				InvalidateOverallBounds();
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
				selection.Clear();
				foreach(TransformedImage image in images.Values)
					if(image.IsVisible() && mouseRectFrustum.DoFrustumCulling(image.GetWorldMatrix(invvieworient), Matrix4.Identity, Matrix4.Identity, new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.5f, 0.5f, 0.5f)))
						selection.Add(image);
				SelectionChanged();
			}
		}
		public void DoubleClick(object sender, Point mousepos)
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
			TransformedImage closest_image = default(TransformedImage);
			foreach(TransformedImage image in images.ReverseValues)
				if(image != null && (dist = image.CastRay(vnear, vdir, invvieworient)) < closest_dist)
				{
					closest_dist = dist;
					closest_image = image;
				}

			if(closest_dist < float.MaxValue)
			{
				dragImage = closest_image;
				dragImageOffset = closest_image.pos - (vnear + vdir * closest_dist);

				// dragImagePlane = plane parallel to screen, going through point of intersection
				Vector3 vsnear = Vector3.TransformPerspective(new Vector3(0.0f, 0.0f, 0.0f), invviewprojmatrix);
				Vector3 vsfar = Vector3.TransformPerspective(new Vector3(0.0f, 0.0f, 1.0f), invviewprojmatrix);
				Vector3 vsdir = (vsfar - vsnear).Normalized();
				dragImagePlane = new Plane(vnear + vdir * closest_dist, vsdir);

				Viewer.browser.OnImageDoubleClick(closest_image);
			}
		}

		public void MouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
		{
			if(!mouseDownInsideImageCloud)
			{
				if(floatimages && colorTableMgr.MouseWheel(e))
					return;

				return;
			}


		}

		public void KeyDown(object sender, KeyEventArgs e)
		{
			switch(e.KeyCode)
			{
			case Keys.C:
				ViewControl[] values = (ViewControl[])Enum.GetValues(typeof(ViewControl));
				ActionManager.Do(SetViewControlAction, (int)viewControl + 1 == values.Length ? values[0] : values[(int)viewControl + 1]);
				break;

			/*case Keys.O:
				if(depthRenderingEnabled)
					ActionManager.Do(DisableDepthRenderingAction);
				else
					ActionManager.Do(EnableDepthRenderingAction);
				break;

			case Keys.F:
				//ActionManager.Do(FocusAction, selection);
				Focus(selection);
				break;

			case Keys.Delete:
				ActionManager.Do(HideAction, selection);
				break;*/
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

		public void FocusSingle(TransformedImage image)
		{
			AABB imageBounds;
			if(viewControl == ViewControl.TwoDimensional)
			{
				imageBounds = image.GetBounds().Clone();
				imageBounds.min.Z = imageBounds.max.Z = (imageBounds.min.Z + imageBounds.max.Z) / 2.0f;
			}
			else
				imageBounds = image.GetBounds();

			if(imageBounds.max.X >= imageBounds.min.X)
			{
				FocusAABB(imageBounds);
				Status("Focus images");
			}
		}
		public void Focus(IEnumerable<TransformedImage> images)
		{
			AABB aabb = new AABB();
			if(viewControl == ViewControl.TwoDimensional)
				foreach(TransformedImage image in images)
				{
					AABB imageBounds = image.GetBounds().Clone();
					imageBounds.min.Z = imageBounds.max.Z = (imageBounds.min.Z + imageBounds.max.Z) / 2.0f;
					aabb.Include(imageBounds);
				}
			else
				foreach(TransformedImage image in images)
					aabb.Include(image.GetBounds());
			if(aabb.max.X >= aabb.min.X)
			{
				FocusAABB(aabb);
				Status("Focus images");
			}
		}

		public void MoveIntoView(TransformedImage image)
		{
			float tanY = (float)Math.Tan(FOV_Y / 2.0f), tanX = tanY * aspectRatio;
			Vector3 pos = Vector3.Transform(new Vector3(0.0f, 0.0f, -Math.Max(0.5f / tanY, 0.5f * image.originalAspectRatio / tanX)), freeview.viewmatrix.Inverted());
			image.pos = pos;

			if(image.selected)
				SelectionMoved();
		}

		/*public void Show(IEnumerable<TransformedImage> images)
		{
			foreach(TransformedImage image in images)
			{
				image.visible = true;
				Viewer.visible.Add(image);
			}
			Status("Show images");
		}
		public void Hide(IEnumerable<TransformedImage> images)
		{
			foreach(TransformedImage image in images)
			{
				image.visible = false;
				Viewer.visible.Remove(image);
			}
			Status("Hide images");
		}*/
		private void SetViewControl(ViewControl vc)
		{
			// Set view mode
			viewControl = vc;
			Status("View control: " + viewControl.ToString());
		}
		private void EnableDepthRendering()
		{
			if(depthRenderingEnabled)
				return;

			// Enable depth rendering
			depthRenderingEnabled = true;
			depthRenderingEnabled_fade = 0.0f;
			Status("Depth rendering enabled");
		}
		private void DisableDepthRendering()
		{
			if(!depthRenderingEnabled)
				return;

			// Disable depth rendering
			depthRenderingEnabled = false;
			depthRenderingEnabled_fade = 1.0f;
			Status("Depth rendering disabled");
		}
		private void SetDepthRendering(bool enabled)
		{
			// Enable or disable depth rendering
			if(enabled)
				EnableDepthRendering();
			else
				DisableDepthRendering();
		}
		private void SwitchDepthRendering()
		{
			// Enable or disable depth rendering
			if(depthRenderingEnabled)
				DisableDepthRendering();
			else
				EnableDepthRendering();
		}

		public void SelectAll()
		{
			foreach(TransformedImage image in images)
				selection.Add(image); //EDIT: should be inside mutex
			SelectionChanged();
		}
		public void Select(IEnumerable<TransformedImage> images)
		{
			selection.Clear();
			foreach(TransformedImage image in images)
				selection.Add(image); //EDIT: should be inside mutex
			SelectionChanged();
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
		public void Clear(IEnumerable<TransformedImage> images)
		{
			foreach(TransformedImage image in images)
			{
				image.ClearTransforms();
				image.skipPosAnimation();
			}
			SelectionChanged();
			//EDIT: Call transforms.Clear(); for all transforms that aren't needed anymore
		}

		public int Count(IEnumerable<TransformedImage> images)
		{
			int numimages;

			ICollection<TransformedImage> c = images as ICollection<TransformedImage>; // Try to cast images to a collection, because ICollection.Count might be faster than iterating
			if(c != null)
				numimages = c.Count;
			else
			{
				numimages = 0;
				using(IEnumerator<TransformedImage> enumerator = images.GetEnumerator())
					while(enumerator.MoveNext())
						numimages++;
			}

			return numimages;
		}
		public List<TransformedImage> CreateGroup(IEnumerable<TransformedImage> images)
		{
			return new List<TransformedImage>(images);
		}
	}
}

