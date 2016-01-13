using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace csharp_viewer
{
	public static class COORDINATE_SYSTEM_SHADER
	{
		public const string VS = @"
			attribute vec3 vpos;
			attribute vec2 texcoord;
			uniform mat4 World;
			varying vec2 uv;

			void main()
			{
				gl_Position = World * vec4(vpos, 1.0);
				uv = texcoord;
			}
		";
		public const string FS = @"
			uniform sampler2D Texture;
			uniform int ColorMode;
			varying vec2 uv;

			void main()
			{
				vec4 clr = texture2D(Texture, uv);

				if(ColorMode == 1)
					clr = vec4(clr.g, clr.r, clr.b, clr.a);
				else if(ColorMode == 2)
					clr = vec4(clr.b, clr.g, clr.r, clr.a);

				gl_FragColor = clr;
			}
		";
	}

	public class CoordinateSystem
	{
		private GLShader sdr;
		private int sdr_colorModeParam;
		private GLTexture2D tex;

		public CoordinateSystem()
		{
			// Load shader
			sdr = new GLShader(new string[] {COORDINATE_SYSTEM_SHADER.VS}, new string[] {COORDINATE_SYSTEM_SHADER.FS});
			sdr_colorModeParam = sdr.GetUniformLocation("ColorMode");

			// Load texture
			tex = GLTexture2D.FromFile(Global.EXE_DIR + "arrow.png");
		}

		public void Draw(Vector3 pos, Matrix4 viewprojmatrix, Matrix4 vieworientmatrix, Vector3 viewpos, float fovx, SizeF backbuffersize)
		{
			Matrix4 worldmatrix = Matrix4.Identity;
			worldmatrix *= Matrix4.CreateTranslation(0.0f, -0.5f, 0.0f);
			float zoom = Vector3.TransformPosition(viewpos - pos, vieworientmatrix).Z * fovx * tex.width / backbuffersize.Width;
			worldmatrix *= Matrix4.CreateScale(zoom, zoom * (float)tex.height / (float)tex.width, zoom);

			Matrix4 movedviewprojmatrix = Matrix4.CreateTranslation(pos) * viewprojmatrix;

			Common.meshQuad.Bind(sdr, tex);

			GL.Disable(EnableCap.DepthTest);

			sdr.Bind(worldmatrix * Matrix4_CreateBillboardRotation(pos, viewpos, new Vector3(1.0f, 0.0f, 0.0f)) * movedviewprojmatrix);
			GL.Uniform1(sdr_colorModeParam, 0);
			Common.meshQuad.Draw();

			sdr.Bind(worldmatrix * Matrix4_CreateBillboardRotation(pos, viewpos, new Vector3(0.0f, 1.0f, 0.0f)) * movedviewprojmatrix);
			GL.Uniform1(sdr_colorModeParam, 1);
			Common.meshQuad.Draw();

			sdr.Bind(worldmatrix * Matrix4_CreateBillboardRotation(pos, viewpos, new Vector3(0.0f, 0.0f, 1.0f)) * movedviewprojmatrix);
			GL.Uniform1(sdr_colorModeParam, 2);
			Common.meshQuad.Draw();

			GL.Enable(EnableCap.DepthTest);
		}

		private static Matrix4 Matrix4_CreateBillboardRotation(Vector3 targetpos, Vector3 viewpos, Vector3 right)
		{
			Vector3 view = targetpos - viewpos; view.Normalize();
			Vector3 up = Vector3.Cross(right, view); right.Normalize();
			view = Vector3.Cross(up, right); up.Normalize();

			return new Matrix4(right.X, right.Y, right.Z, 0.0f,
				up.X, up.Y, up.Z, 0.0f,
				view.X, view.Y, view.Z, 0.0f,
				0.0f, 0.0f, 0.0f, 1.0f);
		}
	}
}

