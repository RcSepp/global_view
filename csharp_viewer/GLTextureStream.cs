#define ASYNC_LOADS

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
		public static int IMAGE_DIV = 1;
		public static int DEPTH_IMAGE_DIV = 1;

		public class RingBuffer<V>
		{
			private class Slot
			{
				public readonly V value;
				public readonly Object owner;

				public Slot(V value, Object owner)
				{
					this.value = value;
					this.owner = owner;
				}
			}
			public class Pointer
			{
				public readonly int idx;
				public Pointer(int idx)
				{
					this.idx = idx;
				}
			}
			private Slot[] buffer;
			int readidx, writeidx;
			bool isfull;
			V nullvalue;

			public RingBuffer(int size, V nullvalue = default(V))
			{
				buffer = new Slot[size];
				readidx = writeidx = 0;
				this.nullvalue = nullvalue;
			}

			public Pointer Enqueue(Object owner, V value)
			{
				if(isfull)
					return null;
				
				Pointer ptr = new Pointer(writeidx);
				buffer[writeidx++] = new Slot(value, owner);
				if(writeidx == buffer.Length)
					writeidx = 0;
				if(readidx == writeidx)
					isfull = true;
				return ptr;
			}

			public V Dequeue(Object owner, out Object prevowner, out bool isnewtex, Pointer ptr = null)
			{
				isnewtex = false;
				if(readidx == writeidx && !isfull)
				{
					prevowner = null;
					return nullvalue;
				}
				isfull = false;

				if(ptr != null && buffer[ptr.idx].owner == owner)
				{
					prevowner = owner;
					Slot slot = buffer[ptr.idx];
					buffer[ptr.idx] = buffer[readidx++];
					if(readidx == buffer.Length)
						readidx = 0;
					return slot.value;
				}

				V value = buffer[readidx].value;
				prevowner = buffer[readidx++].owner;
				if(readidx == buffer.Length)
					readidx = 0;
				isnewtex = true;
				return value;
			}
		}
		private RingBuffer<Tuple<Bitmap, Bitmap>> imagebuffer;
		private RingBuffer<Tuple<int, int>> texturebuffer;
		private readonly int texwidth, texheight, depth_texwidth, depth_texheight;
		private int[] textures, depth_textures;
		private Bitmap bmpFileNotFound;
		private int texFileNotFound;

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

		public GLTextureStream(int numtextures, int texturewidth, int textureheight, bool depthimages = false)
		{
			this.texwidth = texturewidth / IMAGE_DIV;
			this.texheight = textureheight / IMAGE_DIV;
			this.depth_texwidth = depthimages ? texturewidth / IMAGE_DIV : 0;
			this.depth_texheight = depthimages ? textureheight / IMAGE_DIV : 0;

			if(numtextures == -1)
			{
				// Get maximum number of textures for a certain amount of available memory

				int w = ceilBin(texwidth), h = ceilBin(texheight), dw = ceilBin(depth_texwidth), dh = ceilBin(depth_texheight);
				numtextures = 1024 * 1024 * 1024 / ((w * h + dw * dh) * 4); // Optimize for 1GB of GPU memory
				numtextures = Math.Min(numtextures, 1024);
			}

			texturebuffer = new RingBuffer<Tuple<int, int>>(numtextures, /*-1*/ null);
			imagebuffer = new RingBuffer<Tuple<Bitmap, Bitmap>>(numtextures, null);

			textures = new int[numtextures];
			GL.GenTextures(numtextures, textures);
			if(depthimages)
			{
				depth_textures = new int[numtextures];
				GL.GenTextures(numtextures, depth_textures);
			}
			else
				depth_textures = null;
			for(int i = 0; i < numtextures; ++i)
			{
				GL.BindTexture(TextureTarget.Texture2D, textures[i]);
				GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
				GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
				GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Clamp);
				GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Clamp);
				GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, texwidth, texheight, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, IntPtr.Zero);

				if(depthimages)
				{
					GL.BindTexture(TextureTarget.Texture2D, depth_textures[i]);
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Clamp);
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Clamp);
					GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R32f, depth_texwidth, depth_texheight, 0, OpenTK.Graphics.OpenGL.PixelFormat.Red, PixelType.Float, IntPtr.Zero);
					texturebuffer.Enqueue(null, new Tuple<int, int>(textures[i], depth_textures[i]));
				}
				else
					texturebuffer.Enqueue(null, new Tuple<int, int>(textures[i], -1));
				
				imagebuffer.Enqueue(null, new Tuple<Bitmap, Bitmap>(null, null));
			}

			// >>> Create file-not-found bitmap and texture

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
			texFileNotFound = GL.GenTexture();
			GL.BindTexture(TextureTarget.Texture2D, texFileNotFound);
			BitmapData bmpdata = bmpFileNotFound.LockBits(new Rectangle(0, 0, bmpFileNotFound.Width, bmpFileNotFound.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
			GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, bmpdata.Width, bmpdata.Height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, bmpdata.Scan0);
			bmpFileNotFound.UnlockBits(bmpdata);
			GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapNearest);
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

			#if ASYNC_LOADS
			closeLoaderThread = loaderThreadClosed = false;
			loaderQueue = new LinkedList<Texture>();
			loaderQueueMutex = new Mutex();
			loaderThread = new Thread(LoaderThread);
			loaderThread.Start();
			#endif


// For debugging only:
bmpdata = bmpFileNotFound.LockBits(new Rectangle(0, 0, bmpFileNotFound.Width, bmpFileNotFound.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
for(int i = 0; i < numtextures; ++i)
{
	GL.BindTexture(TextureTarget.Texture2D, textures[i]);
	GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, bmpdata.Width, bmpdata.Height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, bmpdata.Scan0);
}
bmpFileNotFound.UnlockBits(bmpdata);
		}
		public void Free()
		{
			#if ASYNC_LOADS
			closeLoaderThread = true;
			#endif

			if(textures != null)
			{
				GL.DeleteTextures(textures.Length, textures);
				textures = null;
			}
			if(depth_textures != null)
			{
				GL.DeleteTextures(depth_textures.Length, depth_textures);
				depth_textures = null;
			}
			texturebuffer = null;
			imagebuffer = null;

			#if ASYNC_LOADS
			while(!loaderThreadClosed)
				Thread.Sleep(1);
			#endif
		}
			
		public Texture CreateTexture(Bitmap bmp, Bitmap depth_bmp = null)
		{
			return new Texture(this, bmp, depth_bmp);
		}
		public Texture CreateTexture(string filename, string depth_filename = null)
		{
			return new Texture(this, filename, texwidth, texheight, depth_filename, depth_texwidth, depth_texheight);
		}

public static int foo = 0;
		public class Texture : GLTexture
		{
			private readonly GLTextureStream owner;
			private readonly string filename, depth_filename;
			public readonly GLTexture depth_tex;
			private Bitmap bmp, depth_bmp;
			private RingBuffer<Tuple<int, int>>.Pointer texptr;
			private RingBuffer<Tuple<Bitmap, Bitmap>>.Pointer bmpptr;
			private int temp_tex;
			private Object prevtexowner, prevbmpowner;

			public Texture(GLTextureStream owner, Bitmap bmp, Bitmap depth_bmp)
				: base(TextureTarget.Texture2D, bmp.Width, bmp.Height)
			{
				this.owner = owner;
				this.filename = null;
				this.depth_filename = null;
				this.bmp = bmp;
				this.depth_bmp = depth_bmp;
				this.texptr = null;
				this.depth_tex = new GLTexture(TextureTarget.Texture2D, depth_bmp.Width, depth_bmp.Height);
			}
			public Texture(GLTextureStream owner, string filename, int width, int height, string depth_filename, int depth_width, int depth_height)
				: base(TextureTarget.Texture2D, width, height)
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
				this.texptr = null;
				this.depth_tex = new GLTexture(TextureTarget.Texture2D, depth_width, depth_height);
			}

			public bool Load()
			{
				if(tex == owner.texFileNotFound)
					return true;

				if(tex == -2) // If texture loading is in progress (by LoaderThread)
					return true;

				if(tex == -3) // If texture loading has finished (by LoaderThread)
				{
					tex = temp_tex;
					LoadTexture(); // Copy bmp to tex
					return true;
				}

				if(tex == -1) // If the texture isn't loaded
				{
					bool isnewtex;
					//Object prevtexowner;
					Tuple<int, int> textuple = owner.texturebuffer.Dequeue(this, out prevtexowner, out isnewtex, texptr); // Request GPU memory for a texture
					tex = textuple == null ? -1 : textuple.Item1;
					depth_tex.tex = textuple == null ? -1 : textuple.Item2;
					if(prevtexowner == this) // If the aquired GPU memory is already loaded with the proper texture
						return true;
					if(isnewtex) // If GPU memory has been granted
					{
						if(bmp == null) // If bitmap isn't loaded (implies that CPU streaming is enabled)
						{
							bool isnewbmp;
							//Object prevbmpowner;
							Tuple<Bitmap, Bitmap> bmptuple = owner.imagebuffer.Dequeue(this, out prevbmpowner, out isnewbmp, bmpptr); // Request CPU memory for an image
							bmp = bmptuple == null ? null : bmptuple.Item1;
							depth_bmp = bmptuple == null ? null : bmptuple.Item2;
							if(prevbmpowner == this) // If the aquired CPU memory is already loaded with the proper image
							{
								LoadTexture(); // Copy bmp to tex
								return true;
							}
							if(isnewbmp) // If CPU memory has been granted
							{
								#if ASYNC_LOADS
								temp_tex = tex;
								tex = -2;
								owner.loaderQueueMutex.WaitOne();
								owner.loaderQueue.AddLast(this);
								owner.loaderQueueMutex.ReleaseMutex();
								#else
								LoadImage();
								LoadTexture(); // Copy bmp to tex
								#endif
								return true;
							}
							else // If no CPU memory was available
							{
								// Return GPU memory to previous owner
								texptr = owner.texturebuffer.Enqueue(prevtexowner, new Tuple<int, int>(tex, depth_tex.tex));
								tex = -1;
								depth_tex.tex = -1;
								return false;
							}
						}
						else // If bitmap is loaded
						{
							LoadTexture(); // Copy bmp to tex
							return true;
						}
					}
					else // If no GPU memory was available
						return false;
				}


				return true;
			}
			public void Unload()
			{
				if(tex == owner.texFileNotFound)
					return;

				#if ASYNC_LOADS
				if(tex < -1) // If async texture loading is queued or in progress
				{
					owner.loaderQueueMutex.WaitOne();
					LinkedListNode<Texture> node;
					if(tex == -2 && (node = owner.loaderQueue.Find(this)) != null) // If async texture loading is still queued after aquiring mutex
					{
						owner.loaderQueue.Remove(this); // Remove this from queue
						owner.loaderQueueMutex.ReleaseMutex();

						if(bmp != null && filename != null) // If bitmap is loaded and CPU streaming is enabled
						{
							// Return GPU memory to previous owner
							bmpptr = owner.imagebuffer.Enqueue(prevbmpowner, new Tuple<Bitmap, Bitmap>(bmp, depth_bmp));
							bmp = null;
							depth_bmp = null;
						}
					}
					else
					{
						owner.loaderQueueMutex.ReleaseMutex();

						if(bmp != null && filename != null) // If bitmap is loaded and CPU streaming is enabled
						{
							// Return GPU memory without owner (since a load operation might be in progress EDIT: make sure it finishes)
							bmpptr = owner.imagebuffer.Enqueue(null, new Tuple<Bitmap, Bitmap>(bmp, depth_bmp));
							bmp = null;
							depth_bmp = null;
						}
					}

					// Return GPU memory to previous owner
					texptr = owner.texturebuffer.Enqueue(prevtexowner, new Tuple<int, int>(temp_tex, depth_tex.tex)); // temp_tex ... Use restored tex
					tex = -1;
					depth_tex.tex = -1;
					return;
				}
				#endif

				if(tex > -1) // If a texture is loaded
				{
					texptr = owner.texturebuffer.Enqueue(this, new Tuple<int, int>(tex, depth_tex.tex));
					tex = -1;
					depth_tex.tex = -1;
				}

				if(bmp != null && filename != null) // If bitmap is loaded and CPU streaming is enabled
				{
					bmpptr = owner.imagebuffer.Enqueue(null, new Tuple<Bitmap, Bitmap>(bmp, depth_bmp)); //EDIT: Replacing null with this crashes. Why?
					bmp = null;
					depth_bmp = null;
				}
			}


			public void LoadImage() //EDIT: Make private
			{
				++GLTextureStream.foo;

				if(bmp != null)
				{
					bmp.Dispose();
					bmp = null;
				}

				if(IMAGE_DIV == 1)
				{
					bmp = (Bitmap)Image.FromFile(filename);

					int w = bmp.Width, h = bmp.Height;
					if(w > width || h > height)
					{
						float sw = (float)width / (float)w;
						float sh = (float)height / (float)h;

						if(sw < sh)
						{
							w = width;
							h = (int)((float)height * sw);
						}
						else
						{
							w = (int)((float)width * sh);
							h = height;
						}
					}
					if(w > width || h > height)
						throw new Exception();
					
					if(w != width || h != height)
					{
						Bitmap bmp2 = new Bitmap(width, height, bmp.PixelFormat);
						Graphics gfx = Graphics.FromImage(bmp2);
						gfx.DrawImage(bmp, new Rectangle((width - w) / 2, (height - h) / 2, w, h));
						gfx.Flush();
						bmp.Dispose();

						bmp = bmp2;
					}

					if(depth_filename != null)
						depth_bmp = (Bitmap)Image.FromFile(depth_filename);
				}
				else
				{
					Image img = Image.FromFile(filename);
					bmp = new Bitmap(width, height, img.PixelFormat);
					Graphics gfx = Graphics.FromImage(bmp);
					gfx.DrawImage(img, new Rectangle(0, 0, width, height));
					gfx.Flush();
					img.Dispose();

					if(depth_filename != null)
					{
						img = Image.FromFile(depth_filename);
						depth_bmp = new Bitmap(depth_tex.width, depth_tex.height, img.PixelFormat);
						gfx = Graphics.FromImage(depth_bmp);
						gfx.DrawImage(img, new Rectangle(0, 0, depth_tex.width, depth_tex.height));
						gfx.Flush();
						img.Dispose();
					}
				}
			}

			private void LoadTexture()
			{
				BitmapData bmpdata = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
				GL.BindTexture(TextureTarget.Texture2D, tex);
				GL.TexSubImage2D(type, 0, 0, 0, width, height, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, bmpdata.Scan0);
				bmp.UnlockBits(bmpdata);

				if(depth_bmp != null)
				{
					bmpdata = depth_bmp.LockBits(new Rectangle(0, 0, depth_bmp.Width, depth_bmp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
					GL.BindTexture(TextureTarget.Texture2D, depth_tex.tex);
					GL.TexSubImage2D(type, 0, 0, 0, depth_tex.width, depth_tex.height, OpenTK.Graphics.OpenGL.PixelFormat.Red, PixelType.Float, bmpdata.Scan0);
					depth_bmp.UnlockBits(bmpdata);
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
				tex.tex = -3; // Sinal that the image has been loaded
			}

			loaderThreadClosed = true;
		}
		#endif
	}
}

