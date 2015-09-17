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
		private RingBuffer<Tuple<Bitmap, Bitmap>> imagebuffer;
		private RingBuffer<Tuple<int, int>> texturebuffer;
		private readonly int texwidth, texheight, depth_texwidth, depth_texheight;
		private int[] textures, depth_textures;
		private Bitmap bmpFileNotFound;
		private int texFileNotFound;

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
		}
		public void Free()
		{
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
		}
			
		public Texture CreateTexture(Bitmap bmp, Bitmap depth_bmp = null)
		{
			return new Texture(this, bmp, depth_bmp);
		}
		public Texture CreateTexture(string filename, string depth_filename = null)
		{
			return new Texture(this, filename, texwidth, texheight, depth_filename, depth_texwidth, depth_texheight);
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
			private readonly string filename, depth_filename;
			public readonly GLTexture depth_tex;
			private Bitmap bmp, depth_bmp;
			private RingBuffer<Tuple<int, int>>.Pointer texptr;
			private RingBuffer<Tuple<Bitmap, Bitmap>>.Pointer bmpptr;

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

				if(bmp == null) // If bitmap isn't loaded (implies that CPU streaming is enabled)
				{
					if(owner.numFrameLoads >= GLTextureStream.MAX_NUM_FRAME_LOADS)
						return false; // Exceeded maximum number of loads from disk for this frame

					bool isnewbmp;
					Tuple<Bitmap, Bitmap> tuple = owner.imagebuffer.Dequeue(this, out isnewbmp, bmpptr); //UNFIXED BUG: LoadImage() is called twice. Shows wrong images when commenting lines 283 & 284, unless also commenting 'bmpptr' in this line. It seems bmpptr returns wrong images!
					bmp = tuple == null ? null : tuple.Item1;
					depth_bmp = tuple == null ? null : tuple.Item2;
					if(isnewbmp)
						LoadImage();
					if(bmp == null)
						return false;
				}

				if(tex == -1)
				{
					bool isnewtex;
					Tuple<int, int> tuple = owner.texturebuffer.Dequeue(this, out isnewtex, texptr);
					tex = tuple.Item1;
					depth_tex.tex = tuple.Item2;
					if(isnewtex)
					{
						//if(filename != null) //EDIT: (see line 266)
						//	LoadImage(); //EDIT: (see line 266)

						//++GLTextureStream.foo;
						BitmapData bmpdata = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
						GL.BindTexture(TextureTarget.Texture2D, tex);
						GL.TexSubImage2D(type, 0, 0, 0, width, height, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, bmpdata.Scan0);
						bmp.UnlockBits(bmpdata);
//						return true;

						if(depth_bmp != null)
						{
							bmpdata = depth_bmp.LockBits(new Rectangle(0, 0, depth_bmp.Width, depth_bmp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
							GL.BindTexture(TextureTarget.Texture2D, depth_tex.tex);
							GL.TexSubImage2D(type, 0, 0, 0, depth_tex.width, depth_tex.height, OpenTK.Graphics.OpenGL.PixelFormat.Red, PixelType.Float, bmpdata.Scan0);
							depth_bmp.UnlockBits(bmpdata);
						}

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
					texptr = owner.texturebuffer.Enqueue(this, new Tuple<int, int>(tex, depth_tex.tex));
					tex = -1;
					depth_tex.tex = -1;
				}

				if(bmp != null && filename != null) // If bitmap is loaded and CPU streaming is enabled
				{
					bmpptr = owner.imagebuffer.Enqueue(this, new Tuple<Bitmap, Bitmap>(bmp, depth_bmp));
					bmp = null;
					depth_bmp = null;
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

				if(IMAGE_DIV == 1)
				{
					bmp = (Bitmap)Image.FromFile(filename);

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
		}
	}
}

