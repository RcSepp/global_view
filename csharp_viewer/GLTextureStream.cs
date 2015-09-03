using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace csharp_viewer
{
	public class GLTextureStream
	{
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

			public V Dequeue(Object owner, out bool isnewtex, Pointer ptr = null)
			{
				isnewtex = false;
				if(readidx == writeidx && !isfull)
					return nullvalue;
				isfull = false;

				if(ptr != null && buffer[ptr.idx].owner == owner)
				{
					Slot slot = buffer[ptr.idx];
					buffer[ptr.idx] = buffer[readidx++];
					if(readidx == buffer.Length)
						readidx = 0;
					return slot.value;
				}

				V value = buffer[readidx++].value;
				if(readidx == buffer.Length)
					readidx = 0;
				isnewtex = true;
				return value;
			}
		}
		private RingBuffer<Bitmap> imagebuffer;
		private RingBuffer<int> texturebuffer;
		private readonly int texwidth, texheight;
		private readonly int[] textures;
		private Bitmap bmpFileNotFound;
		private int texFileNotFound;

		private static int ceilBin(int v)
		{
			int b;
			for(b = 1; v > b; b <<= 1) {}
			return b;
		}

		public GLTextureStream(int numtextures, int texwidth, int texheight)
		{
			this.texwidth = texwidth;
			this.texheight = texheight;

			if(numtextures == -1)
			{
				// Get maximum number of textures for a certain amount of available memory

				int w = ceilBin(texwidth), h = ceilBin(texwidth);
				numtextures = 1024 * 1024 * 1024 / (w * h * 4); // Optimize for 1GB of GPU memory
				numtextures = Math.Min(numtextures, 1024);
			}

			texturebuffer = new RingBuffer<int>(numtextures, -1);
			imagebuffer = new RingBuffer<Bitmap>(numtextures, null);

			textures = new int[numtextures];
			GL.GenTextures(numtextures, textures);
			foreach(int tex in textures)
			{
				GL.BindTexture(TextureTarget.Texture2D, tex);
				GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
				GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
				GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, texwidth, texheight, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, IntPtr.Zero);

				texturebuffer.Enqueue(null, tex);
				imagebuffer.Enqueue(null, null);
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
		}
		public void Free()
		{
			GL.DeleteTextures(textures.Length, textures);
			texturebuffer = null;
			imagebuffer = null;
		}
			
		public Texture CreateTexture(Bitmap bmp)
		{
			return new Texture(this, bmp);
		}
		public Texture CreateTexture(string filename)
		{
			return new Texture(this, filename, texwidth, texheight);
		}

		private const int MAX_NUM_FRAME_LOADS = 32;
		private int numFrameLoads = 0;
		public void Update()
		{
			numFrameLoads = 0;
		}

public static int foo = 0;
		public class Texture : GLTexture
		{
			private readonly GLTextureStream owner;
			private readonly string filename;
			private Bitmap bmp;
			private RingBuffer<int>.Pointer texptr;
			private RingBuffer<Bitmap>.Pointer bmpptr;

			public Texture(GLTextureStream owner, Bitmap bmp)
				: base(TextureTarget.Texture2D, bmp.Width, bmp.Height)
			{
				this.owner = owner;
				this.filename = null;
				this.bmp = bmp;
				this.texptr = null;
			}
			public Texture(GLTextureStream owner, string filename, int width, int height)
				: base(TextureTarget.Texture2D, width, height)
			{
				this.owner = owner;
				this.filename = filename;
				if(File.Exists(filename))
					this.bmp = null;
				else
				{
					this.bmp = owner.bmpFileNotFound;
					tex = owner.texFileNotFound;
				}
				this.texptr = null;
			}

			public bool Load()
			{
				if(tex == owner.texFileNotFound)
					return true;

				if(bmp == null) // If bitmap isn't loaded (implies that CPU streaming is enabled)
				{
					if(owner.numFrameLoads >= GLTextureStream.MAX_NUM_FRAME_LOADS)
						return false; // Exceeded maximum number of loads from disk for this frame

					bool isnewbmp;
					bmp = owner.imagebuffer.Dequeue(this, out isnewbmp, bmpptr); //UNFIXED BUG: LoadImage() is called twice. Shows wrong images when commenting lines 196 & 197, unless also commenting 'bmpptr' in this line. It seems bmpptr returns wrong images!
					if(isnewbmp)
						LoadImage();
					if(bmp == null)
						return false;
				}

				if(tex == -1)
				{
					bool isnewtex;
					tex = owner.texturebuffer.Dequeue(this, out isnewtex, texptr);
					if(isnewtex)
					{
						if(filename != null) //EDIT: (see line 183)
							LoadImage(); //EDIT: (see line 183)

						//++GLTextureStream.foo;
						BitmapData bmpdata = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
						GL.BindTexture(TextureTarget.Texture2D, tex);
						GL.TexSubImage2D(type, 0, 0, 0, width, height, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, bmpdata.Scan0);
						bmp.UnlockBits(bmpdata);
						return true;
					}
					return tex != -1;
				}
				return true;
			}
			public void Unload()
			{
				if(tex == owner.texFileNotFound)
					return;

				if(tex != -1)
				{
					texptr = owner.texturebuffer.Enqueue(this, tex);
					tex = -1;
				}

				if(bmp != null && filename != null) // If bitmap is loaded and CPU streaming is enabled
				{
					bmpptr = owner.imagebuffer.Enqueue(this, bmp);
					bmp = null;
				}
			}


			private void LoadImage()
			{
				++owner.numFrameLoads;

				++GLTextureStream.foo;

				if(bmp != null)
				{
					bmp.Dispose();
					bmp = null;
				}
				
				if(Viewer.IMAGE_DIV == 1)
					bmp = (Bitmap)Image.FromFile(filename);
				else
				{
					Image img = Image.FromFile(filename);
					bmp = new Bitmap(img.Width / Viewer.IMAGE_DIV, img.Height / Viewer.IMAGE_DIV, img.PixelFormat);
					Graphics gfx = Graphics.FromImage(bmp);
					gfx.DrawImage(img, new Rectangle(0, 0, bmp.Width, bmp.Height));
					gfx.Flush();
					img.Dispose();
				}
			}
		}
	}
}

