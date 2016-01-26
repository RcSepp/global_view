//#define DEBUG_GLTEXTURESTREAM
#define ENABLE_TRADEOFF // Tradeoff scales image size by relative remaining memory // Warning: Tradeoff doesn't work with prefetching!
//#define ENABLE_SIZE_PRIORISATION // Include factor 'originalWidth / width;' into priorisation

using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Collections.Generic;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace csharp_viewer
{
	public class GLTextureStream
	{
		public struct ImageMetaData
		{
			public string name, strValue;
			public float value;
			public ImageMetaData(string name, float value, string strValue)
			{
				this.name = name;
				this.value = value;
				this.strValue = strValue;
			}
		}
		public delegate bool ReadImageMetaDataDelegate(TransformedImage image, ImageMetaData[] meta);

		private Bitmap bmpFileNotFound;
		private GLTexture2D texFileNotFound;

		private int currentmemorysize, memorysizelimit;

		public class ImageReference
		{
			public AsyncImageLoader.Image image = null;

			public virtual void OnTextureLoaded() {}
			public virtual void OnTextureUnloaded() {}
			public virtual void OnOrignalDimensionsUpdated() {}

			public readonly string filename, depth_filename, lum_filename;
			public readonly bool isFloatImage;
			public ImageReference(string filename, string depth_filename = null, string lum_filename = null, bool isFloatImage = false)
			{
				this.filename = filename;
				this.depth_filename = depth_filename;
				this.lum_filename = lum_filename;
				this.isFloatImage = isFloatImage;
			}

			private float _renderPriority;
			public float renderPriority
			{
				get {
					return _renderPriority;
				}
				set {
					if(value < _renderPriority)
					{
						_renderPriority = value;
						if(image != null)
							image.OnRenderPriorityDecreased();
					}
					else if(value > _renderPriority)
					{
						_renderPriority = value;
						if(image != null)
							image.OnRenderPriorityIncreased(value);
					}
				}
			}

			private int _renderWidth;
			public int renderWidth
			{
				get {
					return _renderWidth;
				}
				set {
					if(value < _renderWidth)
					{
						_renderWidth = value;
						if(image != null)
							image.OnRenderWidthDecreased();
					}
					else if(value > _renderWidth)
					{
						_renderWidth = value;
						if(image != null)
							image.OnRenderWidthIncreased(value);
					}
				}
			}

			private int _renderHeight;
			public int renderHeight
			{
				get {
					return _renderHeight;
				}
				set {
					if(value < _renderHeight)
					{
						_renderHeight = value;
						if(image != null)
							image.OnRenderHeightDecreased();
					}
					else if(value > _renderHeight)
					{
						_renderHeight = value;
						if(image != null)
							image.OnRenderHeightIncreased(value);
					}
				}
			}

			public GLTexture2D tex { get { return image.tex; } }
			public GLTexture2D tex_depth { get { return image.tex_depth; } }
			public float tex_depth_maxdepth { get { return image.bmp_depth != null ? image.bmp_depth.maxDepth : 0.0f; } }
			public GLTexture2D tex_lum { get { return image.tex_lum; } }
			public int originalWidth { get { return image != null ? image.originalWidth : 0; } }
			public int originalHeight { get { return image != null ? image.originalHeight : 0; } }
			public float originalAspectRatio { get { return image != null ? image.originalAspectRatio : 1.0f; } }
			public int loadedWidth { get { return image != null && image.bmp != null ? image.bmp.Width : 0; } }
			public int loadedHeight { get { return image != null && image.bmp != null ? image.bmp.Height : 0; } }

			public void TriggerReload()
			{
				if(image != null)
					image.TriggerReload();
			}

			public bool ReadyForRendering()
			{
				return image != null ? image.ReadyForRendering() : false;
			}

			public void Dispose()
			{
				if(image != null)
					image.RemoveReference(this);
			}
		}

		public class AsyncImageLoader
		{
			const float MIN_TRADEOFF_FACTOR = 0.2f; // Minimum size still considered worth rendering [% of render width]

			public class Image : IComparable<Image> //EDIT: Make private
			{
				public readonly string filename, depth_filename, lum_filename;
				public readonly bool isFloatImage;
				public Image(string filename, string depth_filename = null, string lum_filename = null, bool isFloatImage = false)
				{
					this.filename = filename;
					this.depth_filename = depth_filename;
					this.lum_filename = lum_filename;
					this.isFloatImage = isFloatImage;

					#if ENABLE_SIZE_PRIORISATION
					sizePriority = 1.0f;
					#endif
					memory = 0;
					metadataLoaded = false;
				}

				public System.Threading.Mutex renderMutex = new System.Threading.Mutex();
				private float renderPriority = 0.0f; // == Maximum of render priorities of all references
				public int renderWidth = 0, renderHeight = 0; // == Maximum of render dimensions of all references
				private IBitmap oldbmp = null;
				public void OnRenderPriorityDecreased()
				{
					renderPriority = 0.0f;
					foreach(ImageReference reference in references)
						renderPriority = Math.Max(reference.renderPriority, renderPriority);
					if(renderPriority > 0.0f)
						return;

					if(tex != null && !texIsStatic && (bmp == null || bmp != oldbmp)) // If a texture is loaded and either the image has been unloaded or changed
					{
						// Unload texture
						tex.Dispose();
						tex = null;

						if(tex_depth != null)
						{
							tex_depth.Dispose();
							tex_depth = null;
						}

						if(tex_lum != null)
						{
							tex_lum.Dispose();
							tex_lum = null;
						}

						foreach(ImageReference reference in references)
							reference.OnTextureUnloaded();
					}
					oldbmp = bmp;
				}
				public void OnRenderWidthDecreased()
				{
					renderWidth = 0;
					foreach(ImageReference reference in references)
						renderWidth = Math.Max(reference.renderWidth, renderWidth);
				}
				public void OnRenderHeightDecreased()
				{
					renderHeight = 0;
					foreach(ImageReference reference in references)
						renderHeight = Math.Max(reference.renderHeight, renderHeight);
				}

				public void OnRenderPriorityIncreased(float newRenderPriority)
				{
					renderPriority = Math.Max(renderPriority, newRenderPriority);
					if(renderPriority > 0.0f)
						return;
				}
				public void OnRenderWidthIncreased(int newRenderWidth)
				{
					renderWidth = Math.Max(renderWidth, newRenderWidth);
				}
				public void OnRenderHeightIncreased(int newRenderHeight)
				{
					renderHeight = Math.Max(renderHeight, newRenderHeight);
				}

				public void TriggerReload()
				{
					if (tex != null && !texIsStatic)
					{
						tex.Dispose();
						tex = null;

						if (tex_depth != null)
						{
							tex_depth.Dispose();
							tex_depth = null;
						}

						if (tex_lum != null)
						{
							tex_lum.Dispose();
							tex_lum = null;
						}

						foreach(ImageReference reference in references)
							reference.OnTextureUnloaded();
					}
				}

				public bool ReadyForRendering()
				{
					if(texIsStatic)
						return true; // Texture static (doesn't get loaded or unloaded)
					if(bmp == null)
						return false; // No image loaded
					if(tex != null && bmp == oldbmp)
						return true; // Texture loaded and unchanged

					if(renderMutex.WaitOne(0))
					{
						if(bmp == null) // Repeate check with locked mutex
						{
							renderMutex.ReleaseMutex();
							return false; // No image loaded
						}

						if(tex == null || bmp != oldbmp) // Repeate check with locked mutex
						{
							tex = new GLTexture2D("layer_tex", bmp, false, !isFloatImage);
							oldbmp = bmp;

							if(bmp_depth != null)
								tex_depth = new GLTexture2D("layer_depthtex", bmp_depth, false, false, OpenTK.Graphics.OpenGL.PixelFormat.Red, PixelInternalFormat.R32f, PixelType.Float);
							if(bmp_lum != null)
								tex_lum = new GLTexture2D("layer_lumtex", bmp_lum, false, true);

							foreach(ImageReference reference in references)
								reference.OnTextureLoaded();
						}
						renderMutex.ReleaseMutex();
						return true; // Texture loaded
					}

					return false; // Mutex could not be aquired
				}


				public List<ImageReference> references = new List<ImageReference>();
				public void AddReference(ImageReference reference)
				{
					references.Add(reference);
					reference.image = this;
					OnRenderPriorityIncreased(renderPriority);
					OnRenderWidthIncreased(renderWidth);
					OnRenderHeightIncreased(renderHeight);
				}
				public void RemoveReference(ImageReference reference)
				{
					references.Remove(reference);
					reference.image = null;
					if(references.Count != 0)
						return;

					if(tex != null)
					{
						tex.Dispose();
						tex = null;
					}

					if(tex_depth != null)
					{
						tex_depth.Dispose();
						tex_depth = null;
					}

					if(tex_lum != null)
					{
						tex_lum.Dispose();
						tex_lum = null;
					}
				}

				public IBitmap bmp = null, bmp_depth = null, bmp_lum = null;
				public GLTexture2D tex = null, tex_depth = null, tex_lum = null;
				private bool texIsStatic = false;
				public int originalWidth = 0, originalHeight = 0;
				public float originalAspectRatio = 1.0f;


				//public TransformedImage.ImageLayer image; //EDIT: Make private
				private int width, height;
				#if ENABLE_SIZE_PRIORISATION
				public float sizePriority; //EDIT: Make private
				#endif
				public int memory; //EDIT: Make private
				private bool metadataLoaded;

				/*public Image(TransformedImage.ImageLayer image)
				{
					this.image = image;
					#if ENABLE_SIZE_PRIORISATION
					sizePriority = 1.0f;
					#endif
					memory = 0;
					metadataLoaded = false;
				}*/

				public bool renderPriorityIsZero() { return renderPriority <= 0; }

				public int RequiredMemory(float tradeoffRenderSizeFactor, bool forceOriginalSize)
				{
					// Returns (guessed) memory required for Load()

					if(renderPriority <= 0 || texIsStatic) // If image doesn't need to be loaded
					{
						return -memory; // Return memory gain achieved by unloading this image
					}

					int factor = 1 + (depth_filename != null ? 1 : 0) + (lum_filename != null ? 1 : 0);

					int adjustedRenderWidth = (int)((float)renderWidth * tradeoffRenderSizeFactor);
					int adjustedRenderHeight = (int)((float)renderHeight * tradeoffRenderSizeFactor);

					if(memory > 0) // If image already loaded
					{
						// Recompute image size
						int newwidth, newheight;
						if (forceOriginalSize || adjustedRenderWidth >= originalWidth || adjustedRenderHeight >= originalHeight)
						{
							newwidth = originalWidth;
							newheight = originalHeight;
						}
						else
						{
							float fw = (float)adjustedRenderWidth / (float)originalWidth;
							float fh = (float)adjustedRenderHeight / (float)originalHeight;

							if(fw > fh)
							{
								newwidth = adjustedRenderWidth;
								newheight = (int)((float)originalHeight * fw);
							}
							else
							{
								newheight = adjustedRenderHeight;
								newwidth = (int)((float)originalWidth * fh);
							}
						}

						// Compare magnitude of change
						float size_diff_factor = (float)newwidth / (float)this.width;
						if(size_diff_factor > 2.0f || size_diff_factor < 0.5f)
							return newwidth * newheight * factor - memory; // Return memory gained or lost by resolution change

						return 0; // Image considered unchanged. Return no extra memory required
					}

					if(originalWidth == 0) // If original size is unknown (because image hasn't been loaded so far)
						return adjustedRenderWidth * adjustedRenderHeight * factor;

					// Compute image size
					int width, height; // width and height are only computed locally
					if (forceOriginalSize || adjustedRenderWidth >= originalWidth || adjustedRenderHeight >= originalHeight)
					{
						width = originalWidth;
						height = originalHeight;
					}
					else
					{
						float fw = (float)adjustedRenderWidth / (float)originalWidth;
						float fh = (float)adjustedRenderHeight / (float)originalHeight;

						if(fw > fh)
						{
							width = adjustedRenderWidth;
							height = (int)((float)originalHeight * fw);
						}
						else
						{
							height = adjustedRenderHeight;
							width = (int)((float)originalWidth * fh);
						}
					}

					return width * height * factor;
				}
				public bool MemoryDisposable()
				{
					return memory > 0 && renderPriority <= 0;
				}
				public bool MemoryShrinkable(float tradeoffRenderSizeFactor)
				{
					if(memory > 0)
					{
						int adjustedRenderWidth = (int)((float)renderWidth * tradeoffRenderSizeFactor);
						int adjustedRenderHeight = (int)((float)renderHeight * tradeoffRenderSizeFactor);

						int newwidth;
						if (adjustedRenderWidth >= originalWidth || adjustedRenderHeight >= originalHeight)
							newwidth = originalWidth;
						else
						{
							float fw = (float)adjustedRenderWidth / (float)originalWidth;
							float fh = (float)adjustedRenderHeight / (float)originalHeight;

							if(fw > fh)
								newwidth = adjustedRenderWidth;
							else
								newwidth = (int)((float)originalWidth * fh);
						}

						float size_diff_factor = (float)newwidth / (float)this.width;
						return size_diff_factor < 0.5f;
					}

					return false;
				}

				public int Load(float tradeoffRenderSizeFactor, bool forceOriginalSize, GLTexture2D texFileNotFound, ReadImageMetaDataDelegate ReadImageMetaData)
				{
					if(texIsStatic || !File.Exists(filename))
					{
						Global.cle.PrintOutput("File not found: " + filename);
						tex = texFileNotFound;
						texIsStatic = true;
						return memory = 0;
					}

					int adustedRenderWidth, adustedRenderHeight;

					if(bmp != null) // If an image is already loaded
					{
						// Compute requested dimensions
						adustedRenderWidth = (int)((float)renderWidth * tradeoffRenderSizeFactor);
						adustedRenderHeight = (int)((float)renderHeight * tradeoffRenderSizeFactor);

						// Compute width & height
						if (forceOriginalSize || adustedRenderWidth >= originalWidth || adustedRenderHeight >= originalHeight)
						{
							width = originalWidth;
							height = originalHeight;
						}
						else
						{
							float fw = (float)adustedRenderWidth / (float)originalWidth;
							float fh = (float)adustedRenderHeight / (float)originalHeight;

							if(fw > fh)
							{
								width = adustedRenderWidth;
								height = (int)((float)originalHeight * fw);
							}
							else
							{
								height = adustedRenderHeight;
								width = (int)((float)originalWidth * fh);
							}
						}

						/*if(bmp != null && width == bmp.Width && height == bmp.Height)
						{
							int abc = 0;
						}*/
						System.Diagnostics.Debug.Assert(bmp == null || width != bmp.Width || height != bmp.Height);

						if(bmp != null && width < bmp.Width && height < bmp.Height) // If requested image is downscaled version of current image
						{
							renderMutex.WaitOne();

							if(bmp != null && width < bmp.Width && height < bmp.Height) // If condition still applies after locking
							{
								// >>> Optimization: Downscale images instead of reloading them

								bmp.Downscale(width, height, isFloatImage ? System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor : System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic);

								if(depth_filename != null)
									bmp_depth.Downscale(width, height, System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor);

								if(lum_filename != null)
									bmp_lum.Downscale(width, height, System.Drawing.Drawing2D.InterpolationMode.Bilinear);

								renderMutex.ReleaseMutex();

								#if ENABLE_SIZE_PRIORISATION
								sizePriority = (float)originalWidth / (float)width;
								#endif

								int _factor = 1 + (depth_filename != null ? 1 : 0) + (lum_filename != null ? 1 : 0);

								int _memorydiff = width * height * _factor - memory;
								memory = width * height * _factor;
								return _memorydiff;
							}

							renderMutex.ReleaseMutex();
						}
					}

					// Load image from disk
					IBitmap newbmp;
					if(!metadataLoaded && ReadImageMetaData != null)
					{
						List<ImageMetaData> meta = new List<ImageMetaData>();

						newbmp = ImageLoader.Load(filename, meta);
						if(newbmp == null)
						{
							Global.cle.PrintOutput("File not readable: " + filename);
							tex = texFileNotFound;
							texIsStatic = true;
							return memory = 0;
						}

						//if(meta.Count == 0 || ReadImageMetaData(image, meta.ToArray()))
						//	metadataLoaded = true;
					}
					else
					{
						newbmp = ImageLoader.Load(filename);
						if(newbmp == null)
						{
							Global.cle.PrintOutput("File not readable: " + filename);
							tex = texFileNotFound;
							texIsStatic = true;
							return memory = 0;
						}
					}
					++GLTextureStream.foo;

					// Load depth image from disk
					IBitmap newbmp_depth = null;
					if(depth_filename != null)
					{
						if(!File.Exists(depth_filename) || (newbmp_depth = ImageLoader.Load(depth_filename)) == null)
						{
							if(newbmp != null)
							{
								newbmp.Dispose();
								newbmp = null;
							}

							Global.cle.PrintOutput((File.Exists(depth_filename) ? "File not readable: " : "File not found: ") + depth_filename);
							tex = texFileNotFound;
							texIsStatic = true;
							return memory = 0;
						}
						++GLTextureStream.foo;
					}

					// Load luminance image from disk
					IBitmap newbmp_lum = null;
					if(lum_filename != null)
					{
						if(!File.Exists(lum_filename) || (newbmp_lum = ImageLoader.Load(lum_filename)) == null)
						{
							if(newbmp != null)
							{
								newbmp.Dispose();
								newbmp = null;
							}
							if(newbmp_depth != null)
							{
								newbmp_depth.Dispose();
								newbmp_depth = null;
							}

							Global.cle.PrintOutput((File.Exists(lum_filename) ? "File not readable: " : "File not found: ") + lum_filename);
							tex = texFileNotFound;
							texIsStatic = true;
							return memory = 0;
						}
						++GLTextureStream.foo;
					}

					// Compute original dimensions
					bool originalDimensionsUpdated = originalWidth != newbmp.Width;
					originalWidth = newbmp.Width;
					originalHeight = newbmp.Height;
					originalAspectRatio = (float)originalWidth / (float)originalHeight;

					// Compute requested dimensions
					if(memory > 0)
					{
						adustedRenderWidth = renderWidth;
						adustedRenderHeight = renderHeight;
					}
					else
					{
						adustedRenderWidth = (int)((float)renderWidth * tradeoffRenderSizeFactor);
						adustedRenderHeight = (int)((float)renderHeight * tradeoffRenderSizeFactor);
					}

					// Compute width & height
					if (forceOriginalSize || adustedRenderWidth >= originalWidth || adustedRenderHeight >= originalHeight)
					{
						width = originalWidth;
						height = originalHeight;
					}
					else
					{
						float fw = (float)adustedRenderWidth / (float)originalWidth;
						float fh = (float)adustedRenderHeight / (float)originalHeight;

						if(fw > fh)
						{
							width = adustedRenderWidth;
							height = (int)((float)originalHeight * fw);
						}
						else
						{
							height = adustedRenderHeight;
							width = (int)((float)originalWidth * fh);
						}
					}

					// Downscale image from originalWidth/originalHeight to width/height
					if(width != originalWidth || height != originalHeight)
					{
						newbmp.Downscale(width, height, isFloatImage ? System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor : System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic);

						if(depth_filename != null)
							newbmp_depth.Downscale(width, height, System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor);

						if(lum_filename != null)
							newbmp_lum.Downscale(width, height, System.Drawing.Drawing2D.InterpolationMode.Bilinear);
					}

					#if DEBUG_GLTEXTURESTREAM
					Graphics gfxDebug = Graphics.FromImage(newbmp);
					gfxDebug.Clear(Color.WhiteSmoke);
					gfxDebug.DrawString(((float)image.originalWidth / (float)width).ToString(), new Font("Consolas", height / 3.0f), Brushes.Black, 0.0f, 0.0f);
					gfxDebug.Flush();
					#endif

					renderMutex.WaitOne();

					if(bmp != null) // If image already loaded (resize required)
					{
						// Unload old image
						bmp.Dispose();
						bmp = null;
						--GLTextureStream.foo;
					}
					bmp = newbmp;

					if(depth_filename != null)
					{
						if(bmp_depth != null)
						{
							// Unload old image
							bmp_depth.Dispose();
							bmp_depth = null;
							--GLTextureStream.foo;
						}
						bmp_depth = newbmp_depth;
					}

					if(lum_filename != null)
					{
						if(bmp_lum != null)
						{
							// Unload old image
							bmp_lum.Dispose();
							bmp_lum = null;
							--GLTextureStream.foo;
						}
						bmp_lum = newbmp_lum;
					}

					if(originalDimensionsUpdated)
						foreach(ImageReference reference in references)
							reference.OnOrignalDimensionsUpdated();

					renderMutex.ReleaseMutex();

					#if ENABLE_SIZE_PRIORISATION
					sizePriority = (float)originalWidth / (float)width;
					#endif

					int factor = 1 + (depth_filename != null ? 1 : 0) + (lum_filename != null ? 1 : 0);

					int memorydiff = width * height * factor - memory;
					memory = width * height * factor;
					return memorydiff;
				}

				public int Unload()
				{
					if(texIsStatic)
						return 0;

					if(bmp == null)
						throw new Exception("assert(bmp != null) triggered inside Unload()");

					renderMutex.WaitOne();

					// Unload image
					bmp.Dispose();
					bmp = null;
					--GLTextureStream.foo;

					if(bmp_depth != null)
					{
						// Unload old image
						bmp_depth.Dispose();
						bmp_depth = null;
						--GLTextureStream.foo;
					}
						
					if(bmp_lum != null)
					{
						// Unload old image
						bmp_lum.Dispose();
						bmp_lum = null;
						--GLTextureStream.foo;
					}

					renderMutex.ReleaseMutex();

					#if ENABLE_SIZE_PRIORISATION
					sizePriority = 1.0f;
					#endif
					width = height = 0;

					int m = memory;
					memory = 0;
					return m;
				}

				public int CompareTo(Image other)
				{
					#if ENABLE_SIZE_PRIORISATION
					return renderPriority == other.renderPriority ? sizePriority.CompareTo(other.sizePriority) : renderPriority.CompareTo(other.renderPriority);
					#else
					return renderPriority.CompareTo(other.renderPriority);
					#endif
				}
			}

			private readonly int memorysize;
			private int availablememory;

			private readonly GLTexture2D texFileNotFound;

			private Thread loaderThread;
			private bool closeLoaderThread, loaderThreadClosed = true;

			public List<Image> prioritySortedImages; //EDIT: Make private
			public Dictionary<string, Image> imageMap; // Mapping filenames to images
			private Mutex addImageMutex;

			private float tradeoffRenderSizeFactor = 1.0f;
			public bool forceOriginalSize = false;

			private ReadImageMetaDataDelegate ReadImageMetaData;

			public AsyncImageLoader(/*TransformedImageCollection images,*/ int memorysize, GLTexture2D texFileNotFound, ReadImageMetaDataDelegate ReadImageMetaData)
			{
				this.memorysize = memorysize;
				this.availablememory = memorysize;
				this.texFileNotFound = texFileNotFound;
				this.ReadImageMetaData = ReadImageMetaData;

				foo = 0;

				prioritySortedImages = new List<Image>();
				imageMap = new Dictionary<string, Image>();
				addImageMutex = new Mutex();

				Start();
			}

			public void Start()
			{
				closeLoaderThread = loaderThreadClosed = false;
				loaderThread = new Thread(LoaderThread);
				loaderThread.Start();
			}
			public void Stop(bool blocking = false)
			{
				closeLoaderThread = true;
				if(blocking)
					while(!loaderThreadClosed)
						Thread.Sleep(1);
			}
			public bool Running { get { return !loaderThreadClosed; } }

			public void AddImage(TransformedImage ti)
			{
				addImageMutex.WaitOne();
				Image image;
				foreach(TransformedImage.ImageLayer layer in ti.activelayers)
				{
					string key = string.Format("{0}|{1}|{2}", layer.filename, layer.depth_filename != null ? layer.depth_filename : "", layer.lum_filename != null ? layer.lum_filename : "");
					if(!imageMap.TryGetValue(key, out image))
					{
						image = new Image(layer.filename, layer.depth_filename, layer.lum_filename, layer.isFloatImage);
						imageMap.Add(key, image);
						prioritySortedImages.Add(image);
					}
					image.AddReference(layer);
				}
				foreach(TransformedImage.ImageLayer layer in ti.inactivelayers)
				{
					string key = string.Format("{0}|{1}|{2}", layer.filename, layer.depth_filename != null ? layer.depth_filename : "", layer.lum_filename != null ? layer.lum_filename : "");
					if(!imageMap.TryGetValue(key, out image))
					{
						image = new Image(layer.filename, layer.depth_filename, layer.lum_filename, layer.isFloatImage);
						imageMap.Add(key, image);
						prioritySortedImages.Add(image);
					}
					image.AddReference(layer);
				}
				addImageMutex.ReleaseMutex();
			}
			public void AddImages(IEnumerable<TransformedImage> tis)
			{
				addImageMutex.WaitOne();
				Image image;
				foreach(TransformedImage ti in tis)
				{
					foreach(TransformedImage.ImageLayer layer in ti.activelayers)
					{
						string key = string.Format("{0}|{1}|{2}", layer.filename, layer.depth_filename != null ? layer.depth_filename : "", layer.lum_filename != null ? layer.lum_filename : "");
						if(!imageMap.TryGetValue(key, out image))
						{
							image = new Image(layer.filename, layer.depth_filename, layer.lum_filename, layer.isFloatImage);
							imageMap.Add(key, image);
							prioritySortedImages.Add(image);
						}
						image.AddReference(layer);
					}
					foreach(TransformedImage.ImageLayer layer in ti.inactivelayers)
					{
						string key = string.Format("{0}|{1}|{2}", layer.filename, layer.depth_filename != null ? layer.depth_filename : "", layer.lum_filename != null ? layer.lum_filename : "");
						if(!imageMap.TryGetValue(key, out image))
						{
							image = new Image(layer.filename, layer.depth_filename, layer.lum_filename, layer.isFloatImage);
							imageMap.Add(key, image);
							prioritySortedImages.Add(image);
						}
						image.AddReference(layer);
					}
				}
				addImageMutex.ReleaseMutex();
			}
			public void RemoveImage(TransformedImage ti) // O(n)
			{
				//EDIT: Untested
				addImageMutex.WaitOne();
				Image image;
				foreach(TransformedImage.ImageLayer layer in ti.activelayers)
				{
					string key = string.Format("{0}|{1}|{2}", layer.filename, layer.depth_filename != null ? layer.depth_filename : "", layer.lum_filename != null ? layer.lum_filename : "");
					if(imageMap.TryGetValue(key, out image))
					{
						image.RemoveReference(layer);
						if(image.references.Count == 0)
						{
							availablememory += image.Unload();
							imageMap.Remove(key);
							prioritySortedImages.Remove(image);
						}
						//EDIT: Update things like renderPriority and call Unload() if renderPriority <= 0, ... ( Maybe do some of this inside RemoveReference()? )
					}
				}

				/*image.RemoveReference(ti);
				prioritySortedImages.RemoveAll(delegate(Image img) {
					if(image.activelayers.Contains(img.image) || image.inactivelayers.Contains(img.image))
					{
						if(img.bmp != null)
							availablememory += img.Unload();
						return true;
					}
					else
						return false;
				});
				addImageMutex.ReleaseMutex();*/ //EDIT
			}
			public void RemoveImages(IEnumerable<TransformedImage> images) // O(n^2)
			{
				/*addImageMutex.WaitOne();
				prioritySortedImages.RemoveAll(delegate(Image img) {
					foreach(TransformedImage image in images)
						if(image.activelayers.Contains(img.image) || image.inactivelayers.Contains(img.image))
						{
							if(img.bmp != null)
								availablememory += img.Unload();
							return true;
						}
					return false;
				});
				addImageMutex.ReleaseMutex();*/ //EDIT
			}
			public void ClearImages()
			{
				addImageMutex.WaitOne();
				foreach(Image image in prioritySortedImages)
					if(image.bmp != null)
						availablememory += image.Unload();
				prioritySortedImages.Clear();
				addImageMutex.ReleaseMutex();
			}

			private void LoaderThread()
			{
				while(!closeLoaderThread)
				{
					//Thread.Sleep(100);
					Thread.Sleep(1);

					addImageMutex.WaitOne();

					prioritySortedImages.Sort();

					// Load highest priority image
					int unloadidx = 0, len = prioritySortedImages.Count, loadidx = len - 1;
					//bool outofmemory = false;
					for(; loadidx >= 0; --loadidx)
					{
						Image img = prioritySortedImages[loadidx];
						int requiredmemory = img.RequiredMemory(tradeoffRenderSizeFactor, forceOriginalSize);

						if(requiredmemory <= 0) // If image is already loaded
							continue; // continue checking next-highest priority image
						if(requiredmemory <= availablememory) // If we have enough memory to load img
						{
							availablememory -= img.Load(tradeoffRenderSizeFactor, forceOriginalSize, texFileNotFound, ReadImageMetaData); // Load image and update available memory
							break; // Done
						}
						else // If we don't have enough memory to load img
						{
							// Unload lower priority images to regain memory and retry loading img
							bool imageLoaded = false;
							for(; unloadidx < len; ++unloadidx)
							//for(; unloadidx < loadidx; ++unloadidx)
							{
								Image u_img = prioritySortedImages[unloadidx];
								if(u_img.MemoryDisposable()) // If image is loaded, but doesn't need to be (renderPriority <= 0)
								{
									availablememory += u_img.Unload(); // Unload image and update available memory
									if(requiredmemory <= availablememory) // If we now have enough memory to load img
									{
										availablememory -= img.Load(tradeoffRenderSizeFactor, forceOriginalSize, texFileNotFound, ReadImageMetaData); // Load image and update available memory
										imageLoaded = true;
										break; // Done
									}
								}
								else if(!forceOriginalSize && u_img.MemoryShrinkable(tradeoffRenderSizeFactor)) // If image is loaded, but can be shrinked
								{
									availablememory -= u_img.Load(tradeoffRenderSizeFactor, forceOriginalSize, texFileNotFound, ReadImageMetaData); // Reload image and update available memory
									if(requiredmemory <= availablememory) // If we now have enough memory to load img
									{
										availablememory -= img.Load(tradeoffRenderSizeFactor, forceOriginalSize, texFileNotFound, ReadImageMetaData); // Load image and update available memory
										imageLoaded = true;
										break; // Done
									}
								}
							}

							if(imageLoaded)
								break;

							//outofmemory = true;
						}
						//Thread.Sleep(1);
					}

					#if ENABLE_TRADEOFF
					int theoreticalusedmemory = 0;
					foreach(Image img in prioritySortedImages)
						if(!img.renderPriorityIsZero())
							theoreticalusedmemory += img.memory;

					tradeoffRenderSizeFactor = Math.Max(MIN_TRADEOFF_FACTOR, (float)(memorysize - theoreticalusedmemory) / (float)memorysize);
					//tradeoffRenderSizeFactor = (float)availablememory / (float)memorysize;
					GLTextureStream.foo2 = tradeoffRenderSizeFactor.ToString();
					#else
					GLTextureStream.foo2 = (availablememory / 262144).ToString() + " MB";
					#endif

					addImageMutex.ReleaseMutex();
				}

				loaderThreadClosed = true;
			}
		}
		private AsyncImageLoader loader;

		public bool forceOriginalSize { get { return loader.forceOriginalSize; } set { loader.forceOriginalSize = value; } }

		private static int ceilBin(int v)
		{
			int b;
			for(b = 1; v > b; b <<= 1) {}
			return b;
		}

		public GLTextureStream(int memorysize, ReadImageMetaDataDelegate ReadImageMetaData)
		{
			#if DEBUG_GLTEXTURESTREAM
			memorysize = 8 * 79 * 79;
			#endif

			this.currentmemorysize = 0;
			this.memorysizelimit = memorysize;

			// >>> Create file-not-found bitmap and texture

			int texwidth = 512, texheight = 512;
			// Find font size to fit "File not found" into Size(texwidth, texheight)
			Bitmap bmpTemp = new Bitmap(1, 1);
			Graphics gfx = Graphics.FromImage(bmpTemp);
			float fontsize = 16.0f;
			Size fileNotFoundSize;
			Font fntFileNotFound;
			do
			{
				fontsize *= 2.0f;
				fntFileNotFound = new Font("Impact", fontsize);
				fileNotFoundSize = gfx.MeasureString("File not found", fntFileNotFound).ToSize();
			} while(fileNotFoundSize.Width < texwidth && fileNotFoundSize.Height < texheight);
			fontsize /= 2.0f;
			fntFileNotFound = new Font("Impact", fontsize);
			fileNotFoundSize = gfx.MeasureString("File not found", fntFileNotFound).ToSize();
			bmpTemp.Dispose();

			// Draw bitmap
			bmpFileNotFound = new Bitmap(texwidth, texheight);
			gfx = Graphics.FromImage(bmpFileNotFound);
			gfx.Clear(Color.IndianRed);
			gfx.DrawString("File not found", fntFileNotFound, Brushes.LightSlateGray, (float)((texwidth - fileNotFoundSize.Width) / 2), (float)((texheight - fileNotFoundSize.Height) / 2));
			gfx.Flush();

			// Load texture
			texFileNotFound = new GLTexture2D("texFileNotFound", new GdiBitmap(bmpFileNotFound), true);

			loader = new AsyncImageLoader(memorysize, texFileNotFound, ReadImageMetaData);

			ActionManager.CreateAction("Start image loader thread", "StartLoader", delegate(object[] parameters) { Enabled = true; return null; });
			ActionManager.CreateAction("Stop image loader thread", "StopLoader", delegate(object[] parameters) { Enabled = false; return null; });
		}
		public void Free()
		{
			loader.Stop(blocking: true);
			loader.ClearImages();
		}

		public bool Enabled
		{
			get {
				return loader.Running;
			}
			set {
				if(value == true && loader.Running == false)
					loader.Start();
				else if(value == false && loader.Running == true)
					loader.Stop(blocking: false);
			}
		}

		public void AddImage(TransformedImage image)
		{
			loader.AddImage(image);
		}
		public void AddImages(IEnumerable<TransformedImage> images)
		{
			loader.AddImages(images);
		}
		public void RemoveImage(TransformedImage image)
		{
			loader.RemoveImage(image);
		}
		public void RemoveImages(IEnumerable<TransformedImage> images)
		{
			loader.RemoveImages(images);
		}
		public void ClearImages()
		{
			loader.ClearImages();
		}

		#if DEBUG_GLTEXTURESTREAM
		GLFont fntDebug = null;
		GLLabel[] lblDebug;
		public void DrawDebugInfo(Size backbufferSize)
		{
			if(fntDebug == null)
			{
				fntDebug = Common.fontText2;

				lblDebug = new GLLabel[loader.prioritySortedImages.Count];
				for(int i = 0; i < loader.prioritySortedImages.Count; ++i)
				{
					lblDebug[i] = new GLLabel();
					lblDebug[i].Font = fntDebug;
					lblDebug[i].Bounds = new Rectangle(100, 32 * i, 64, (int)Math.Ceiling(fntDebug.MeasureString(" ").Y));
					lblDebug[i].OnParentSizeChanged(backbufferSize, backbufferSize);
				}
			}
				
			for(int i = 0; i < loader.prioritySortedImages.Count; ++i)
			{
				//lblDebug[i].Text = loader.prioritySortedImages[i].memory.ToString();
				lblDebug[i].Text = loader.prioritySortedImages[i].image.strValues[0] + ": " + loader.prioritySortedImages[i].renderPriority.ToString();
				lblDebug[i].Draw(0.0f);
			}
		}
		#else
		public void DrawDebugInfo(Size backbufferSize) {}
		#endif

public static int foo = 0;
public static string foo2 = "";
	}
}

