//#define ENABLE_PROFILING

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

		public static int allocatedTextureCounter = 0;
		#if ENABLE_PROFILING
		public static System.Collections.Generic.Dictionary<GLTexture, string> allocatedTextureNames = new System.Collections.Generic.Dictionary<GLTexture, string>();
		#endif
		public static void PrintAllocatedTextureNames()
		{
			#if ENABLE_PROFILING
			foreach(string textureName in allocatedTextureNames.Values)
				Global.cle.PrintOutput(textureName);
			#endif
		}

		public GLTexture(string name, TextureTarget type, int width, int height)
		{
			#if ENABLE_PROFILING
			allocatedTextureCounter++;
			allocatedTextureNames.Add(this, name);
			#endif

			this.type = type;
			this.width = width;
			this.height = height;
		}

		public void Bind()
		{
			GL.BindTexture(type, tex);
		}

		public void Dispose()
		{
			if(tex != -1)
			{
				GL.DeleteTexture(tex);
				#if ENABLE_PROFILING
				allocatedTextureCounter--;
				allocatedTextureNames.Remove(this);
				#endif
			}
		}
	}
}

