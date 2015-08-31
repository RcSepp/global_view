using System;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace csharp_viewer
{
	public class FontDefinition
	{
		public int[] charpos_x, charpos_y;

		public FontDefinition(int[] charpos_x, int[] charpos_y)
		{
			this.charpos_x = charpos_x;
			this.charpos_y = charpos_y;
		}
	}

	public static class FONT_SHADER
	{
		public static string VS = @"
				attribute vec3 vpos;
				attribute vec2 vtexcoord;
				uniform mat4 WorldViewProj;
				uniform mat3 TextureTransform;
				varying vec2 vTextureCoord;

				void main(void)
				{
					gl_Position = WorldViewProj * vec4(vpos, 1.0);
					vTextureCoord = (TextureTransform * vec3(vtexcoord, 1.0)).xy;
				}
			";
		public static string FS = @"
				varying vec2 vTextureCoord;
				uniform sampler2D Texture;
				uniform float luminance, t, texbounds[4];
				uniform vec2 InvTexSize;
				uniform vec4 Color;

				void main(void)
				{
					gl_FragColor = texture2D(Texture, vTextureCoord) * Color;
				}
			";
	}

	public class GLNumberFont
	{
		readonly GLTexture2D texture;
		readonly GLMesh meshquad;
		readonly FontDefinition fontdef;
		readonly int fixedwidth;

		private static GLShader fontshader = null;
		private static int fontshader_coloruniform;

		public GLNumberFont(string filename, FontDefinition fontDefinition, GLMesh meshquad, bool isFixedWidth)
		{
			this.texture = GLTexture2D.FromFile(filename);
			this.fontdef = fontDefinition;
			this.meshquad = meshquad;
			this.fixedwidth = /*isFixedWidth ? fontdef[0][10] / 10 :*/ 0;

			if(fontshader == null)
			{
				fontshader = new GLShader(new string[] { FONT_SHADER.VS }, new string[] { FONT_SHADER.FS });
				fontshader_coloruniform = fontshader.GetUniformLocation("Color");
			}
		}

		private Matrix3 Matrix3_CreateTranslation(float tx, float ty)
		{
			return new Matrix3(1.0f, 0.0f, 0.0f,
				0.0f, 1.0f, 0.0f,
				tx, ty, 1.0f);
		}
		private Matrix3 Matrix3_CreateScale(float sx, float sy)
		{
			return new Matrix3(sx, 0.0f, 0.0f,
				0.0f, sy, 0.0f,
				0.0f, 0.0f, 1.0f);
		}

		public void DrawString(float x, float y, int number, System.Drawing.Size backbufferSize, Color4 color)
		{
			// Fonts look best when they are drawn on integer positions (so they don't have to be interpolated over multiple pixels)
			x = (float)(int)x;
			y = (float)(int)y;

			// Bind texture and save the size of a pixel in the texture's coordinate system
			fontshader.Bind();
			GL.Uniform4(fontshader_coloruniform, color);
			meshquad.Bind(fontshader, texture);
			/*if(sdr.InvTexSizeUniform)
				gl.uniform2f(sdr.InvTexSizeUniform, 1.0 / texture.image.width, 1.0 / texture.image.height);*/

			string strnumber = number.ToString(); // Convert number to string
			for(int i = 0; i < strnumber.Length; ++i)
			{
				int digit = strnumber[i] - '0';
				Vector2 digit_pos = new Vector2(fontdef.charpos_x[digit], fontdef.charpos_y[0]);
				Vector2 digit_size = new Vector2(fontdef.charpos_x[digit + 1] - fontdef.charpos_x[digit], fontdef.charpos_y[1] - fontdef.charpos_y[0]);

				// Transform texture quad texture coordinates to select the letter
				Matrix3 texcoordtrans = Matrix3.Identity;
				texcoordtrans *= Matrix3_CreateScale(digit_size.X / texture.width, digit_size.Y / texture.height);
				texcoordtrans *= Matrix3_CreateTranslation(digit_pos.X / texture.width, digit_pos.Y / texture.height);
				GL.UniformMatrix3(fontshader.defparams.textransform, false, ref texcoordtrans);

				/*// Save digit bounds so that digits can be clamped to avoid bleeding over of neighboring digits
				if(sdr.texboundsUniform)
					gl.uniform1fv(sdr.texboundsUniform, [digit_pos[0] / texture.image.width, (digit_pos[0] + digit_size[0]) / texture.image.width,
						digit_pos[1] / texture.image.height, (digit_pos[1] + digit_size[1]) / texture.image.height]);*/

				if(fixedwidth != 0)
					digit_size.X = fixedwidth;

				// Transform texture quad vertices to position the letter
				Matrix4 trans = Matrix4.Identity;
				trans *= Matrix4.CreateScale(2.0f * digit_size.X / backbufferSize.Width, (2.0f * digit_size.Y - 4.0f) / backbufferSize.Height, 1.0f);
				trans *= Matrix4.CreateTranslation(-1.0f + 2.0f * (x + 0.5f) / backbufferSize.Width, 1.0f - 2.0f * (y + texture.height) / backbufferSize.Height, 0.0f);
				GL.UniformMatrix4(fontshader.defparams.worldviewproj, false, ref trans);

				meshquad.Draw();
				x += digit_size.X;
			}
		}
		public void DrawString(float x, float y, int number, System.Drawing.Size backbufferSize)
		{
			DrawString(x, y, number, backbufferSize, Color4.White);
		}
	}

	public class GLTextFont
	{
		readonly GLTexture2D texture;
		readonly GLMesh meshquad;
		readonly Vector2 charsize;
		readonly int fixedwidth;

		private static GLShader fontshader = null;
		private static int fontshader_coloruniform;

		public GLTextFont(string filename, Vector2 charsize, GLMesh meshquad)
		{
			this.texture = GLTexture2D.FromFile(filename);
			this.charsize = charsize;
			this.meshquad = meshquad;

			if(fontshader == null)
			{
				fontshader = new GLShader(new string[] { FONT_SHADER.VS }, new string[] { FONT_SHADER.FS });
				fontshader_coloruniform = fontshader.GetUniformLocation("Color");
			}
		}

		private Matrix3 Matrix3_CreateTranslation(float tx, float ty)
		{
			return new Matrix3(1.0f, 0.0f, 0.0f,
				0.0f, 1.0f, 0.0f,
				tx, ty, 1.0f);
		}
		private Matrix3 Matrix3_CreateScale(float sx, float sy)
		{
			return new Matrix3(sx, 0.0f, 0.0f,
				0.0f, sy, 0.0f,
				0.0f, 0.0f, 1.0f);
		}

		public void DrawString(float x, float y, string text, System.Drawing.Size backbufferSize, Color4 color)
		{
			// Fonts look best when they are drawn on integer positions (so they don't have to be interpolated over multiple pixels)
			float linestart = x = (float)(int)x;
			y = (float)(int)y;

			// Bind texture and save the size of a pixel in the texture's coordinate system
			fontshader.Bind();
			GL.Uniform4(fontshader_coloruniform, color);
			meshquad.Bind(fontshader, texture);
			/*if(sdr.InvTexSizeUniform)
				gl.uniform2f(sdr.InvTexSizeUniform, 1.0 / texture.image.width, 1.0 / texture.image.height);*/

			for(int i = 0; i < text.Length; ++i)
			{
				char chr = text[i];
				if(chr == '\n')
				{
					x = linestart;
					y += charsize.Y;
				}
				if(chr != ' ')
				{
					Vector2 charpos;
					switch(chr)
					{
					case '-': charpos = new Vector2((float)(0 * charsize.X), (float)(3 * charsize.Y)); break;
					case '/': charpos = new Vector2((float)(1 * charsize.X), (float)(3 * charsize.Y)); break;
					case ':': charpos = new Vector2((float)(2 * charsize.X), (float)(3 * charsize.Y)); break;
					case '>': charpos = new Vector2((float)(3 * charsize.X), (float)(3 * charsize.Y)); break;
					case '(': charpos = new Vector2((float)(4 * charsize.X), (float)(3 * charsize.Y)); break;
					case ')': charpos = new Vector2((float)(5 * charsize.X), (float)(3 * charsize.Y)); break;
					case '%': charpos = new Vector2((float)(6 * charsize.X), (float)(3 * charsize.Y)); break;
					case '.': charpos = new Vector2((float)(7 * charsize.X), (float)(3 * charsize.Y)); break;

					default:
						if(char.IsNumber(chr))
							charpos = new Vector2((float)(chr - '0') * charsize.X, (float)(2 * charsize.Y)); // Character is digit
						else
							charpos = new Vector2((float)(char.ToLower(chr) - 'a') * charsize.X, (float)(char.IsUpper(chr) ? 0 : 1) * charsize.Y); // Character is letter
						break;
					}

					// Transform texture quad texture coordinates to select the letter
					Matrix3 texcoordtrans = Matrix3.Identity;
					texcoordtrans *= Matrix3_CreateScale(charsize.X / texture.width, charsize.Y / texture.height);
					texcoordtrans *= Matrix3_CreateTranslation((charpos.X - 0.5f) / texture.width, (charpos.Y - 0.5f) / texture.height);
					GL.UniformMatrix3(fontshader.defparams.textransform, false, ref texcoordtrans);

					/*// Save character bounds so that characters can be clamped to avoid bleeding over of neighboring characters
					if(sdr.texboundsUniform)
						gl.uniform1fv(sdr.texboundsUniform, [charpos[0] / texture.image.width, (charpos[0] + charsize[0]) / texture.image.width,
							charpos[1] / texture.image.height, (charpos[1] + charsize[1]) / texture.image.height]);*/

					// Transform texture quad vertices to position the letter
					Matrix4 trans = Matrix4.Identity;
					trans *= Matrix4.CreateScale(2.0f * charsize.X / backbufferSize.Width, (2.0f * charsize.Y - 4.0f) / backbufferSize.Height, 1.0f);
					trans *= Matrix4.CreateTranslation(-1.0f + 2.0f * (x + 0.5f) / backbufferSize.Width, 1.0f - 2.0f * (y + charsize.Y) / backbufferSize.Height, 0.0f);
					GL.UniformMatrix4(fontshader.defparams.worldviewproj, false, ref trans);

					meshquad.Draw();
				}
				x += charsize.X;
			}
		}
		public void DrawString(float x, float y, string text, System.Drawing.Size backbufferSize)
		{
			DrawString(x, y, text, backbufferSize, Color4.White);
		}

		public Vector2 MeasureString(string text)
		{
			Vector2 size = new Vector2(0.0f, charsize.Y);
			float x = 0.0f;
			foreach(char chr in text)
			{
				if(chr == '\n')
				{
					size.X = Math.Max(size.X, x);
					size.Y += charsize.Y;
					x = 0.0f;
				}
				else
					x += charsize.X;
			}
			size.X = Math.Max(size.X, x);
			return size;
		}
	}
}

