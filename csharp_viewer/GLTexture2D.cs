using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace csharp_viewer
{
	public class GLTexture2D : GLTexture
	{
		public GLTexture2D(string name, Bitmap bmp, bool genmipmaps = false, bool linearfilter = false, OpenTK.Graphics.OpenGL.PixelFormat sourceformat = OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelInternalFormat destformat = PixelInternalFormat.Rgba, PixelType sourcetype = PixelType.UnsignedByte)
			: base(name, TextureTarget.Texture2D, bmp.Width, bmp.Height)
		{
			tex = GL.GenTexture();
			GL.BindTexture(TextureTarget.Texture2D, tex);

			BitmapData bmpdata = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

			GL.TexImage2D(TextureTarget.Texture2D, 0, destformat, bmpdata.Width, bmpdata.Height, 0, sourceformat, sourcetype, bmpdata.Scan0);
			bmp.UnlockBits(bmpdata);

			if(genmipmaps)
			{
				GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
				if(linearfilter)
				{
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
				}
				else
				{
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.NearestMipmapNearest);
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
				}
			}
			else
			{
				if(linearfilter)
				{
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
				}
				else
				{
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
				}
			}
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Clamp);
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Clamp);
		}
		public static GLTexture2D FromFile(string filename, bool genmipmaps = false)
		{
			if(!File.Exists(filename))
				throw new FileNotFoundException();

			return new GLTexture2D(filename, new Bitmap(filename), genmipmaps);
		}

		public GLTexture2D(string name, byte[] bytes, int width, int height, bool genmipmaps = false, OpenTK.Graphics.OpenGL.PixelFormat sourceformat = OpenTK.Graphics.OpenGL.PixelFormat.Rgb, PixelInternalFormat destformat = PixelInternalFormat.Rgba, PixelType sourcetype = PixelType.UnsignedByte)
			: base(name, TextureTarget.Texture2D, width, height)
		{
			tex = GL.GenTexture();
			GL.BindTexture(TextureTarget.Texture2D, tex);
			GL.TexImage2D(TextureTarget.Texture2D, 0, destformat, width, height, 0, sourceformat, sourcetype, bytes);

			if(genmipmaps)
			{
				GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
				GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapNearest);
				GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
			}
			else
			{
				GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
				GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
			}
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Clamp);
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Clamp);
		}
		public GLTexture2D(string name, int width, int height, bool genmipmaps = false, OpenTK.Graphics.OpenGL.PixelFormat sourceformat = OpenTK.Graphics.OpenGL.PixelFormat.Rgb, PixelInternalFormat destformat = PixelInternalFormat.Rgba, PixelType sourcetype = PixelType.UnsignedByte, bool linearfilter = false)
			: base(name, TextureTarget.Texture2D, width, height)
		{
			tex = GL.GenTexture();
			GL.BindTexture(TextureTarget.Texture2D, tex);
			GL.TexImage2D(TextureTarget.Texture2D, 0, destformat, width, height, 0, sourceformat, sourcetype, IntPtr.Zero);

			if(genmipmaps)
			{
				GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
				if(linearfilter)
				{
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
				}
				else
				{
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.NearestMipmapNearest);
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
				}
			}
			else
			{
				if(linearfilter)
				{
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
				}
				else
				{
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
				}
			}
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Clamp);
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Clamp);
		}

		public GLTexture2D(string name, int tex, int width, int height)
			: base(name, TextureTarget.Texture2D, width, height)
		{
			this.tex = tex;
		}
	}
}

