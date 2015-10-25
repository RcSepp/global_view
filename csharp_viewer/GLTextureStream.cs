#define IMAGE_STREAMING
#define ASYNC_LOADS // Async loads are only in effect with image streaming
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

		#if ASYNC_LOADS
		private Thread loaderThread;
		private LinkedList<Texture> loaderQueue;
		private Mutex loaderQueueMutex;
		private bool closeLoaderThread, loaderThreadClosed;
		#endif

		private static int ceilBin(int v)
		{
			int b;
			for(b = 1; v > b; b <<= 1) {}
			return b;
		}

		public GLTextureStream(int memorysize, bool depthimages = false)
		{
			#if DEBUG_GLTEXTURESTREAM
			memorysize = 8 * 256 * 256;
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

			#if ASYNC_LOADS
			closeLoaderThread = loaderThreadClosed = false;
			loaderQueue = new LinkedList<Texture>();
			loaderQueueMutex = new Mutex();
			loaderThread = new Thread(LoaderThread);
			loaderThread.Start();
			#endif
		}
		public void Free()
		{
			#if ASYNC_LOADS
			closeLoaderThread = true;
			#endif

			#if ASYNC_LOADS
			while(!loaderThreadClosed)
				Thread.Sleep(1);
			#endif
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
			{
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
					owner.loaderQueueMutex.WaitOne();
					owner.loaderQueue.AddLast(this);
					owner.loaderQueueMutex.ReleaseMutex();
				}

				return true;
			}
			public void Unload()
			{
				if(tex == owner.texFileNotFound)
					return;

				texstatus = LoadStatus.None;

				#if ASYNC_LOADS
				if(tex != null && texstatus != LoadStatus.None) // If async texture loading is queued or in progress
				{
					owner.loaderQueueMutex.WaitOne();
					owner.loaderQueue.Remove(this);
					owner.loaderQueueMutex.ReleaseMutex();

					if(tex != null)
					{
						GL.DeleteTexture(tex.tex);
						tex = null;
					}
					return;
				}
				#endif

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


			public void LoadImage() //EDIT: Make private
			{
				++GLTextureStream.foo;

				/*bmp = (Bitmap)Image.FromFile(filename);
				width = bmp.Width;
				height = bmp.Height;*/

				Image img = Image.FromFile(filename);

				if(requiredwidth >= img.Width || requiredheight >= img.Height)
				{
					bmp = (Bitmap)img;
					width = bmp.Width;
					height = bmp.Height;
				}
				else
				{
					float fw = (float)requiredwidth / (float)img.Width;
					float fh = (float)requiredheight / (float)img.Height;

					if(fw > fh)
					{
						width = requiredwidth;
						height = (int)((float)img.Height * fw);
					}
					else
					{
						height = requiredheight;
						width = (int)((float)img.Width * fh);
					}

					bmp = new Bitmap(width, height, img.PixelFormat);
					Graphics gfx = Graphics.FromImage(bmp);
					gfx.DrawImage(img, new Rectangle(0, 0, width, height));
					gfx.Flush();

					img.Dispose();
				}

				if(depth_filename != null)
				{
					/*img = Image.FromFile(depth_filename);
					gfx = Graphics.FromImage(depth_bmp);
					gfx.DrawImage(img, new Rectangle(0, 0, depth_tex.width, depth_tex.height));
					gfx.Flush();
					img.Dispose();*/
				}
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

		#if ASYNC_LOADS
		private void LoaderThread()
		{
			while(!closeLoaderThread)
			{
				Thread.Sleep(1);
				if(loaderQueueMutex.WaitOne(1) == false)
					continue;
				if(loaderQueue.Count == 0)
				{
					loaderQueueMutex.ReleaseMutex();
					continue;
				}
				Texture tex = loaderQueue.First.Value;
				loaderQueue.RemoveFirst();
				loaderQueueMutex.ReleaseMutex();

				tex.LoadImage();
				#if DEBUG_GLTEXTURESTREAM
				Thread.Sleep(1000);
				#endif

				if(tex.bmp != null)
					tex.texstatus = Texture.LoadStatus.Loaded; // Sinal that the image has been loaded
			}

			loaderThreadClosed = true;
		}
		#endif
	}
}

