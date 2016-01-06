using System;
using System.Collections.Generic;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace csharp_viewer
{
	public class TransformedImage : Cinema.CinemaImage
	{
		public class ImageLayer
		{
			public string filename;
			public string depth_filename, lum_filename;

			public GLTexture2D tex = null, tex_depth = null, tex_lum = null;
			public System.Drawing.Bitmap bmp = null, bmp_depth = null, bmp_lum = null;
			private System.Drawing.Bitmap oldbmp = null;
			public bool texIsStatic = false;
			public System.Threading.Mutex renderMutex = new System.Threading.Mutex();
			public int originalWidth = 0, originalHeight = 0;
			public float originalAspectRatio = 1.0f;
			public int renderWidth, renderHeight;
			public float renderPriority = 0;

			public bool isFloatImage = false;
			public bool HasDepthInfo { get {return depth_filename != null;} }

			public void RemoveIfUnloaded()
			{
				if(tex != null && !texIsStatic && (bmp == null || bmp != oldbmp)) // If a texture is loaded and either the image has been unloaded or changed
				{
					// Unload texture
					GL.DeleteTexture(tex.tex);
					tex = null;

					if(tex_depth != null)
					{
						GL.DeleteTexture(tex_depth.tex);
						tex_depth = null;
					}

					if(tex_lum != null)
					{
						GL.DeleteTexture(tex_lum.tex);
						tex_lum = null;
					}
				}
				oldbmp = bmp;
			}

			public bool CreateIfLoaded() // Returns true if ready for rendering
			{
				if(texIsStatic)
					return true;

				if(renderMutex.WaitOne(0))
				{
					if(bmp != null)
					{
						if(tex == null)
							tex = new GLTexture2D(bmp, false, !isFloatImage);
						if(bmp_depth != null && tex_depth == null)
							tex_depth = new GLTexture2D(bmp_depth, false, false, PixelFormat.Red, PixelInternalFormat.R32f, PixelType.Float);
						if(bmp_lum != null && tex_lum == null)
							tex_lum = new GLTexture2D(bmp_lum, false, true);
						renderMutex.ReleaseMutex();
						return true;
					}

					renderMutex.ReleaseMutex();
					return false;
				}

				return false;
			}
		}
		public List<ImageLayer> layers = new List<ImageLayer>();

		//public GLTexture2D tex = null, tex_depth = null, tex_lum = null;
		public int[] key;

		public bool selected = false;
		public Vector3 pos, scl; //TODO: Make publicly readonly
		public Quaternion rot; //TODO: Make publicly readonly
		private Vector3 animatedPos = new Vector3(float.NaN);
		public void skipPosAnimation() { animatedPos = pos; }
		private bool locationInvalid = false;
		public void InvalidateLocation() { locationInvalid = true; }

		public delegate void LocationChangedDelegate();
		public event LocationChangedDelegate LocationChanged;

		private List<ImageTransform> transforms = new List<ImageTransform>();

		//public int originalWidth = 0, originalHeight = 0;
		//public float originalAspectRatio = 1.0f;
		//public int renderWidth, renderHeight;
		//public float renderPriority = 0;
		//public System.Drawing.Bitmap bmp = null, bmp_depth = null, bmp_lum = null;
		//private System.Drawing.Bitmap oldbmp = null;
		//public System.Threading.Mutex renderMutex = new System.Threading.Mutex();
		//public bool texIsStatic = false;
		//public bool isFloatImage = false;

		private float prefetchHoldTime = 0.0f; // To avoid images to be unloaded between prefetching and rendering, prefetchHoldTime gets set to the expected render time inside PrefetchRenderPriority()

		private bool visible_manual = true; // Publicly controllable visibility
		private bool visible_static = true; // == visible_manual + visibility for static image skip-transforms
		public bool Visible
		{
			get { return visible_manual; }
			set {
				if((visible_static = visible_manual = value) == true)
					foreach(ImageTransform t in transforms)
						if(t.SkipImageInterval == ImageTransform.UpdateInterval.Static)
							visible_static &= !t.SkipImage(key, this);
			}
		}

		/*private bool color_manual = true; // Publicly controllable color
		private bool color_static = true; // == color_manual + color for static image color-transforms
		public bool Color
		{
			get { return color_manual; }
			set {
				if((color_static = color_manual = value) == true)
					foreach(ImageTransform t in transforms)
						if(t.SkipImageInterval == ImageTransform.UpdateInterval.Static)
							color_static &= !t.SkipImage(key, this);
			}
		}*/

		//public bool HasDepthInfo { get {return depth_filename != null;} }

		public void Update(float dt)
		{
			Common.AnimateTransition(ref animatedPos, pos, dt);
		}

		public void PrepareRender()
		{
			foreach(ImageTransform t in transforms)
				t.PrepareImage(key, this);
		}

		public bool IsVisible()
		{
			// Compute dynamic visibility and check if dynamic location updates are required
			bool visible_dynamic = visible_static;
			foreach(ImageTransform t in transforms)
				if(t.SkipImageInterval == ImageTransform.UpdateInterval.Dynamic)
					visible_dynamic &= !t.SkipImage(key, this);
			return visible_dynamic;
		}
		public bool IsVisible(ImageCloud.FreeView freeview, Matrix4 invvieworient, System.Drawing.Size backbuffersize, out Matrix4[] transforms)
		{
			// Compute dynamic visibility and check if dynamic location updates are required
			bool visible_dynamic = visible_static;
			bool hasDynamicLocationTransform = locationInvalid;
			foreach(ImageTransform t in this.transforms)
			{
				if(t.SkipImageInterval == ImageTransform.UpdateInterval.Dynamic)
					visible_dynamic &= !t.SkipImage(key, this);
				if(t.locationTransformInterval == ImageTransform.UpdateInterval.Dynamic)
					hasDynamicLocationTransform = true;
			}

			foreach(ImageLayer layer in layers)
				layer.RemoveIfUnloaded();
			/*if(tex != null && !texIsStatic && (bmp == null || bmp != oldbmp)) // If a texture is loaded and either the image has been unloaded or changed
			{
				// Unload texture
				GL.DeleteTexture(tex.tex);
				tex = null;

				if(tex_depth != null)
				{
					GL.DeleteTexture(tex_depth.tex);
					tex_depth = null;
				}

				if(tex_lum != null)
				{
					GL.DeleteTexture(tex_lum.tex);
					tex_lum = null;
				}
			}
			oldbmp = bmp;*/

			transforms = new Matrix4[layers.Count];
			for(int i = 0; i < transforms.Length; ++i)
				transforms[i] = invview; //Matrix4.Identity
			if(visible_dynamic)
			{
				if(hasDynamicLocationTransform)
					ComputeLocation();

				bool inside_frustum = false;
				for(int i = 0; i < transforms.Length; ++i)
				{
					//transforms[i] *= Matrix4.CreateTranslation(-0.5f, -0.5f, 0.0f);
					transforms[i] *= Matrix4.CreateScale(layers[i].originalAspectRatio, 1.0f, 1.0f);
					//transforms[i] *= Matrix4.CreateScale(2.0f, 2.0f, 1.0f);
					//if(depth_filename == null) // Do not always face screen when rendering volume images
						transforms[i] *= invvieworient;
					transforms[i] *= Matrix4.CreateTranslation(animatedPos);

					if(freeview.DoFrustumCulling(transforms[i], Matrix4.Identity, Matrix4.Identity, new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.5f, 0.5f, 0.5f)))
					{
						transforms[i] *= freeview.viewprojmatrix;

						// Set render priority and dimensions (thread safety: priority has to be set after width/height)
						Vector3 vsize = Vector3.TransformPerspective(new Vector3(0.5f, 0.5f, 0.0f), transforms[i]) - Vector3.TransformPerspective(new Vector3(0.0f, 0.0f, 0.0f), transforms[i]); // Size of image in device units
						layers[i].renderWidth = Math.Max(1, (int)(vsize.X * (float)backbuffersize.Width));
						layers[i].renderHeight = Math.Max(1, (int)(vsize.Y * (float)backbuffersize.Height));

						//if(layers[i].renderPriority == 0)
							layers[i].renderPriority = 1000;

						inside_frustum = true;
					}
					else if(Global.time > prefetchHoldTime)
						layers[i].renderPriority = 0;
				}
				return inside_frustum;
			}
			else
			{
				if(Global.time > prefetchHoldTime)
					foreach(ImageLayer layer in layers)
						layer.renderPriority = 0;
				return false;
			}
		}
		public void PrefetchRenderPriority(ImageCloud.FreeView freeview, Matrix4 invvieworient, System.Drawing.Size backbuffersize)
		{
			// Compute dynamic visibility and check if dynamic location updates are required
			bool visible_dynamic = visible_static;
			bool hasDynamicSkipTransform = false, hasDynamicLocationTransform = false;
			foreach(ImageTransform t in this.transforms)
			{
				if(t.SkipImageInterval == ImageTransform.UpdateInterval.Dynamic)
				{
					hasDynamicSkipTransform = true;
					visible_dynamic &= !t.SkipImage(key, this);
				}
				//if(t.locationTransformInterval == ImageTransform.UpdateInterval.Dynamic) //EDIT: Compute location locally
				//	hasDynamicLocationTransform = true; //EDIT: Compute location locally
			}

			if(!hasDynamicSkipTransform && !hasDynamicLocationTransform) // If neither location nor skipping is time variant
				return; // Don't prefetch

			Matrix4[] transforms = new Matrix4[layers.Count];
			for(int i = 0; i < transforms.Length; ++i)
				transforms[i] = invview; //Matrix4.Identity
			if(visible_dynamic)
			{
				//if(hasDynamicLocationTransform) //EDIT: Compute location locally
				//	ComputeLocation(); //EDIT: Compute location locally

				for(int i = 0; i < transforms.Length; ++i)
				{
					//transforms[i] *= Matrix4.CreateTranslation(-0.5f, -0.5f, 0.0f);
					transforms[i] *= Matrix4.CreateScale(layers[i].originalAspectRatio, 1.0f, 1.0f);
					//transforms[i] *= Matrix4.CreateScale(2.0f, 2.0f, 1.0f);
					//if(depth_filename == null) // Do not always face screen when rendering volume images
						transforms[i] *= invvieworient;
					transforms[i] *= Matrix4.CreateTranslation(animatedPos);

					if(freeview.DoFrustumCulling(transforms[i], Matrix4.Identity, Matrix4.Identity, new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.5f, 0.5f, 0.5f)))
					{
						transforms[i] *= freeview.viewprojmatrix;

						// Set render priority and dimensions (thread safety: priority has to be set after width/height)
						Vector3 vsize = Vector3.TransformPerspective(new Vector3(0.5f, 0.5f, 0.0f), transforms[i]) - Vector3.TransformPerspective(new Vector3(0.0f, 0.0f, 0.0f), transforms[i]); // Size of image in device units
						layers[i].renderWidth = Math.Max(1, (int)(vsize.X * (float)backbuffersize.Width));
						layers[i].renderHeight = Math.Max(1, (int)(vsize.Y * (float)backbuffersize.Height));
						layers[i].renderPriority = 1200; // Prefetch renderPriority should be higher than render renderPriority to prefere images being loaded before they are being rendered
						prefetchHoldTime = Global.time;
					}
				}
			}
		}
		public void Render(GLMesh mesh, ImageCloud.RenderShader sdr_default, ImageCloud.RenderShader sdr_float, Vector2 invbackbuffersize, float depthscale, ImageCloud.FreeView freeview, Matrix4[] transforms, int fragmentcounter, GLTexture2D texdepth = null)
		{
			for(int i = 0; i < transforms.Length; ++i)
			{
				ImageLayer layer = layers[i];
				bool texloaded = layer.CreateIfLoaded();
				ImageCloud.RenderShader sdr = layer.isFloatImage ? sdr_float : sdr_default;
				/*bool texloaded;
				if(texIsStatic)
					texloaded = true;
				else if(renderMutex.WaitOne(0))
				{
					if(bmp != null)
					{
						if(tex == null)
							tex = new GLTexture2D(bmp, false, !isFloatImage);
						if(bmp_depth != null && tex_depth == null)
							tex_depth = new GLTexture2D(bmp_depth, false, false, PixelFormat.Red, PixelInternalFormat.R32f, PixelType.Float);
						if(bmp_lum != null && tex_lum == null)
							tex_lum = new GLTexture2D(bmp_lum, false, true);
						renderMutex.ReleaseMutex();
						texloaded = true;
					}
					else
					{
						renderMutex.ReleaseMutex();
						texloaded = false;
					}
				}
				else
					texloaded = false;*/

				Color4 clr = new Color4(1.0f, 1.0f, 1.0f, 1.0f);
				/*if(selected)
				{
					clr.R = 1.0f;//Color4.Azure.R / 2.0f;
					clr.G = 0.5f;//Color4.Azure.G / 2.0f;
					clr.B = 0.5f;//Color4.Azure.B / 2.0f;
				}
				//clr.A = selected ? 1.0f : 0.3f;
				clr.A = 1.0f;*/

				//texloaded = true;
				sdr.Bind(transforms[i], clr, texloaded, layer.tex_depth != null, layer.tex_lum != null, invbackbuffersize, depthscale/*, invview*/);
				/*GL.Uniform4(sdr_colorParam, clr);
				//if(sdr_imageViewInv != -1)
				//	GL.UniformMatrix4(sdr_imageViewInv, false, ref invview);
				if(sdr_DepthScale != -1)
					GL.Uniform1(sdr_DepthScale, depthscale);*/

				mesh.Bind(sdr, layer.tex, layer.tex_depth, texdepth, layer.tex_lum);//mesh.Bind(sdr, tex.tex, tex.depth_tex);
				//GL.BeginQuery(QueryTarget.SamplesPassed, fragmentcounter);
				mesh.Draw();
				//GL.EndQuery(QueryTarget.SamplesPassed);

				/*int numfragments;
				GL.GetQueryObject(fragmentcounter, GetQueryObjectParam.QueryResult, out numfragments);
				renderPriority = numfragments;*/
			}

			foreach(ImageTransform t in this.transforms)
				t.RenderImage(key, this, freeview);
		}
		public void RenderDepth(GLMesh mesh, GLShader sdrDepth, ImageCloud.FreeView freeview, Matrix4[] transforms, GLTexture2D texdepth = null)
		{
			for(int i = 0; i < transforms.Length; ++i)
			{
				ImageLayer layer = layers[i];
				bool texloaded = layer.CreateIfLoaded();
				/*bool texloaded;
				if(texIsStatic)
					texloaded = true;
				else if(renderMutex.WaitOne(0))
				{
					if(bmp != null)
					{
						if(tex == null)
							tex = new GLTexture2D(bmp, false, !isFloatImage);
						if(bmp_depth != null && tex_depth == null)
							tex_depth = new GLTexture2D(bmp_depth, false, false, PixelFormat.Red, PixelInternalFormat.R32f, PixelType.Float);
						if(bmp_lum != null && tex_lum == null)
							tex_lum = new GLTexture2D(bmp_lum, false, true);
						renderMutex.ReleaseMutex();
						texloaded = true;
					}
					else
					{
						renderMutex.ReleaseMutex();
						texloaded = false;
					}
				}
				else
					texloaded = false;*/
			
				//texloaded = true;
				sdrDepth.Bind(transforms[i]);

				mesh.Bind(sdrDepth, layer.tex_depth, texdepth);
				mesh.Draw();
			}
		}

		public Matrix4 GetWorldMatrix(Matrix4 invvieworient)
		{
			Matrix4 worldmatrix = invview; //Matrix4.Identity
			//worldmatrix *= Matrix4.CreateTranslation(-0.5f, -0.5f, 0.0f);
			//worldmatrix *= Matrix4.CreateScale((float)img.Width / (float)img.Height, 1.0f, 1.0f);
			//worldmatrix *= Matrix4.CreateScale(2.0f, 2.0f, 1.0f);
			//if(depth_filename == null) // Do not always face screen when rendering volume images
				worldmatrix *= invvieworient;//Matrix4_CreateBillboardRotation(animatedPos, viewpos);
			worldmatrix *= Matrix4.CreateTranslation(animatedPos);

			return worldmatrix;
		}

		private static Matrix4 Matrix4_CreateBillboardRotation(Vector3 targetpos, Vector3 viewpos)
		{
			Vector3 view = targetpos - viewpos; view.Normalize();
			Vector3 up = new Vector3(0.0f, 1.0f, 0.0f);
			Vector3 right = Vector3.Cross(view, up); right.Normalize();
			up = Vector3.Cross(right, view); up.Normalize();

			return new Matrix4(right.X, right.Y, right.Z, 0.0f,
							   up.X, up.Y, up.Z, 0.0f,
							   view.X, view.Y, view.Z, 0.0f,
							   0.0f, 0.0f, 0.0f, 1.0f);
		}

		public float CastRay(Vector3 from, Vector3 dir, Matrix4 invvieworient)
		{
			//if(tex == null)
			//	return float.MaxValue;

			// Compute dynamic visibility
			bool visible_dynamic = visible_static;
			foreach(ImageTransform t in transforms)
				if(t.SkipImageInterval == ImageTransform.UpdateInterval.Dynamic)
					visible_dynamic &= !t.SkipImage(key, this);
			if(!visible_dynamic)
				return float.MaxValue;

			Matrix4 invworldmatrix = GetWorldMatrix(invvieworient);
			invworldmatrix.Invert();
			from = Vector3.TransformPerspective(from, invworldmatrix); //TransformPosition
			if(from.Z < 0.0f)
				return float.MaxValue;
			dir = Vector3.TransformNormal(dir, invworldmatrix);

			Vector3 dest = from - dir * (from.Z / dir.Z);

			//System.Windows.Forms.MessageBox.Show(dest.ToString());

			float halfwidth = 0.5f * layers[0].originalAspectRatio, halfheight = 0.5f;
			return -halfwidth < dest.X && dest.X < halfwidth && -halfheight < dest.Y && dest.Y < halfheight ? from.Z : float.MaxValue;
		}

		public void AddTransform(ImageTransform transform)
		{
			transforms.Add(transform);

			// Evaluate static transform visibility
			visible_static = visible_manual;
			foreach(ImageTransform t in transforms)
				if(t.SkipImageInterval == ImageTransform.UpdateInterval.Static)
					visible_static &= !t.SkipImage(key, this);

			ComputeLocation();

			// Update transform bounds
			transform.OnAddTransform(key, this);
		}
		public void RemoveTransform(ImageTransform transform)
		{
			if(!transforms.Remove(transform))
				return;

			// Evaluate static transform visibility
			visible_static = visible_manual;
			foreach(ImageTransform t in transforms)
				if(t.SkipImageInterval == ImageTransform.UpdateInterval.Static)
					visible_static &= !t.SkipImage(key, this);

			ComputeLocation();
		}
		public void ClearTransforms()
		{
			transforms.Clear();

			// Reset static visibility to manual visibility
			visible_static = visible_manual;

			ComputeLocation();
		}

		public void ComputeLocation()
		{
			locationInvalid = false;
			pos = Vector3.Zero;
			rot = Quaternion.Identity;
			scl = Vector3.One;

			Vector3 transformpos;
			Matrix4 transformmatrix = Matrix4.Identity;
			for(int t = 0; t < transforms.Count - 1; ++t)
			{
				transforms[t].LocationTransform(key, this, out transformpos, ref rot, ref scl);
				pos += Vector3.TransformPosition(transformpos, transformmatrix);
				//transformmatrix *= transforms[t].GetTransformBounds(this).GetTransform();
			}
			if(transforms.Count > 0)
			{
				transforms[transforms.Count - 1].LocationTransform(key, this, out transformpos, ref rot, ref scl);
				pos += Vector3.TransformPosition(transformpos, transformmatrix);
			}

			// Do not animate towards initial position
			if(float.IsNaN(animatedPos.X))
				skipPosAnimation();

			if(LocationChanged != null)
				LocationChanged();
		}

		public AABB GetBounds()
		{
			AABB aabb = new AABB();

			//foreach(ImageTransform t in transforms)
			//	aabb.Include(t.ImageBounds(key, this));

			Matrix4 worldmatrix = invview; //Matrix4.Identity
			//worldmatrix *= Matrix4.CreateScale(2.0f, 2.0f, 1.0f);
			worldmatrix *= Matrix4.CreateTranslation(pos);

			aabb.Include(Vector3.TransformPosition(new Vector3(-0.5f * layers[0].originalAspectRatio, -0.5f, -0.5f * layers[0].originalAspectRatio), worldmatrix));
			aabb.Include(Vector3.TransformPosition(new Vector3( 0.5f * layers[0].originalAspectRatio,  0.5f,  0.5f * layers[0].originalAspectRatio), worldmatrix));

			return aabb;
		}
	}

	/*public class TransformedImageCollection : Dictionary<int[], TransformedImage>
	{

	}*/
	public class TransformedImageCollection : List<TransformedImage>
	{
		public List<TransformedImage> Values { get { return this; } }
		public IEnumerable<TransformedImage> ReverseValues { get
		{
			for(int i = this.Count - 1; i >= 0; --i)
			{
				yield return this[i];
			}
		}}
	}
}

