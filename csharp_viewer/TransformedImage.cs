using System;
using System.Collections.Generic;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace csharp_viewer
{
	public class TransformedImage : Cinema.CinemaImage
	{
		public class ImageLayer : GLTextureStream.ImageReference
		{
			public TransformedImage _image;
			public ImageLayer(TransformedImage image, string filename, string depth_filename = null, string lum_filename = null, bool isFloatImage = false)
				: base(filename, depth_filename, lum_filename, isFloatImage)
			{
				this._image = image;
			}

			public int[] key;
			public bool[] keymask;
			public Cinema.CinemaStore.Parameter[] parameters;
			public int[] globalparamindices;

			public bool HasDepthInfo { get {return depth_filename != null;} }

			public override void OnOrignalDimensionsUpdated()
			{
				_image.OnOrignalDimensionsUpdated(originalWidth, originalHeight);
			}
		}
		public List<ImageLayer> inactivelayers = new List<ImageLayer>(), activelayers = new List<ImageLayer>();
		public ImageLayer FirstLayer
		{
			get {
				if(activelayers.Count != 0)
					return activelayers[0];
				if(inactivelayers.Count != 0)
					return inactivelayers[0];
				return null;
			}
		}

		public void AddLayer(ImageLayer newlayer)
		{
			bool isActive = true;
			int paramidx = 0;
			foreach(Cinema.CinemaStore.Parameter parameter in Global.parameters)
			{
				if(newlayer.keymask[paramidx] && !parameter.isChecked[newlayer.key[paramidx]])
				{
					isActive = false;
					break;
				}
				paramidx++;
			}
			if(isActive)
				activelayers.Add(newlayer);
			else
				inactivelayers.Add(newlayer);
		}

		int originalWidth = 0, originalHeight = 0;
		float originalAspectRatio = 1.0f;
		private void OnOrignalDimensionsUpdated(int originalWidth, int originalHeight)
		{
			this.originalWidth = Math.Max(this.originalWidth, originalWidth);
			this.originalHeight = Math.Max(this.originalHeight, originalHeight);
			this.originalAspectRatio = (float)this.originalWidth / (float)this.originalHeight;
		}

		public static class FramebufferCollection
		{
			private const int MAX_NUMFRAMEBUFFERS = 16;

			public class FramebufferAndTime
			{
				public int framebuffer;
				public GLTexture2D tex, texDepth1, texDepth2;
				public DateTime lastAccessTime;
			}
			private static Dictionary<System.Drawing.Size, FramebufferAndTime> framebuffers = new Dictionary<System.Drawing.Size, FramebufferAndTime>();

			public static FramebufferAndTime RequestFramebuffer(int framebufferWidth, int framebufferHeight)
			{
				System.Drawing.Size framebufferSize = new System.Drawing.Size(framebufferWidth, framebufferHeight);

				FramebufferAndTime fb_t;
				if(!framebuffers.TryGetValue(framebufferSize, out fb_t))
				{
					framebuffers.Add(framebufferSize, fb_t = new FramebufferAndTime());
					fb_t.framebuffer = GL.GenFramebuffer();
					fb_t.tex = new GLTexture2D("framebuffer_tex", framebufferWidth, framebufferHeight, false, PixelFormat.Rgba, PixelInternalFormat.Rgba, PixelType.Byte, linearfilter:true);
					fb_t.texDepth1 = new GLTexture2D("framebuffer_texDepth1", framebufferWidth, framebufferHeight, false, PixelFormat.DepthComponent, PixelInternalFormat.DepthComponent, PixelType.Float);
					fb_t.texDepth2 = new GLTexture2D("framebuffer_texDepth2", framebufferWidth, framebufferHeight, false, PixelFormat.DepthComponent, PixelInternalFormat.DepthComponent, PixelType.Float);
					GL.BindFramebuffer(FramebufferTarget.Framebuffer, fb_t.framebuffer);
					GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, fb_t.tex.tex, 0);
					GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, fb_t.texDepth1.tex, 0);
					//GL.DrawBuffer(DrawBufferMode.ColorAttachment0); //EDIT: May not be neccessary
					FramebufferErrorCode ferr;
					if((ferr = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer)) != FramebufferErrorCode.FramebufferComplete)
						throw new Exception(ferr.ToString());
					fb_t.lastAccessTime = DateTime.Now;

					if(framebuffers.Count > MAX_NUMFRAMEBUFFERS)
					{
						// Remove framebuffer that hasn't been used for the longest time
						DateTime oldestTime = DateTime.Now;
						KeyValuePair<System.Drawing.Size, FramebufferAndTime> oldestPair = new KeyValuePair<System.Drawing.Size, FramebufferAndTime>();
						foreach(KeyValuePair<System.Drawing.Size, FramebufferAndTime> pair in framebuffers)
						{
							if(pair.Value.lastAccessTime < oldestTime)
							{
								oldestTime = pair.Value.lastAccessTime;
								oldestPair = pair;
							}
						}
						System.Diagnostics.Debug.Assert(oldestPair.Key != framebufferSize);
						GL.DeleteFramebuffer(oldestPair.Value.framebuffer);
						oldestPair.Value.tex.Dispose();
						oldestPair.Value.texDepth1.Dispose();
						oldestPair.Value.texDepth2.Dispose();
						oldestPair.Value.tex = oldestPair.Value.texDepth1 = oldestPair.Value.texDepth2 = null;
						framebuffers.Remove(oldestPair.Key);
					}
				}
				else
					fb_t.lastAccessTime = DateTime.Now;

				GL.BindFramebuffer(FramebufferTarget.Framebuffer, fb_t.framebuffer);

				GL.PushAttrib(AttribMask.ViewportBit | AttribMask.DepthBufferBit | AttribMask.ColorBufferBit);
				GL.Enable(EnableCap.DepthTest);
				GL.Disable(EnableCap.DepthClamp);
				GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
				GL.Enable(EnableCap.Blend);
				GL.DepthFunc(DepthFunction.Lequal);
				//GL.BlendEquation(BlendEquationMode.);
				//GL.DrawBuffer(DrawBufferMode.ColorAttachment0); //EDIT: May not be neccessary
				GL.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
				GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
				GL.Viewport(0, 0, framebufferSize.Width, framebufferSize.Height);

				return fb_t;
			}
			public static void ReturnFramebuffer(FramebufferAndTime framebuffer)
			{
				GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

				// Restore old framebuffer state
				GL.PopAttrib();
			}
		}
			
		public GLTexture2D AssembleImage(ImageCloud.RenderShader sdr_assemble)
		{
			switch(activelayers.Count)
			{
			case 0:
				return null;
			case 1:
				if(!activelayers[0].ReadyForRendering())
					return null;

				if(!activelayers[0].HasDepthInfo && activelayers[0].tex_lum == null)
					return activelayers[0].tex;
				break;
			}
			foreach(ImageLayer layer in activelayers)
				if(!layer.ReadyForRendering())
					return null;

			// Get framebuffer dimensions
			int framebufferWidth = 0, framebufferHeight = 0;
			float invMaxDepth = float.MinValue; // max(pixels inside bmp_depth)
			foreach(ImageLayer layer in activelayers)
			{
				framebufferWidth = Math.Max(framebufferWidth, layer.loadedWidth);
				framebufferHeight = Math.Max(framebufferHeight, layer.loadedHeight);
				invMaxDepth = Math.Max(invMaxDepth, layer.tex_depth_maxdepth);
			}
			if(framebufferWidth == 0 || framebufferHeight == 0)
				return null; // None of the active layers has been created so far
			invMaxDepth = 1.0f / invMaxDepth;

			FramebufferCollection.FramebufferAndTime finalTexFramebuffer = FramebufferCollection.RequestFramebuffer(framebufferWidth, framebufferHeight);

			// Render activelayers using depth peeling:
			// All layers are rendered activelayers.Count-times swapping between two depth buffers while using the unused depth buffer for comparison
			// This results in each pass rendering one layer of depth
			for(int pass = 0; pass < activelayers.Count; ++pass)
				foreach(ImageLayer layer in activelayers)
				{
					bool texloaded = layer.ReadyForRendering();
					if(!texloaded)
						continue;

					if(pass != 0)
					{
						// Swap depth buffers
						GLTexture2D swp = finalTexFramebuffer.texDepth1;
						finalTexFramebuffer.texDepth1 = finalTexFramebuffer.texDepth2;
						finalTexFramebuffer.texDepth2 = swp;

						GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, finalTexFramebuffer.texDepth1.tex, 0);
					}
					
					sdr_assemble.Bind(Matrix4.CreateScale(2.0f * layer.originalAspectRatio, -2.0f, 1.0f));
					GL.Uniform1(sdr_assemble.GetUniformLocation("IsFloatTexture"), layer.isFloatImage ? 1 : 0);
					GL.Uniform1(sdr_assemble.GetUniformLocation("HasLuminance"), layer.tex_lum != null ? 1 : 0);
					GL.Uniform1(sdr_assemble.GetUniformLocation("HasLastDepthPass"), pass != 0 ? 1 : 0);
					GL.Uniform1(sdr_assemble.GetUniformLocation("InvMaxDepth"), invMaxDepth);
					Common.meshQuad2.Bind(sdr_assemble, layer.tex, layer.tex_depth, layer.tex_lum, pass != 0 ? finalTexFramebuffer.texDepth2 : null);
					Common.meshQuad2.Draw();
				}

			// Debug: Save screenshot //EDIT: Not working
			if(ImageCloud.saveAssembledImage == true)
			{
				ImageCloud.saveAssembledImage = false;
				byte[] bytes = new byte[4 * framebufferWidth * framebufferHeight];
				GL.DrawBuffer(DrawBufferMode.ColorAttachment2);
				GL.ReadPixels(0, 0, framebufferWidth, framebufferHeight, PixelFormat.Bgra, PixelType.Byte, bytes);
				for(int i = 3; i < bytes.Length; i += 4)
					bytes[i] = 0xFF;
				System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(framebufferWidth, framebufferHeight);
				System.Drawing.Imaging.BitmapData bmpdata = bmp.LockBits(new System.Drawing.Rectangle(System.Drawing.Point.Empty, bmp.Size), System.Drawing.Imaging.ImageLockMode.WriteOnly, bmp.PixelFormat);
				System.Runtime.InteropServices.Marshal.Copy(bytes, 0, bmpdata.Scan0, bytes.Length);
				bmp.UnlockBits(bmpdata);
				bmp.Save("AssembledImageScreenshot.png");
			}

			FramebufferCollection.ReturnFramebuffer(finalTexFramebuffer);

			return finalTexFramebuffer.tex;
		}

		public void Dispose()
		{
			foreach(ImageLayer layer in activelayers)
				layer.Dispose();
			activelayers.Clear();
			foreach(ImageLayer layer in inactivelayers)
				layer.Dispose();
			inactivelayers.Clear();
		}

		public void OnParameterChanged(Cinema.CinemaStore.Parameter parameter, int paramidx)
		{
			for(int i = activelayers.Count - 1; i >= 0; i--)
			{
				ImageLayer layer = activelayers[i];
				if(layer.keymask[paramidx] && !parameter.isChecked[layer.key[paramidx]])
				{
					// Deactivate layer
					activelayers.RemoveAt(i);
					inactivelayers.Add(layer);
					layer.renderPriority = 0;
				}
			}

			for(int i = inactivelayers.Count - 1; i >= 0; i--)
			{
				ImageLayer layer = inactivelayers[i];
				if(layer.keymask[paramidx] && parameter.isChecked[layer.key[paramidx]])
				{
					bool isActive = true;
					int _paramidx = 0;
					foreach(Cinema.CinemaStore.Parameter _parameter in Global.parameters)
					{
						if(layer.keymask[_paramidx] && !_parameter.isChecked[layer.key[_paramidx]])
						{
							isActive = false;
							break;
						}
						_paramidx++;
					}
					if(isActive)
					{
						// Activate layer
						inactivelayers.RemoveAt(i);
						activelayers.Add(layer);
					}
				}
			}
		}

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

		public List<ImageTransform> transforms = new List<ImageTransform>();

		private float prefetchHoldTime = 0.0f; // To avoid images to be unloaded between prefetching and rendering, prefetchHoldTime gets set to the expected render time inside PrefetchRenderPriority()

		private float visibilityFactor = 0.0f; // Percentage of pixel visible on screen (computed by the GPU)

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
			// Compute dynamic visibility
			bool visible_dynamic = visible_static;
			foreach(ImageTransform t in transforms)
				if(t.SkipImageInterval == ImageTransform.UpdateInterval.Dynamic || t.SkipImageInterval == ImageTransform.UpdateInterval.Temporal)
					visible_dynamic &= !t.SkipImage(key, this);
			return visible_dynamic;
		}
		public bool IsVisible(ImageCloud.FreeView freeview, Matrix4 invvieworient, System.Drawing.Size backbuffersize, out Matrix4 transform)
		{
			// Compute dynamic visibility and check if dynamic location updates are required
			bool visible_dynamic = visible_static;
			bool hasDynamicLocationTransform = locationInvalid;
			foreach(ImageTransform t in transforms)
			{
				if(t.SkipImageInterval == ImageTransform.UpdateInterval.Dynamic || t.SkipImageInterval == ImageTransform.UpdateInterval.Temporal)
					visible_dynamic &= !t.SkipImage(key, this);
				if(t.locationTransformInterval == ImageTransform.UpdateInterval.Dynamic || t.locationTransformInterval == ImageTransform.UpdateInterval.Temporal)
					hasDynamicLocationTransform = true;
			}

			//foreach(ImageLayer layer in activelayers)
			//	layer.RemoveIfUnloaded();

			transform = invview; //Matrix4.Identity
			if(visible_dynamic)
			{
				if(hasDynamicLocationTransform)
					ComputeLocation();

				//transform *= Matrix4.CreateTranslation(-0.5f, -0.5f, 0.0f);
				transform *= Matrix4.CreateScale(originalAspectRatio, 1.0f, 1.0f);
				//transform *= Matrix4.CreateScale(2.0f, 2.0f, 1.0f);
				//if(depth_filename == null) // Do not always face screen when rendering volume images
					transform *= invvieworient;
				transform *= Matrix4.CreateTranslation(animatedPos);

				if(freeview.DoFrustumCulling(transform, Matrix4.Identity, Matrix4.Identity, new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.5f, 0.5f, 0.5f)))
				{
					transform *= freeview.viewprojmatrix;

					// Set render priority and dimensions (thread safety: priority has to be set after width/height)
					Vector3 vsize = Vector3.TransformPerspective(new Vector3(0.5f, 0.5f, 0.0f), transform) - Vector3.TransformPerspective(new Vector3(0.0f, 0.0f, 0.0f), transform); // Size of image in device units
					int renderWidth = Math.Max(1, (int)(vsize.X * (float)backbuffersize.Width));
					int renderHeight = Math.Max(1, (int)(vsize.Y * (float)backbuffersize.Height));
					float renderPriority = visibilityFactor * (this.selected ? 2000 : 1000);
					foreach(ImageLayer activelayer in activelayers)
					{
						activelayer.renderWidth = renderWidth;
						activelayer.renderHeight = renderHeight;

						//if(activelayer.renderPriority == 0)
						activelayer.renderPriority = renderPriority;
					}

					return true;
				}
				else if(Global.time > prefetchHoldTime)
					foreach(ImageLayer activelayer in activelayers)
						activelayer.renderPriority = 0;

				return false;
			}
			else
			{
				if(Global.time > prefetchHoldTime)
					foreach(ImageLayer activelayer in activelayers)
						activelayer.renderPriority = 0;
				return false;
			}
		}
		public void PrefetchRenderPriority(ImageCloud.FreeView freeview, Matrix4 invvieworient, System.Drawing.Size backbuffersize)
		{
			// Compute dynamic visibility and check if dynamic location updates are required
			bool visible_dynamic = visible_static;
			bool hasTemporalSkipTransform = false, hasTemporalLocationTransform = false;
			foreach(ImageTransform t in this.transforms)
			{
				if(t.SkipImageInterval == ImageTransform.UpdateInterval.Dynamic || t.SkipImageInterval == ImageTransform.UpdateInterval.Temporal)
				{
					if(t.SkipImageInterval == ImageTransform.UpdateInterval.Temporal)
						hasTemporalSkipTransform = true;
					visible_dynamic &= !t.SkipImage(key, this);
				}
				//if(t.locationTransformInterval == ImageTransform.UpdateInterval.Temporal) //EDIT: Compute location locally
				//	hasTemporalLocationTransform = true; //EDIT: Compute location locally
			}

			if(!hasTemporalSkipTransform && !hasTemporalLocationTransform) // If neither location nor skipping is time variant
				return; // Don't prefetch

			Matrix4 transform = invview; //Matrix4.Identity
			if(visible_dynamic)
			{
				//if(hasTemporalLocationTransform) //EDIT: Compute location locally
				//	ComputeLocation(); //EDIT: Compute location locally

				//transform *= Matrix4.CreateTranslation(-0.5f, -0.5f, 0.0f);
				transform *= Matrix4.CreateScale(originalAspectRatio, 1.0f, 1.0f);
				//transform *= Matrix4.CreateScale(2.0f, 2.0f, 1.0f);
				//if(depth_filename == null) // Do not always face screen when rendering volume images
					transform *= invvieworient;
				transform *= Matrix4.CreateTranslation(animatedPos);

				if(freeview.DoFrustumCulling(transform, Matrix4.Identity, Matrix4.Identity, new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.5f, 0.5f, 0.5f)))
				{
					transform *= freeview.viewprojmatrix;

					// Set render priority and dimensions (thread safety: priority has to be set after width/height)
					Vector3 vsize = Vector3.TransformPerspective(new Vector3(0.5f, 0.5f, 0.0f), transform) - Vector3.TransformPerspective(new Vector3(0.0f, 0.0f, 0.0f), transform); // Size of image in device units
					int renderWidth = Math.Max(1, (int)(vsize.X * (float)backbuffersize.Width));
					int renderHeight = Math.Max(1, (int)(vsize.Y * (float)backbuffersize.Height));
					float renderPriority = 1200; // Prefetch renderPriority should be higher than render renderPriority to prefere images being loaded before they are being rendered
					foreach(ImageLayer activelayer in activelayers)
					{
						activelayer.renderWidth = renderWidth;
						activelayer.renderHeight = renderHeight;
						activelayer.renderPriority = renderPriority;
					}
					prefetchHoldTime = Global.time;
				}
			}
		}
		public void Render(GLMesh mesh, ImageCloud.RenderShader sdr_default, ImageCloud.RenderShader sdr_float, ImageCloud.RenderShader sdr_assemble, float invNumBackbufferPixels, float depthscale, ImageCloud.FreeView freeview, Matrix4 transform, int fragmentcounter)
		{
			foreach(ImageLayer layer in activelayers)
				layer.ReadyForRendering();

			GLTexture2D tex;
			GLTexture2D tex_lum = null;
			ImageCloud.RenderShader sdr;
			/*switch(activelayers.Count)
			{
			case 0: // If no layers are active
				// Render blank
				tex = null;
				sdr = sdr_default;
				break;

			case 1: // If one layer is active
				if(!activelayers[0].ReadyForRendering()) // If the layer isn't ready
				{
					// Render blank
					tex = null;
					sdr = sdr_default;
					break;
				}

				// Render single image with luminance (if not null) and a shader depending on image content (float or default)
				tex = activelayers[0].tex;
				tex_lum = activelayers[0].tex_lum;
				sdr = activelayers[0].isFloatImage ? sdr_float : sdr_default;
				break;

			default: // If more than one layer is active
				// Render assembled
				tex = AssembleImage(sdr_assemble);
				sdr = sdr_default;
				break;
			}*/
			if(activelayers.Count == 0) // If no layers are active
			{
				// Render blank
				tex = null;
				sdr = sdr_default;
			}
			else if(activelayers.Count + inactivelayers.Count > 1) // If image is multiple layer image
			{
				// Render assembled
				tex = AssembleImage(sdr_assemble);
				sdr = sdr_default;
			}
			else if(!activelayers[0].ReadyForRendering()) // If image is single layer image, but not ready for rendering
			{
				// Render blank
				tex = null;
				sdr = sdr_default;
			}
			else // If image is single layer image and ready for rendering
			{
				// Render single image with luminance (if not null) and a shader depending on image content (float or default)
				tex = activelayers[0].tex;
				tex_lum = activelayers[0].tex_lum;
				sdr = activelayers[0].isFloatImage ? sdr_float : sdr_default;
			}

			Color4 _clr = new Color4(1.0f, 1.0f, 1.0f, 1.0f);
			sdr.Bind(transform: transform, clr: _clr, texloaded: tex != null, haslum: tex_lum != null, depthscale: depthscale);
			mesh.Bind(sdr, tex, tex_lum);

			GL.BeginQuery(QueryTarget.SamplesPassed, fragmentcounter);
			mesh.Draw();
			GL.EndQuery(QueryTarget.SamplesPassed);

			int numfragments;
			GL.GetQueryObject(fragmentcounter, GetQueryObjectParam.QueryResult, out numfragments);
			visibilityFactor = (float)numfragments * invNumBackbufferPixels;

			foreach(ImageTransform t in transforms)
				t.RenderImage(key, this, freeview);
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

		public float CastRay(Vector3 from, Vector3 dir, Matrix4 invvieworient, out Vector2 uv)
		{
			//if(tex == null)
			//	return float.MaxValue;

			// Compute dynamic visibility
			bool visible_dynamic = visible_static;
			foreach(ImageTransform t in transforms)
				if(t.SkipImageInterval == ImageTransform.UpdateInterval.Dynamic || t.SkipImageInterval == ImageTransform.UpdateInterval.Temporal)
					visible_dynamic &= !t.SkipImage(key, this);
			if(!visible_dynamic)
			{
				uv = Vector2.Zero;
				return float.MaxValue;
			}

			Matrix4 invworldmatrix = GetWorldMatrix(invvieworient);
			invworldmatrix.Invert();
			from = Vector3.TransformPerspective(from, invworldmatrix); //TransformPosition
			if(from.Z < 0.0f)
			{
				uv = Vector2.Zero;
				return float.MaxValue;
			}
			dir = Vector3.TransformNormal(dir, invworldmatrix);

			Vector3 dest = from - dir * (from.Z / dir.Z);
			uv = new Vector2(0.5f + dest.X / originalAspectRatio, 0.5f - dest.Y);

			//System.Windows.Forms.MessageBox.Show(dest.ToString());

			//float halfwidth = 0.5f * originalAspectRatio, halfheight = 0.5f;
			//return -halfwidth < dest.X && dest.X < halfwidth && -halfheight < dest.Y && dest.Y < halfheight ? from.Z : float.MaxValue;
			return 0.0f < uv.X && uv.X < 1.0f && 0.0f < uv.Y && uv.Y < 1.0f ? from.Z : float.MaxValue;
		}
		public Vector2 GetIntersectionUV(Vector3 from, Vector3 dir, Matrix4 invvieworient)
		{
			Matrix4 invworldmatrix = GetWorldMatrix(invvieworient);
			invworldmatrix.Invert();
			from = Vector3.TransformPerspective(from, invworldmatrix); //TransformPosition
			dir = Vector3.TransformNormal(dir, invworldmatrix);

			Vector3 dest = from - dir * (from.Z / dir.Z);
			return new Vector2(0.5f + dest.X / originalAspectRatio, 0.5f - dest.Y);
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

			aabb.Include(Vector3.TransformPosition(new Vector3(-0.5f * originalAspectRatio, -0.5f, -0.5f * originalAspectRatio), worldmatrix));
			aabb.Include(Vector3.TransformPosition(new Vector3( 0.5f * originalAspectRatio,  0.5f,  0.5f * originalAspectRatio), worldmatrix));

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

