﻿using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace csharp_viewer
{
	public class GLTexture1D : GLTexture
	{
		private byte[] bytes;

		public GLTexture1D(byte[] bytes, int width, bool genmipmaps = false)
			: base(TextureTarget.Texture1D, width, 1)
		{
			this.bytes = bytes;
			tex = GL.GenTexture();
			GL.BindTexture(TextureTarget.Texture1D, tex);
			GL.TexImage1D<byte>(TextureTarget.Texture1D, 0, PixelInternalFormat.Rgba, width, 0, OpenTK.Graphics.OpenGL.PixelFormat.Rgb, PixelType.UnsignedByte, bytes);

			if(genmipmaps)
			{
				GL.GenerateMipmap(GenerateMipmapTarget.Texture1D);
				GL.TexParameter(TextureTarget.Texture1D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapNearest);
				GL.TexParameter(TextureTarget.Texture1D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
			}
			else
			{
				GL.TexParameter(TextureTarget.Texture1D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
				GL.TexParameter(TextureTarget.Texture1D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
			}
			GL.TexParameter(TextureTarget.Texture1D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Clamp);
		}

		public byte[] Lock()
		{
			return bytes;
		}
		public void Unlock()
		{
			GL.BindTexture(TextureTarget.Texture1D, tex);
			GL.TexImage1D<byte>(TextureTarget.Texture1D, 0, PixelInternalFormat.Rgba, width, 0, OpenTK.Graphics.OpenGL.PixelFormat.Rgb, PixelType.UnsignedByte, bytes);
		}

		public void Interpolate(float x, out byte r, out byte g, out byte b)
		{
			float f = x * (float)width;
			int i = (int)f;
			if(i < 0 || i >= width)
			{
				r = g = b = 0;
				return;
			}

			f -= (float)i;
			if(i + 1 == width || f <= 0.0f)
			{
				r = bytes[i * 3 + 0];
				g = bytes[i * 3 + 1];
				b = bytes[i * 3 + 2];
				return;
			}

			float h = 1.0f - f;
			int j = i + 1;
			r = (byte)((float)bytes[i * 3 + 0] * h + (float)bytes[j * 3 + 0] * f);
			g = (byte)((float)bytes[i * 3 + 1] * h + (float)bytes[j * 3 + 1] * f);
			b = (byte)((float)bytes[i * 3 + 2] * h + (float)bytes[j * 3 + 2] * f);
		}
	}
}

