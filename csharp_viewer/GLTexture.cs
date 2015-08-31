using System;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace csharp_viewer
{
	public class GLTexture
	{
		public int tex = -1;
		public readonly int width, height;
		public readonly TextureTarget type;

		public GLTexture(TextureTarget type, int width, int height)
		{
			this.type = type;
			this.width = width;
			this.height = height;
		}

		public void Bind()
		{
			GL.BindTexture(type, tex);
		}
	}
}

