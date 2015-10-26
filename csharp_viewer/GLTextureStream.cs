#define IMAGE_STREAMING
//#define DEBUG_GLTEXTURESTREAM

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
		private Bitmap bmpFileNotFound;
		private GLTexture2D texFileNotFound;

		private int currentmemorysize, memorysizelimit;

		private class AsyncImageLoader
		{
			private class Image : IComparable<Image>
			{
				private TransformedImage image;
				private int width, height;
				private int memory;

				public Image(TransformedImage image)
				{
					this.image = image;
					memory = 0;
				}

				public int RequiredMemory()
				{
					// Computes width & height. Returns (guessed) memory required for Load()

					if(image.renderPriority == 0) // If image doesn't need to be loaded
					{
						return -memory; // Return memory gain achieved by unloading this image
					}

					if(memory > 0) // If image already loaded
						return 0; // Return no extra memory required. EDIT: Implement switching resolutions here

					if(image.originalWidth == 0) // If original size is unknown (because image hasn't been loaded so far)
						return image.renderWidth * image.renderHeight;

					if(image.renderWidth >= image.originalWidth || image.renderHeight >= image.originalHeight)
					{
						width = image.originalWidth;
						height = image.originalHeight;
					}
					else
					{
						float fw = (float)image.renderWidth / (float)image.originalWidth;
						float fh = (float)image.renderHeight / (float)image.originalHeight;

						if(fw > fh)
						{
							width = image.renderWidth;
							height = (int)((float)image.originalHeight * fw);
						}
						else
						{
							height = image.renderHeight;
							width = (int)((float)image.originalWidth * fh);
						}
					}

					return width * height;
				}

				public int Load()
				{
					

					// Load image from disk
					Bitmap newbmp = (Bitmap)Bitmap.FromFile(image.filename);

					// Compute width & height if not computed inside RequiredMemory()
					if(image.originalWidth == 0) // If original size is unknown (because image hasn't been loaded so far)
					{
						image.originalWidth = newbmp.Width;
						image.originalHeight = newbmp.Height;
						image.originalAspectRatio = (float)image.originalWidth / (float)image.originalHeight;

						if(image.renderWidth >= image.originalWidth || image.renderHeight >= image.originalHeight)
						{
							width = image.originalWidth;
							height = image.originalHeight;
						}
						else
						{
							float fw = (float)image.renderWidth / (float)image.originalWidth;
							float fh = (float)image.renderHeight / (float)image.originalHeight;

							if(fw > fh)
							{
								width = image.renderWidth;
								height = (int)((float)image.originalHeight * fw);
							}
							else
							{
								height = image.renderHeight;
								width = (int)((float)image.originalWidth * fh);
							}
						}
					}

					// Downscale image from originalWidth/originalHeight to width/height
					if(width != image.originalWidth || height != image.originalHeight)
					{
						Bitmap originalBmp = newbmp;
						newbmp = new Bitmap(width, height, originalBmp.PixelFormat);
						Graphics gfx = Graphics.FromImage(newbmp);
						gfx.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
						gfx.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;
						gfx.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
						gfx.DrawImage(originalBmp, new Rectangle(0, 0, width, height));
						gfx.Flush();
						originalBmp.Dispose();
						originalBmp = null;
					}

					image.renderMutex.WaitOne();
					image.bmp = newbmp;
					image.renderMutex.ReleaseMutex();

					return (memory = width * height);
				}

				public int Unload()
				{
					if(image.bmp == null)
						throw new Exception("assert(bmp != null) triggered inside Unload()");

					image.renderMutex.WaitOne();

					// Unload image from RAM
					image.bmp.Dispose();
					image.bmp = null;

					image.renderMutex.ReleaseMutex();

					int m = memory;
					memory = 0;
					return m;
				}

				public int CompareTo(Image other)
				{
					return image.renderPriority.CompareTo(other.image.renderPriority);
				}
			}

			private readonly TransformedImageCollection images;
			private int availablememory;

			private Thread loaderThread;
			private bool closeLoaderThread, loaderThreadClosed;

			private List<Image> prioritySortedImages;

			public AsyncImageLoader(TransformedImageCollection images, int memorysizelimit)
			{
				this.images = images;
				this.availablememory = memorysizelimit;

				prioritySortedImages = new List<Image>(images.Count);
				foreach(TransformedImage image in images)
					prioritySortedImages.Add(new Image(image));

				closeLoaderThread = loaderThreadClosed = false;
				loaderThread = new Thread(LoaderThread);
				loaderThread.Start();
			}

			public void CloseThread()
			{
				closeLoaderThread = true;
			}

			public void WaitForThreadClose()
			{
				while(!loaderThreadClosed)
					Thread.Sleep(1);
			}

			private void LoaderThread()
			{
				while(!closeLoaderThread)
				{
					Thread.Sleep(1);

					prioritySortedImages.Sort();

					// Load highest priority image
					for(int i = prioritySortedImages.Count - 1; i > 0; --i)
					{
						Image img = prioritySortedImages[i];
						int requiredmemory = img.RequiredMemory();

						if(requiredmemory <= 0) // If image is already loaded
							continue; // continue checking next-highest priority image
						if(requiredmemory <= availablememory) // If we have enough memory to load img
						{
							availablememory -= img.Load(); // Load image and update available memory
							break; // Done
						}
						else // If we don't have enough memory to load img
						{
							// Unload lower priority images to regain memory and retry loading img
							for(int j = 0; j < i; ++j)
							{
								Image u_img = prioritySortedImages[j];
								if(u_img.RequiredMemory() < 0) // If image is loaded, but doesn't need to be (renderPriority == 0)
								{
									availablememory += u_img.Unload(); // Unload image and update available memory
									if(requiredmemory <= availablememory) // If we now have enough memory to load img
									{
										availablememory -= img.Load(); // Load image and update available memory
										break; // Done
									}
								}
							}
						}
					}
				}

				loaderThreadClosed = true;
			}
		}
		private AsyncImageLoader loader;

		private static int ceilBin(int v)
		{
			int b;
			for(b = 1; v > b; b <<= 1) {}
			return b;
		}

		public GLTextureStream(TransformedImageCollection images, int memorysize, bool depthimages = false)
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
			texFileNotFound = new GLTexture2D(bmpFileNotFound, true);

			loader = new AsyncImageLoader(images, memorysize);
		}
		public void Free()
		{
			loader.CloseThread();

			// Free local resources ...

			loader.WaitForThreadClose();
		}

		#if DEBUG_GLTEXTURESTREAM
		GLFont fntDebug = null;
		GLButton[] cmdDebug;
		GLLabel[] lblDebug;
		public void DrawDebugInfo(Size backbufferSize)
		{
			/*if(fntDebug == null)
			{
				fntDebug = new GLTextFont2(new Font("Consolas", 10.0f));

				cmdDebug = new GLButton[textures.Length];
				for(int i = 0; i < cmdDebug.Length; ++i)
				{
					cmdDebug[i] = new GLButton(new GLTexture2D(textures[i], texwidth, texheight), new Rectangle(100 * i, 36, 64, 64));
					cmdDebug[i].OnParentSizeChanged(backbufferSize, backbufferSize);
				}

				lblDebug = new GLLabel[2 * textures.Length];
				for(int i = 0; i < textures.Length; ++i)
				{
					lblDebug[i] = new GLLabel();
					lblDebug[i].Font = fntDebug;
					lblDebug[i].Bounds = new Rectangle(100 * i, 100, 64, (int)Math.Ceiling(fntDebug.MeasureString(" ").Y));
					lblDebug[i].OnParentSizeChanged(backbufferSize, backbufferSize);

					#if IMAGE_STREAMING
					lblDebug[i + textures.Length] = new GLLabel();
					lblDebug[i + textures.Length].Font = fntDebug;
					lblDebug[i + textures.Length].Bounds = new Rectangle(100 * i, lblDebug[i].Bounds.Bottom, 64, (int)Math.Ceiling(fntDebug.MeasureString(" ").Y));
					lblDebug[i + textures.Length].OnParentSizeChanged(backbufferSize, backbufferSize);
					#endif
				}
			}

			for(int i = 0; i < cmdDebug.Length; ++i)
				cmdDebug[i].Draw(0.0f);
			for(int i = 0; i < textures.Length; ++i)
			{
				lblDebug[i].Text = texturebuffer.buffer[i] == null ? "null" : texturebuffer.buffer[i].idx.ToString();
				lblDebug[i].Draw(0.0f);

				#if IMAGE_STREAMING
				lblDebug[i + textures.Length].Draw(0.0f);
				lblDebug[i + textures.Length].Text = imagebuffer.buffer[i] == null ? "null" : imagebuffer.buffer[i].idx.ToString();
				#endif
			}*/
		}
		#else
		public void DrawDebugInfo(Size backbufferSize) {}
		#endif

		public Texture CreateTexture(string filename, string depth_filename = null)
		{
			return new Texture(this, filename, depth_filename);
		}

public static int foo = 0;
		public class Texture
		{
			private readonly GLTextureStream owner;
			private readonly string filename, depth_filename;
			public GLTexture2D tex, depth_tex;
			public Bitmap bmp, depth_bmp;
			public int width = 1, height = 1;
			public int requiredwidth, requiredheight;
			public enum LoadStatus {None, Queued, Loaded};
			public LoadStatus texstatus;
			//private Object prevtexowner, prevbmpowner;

			public Texture(GLTextureStream owner, string filename, string depth_filename)
			{
				this.owner = owner;
				this.filename = filename;
				if(File.Exists(filename))
				{
					this.bmp = null;
					this.depth_filename = File.Exists(depth_filename) ? depth_filename : null;
					if(depth_filename != null && this.depth_filename == null)
						throw new FileNotFoundException(depth_filename);
				}
				else
				{
					this.bmp = owner.bmpFileNotFound;
					this.depth_filename = null;
					tex = owner.texFileNotFound;
				}
				this.depth_bmp = null;
				this.depth_tex = null;

				#if !IMAGE_STREAMING
				bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
				LoadImage();
				#endif
			}

			public bool Load(int w, int h)
			{return true; // Deprecated
				if(tex == owner.texFileNotFound)
					return true;

				switch(texstatus)
				{
				case LoadStatus.Queued: // If texture loading is in progress (by LoaderThread)
					return true;

				case LoadStatus.Loaded: // If texture loading has finished (by LoaderThread)
					texstatus = LoadStatus.None;
					LoadTexture(); // Copy bmp to tex
					return true;
				}

				if(tex == null)
				{
					if(owner.currentmemorysize + w * h > owner.memorysizelimit)
						return false;
					owner.currentmemorysize += w * h;
					
					texstatus = LoadStatus.Queued;
					requiredwidth = w;
					requiredheight = h;
					//owner.loader.Queue(this);
				}

				return true;
			}
			public void Unload()
			{return; // Deprecated
				if(tex == owner.texFileNotFound)
					return;

				texstatus = LoadStatus.None;

				if(tex != null && texstatus != LoadStatus.None) // If async texture loading is queued or in progress
				{
					//owner.loader.Unqueue(this);

					if(tex != null)
					{
						GL.DeleteTexture(tex.tex);
						tex = null;
					}
					return;
				}

				if(tex != null)
				{
					GL.DeleteTexture(tex.tex);
					tex = null;
					owner.currentmemorysize -= requiredwidth * requiredheight;
				}

				#if IMAGE_STREAMING
				if(bmp != null)
				{
					bmp.Dispose();
					bmp = null;
				}
				#endif
			}

			private void LoadTexture()
			{
				tex = new GLTexture2D(bmp, false);

				if(depth_bmp != null)
				{
					/*bmpdata = depth_bmp.LockBits(new Rectangle(0, 0, depth_bmp.Width, depth_bmp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
					GL.BindTexture(TextureTarget.Texture2D, depth_tex.tex);
					GL.TexSubImage2D(type, 0, 0, 0, depth_tex.width, depth_tex.height, OpenTK.Graphics.OpenGL.PixelFormat.Red, PixelType.Float, bmpdata.Scan0);
					depth_bmp.UnlockBits(bmpdata);*/
				}
			}
		}
	}
}

