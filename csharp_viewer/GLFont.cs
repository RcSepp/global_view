//#define DEBUG_GLFONT

using System;
using System.Drawing;

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

	public abstract class GLFont
	{
		protected abstract void DrawString(float x, float y, float w, string text, System.Drawing.Size backbufferSize, Color4 color);
		public void DrawString(float x, float y, string text, System.Drawing.Size backbufferSize, Color4 color)
		{
			DrawString(x, y, 0.0f, text, backbufferSize, color);
		}
		public void DrawString(float x, float y, string text, System.Drawing.Size backbufferSize)
		{
			DrawString(x, y, 0.0f, text, backbufferSize, Color4.White);
		}
		public void DrawStringAt(Vector3 pos, Matrix4 worldviewprojmatrix, string text, Size backbuffersize, Color4 color)
		{
			Vector3 v_screen = Vector3.TransformPerspective(pos, worldviewprojmatrix);
			if(v_screen.X > -1.1f && v_screen.X < 1.1f && v_screen.Y > -1.1f && v_screen.Y < 1.1f && v_screen.Z > 0.0f && v_screen.Z < 1.0f)
			{
				v_screen.X = (0.5f + v_screen.X / 2.0f) * (float)backbuffersize.Width;
				v_screen.Y = (0.5f - v_screen.Y / 2.0f) * (float)backbuffersize.Height;
				DrawString(v_screen.X, v_screen.Y, v_screen.Z, text, backbuffersize, color);
			}
		}
		public void DrawStringAt(Vector3 pos, Matrix4 worldviewprojmatrix, string text, Size backbuffersize)
		{
			DrawStringAt(pos, worldviewprojmatrix, text, backbuffersize, Color4.White);
		}
		public abstract Vector2 MeasureString(string text);
	}

	public class GLNumberFont : GLFont
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

		protected override void DrawString(float x, float y, float w, string strnumber, System.Drawing.Size backbufferSize, Color4 color)
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

			for(int i = 0; i < strnumber.Length; ++i)
			{
				int digit = strnumber[i] - '0';
				Vector2 digit_pos = new Vector2(fontdef.charpos_x[digit], fontdef.charpos_y[0]);
				Vector2 digit_size = new Vector2(fontdef.charpos_x[digit + 1] - fontdef.charpos_x[digit], fontdef.charpos_y[1] - fontdef.charpos_y[0]);

				// Transform texture quad texture coordinates to select the letter
				Matrix3 texcoordtrans = Matrix3.Identity;
				//texcoordtrans *= Matrix3_CreateScale(digit_size.X / texture.width, digit_size.Y / texture.height);
				texcoordtrans *= GLTextFont.Matrix3_CreateScale(digit_size.X / texture.width, (digit_size.Y - 2.0f) / texture.height);
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
				trans *= Matrix4.CreateTranslation(-1.0f + 2.0f * (x + 0.5f) / backbufferSize.Width, 1.0f - 2.0f * (y + texture.height) / backbufferSize.Height, w);
				GL.UniformMatrix4(fontshader.defparams.worldviewproj, false, ref trans);

				meshquad.Draw();
				x += digit_size.X;
			}
		}
		public void DrawString(float x, float y, int number, System.Drawing.Size backbufferSize, Color4 color)
		{
			DrawString(x, y, number.ToString(), backbufferSize, color);
		}

		public override Vector2 MeasureString(string strnumber)
		{
			return new Vector2(fixedwidth * strnumber.Length, fontdef.charpos_y[1] - fontdef.charpos_y[0]);
		}
	}

	public class GLTextFont : GLFont
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

		public static Matrix3 Matrix3_CreateTranslation(float tx, float ty)
		{
			return new Matrix3(1.0f, 0.0f, 0.0f,
				0.0f, 1.0f, 0.0f,
				tx, ty, 1.0f);
		}
		public static Matrix3 Matrix3_CreateScale(float sx, float sy)
		{
			return new Matrix3(sx, 0.0f, 0.0f,
				0.0f, sy, 0.0f,
				0.0f, 0.0f, 1.0f);
		}

		protected override void DrawString(float x, float y, float w, string text, System.Drawing.Size backbufferSize, Color4 color)
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
					//texcoordtrans *= Matrix3_CreateScale(charsize.X / texture.width, charsize.Y / texture.height);
					texcoordtrans *= GLTextFont.Matrix3_CreateScale(charsize.X / texture.width, (charsize.Y - 2.0f) / texture.height);
					texcoordtrans *= Matrix3_CreateTranslation((charpos.X - 0.5f) / texture.width, (charpos.Y - 0.5f) / texture.height);
					GL.UniformMatrix3(fontshader.defparams.textransform, false, ref texcoordtrans);

					/*// Save character bounds so that characters can be clamped to avoid bleeding over of neighboring characters
					if(sdr.texboundsUniform)
						gl.uniform1fv(sdr.texboundsUniform, [charpos[0] / texture.image.width, (charpos[0] + charsize[0]) / texture.image.width,
							charpos[1] / texture.image.height, (charpos[1] + charsize[1]) / texture.image.height]);*/

					// Transform texture quad vertices to position the letter
					Matrix4 trans = Matrix4.Identity;
					trans *= Matrix4.CreateScale(2.0f * charsize.X / backbufferSize.Width, (2.0f * charsize.Y - 4.0f) / backbufferSize.Height, 1.0f);
					trans *= Matrix4.CreateTranslation(-1.0f + 2.0f * (x + 0.5f) / backbufferSize.Width, 1.0f - 2.0f * (y + charsize.Y) / backbufferSize.Height, w);
					GL.UniformMatrix4(fontshader.defparams.worldviewproj, false, ref trans);

					meshquad.Draw();
				}
				x += charsize.X;
			}
		}

		public override Vector2 MeasureString(string text)
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

	public class GLTextFont2 : GLFont
	{
		private const int CHARMAP_PADDING = 2;
		private const int CHARMAP_CHAR_SIZE_INFLATE = 2;
		private const int CHARMAP_CHAR_DIST_INFLATE = 2;

		private GLTexture2D texture;
		private Rectangle[] charBounds = new Rectangle[256];
		private int lineHeight, blankWidth;

		private static GLShader fontshader = null;
		private static int fontshader_coloruniform;

		public GLTextFont2(Font font)
		{
			if(fontshader == null)
			{
				fontshader = new GLShader(new string[] { FONT_SHADER.VS }, new string[] { FONT_SHADER.FS });
				fontshader_coloruniform = fontshader.GetUniformLocation("Color");
			}

			/*string charmap = "";
			for(int i = 0; i < 255; ++i)
				if(!char.IsControl((char)i))
				charmap += (char)i;

			Bitmap bmp = new Bitmap(1, 1);
			Graphics gfx = Graphics.FromImage(bmp);
			Size charmapSize = gfx.MeasureString(charmap, font, 1024).ToSize();
			bmp = new Bitmap(charmapSize.Width, charmapSize.Height);
			gfx = Graphics.FromImage(bmp);
			gfx.DrawString(charmap, font, Brushes.White, new RectangleF(0.0f, 0.0f, (float)charmapSize.Width, (float)charmapSize.Height));
			gfx.Flush();
			bmp.Save("charmap.png");*/

			int x = CHARMAP_PADDING, y = CHARMAP_PADDING, lineMaxHeight = 0;
			lineHeight = 0;
			Bitmap bmp = new Bitmap(1, 1);
			Graphics gfx = Graphics.FromImage(bmp);
			for(int i = 0; i < 255; ++i)
				if(!char.IsControl((char)i))
				{
					Size charmapSize = gfx.MeasureString(new string((char)i, 1), font).ToSize();
					charmapSize.Width += CHARMAP_CHAR_SIZE_INFLATE;
					charmapSize.Height += CHARMAP_CHAR_SIZE_INFLATE;
					charBounds[i] = new Rectangle(x, y, charmapSize.Width, charmapSize.Height);
					x += charmapSize.Width + CHARMAP_CHAR_DIST_INFLATE;
					lineMaxHeight = Math.Max(lineMaxHeight, charmapSize.Height);

					if(x > 1024)
					{
						y += lineMaxHeight + CHARMAP_CHAR_DIST_INFLATE;
						x = CHARMAP_PADDING + charmapSize.Width + CHARMAP_CHAR_DIST_INFLATE;
						lineHeight = Math.Max(lineHeight, lineMaxHeight);
						lineMaxHeight = charmapSize.Height;
						charBounds[i].X = CHARMAP_PADDING;
						charBounds[i].Y += lineMaxHeight;
					}
				}
				else
					charBounds[i] = Rectangle.Empty;
			lineHeight = Math.Max(lineHeight, lineMaxHeight);

			blankWidth = (int)Math.Ceiling(gfx.MeasureString(" ", font).Width);

			#if !DEBUG_GLFONT
			string bmp_filename = "charmap_" + font.FontFamily.Name.Replace(" ", "") + "_" + font.Size + ".png";
			if(System.IO.File.Exists(bmp_filename))
				bmp = (Bitmap)Bitmap.FromFile(bmp_filename);
			else
			{
				#endif
				bmp = new Bitmap(1024, y + lineMaxHeight);
				gfx = Graphics.FromImage(bmp);
				#if DEBUG_GLFONT
				gfx.Clear(Color.Black);
				#endif
				gfx.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias; // Do not use ClearType
				for(int i = 0; i < 255; ++i)
					if(!char.IsControl((char)i))
						gfx.DrawString(new string((char)i, 1), font, Brushes.White, new RectangleF((float)charBounds[i].X - 1, (float)charBounds[i].Y - 2, (float)charBounds[i].Width, (float)charBounds[i].Height));
				gfx.Flush();
				#if DEBUG_GLFONT
				bmp.Save("charmap_" + font.FontFamily.Name.Replace(" ", "") + "_" + font.Size + "_debug.png");
				#else
				bmp.Save("charmap_" + font.FontFamily.Name.Replace(" ", "") + "_" + font.Size + ".png");
			}
			#endif

			texture = new GLTexture2D(bmp);
		}

		protected override void DrawString(float x, float y, float w, string text, System.Drawing.Size backbufferSize, Color4 color)
		{
			// Fonts look best when they are drawn on integer positions (so they don't have to be interpolated over multiple pixels)
			float linestart = x = (float)(int)x;
			y = (float)(int)y;

			// Bind texture and save the size of a pixel in the texture's coordinate system
			fontshader.Bind();
			GL.Uniform4(fontshader_coloruniform, color);
			Common.meshQuad.Bind(fontshader, texture);
			/*if(sdr.InvTexSizeUniform)
				gl.uniform2f(sdr.InvTexSizeUniform, 1.0 / texture.image.width, 1.0 / texture.image.height);*/

			for(int i = 0; i < text.Length; ++i)
			{
				char chr = text[i];
				if(chr == '\n')
				{
					x = linestart;
					y += (float)lineHeight;
				}
				if(!char.IsControl(chr))
				{
					Vector2 charsize = new Vector2((float)charBounds[(int)chr].Width, (float)charBounds[(int)chr].Height);

					// Transform texture quad texture coordinates to select the letter
					Matrix3 texcoordtrans = Matrix3.Identity;
					texcoordtrans *= GLTextFont.Matrix3_CreateScale(charsize.X / texture.width, (charsize.Y - 2.0f) / texture.height);
					texcoordtrans *= GLTextFont.Matrix3_CreateTranslation(((float)charBounds[(int)chr].X - 0.5f) / texture.width, ((float)charBounds[(int)chr].Y - 0.5f) / texture.height);
					GL.UniformMatrix3(fontshader.defparams.textransform, false, ref texcoordtrans);

					/*// Save character bounds so that characters can be clamped to avoid bleeding over of neighboring characters
					if(sdr.texboundsUniform)
						gl.uniform1fv(sdr.texboundsUniform, [charpos[0] / texture.image.width, (charpos[0] + charsize[0]) / texture.image.width,
							charpos[1] / texture.image.height, (charpos[1] + charsize[1]) / texture.image.height]);*/

					// Transform texture quad vertices to position the letter
					Matrix4 trans = Matrix4.Identity;
					trans *= Matrix4.CreateScale(2.0f * charsize.X / backbufferSize.Width, (2.0f * charsize.Y - 4.0f) / backbufferSize.Height, 1.0f);
					trans *= Matrix4.CreateTranslation(-1.0f + 2.0f * (x + 0.5f) / backbufferSize.Width, 1.0f - 2.0f * (y - 0.25f + charsize.Y) / backbufferSize.Height, w);
					GL.UniformMatrix4(fontshader.defparams.worldviewproj, false, ref trans);

					Common.meshQuad.Draw();
					x += charsize.X;
				}
				else
					x += blankWidth;
			}
		}

		public override Vector2 MeasureString(string text)
		{
			Vector2 size = new Vector2(0.0f, (float)lineHeight);
			float x = 0.0f;
			foreach(char chr in text)
			{
				if(chr == '\n')
				{
					size.X = Math.Max(size.X, x);
					size.Y += lineHeight;
					x = 0.0f;
				}
				else if(!char.IsControl(chr))
					x += (float)charBounds[(int)chr].Width;
				else
					x += blankWidth;
			}
			size.X = Math.Max(size.X, x);
			return size;
		}
	}
}

