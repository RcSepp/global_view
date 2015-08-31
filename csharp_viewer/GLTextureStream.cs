﻿using System;
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

			int[] textures = new int[numtextures];
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
		}
			
		public Texture CreateTexture(Bitmap bmp)
		{
			return new Texture(this, bmp);
		}
		public Texture CreateTexture(string filename)
		{
			return new Texture(this, filename, texwidth, texheight);
		}

		private const int MAX_NUM_FRAME_LOADS = 1;
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
				this.bmp = null;
				this.texptr = null;
			}

			public bool Load()
			{
//return false;
				if(bmp == null) // If bitmap isn't loaded (implies that CPU streaming is enabled)
				{
					if(owner.numFrameLoads >= GLTextureStream.MAX_NUM_FRAME_LOADS)
						return false; // Exceeded maximum number of loads from disk for this frame

					bool isnewbmp;
					bmp = owner.imagebuffer.Dequeue(this, out isnewbmp, bmpptr);
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
						if(filename != null)
							LoadImage();

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
				}
			}
		}
	}
}

