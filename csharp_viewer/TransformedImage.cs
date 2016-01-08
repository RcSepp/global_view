using System;
using System.Collections.Generic;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace csharp_viewer
{
	public class TransformedImage : Cinema.CinemaImage
	{
		private static class IMAGE_ASSEMBLY_SHADER
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
				varying vec2 uv;
				uniform sampler2D Texture, Texture2, Texture3;
				uniform int HasLuminance;

				//vec4 shade(sampler2D sampler, in vec2 uv);

				void main()
				{
					vec4 color = vec4(texture2D(Texture, uv));
					float depth = texture2D(Texture2, uv).r / 512.0; //EDIT: Set with a uniform representing global max depth
					vec4 lum = vec4(texture2D(Texture3, uv));

					if(depth > gl_FragCoord.z)
						discard;
					
					gl_FragData[0] = color;
					gl_FragData[1] = vec4(0.0, 0.0, 0.0, 1.0);
					gl_FragData[2] = HasLuminance != 0 ? vec4(lum.rgb, 1.0) : vec4(1.0, 1.0, 1.0, 1.0);
					gl_FragDepth = depth;
				}
			";
		}

		public class ImageLayer
		{
			public TransformedImage image;
			public ImageLayer(TransformedImage image)
			{
				this.image = image;
			}

			public string filename;
			public string depth_filename, lum_filename;

			public int[] key;
			public Cinema.CinemaStore.Parameter[] parameters;
			public int[] globalparamindices;

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

					image.FreeAssembledImage();
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
						{
							tex = new GLTexture2D(bmp, false, !isFloatImage);
							image.FreeAssembledImage();
						}
						if(bmp_depth != null && tex_depth == null)
						{
							tex_depth = new GLTexture2D(bmp_depth, false, false, PixelFormat.Red, PixelInternalFormat.R32f, PixelType.Float);
							image.FreeAssembledImage();
						}
						if(bmp_lum != null && tex_lum == null)
						{
							tex_lum = new GLTexture2D(bmp_lum, false, true);
							image.FreeAssembledImage();
						}
						renderMutex.ReleaseMutex();
						return true;
					}

					renderMutex.ReleaseMutex();
					return false;
				}

				return false;
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
				if(!parameter.isChecked[newlayer.key[paramidx++]])
				{
					isActive = false;
					break;
				}
			if(isActive)
				activelayers.Add(newlayer);
			else
				inactivelayers.Add(newlayer);
		}

		public static class FramebufferCollection
		{
			private const int MAX_NUMFRAMEBUFFERS = 16;

			private class FramebufferAndTime
			{
				public int framebuffer;
				public DateTime lastAccessTime;
			}
			private static Dictionary<System.Drawing.Size, FramebufferAndTime> framebuffers = new Dictionary<System.Drawing.Size, FramebufferAndTime>();

			public static int RequestFramebuffer(GLTexture2D tex0, GLTexture2D tex1, GLTexture2D tex2, GLTexture2D texDepth)
			{
				System.Drawing.Size framebufferSize = new System.Drawing.Size(tex0.width, tex0.height);

				FramebufferAndTime fb_t;
				if(!framebuffers.TryGetValue(framebufferSize, out fb_t))
				{
					framebuffers.Add(framebufferSize, fb_t = new FramebufferAndTime());
					fb_t.framebuffer = GL.GenFramebuffer();
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
						framebuffers.Remove(oldestPair.Key);
					}
				}
				else
					fb_t.lastAccessTime = DateTime.Now;

				GL.BindFramebuffer(FramebufferTarget.Framebuffer, fb_t.framebuffer);

				GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, tex0.tex, 0);
				GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, tex1.tex, 0);
				GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment2, TextureTarget.Texture2D, tex2.tex, 0);
				GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, texDepth.tex, 0);
				//FramebufferErrorCode ferr;
				//if((ferr = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer)) != FramebufferErrorCode.FramebufferComplete)
				//	throw new Exception(ferr.ToString());

				GL.PushAttrib(AttribMask.ViewportBit);
				GL.Enable(EnableCap.DepthTest);
				GL.Disable(EnableCap.DepthClamp);
				GL.DepthFunc(DepthFunction.Lequal);
				GL.BlendEquation(BlendEquationMode.FuncAdd);
				GL.DrawBuffers(3, new DrawBuffersEnum[] {DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1, DrawBuffersEnum.ColorAttachment2});
				GL.ClearColor(1.0f, 0.0f, 0.0f, 1.0f);
				GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
				GL.Viewport(0, 0, framebufferSize.Width, framebufferSize.Height);

				return fb_t.framebuffer;
			}
			public static void ReturnFramebuffer(int framebuffer)
			{
				GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
				//GL.DeleteFramebuffer(framebuffer);
			}
		}

		public GLTexture2D finalTex, finalTexFloat, finalTexLum;
		public bool finalTexIsAssembled, finalTexHasDefaultComponent, finalTexHasFloatComponent;
		private static GLShader sdrImageAssembly;
		public bool AssembleImage()
		{
			if(finalTex != null)
				return false; // Texture already assembled

			switch(activelayers.Count)
			{
			case 0:
				finalTexIsAssembled = false;
				finalTexHasDefaultComponent = false;
				finalTexHasFloatComponent = false;
				finalTex = null;
				finalTexFloat = null;
				finalTexLum = null;
				return false;
			case 1:
				finalTexIsAssembled = false;
				finalTexHasDefaultComponent = !activelayers[0].isFloatImage;
				finalTexHasFloatComponent = activelayers[0].isFloatImage;
				finalTex = activelayers[0].tex;
				finalTexFloat = activelayers[0].tex;
				finalTexLum = activelayers[0].tex_lum;
				return false;
			}
			finalTexIsAssembled = true;

			if(sdrImageAssembly == null)
				sdrImageAssembly = new GLShader(new string[] {IMAGE_ASSEMBLY_SHADER.VS}, new string[] {IMAGE_ASSEMBLY_SHADER.FS});

			// Get framebuffer dimensions
			int framebufferWidth = 0, framebufferHeight = 0;
			foreach(ImageLayer layer in activelayers)
			{
				framebufferWidth = Math.Max(framebufferWidth, layer.renderWidth);
				framebufferHeight = Math.Max(framebufferHeight, layer.renderWidth);
			}
			if(framebufferWidth == 0 || framebufferHeight == 0)
				return false; // None of the active layers has been created so far

			// Create and activate rendertexture, depthtexture and framebuffer
			finalTex = new GLTexture2D(framebufferWidth, framebufferHeight, false, PixelFormat.Rgba, PixelInternalFormat.Rgba, PixelType.Byte, linearfilter:true);
			finalTexFloat = new GLTexture2D(framebufferWidth, framebufferHeight, false, PixelFormat.Rgba, PixelInternalFormat.Rgba, PixelType.Byte, linearfilter:false);
			finalTexLum = new GLTexture2D(framebufferWidth, framebufferHeight, false, PixelFormat.Rgba, PixelInternalFormat.Rgba, PixelType.Byte, linearfilter:true);
			GLTexture2D finalTexDepth = new GLTexture2D(framebufferWidth, framebufferHeight, false, PixelFormat.DepthComponent, PixelInternalFormat.DepthComponent, PixelType.Float);

			int finalTexFramebuffer = FramebufferCollection.RequestFramebuffer(finalTex, finalTexFloat, finalTexLum, finalTexDepth);

			// Render activelayers 
			finalTexHasDefaultComponent = finalTexHasFloatComponent = false;
			foreach(ImageLayer layer in activelayers)
			{
				bool texloaded = layer.CreateIfLoaded();
				if(!texloaded)
					continue;

				if(layer.isFloatImage)
				{
					finalTexHasFloatComponent = true;
					GL.DrawBuffers(3, new DrawBuffersEnum[] {
						DrawBuffersEnum.ColorAttachment1,
						DrawBuffersEnum.ColorAttachment0,
						DrawBuffersEnum.ColorAttachment2
					});
				}
				else
				{
					finalTexHasDefaultComponent = true;
					GL.DrawBuffers(3, new DrawBuffersEnum[] {
						DrawBuffersEnum.ColorAttachment0,
						DrawBuffersEnum.ColorAttachment1,
						DrawBuffersEnum.ColorAttachment2
					});
				}
				
				sdrImageAssembly.Bind(Matrix4.CreateScale(2.0f * layer.originalAspectRatio, -2.0f, 1.0f));
				GL.Uniform1(sdrImageAssembly.GetUniformLocation("HasLuminance"), layer.tex_lum != null ? 1 : 0);
				Common.meshQuad2.Bind(sdrImageAssembly, layer.tex, layer.tex_depth, layer.tex_lum);
				Common.meshQuad2.Draw();
			}

			// Debug: Save screenshot //EDIT: Not working
			if(ImageCloud.saveAssembledImage == true)
			{
				ImageCloud.saveAssembledImage = false;
				byte[] bytes = new byte[4 * finalTex.width * finalTex.height];
				GL.DrawBuffer(DrawBufferMode.ColorAttachment2);
				GL.ReadPixels(0, 0, finalTex.width, finalTex.height, PixelFormat.Bgra, PixelType.Byte, bytes);
				for(int i = 3; i < bytes.Length; i += 4)
					bytes[i] = 0xFF;
				System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(finalTex.width, finalTex.height);
				System.Drawing.Imaging.BitmapData bmpdata = bmp.LockBits(new System.Drawing.Rectangle(System.Drawing.Point.Empty, bmp.Size), System.Drawing.Imaging.ImageLockMode.WriteOnly, bmp.PixelFormat);
				System.Runtime.InteropServices.Marshal.Copy(bytes, 0, bmpdata.Scan0, bytes.Length);
				bmp.UnlockBits(bmpdata);
				bmp.Save("AssembledImageScreenshot.png");
			}

			// Deactivate and remove depthtexture and framebuffer
			FramebufferCollection.ReturnFramebuffer(finalTexFramebuffer);
			GL.DeleteTexture(finalTexDepth.tex);
			finalTexDepth = null;

			// Restore old framebuffer state
			GL.ClearColor(0.0f, 0.1f, 0.3f, 1.0f);
			GL.PopAttrib();

			return true;
		}
		public void FreeAssembledImage()
		{
			if(finalTex != null)
			{
				if(finalTexIsAssembled)
				{
					GL.DeleteTexture(finalTex.tex);
					GL.DeleteTexture(finalTexFloat.tex);
					GL.DeleteTexture(finalTexLum.tex);
				}
				finalTex = finalTexFloat = finalTexLum = null;
			}
		}

		public void OnParameterChanged(Cinema.CinemaStore.Parameter parameter, int paramidx)
		{
			for(int i = activelayers.Count - 1; i >= 0; i--)
			{
				ImageLayer layer = activelayers[i];
				if(!parameter.isChecked[layer.key[paramidx]])
				{
					// Deactivate layer
					activelayers.RemoveAt(i);
					inactivelayers.Add(layer);
					FreeAssembledImage();
					layer.renderPriority = 0;
				}
			}

			for(int i = inactivelayers.Count - 1; i >= 0; i--)
			{
				ImageLayer layer = inactivelayers[i];
				if(parameter.isChecked[layer.key[paramidx]])
				{
					bool isActive = true;
					int _paramidx = 0;
					foreach(Cinema.CinemaStore.Parameter _parameter in Global.parameters)
						if(!_parameter.isChecked[layer.key[_paramidx++]])
						{
							isActive = false;
							break;
						}
					if(isActive)
					{
						// Activate layer
						inactivelayers.RemoveAt(i);
						activelayers.Add(layer);
						FreeAssembledImage();
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

			foreach(ImageLayer layer in activelayers)
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

			transforms = new Matrix4[activelayers.Count];
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
					transforms[i] *= Matrix4.CreateScale(activelayers[i].originalAspectRatio, 1.0f, 1.0f);
					//transforms[i] *= Matrix4.CreateScale(2.0f, 2.0f, 1.0f);
					//if(depth_filename == null) // Do not always face screen when rendering volume images
						transforms[i] *= invvieworient;
					transforms[i] *= Matrix4.CreateTranslation(animatedPos);

					if(freeview.DoFrustumCulling(transforms[i], Matrix4.Identity, Matrix4.Identity, new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.5f, 0.5f, 0.5f)))
					{
						transforms[i] *= freeview.viewprojmatrix;

						// Set render priority and dimensions (thread safety: priority has to be set after width/height)
						Vector3 vsize = Vector3.TransformPerspective(new Vector3(0.5f, 0.5f, 0.0f), transforms[i]) - Vector3.TransformPerspective(new Vector3(0.0f, 0.0f, 0.0f), transforms[i]); // Size of image in device units
						activelayers[i].renderWidth = Math.Max(1, (int)(vsize.X * (float)backbuffersize.Width));
						activelayers[i].renderHeight = Math.Max(1, (int)(vsize.Y * (float)backbuffersize.Height));

						//if(activelayers[i].renderPriority == 0)
							activelayers[i].renderPriority = 1000;

						inside_frustum = true;
					}
					else if(Global.time > prefetchHoldTime)
						activelayers[i].renderPriority = 0;
				}
				return inside_frustum;
			}
			else
			{
				if(Global.time > prefetchHoldTime)
					foreach(ImageLayer layer in activelayers)
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

			Matrix4[] transforms = new Matrix4[activelayers.Count];
			for(int i = 0; i < transforms.Length; ++i)
				transforms[i] = invview; //Matrix4.Identity
			if(visible_dynamic)
			{
				//if(hasDynamicLocationTransform) //EDIT: Compute location locally
				//	ComputeLocation(); //EDIT: Compute location locally

				for(int i = 0; i < transforms.Length; ++i)
				{
					//transforms[i] *= Matrix4.CreateTranslation(-0.5f, -0.5f, 0.0f);
					transforms[i] *= Matrix4.CreateScale(activelayers[i].originalAspectRatio, 1.0f, 1.0f);
					//transforms[i] *= Matrix4.CreateScale(2.0f, 2.0f, 1.0f);
					//if(depth_filename == null) // Do not always face screen when rendering volume images
						transforms[i] *= invvieworient;
					transforms[i] *= Matrix4.CreateTranslation(animatedPos);

					if(freeview.DoFrustumCulling(transforms[i], Matrix4.Identity, Matrix4.Identity, new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.5f, 0.5f, 0.5f)))
					{
						transforms[i] *= freeview.viewprojmatrix;

						// Set render priority and dimensions (thread safety: priority has to be set after width/height)
						Vector3 vsize = Vector3.TransformPerspective(new Vector3(0.5f, 0.5f, 0.0f), transforms[i]) - Vector3.TransformPerspective(new Vector3(0.0f, 0.0f, 0.0f), transforms[i]); // Size of image in device units
						activelayers[i].renderWidth = Math.Max(1, (int)(vsize.X * (float)backbuffersize.Width));
						activelayers[i].renderHeight = Math.Max(1, (int)(vsize.Y * (float)backbuffersize.Height));
						activelayers[i].renderPriority = 1200; // Prefetch renderPriority should be higher than render renderPriority to prefere images being loaded before they are being rendered
						prefetchHoldTime = Global.time;
					}
				}
			}
		}
		public void Render(GLMesh mesh, ImageCloud.RenderShader sdr_default, ImageCloud.RenderShader sdr_float, ImageCloud.RenderShader sdr_assembled, Vector2 invbackbuffersize, float depthscale, ImageCloud.FreeView freeview, Matrix4[] transforms, int fragmentcounter, GLTexture2D texdepth = null)
		{
			AssembleImage();
			if(finalTex != null)
			{
				bool _texloaded = true;
				ImageCloud.RenderShader _sdr = sdr_assembled;
				Color4 _clr = new Color4(1.0f, 1.0f, 1.0f, 1.0f);
				_sdr.Bind(transforms[0], _clr, _texloaded, false, true, invbackbuffersize, depthscale);
				GL.Uniform1(_sdr.GetUniformLocation("HasDefaultComponent"), finalTexHasDefaultComponent ? 1 : 0);
				GL.Uniform1(_sdr.GetUniformLocation("HasFloatComponent"), finalTexHasFloatComponent ? 1 : 0);
				mesh.Bind(_sdr, finalTex, finalTexFloat, finalTexLum);
				mesh.Draw();
			}
			return;

			for(int i = 0; i < transforms.Length; ++i)
			{
				ImageLayer layer = activelayers[i];
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
				ImageLayer layer = activelayers[i];
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

			float halfwidth = 0.5f * FirstLayer.originalAspectRatio, halfheight = 0.5f;
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

			aabb.Include(Vector3.TransformPosition(new Vector3(-0.5f * FirstLayer.originalAspectRatio, -0.5f, -0.5f * FirstLayer.originalAspectRatio), worldmatrix));
			aabb.Include(Vector3.TransformPosition(new Vector3( 0.5f * FirstLayer.originalAspectRatio,  0.5f,  0.5f * FirstLayer.originalAspectRatio), worldmatrix));

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

