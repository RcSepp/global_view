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
		public static int IMAGE_DIV = 2;
		public static int DEPTH_IMAGE_DIV = 1;

		public class RingBuffer<V>
		{
			public class Slot
			{
				public readonly V value;
				public Object owner; // Class who created value
				public int idx; // Index of this slot in the buffer

				/*public Slot(V value, Object owner)
				{
					this.value = value;
					this.owner = owner;
					this.idx = -1; // This slot isn't assigned to a buffer yet
				}*/
				public Slot(V value, Object owner, int idx)
				{
					this.value = value;
					this.owner = owner;
					this.idx = idx;
				}

				public bool inBuffer { get { return idx != -1; } }
			}
			public Slot[] buffer; //EDIT: Make private
			public int readidx, writeidx; //EDIT: Make private
			private bool isfull;

			public RingBuffer(int size, Func<int, V> getValue)
			{
				buffer = new Slot[size];
				for(int i = 0; i < size; ++i)
					buffer[i] = new Slot(getValue(i), null, i);
				readidx = writeidx = 0;
				isfull = true;
			}
				
			public void Enqueue(Slot slot) // Make slot available for reuse by a different owner
			{
				/*foreach(Slot s in buffer)
					if(s.owner == slot.owner)
						throw new Exception("Already in buffer");*/

				if(slot.idx != -1)
					return; // Slot is already in buffer

				if(isfull)
					throw new Exception("Buffer overflow");

				// Insert slot at writeidx
				buffer[slot.idx = writeidx] = slot;

				// Advance writeidx
				if(++writeidx == buffer.Length)
					writeidx = 0;
				if(readidx == writeidx)
					isfull = true;

				/*foreach(Slot s in buffer)
					if(s != null && buffer[s.idx] != s)
						throw new Exception("assert: index error");*/
			}

			/*public bool IndexValid(int idx)
			{
				if(readidx < writeidx)
					return idx >= readidx && idx <= writeidx;
				else
					return !isfull && (idx > readidx || idx < writeidx);
			}*/

			public void Reclaim(Slot slot) // Reclaim previously enqueued slot. This undos Enqueue() and should only be performed if slot.owner hasn't been overwritten
			{
				if(slot.idx == -1 && readidx == writeidx && !isfull) // If the slot isn't in the buffer or the buffer is empty:
					throw new Exception("Illegal use of Reclaim()");

				// Get next free slot -> readslot
				Slot readslot = buffer[readidx];

				// Exchange slot <-> readslot
				buffer[readslot.idx = slot.idx] = readslot;
				buffer[slot.idx = readidx] = slot;

				// Remove slot from buffer
				buffer[readidx] = null;
				slot.idx = -1;

				// Advance readidx
				isfull = false;
				if(++readidx == buffer.Length)
					readidx = 0;

				/*foreach(Slot s in buffer)
					if(s != null && buffer[s.idx] != s)
						throw new Exception("assert: index error");*/
			}

			public Slot Dequeue() // Get next free slot or return null if empty
			{
				if(readidx == writeidx && !isfull) // If the buffer is empty:
					return null;
				
				// Get next free slot
				Slot slot = buffer[readidx];

				// Remove slot from buffer
				buffer[readidx] = null;
				slot.idx = -1;

				// Advance readidx
				isfull = false;
				if(++readidx == buffer.Length)
					readidx = 0;

				return slot;
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
			#if DEBUG_GLTEXTURESTREAM
			numtextures = 8;
			#endif

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

			textures = new int[numtextures];
			GL.GenTextures(numtextures, textures);
			if(depthimages)
			{
				depth_textures = new int[numtextures];
				GL.GenTextures(numtextures, depth_textures);
			}
			else
				depth_textures = null;
			UInt32[] clearbytes = new UInt32[texwidth * texheight];
			for(int i = 0; i < numtextures; ++i)
			{
				GL.BindTexture(TextureTarget.Texture2D, textures[i]);
				GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
				GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
				GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Clamp);
				GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Clamp);
				GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, texwidth, texheight, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, clearbytes);

				if(depthimages)
				{
					GL.BindTexture(TextureTarget.Texture2D, depth_textures[i]);
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Clamp);
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Clamp);
					GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R32f, depth_texwidth, depth_texheight, 0, OpenTK.Graphics.OpenGL.PixelFormat.Red, PixelType.Float, IntPtr.Zero);
				}
			}

			if(depthimages)
			{
				texturebuffer = new RingBuffer<Tuple<int, int>>(numtextures, delegate(int i) {
					return new Tuple<int, int>(textures[i], depth_textures[i]);
				});
				#if IMAGE_STREAMING
				imagebuffer = new RingBuffer<Tuple<Bitmap, Bitmap>>(numtextures, delegate(int i) {
					return new Tuple<Bitmap, Bitmap>(new Bitmap(texwidth, texheight, System.Drawing.Imaging.PixelFormat.Format32bppArgb), new Bitmap(depth_texwidth, depth_texheight, System.Drawing.Imaging.PixelFormat.Format32bppArgb));
				});
				#endif
			}
			else
			{
				texturebuffer = new RingBuffer<Tuple<int, int>>(numtextures, delegate(int i) {
					return new Tuple<int, int>(textures[i], -1);
				});
				#if IMAGE_STREAMING
				imagebuffer = new RingBuffer<Tuple<Bitmap, Bitmap>>(numtextures, delegate(int i) {
					return new Tuple<Bitmap, Bitmap>(new Bitmap(texwidth, texheight, System.Drawing.Imaging.PixelFormat.Format32bppArgb), null);
				});
				#endif
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

		#if DEBUG_GLTEXTURESTREAM
		GLFont fntDebug = null;
		GLButton[] cmdDebug;
		GLLabel[] lblDebug;
		public void DrawDebugInfo(Size backbufferSize)
		{
			if(fntDebug == null)
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
				/*lblDebug[0].Text = "read";
				lblDebug[1].Text = "write";*/
			}

			/*lblDebug[0].Bounds = new Rectangle(100 * texturebuffer.readidx, lblDebug[0].Bounds.Y, lblDebug[0].Bounds.Width, lblDebug[0].Bounds.Height);
			lblDebug[1].Bounds = new Rectangle(100 * texturebuffer.writeidx, lblDebug[1].Bounds.Y, lblDebug[1].Bounds.Width, lblDebug[1].Bounds.Height);*/

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
			}
		}
		#else
		public void DrawDebugInfo(Size backbufferSize) {}
		#endif

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
			//private RingBuffer<Tuple<int, int>>.Pointer texptr;
			//private RingBuffer<Tuple<Bitmap, Bitmap>>.Pointer bmpptr;
			private int temp_tex;
			//private Object prevtexowner, prevbmpowner;

			private RingBuffer<Tuple<int, int>>.Slot texslot = null;
			private RingBuffer<Tuple<Bitmap, Bitmap>>.Slot bmpslot = null;

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
				//this.texptr = null;
				this.depth_tex = new GLTexture(TextureTarget.Texture2D, depth_width, depth_height);

				#if !IMAGE_STREAMING
				bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
				LoadImage();
				#endif
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
					texslot.owner = bmpslot.owner = this; // Claim texslot and bmpslot
					return true;
				}

				if(tex == -1) // If the texture isn't loaded
				{
					if(texslot != null && texslot.inBuffer && texslot.owner == this) // If texture hasn't been removed or overwritten yet
					{
						owner.texturebuffer.Reclaim(texslot); // Reclaim texture from buffer
						tex = texslot.value.Item1;
						depth_tex.tex = texslot.value.Item2;
						return true;
					}

					texslot = owner.texturebuffer.Dequeue(); // Try to get a new texture
					if(texslot == null) // If no GPU memory was available
						return false;

					if(bmp == null) // If bitmap isn't loaded (implies that CPU streaming is enabled)
					{
						if(bmpslot != null && bmpslot.inBuffer && bmpslot.owner == this) // If image hasn't been removed or overwritten yet
						{
							owner.imagebuffer.Reclaim(bmpslot); // Reclaim image from buffer
							tex = texslot.value.Item1;
							depth_tex.tex = texslot.value.Item2;
							bmp = bmpslot.value.Item1;
							depth_bmp = bmpslot.value.Item2;
							LoadTexture(); // Copy bmp to tex
							texslot.owner = this; // Claim texslot
							return true;
						}

						bmpslot = owner.imagebuffer.Dequeue(); // Try to get a new image
						if(bmpslot == null) // If no CPU memory was available
						{
							owner.texturebuffer.Enqueue(texslot); // Requeue texture unchanged
							return false;
						}

						#if ASYNC_LOADS
						temp_tex = texslot.value.Item1;
						depth_tex.tex = texslot.value.Item2;
						bmp = bmpslot.value.Item1;
						depth_bmp = bmpslot.value.Item2;
						tex = -2;
						owner.loaderQueueMutex.WaitOne();
						owner.loaderQueue.AddLast(this);
						owner.loaderQueueMutex.ReleaseMutex();
						#else
						tex = texslot.value.Item1;
						depth_tex.tex = texslot.value.Item2;
						bmp = bmpslot.value.Item1;
						depth_bmp = bmpslot.value.Item2;
						LoadImage();
						LoadTexture(); // Copy bmp to tex
						texslot.owner = bmpslot.owner = this; // Claim texslot and bmpslot
						#endif
					}
					else // If bitmap is loaded
					{
						tex = texslot.value.Item1;
						depth_tex.tex = texslot.value.Item2;
						LoadTexture(); // Copy bmp to tex
						texslot.owner = this; // Claim texslot
						return true;
					}
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
						owner.loaderQueue.Remove(node); // Remove this from queue
						owner.loaderQueueMutex.ReleaseMutex();
if(texslot != null) texslot.owner = null;//DELETE
bmpslot.owner = null;//DELETE
					}
					else
					{
						owner.loaderQueueMutex.ReleaseMutex();
if(texslot != null) texslot.owner = null;//DELETE
						bmpslot.owner = null; // Remove owner, since it is unclear if the image has been overwritten by the loader thread or not
					}
				}
				#endif

				if(texslot != null && tex != -1)
				{
					owner.texturebuffer.Enqueue(texslot);
					tex = -1;
				}

				#if IMAGE_STREAMING
				if(bmpslot != null && bmp != null)
				{
					owner.imagebuffer.Enqueue(bmpslot);
					bmp = null;
				}
				#endif
			}


			public void LoadImage() //EDIT: Make private
			{
				++GLTextureStream.foo;

				Image img = Image.FromFile(filename);
				Graphics gfx = Graphics.FromImage(bmp);
				gfx.DrawImage(img, new Rectangle(0, 0, width, height));
				gfx.Flush();
				img.Dispose();

				if(depth_filename != null)
				{
					img = Image.FromFile(depth_filename);
					gfx = Graphics.FromImage(depth_bmp);
					gfx.DrawImage(img, new Rectangle(0, 0, depth_tex.width, depth_tex.height));
					gfx.Flush();
					img.Dispose();
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
				#if DEBUG_GLTEXTURESTREAM
				Thread.Sleep(1000);
				#endif
				tex.tex = -3; // Sinal that the image has been loaded
			}

			loaderThreadClosed = true;
		}
		#endif
	}
}

