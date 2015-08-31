using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace csharp_viewer
{
	public class GLTexture1D
	{
		public int tex;

		public GLTexture1D(byte[] bytes, int width, bool genmipmaps = false)
		{
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
		}

		public void Bind()
		{
			GL.BindTexture(TextureTarget.Texture1D, tex);
		}
	}
}

