using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Xml;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace csharp_viewer
{
	public class ColorTableManager : GLControl
	{
		private static int COLOR_TABLE_SIZE = 256;
		private static int FINAL_COLORMAP_SIZE = 1024;

		/*private static GLShader colortableshader = null;
		private static class COLOR_TABLE_SHADER
		{
			public const string VS = @"
				attribute vec3 vpos;
				attribute vec2 vtexcoord;
				varying float value;
				uniform mat4 World;

				void main()
				{
					gl_Position = World * vec4(vpos, 1.0);
					value = vtexcoord.x;
				}
			";
			public const string FS = @"
				varying float value;
				uniform sampler1D InnerColorTable, OuterColorTable;
				uniform int HasOuterColorTable;
				uniform float MinValue, MaxValue;

				void main()
				{
					if(value < MinValue)
						gl_FragColor = HasOuterColorTable != 0 ? vec4(texture1D(OuterColorTable, value).rgb, 1.0) : vec4(texture1D(InnerColorTable, 0.0).rgb, 1.0);
					else if(value > MaxValue)
						gl_FragColor = HasOuterColorTable != 0 ? vec4(texture1D(OuterColorTable, value).rgb, 1.0) : vec4(texture1D(InnerColorTable, 1.0 - 1e-5).rgb, 1.0);
					else
						gl_FragColor = vec4(texture1D(InnerColorTable, (value - MinValue) / (MaxValue - MinValue)).rgb, 1.0);
				}
			";
		}*/

		private static GLShader lineshader = null;
		private static class LINE_SHADER
		{
			public const string VS = @"
				attribute vec3 vpos;
				uniform mat4 World;

				void main()
				{
					gl_Position = World * vec4(vpos, 1.0);
				}
			";
			public const string FS = @"
				void main()
				{
					gl_FragColor = vec4(1.0, 1.0, 1.0, 1.0);
				}
			";
		}

		private static GLShader cmpreviewshader = null;
		private static class COLORMAP_PREVIEW_SHADER
		{
			public const string VS = @"
				attribute vec3 vpos;
				attribute vec2 vtexcoord;
				varying float value;
				uniform mat4 World;

				void main()
				{
					value = vtexcoord.x;
					gl_Position = World * vec4(vpos, 1.0);
				}
			";
			public const string FS = @"
				varying float value;
				uniform sampler1D Texture;

				void main()
				{
					gl_FragColor = vec4(texture1D(Texture, value).rgb, 1.0);
				}
			";
		}

		/*public class GLButton
		{
			private GLTexture2D tex;
			private Size backbuffersize = Size.Empty;
			private Action clickAction;
			public EventHandler Click;
			public Rectangle bounds;
			public readonly AnchorStyles anchor;

			public GLButton(string texfilename, Rectangle bounds, AnchorStyles anchor, string actionname, string actiondesc)
			{
				this.anchor = anchor;

				tex = GLTexture2D.FromFile(texfilename, false);
				clickAction = ActionManager.CreateAction(actiondesc, actionname, this, "ClickInternal");

				if(bounds.Width <= 0)
					bounds.Width = tex.width;
				if(bounds.Height <= 0)
					bounds.Height = tex.height;
				this.bounds = bounds;
			}

			public void Draw()
			{
				Matrix4 trans = Matrix4.Identity;
				trans *= Matrix4.CreateScale(2.0f * bounds.Width / backbuffersize.Width, (2.0f * bounds.Height - 4.0f) / backbuffersize.Height, 1.0f);
				trans *= Matrix4.CreateTranslation(-1.0f + 2.0f * (bounds.Left + 0.5f) / backbuffersize.Width, 1.0f - 2.0f * (bounds.Bottom - 0.5f) / backbuffersize.Height, 0.0f);

				Common.sdrTextured.Bind(trans);
				Common.meshQuad.Bind(colortableshader, tex);
				Common.meshQuad.Draw();
			}

			public void OnSizeChanged(Size backbuffersize)
			{
				if(this.backbuffersize.Width > 0 && this.backbuffersize.Height > 0)
				{
					// Adjust bounds to top-left anchor
					if((anchor & AnchorStyles.Right) != 0)
						bounds.X = this.backbuffersize.Width - bounds.Right;
					if((anchor & AnchorStyles.Bottom) != 0)
						bounds.Y = this.backbuffersize.Height - bounds.Bottom;
				}

				this.backbuffersize = backbuffersize;

				// Adjust bounds to anchor
				if((anchor & AnchorStyles.Right) != 0)
					bounds.X = backbuffersize.Width - bounds.Right;
				if((anchor & AnchorStyles.Bottom) != 0)
					bounds.Y = backbuffersize.Height - bounds.Bottom;
			}

			public void PerformClick()
			{
				ActionManager.Do(clickAction);
			}
			private void ClickInternal()
			{
				if(Click != null)
					Click(this, null);
			}

			public bool MouseDown(MouseEventArgs e)
			{
				if(bounds.Contains(e.Location))
				{
					PerformClick();
					return true;
				}
				else
					return false;
			}
		}*/

		public class GLCursor
		{
			private readonly GLTexture2D tex;
			private readonly Point hotspot;
			private Size backbuffersize = Size.Empty;
			public bool visible = false;

			public GLCursor(string texfilename, Point hotspot)
			{
				this.tex = GLTexture2D.FromFile(texfilename, false);
				this.hotspot = hotspot;
			}

			public void Draw(Point mousepos)
			{
				if(visible)
				{
					mousepos.X -= hotspot.X;
					mousepos.Y -= hotspot.Y;

					Matrix4 trans = Matrix4.Identity;
					trans *= Matrix4.CreateScale(2.0f * tex.width / backbuffersize.Width, (2.0f * tex.height - 4.0f) / backbuffersize.Height, 1.0f);
					trans *= Matrix4.CreateTranslation(-1.0f + 2.0f * (mousepos.X + 0.5f) / backbuffersize.Width, 1.0f - 2.0f * (mousepos.Y + tex.height - 0.5f) / backbuffersize.Height, 0.0f);

					Common.sdrTextured.Bind(trans);
					Common.meshQuad.Bind(Common.sdrTextured, tex);
					Common.meshQuad.Draw();
				}
			}

			public void OnSizeChanged(Size backbuffersize)
			{
				this.backbuffersize = backbuffersize;
			}
		}

		public class NamedColorTable
		{
			public GLTexture1D tex;
			public Vector3 nanColor;
			public string name, groupname;

			public NamedColorTable(GLTexture1D tex, Vector3 nanColor, string name, string groupname)
			{
				this.name = name;
				this.groupname = groupname;
				this.nanColor = nanColor;
				this.tex = tex;
			}
			public static NamedColorTable None = new NamedColorTable(null, Vector3.Zero, "none", "none");

			public override string ToString()
			{
				return name;
			}
		}

		private class ColorMapPicker
		{
			private const int HEADER_HEIGHT = 30;
			private const int ROW_HEIGHT = 18;

			private const int COLORMAP_LEFT = 2;
			private const int COLORMAP_TOP = 2;
			private const int COLORMAP_HEIGHT = 16;
			private const float COLORMAP_RELATIVE_WIDTH = 0.3f; // Colormap width in % of cell width

			public Rectangle bounds;
			private Size backbuffersize = Size.Empty;
			private class ColorMapGroup
			{
				public string name;
				public int width;
				public List<NamedColorTable> colormaps;

				public ColorMapGroup(string groupname)
				{
					this.name = groupname;
					this.width = 0;
					this.colormaps = new List<NamedColorTable>();
				}
			}
			private Dictionary<string, ColorMapGroup> groups = new Dictionary<string, ColorMapGroup>();
			private NamedColorTable highlightedColormap = null;
			private int totalwidth = 0, totalheight = 0;
			public GLFont font;

			public delegate void ColormapDelegate(NamedColorTable colormap);
			public event ColormapDelegate ColormapDragStart;

			public ColorMapPicker(GLFont font)
			{
				this.font = font;
			}

			public void AddColorMap(NamedColorTable colormap)
			{
				ColorMapGroup group;
				if(!groups.TryGetValue(colormap.groupname, out group))
					groups.Add(colormap.groupname, group = new ColorMapGroup(colormap.groupname));
				group.colormaps.Add(colormap);

				// Update group width and total width
				int newwidth = (int)Math.Ceiling(font.MeasureString(colormap.name).X);
				if(newwidth > group.width)
				{
					group.width = newwidth;

					totalwidth = 0;
					foreach(ColorMapGroup g in groups.Values)
						totalwidth += g.width;
				}
				totalheight = Math.Max(totalheight, HEADER_HEIGHT + group.colormaps.Count * ROW_HEIGHT + 4);
			}

			public void Draw()
			{
				GL.Enable(EnableCap.ScissorTest);
				GL.Scissor(bounds.Left, backbuffersize.Height - bounds.Bottom, bounds.Width, bounds.Height);

				// Draw solid color background
				Matrix4 trans = Matrix4.Identity;
				trans *= Matrix4.CreateScale(2.0f * bounds.Width / backbuffersize.Width, (2.0f * totalheight - 4.0f) / backbuffersize.Height, 1.0f);
				trans *= Matrix4.CreateTranslation(-1.0f + 2.0f * (bounds.Left + 0.5f) / backbuffersize.Width, 1.0f - 2.0f * (bounds.Top + totalheight - 0.5f) / backbuffersize.Height, 0.0f);
				Common.sdrSolidColor.Bind(trans);
				GL.Uniform4(Common.sdrSolidColor_colorUniform, new Color4(239, 239, 239, 255));
				Common.meshQuad.Bind(Common.sdrSolidColor);
				Common.meshQuad.Draw();

				int cell_x = bounds.Left, label_x, y = bounds.Top;
				foreach(ColorMapGroup group in groups.Values)
				{
					// Compute scaled group width and label x
					float width = (float)group.width * (float)backbuffersize.Width / (float)totalwidth;
					label_x = cell_x + 2 * COLORMAP_LEFT + (int)Math.Ceiling(COLORMAP_RELATIVE_WIDTH * width);

					// Draw table header
					font.DrawString((float)cell_x, (float)y, group.name, bounds.Size, new Color4(52, 52, 52, 255));
					y += HEADER_HEIGHT;

					trans = Matrix4.Identity;
					trans *= Matrix4.CreateScale(2.0f * COLORMAP_RELATIVE_WIDTH * width / backbuffersize.Width, (2.0f * (float)COLORMAP_HEIGHT - 4.0f) / backbuffersize.Height, 1.0f);
					trans *= Matrix4.CreateTranslation(-1.0f + 2.0f * ((float)(cell_x + COLORMAP_LEFT) + 0.5f) / backbuffersize.Width, 1.0f - 2.0f * ((float)(y + COLORMAP_TOP + COLORMAP_HEIGHT) - 0.5f) / backbuffersize.Height, 0.0f);

					// Draw cells
					foreach(NamedColorTable colormap in group.colormaps)
					{
						if(colormap != highlightedColormap)
						{
							// Draw colormap name
							font.DrawString((float)label_x, (float)y + 1.0f, colormap.name, backbuffersize, new Color4(38, 38, 38, 255));
						}

						// Draw colormap
						cmpreviewshader.Bind(colormap == highlightedColormap ? Matrix4.CreateScale(3.0f, 1.0f, 1.0f) * trans : trans);
						Common.meshQuad.Bind(cmpreviewshader, colormap.tex);
						Common.meshQuad.Draw();

						// Advance to next row
						y += ROW_HEIGHT;
						trans *= Matrix4.CreateTranslation(0.0f, -2.0f * (float)ROW_HEIGHT / backbuffersize.Height, 0.0f);
					}

					// Advance to next column
					cell_x += (int)Math.Ceiling(width);
					y = bounds.Top;
				}

				GL.Disable(EnableCap.ScissorTest);
			}

			public void OnSizeChanged(Size backbuffersize)
			{
				this.backbuffersize = backbuffersize;
			}

			private NamedColorTable ColorMapFromPoint(Point pos)
			{
				if(pos.X < bounds.Left || pos.Y < bounds.Top + HEADER_HEIGHT)
					return null;

				int cell_right = bounds.Left, y = bounds.Top;
				foreach(ColorMapGroup group in groups.Values)
				{
					// Add scaled group width to cell_right
					cell_right += (int)Math.Ceiling((float)group.width * (float)backbuffersize.Width / (float)totalwidth);

					if(pos.X < cell_right)
					{
						int colormapidx = (pos.Y - bounds.Top - HEADER_HEIGHT) / ROW_HEIGHT;
						return colormapidx < group.colormaps.Count ? group.colormaps[colormapidx] : null;
					}
				}

				return null;
			}

			public bool MouseDown(MouseEventArgs e)
			{
				if(bounds.Contains(e.Location) && e.Location.Y < bounds.Top + totalheight)
				{
					NamedColorTable colormap;
					if(ColormapDragStart != null && (colormap = ColorMapFromPoint(e.Location)) != null)
						ColormapDragStart(colormap);
					return true;
				}
				else
					return false;
			}

			public bool MouseMove(MouseEventArgs e)
			{
				if(bounds.Contains(e.Location))
				{
					highlightedColormap = ColorMapFromPoint(e.Location);
					return true;
				}
				else
				{
					highlightedColormap = null;
					return false;
				}
			}
		}
		private ColorMapPicker picker;
		private bool pickerVisible = false;
		public NamedColorTable draggedColormap = null;

		private class Section
		{
			public NamedColorTable colorMap;
			public Splitter start, end;
			public float startValue, endValue;
			public bool flipped, interjector;
		}
		private class Splitter
		{
			public bool isfixed, interjector;
			public float pos;
			public Section left, right;
		}

		private class InputSection : GLControl
		{
			private readonly ColorTableManager colorTableMgr;
			private readonly GLFont font;

			private GLMesh meshLines, meshHistogram;
			private GLTexture2D texSplitter, texInterjectorLeft, texInterjectorRight;

			private List<Section> sections;
			private List<Splitter> splitters;

			private GLTexture1D colormapTexture;
			public GLTexture1D Colormap { get { return colormapTexture;} }
			private Color4 colormapNanColor;
			private float colormapUpdateStart = float.MaxValue, colormapUpdateEnd = float.MinValue;

			private Matrix4 transform = Matrix4.Identity, invtransform = Matrix4.Identity;

			private int dragSplitterIndex = -1;

			private Section dragPointSection = null;
			private enum DragPoint {None, Start, End};
			private DragPoint dragPoint = DragPoint.None;
			private float dragOffset, dragOffset2;

			public Action InsertSplitterPinAction, InsertNestedPinAction, MovePinAction, SetSectionColormapAction, ResetColorTableAction;

			public InputSection(ColorTableManager colorTableMgr, GLFont font)
			{
				this.colorTableMgr = colorTableMgr;
				this.font = font;

				InsertSplitterPinAction = ActionManager.CreateAction("Insert splitter pin", this, "InsertSplitterPin");
				InsertNestedPinAction = ActionManager.CreateAction("Insert nesting pin", this, "InsertNestedPin");
				MovePinAction = ActionManager.CreateAction("Move pin", this, "MovePin");
				SetSectionColormapAction = ActionManager.CreateAction("Set colormap of section", this, "SetSectionColormap");
				ResetColorTableAction = ActionManager.CreateAction("Reset colormap", this, "ResetColorTable");

				// Create colormap
				colormapTexture = new GLTexture1D(new byte[3 * FINAL_COLORMAP_SIZE], FINAL_COLORMAP_SIZE, false);

				// Create histogram grid as line list mesh
				List<Vector3> positions = new List<Vector3>();
				/*positions.AddRange(new Vector3[] {
					// Outline
					new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.5f, 0.0f),
					new Vector3(0.0f, 0.5f, 0.0f), new Vector3(1.0f, 0.5f, 0.0f),
					new Vector3(1.0f, 0.5f, 0.0f), new Vector3(1.0f, 0.0f, 0.0f),
					new Vector3(1.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f)
				});*/
				for(int i = 0; i <= 10; ++i)
				{
					float y = (float)i / 10.0f * 0.9f + 0.1f;
					positions.Add(new Vector3(0.0f, y, 0.0f));
					positions.Add(new Vector3(1.0f, y, 0.0f));
				}
				for(int i = 0; i <= 10; ++i)
				{
					float x = (float)i / 10.0f;
					positions.Add(new Vector3(x, 0.1f, 0.0f));
					positions.Add(new Vector3(x, 1.0f, 0.0f));
				}
				meshLines = new GLMesh(positions.ToArray(), null, null, null, null, null, PrimitiveType.Lines);

				// Create textures
				texSplitter = GLTexture2D.FromFile("splitter.png", false);
				texInterjectorLeft = GLTexture2D.FromFile("interjectorLeft.png", false);
				texInterjectorRight = GLTexture2D.FromFile("interjectorRight.png", false);

				// Create number font
				//font = new GLNumberFont("HelveticaNeue_12.png", new FontDefinition(new int[] {0, 14, 26, 39, 53, 67, 80, 93, 106, 120, 133}, new int[] {0, 19}), Common.meshQuad, true);
				//font = new GLNumberFont("HelveticaNeue_16.png", new FontDefinition(new int[] {0, 18, 34, 53, 71, 89, 106, 124, 142, 160, 178}, new int[] {0, 25}), true);
				//font = new GLTextFont("fntDefault.png", new Vector2(19.0f, 32.0f), Common.meshQuad);
			}

			protected override void Draw(float dt, Matrix4 _transform)
			{
				GL.Enable(EnableCap.ScissorTest);
				GL.Scissor(Bounds.Left, BackbufferSize.Height - Bounds.Bottom, Bounds.Width + 1, Bounds.Height + 1);

				if(colormapUpdateEnd >= colormapUpdateStart)
				{
					// Create colormap from sections
					List<Section>.Enumerator sectionEnum = sections.GetEnumerator();
					if(sectionEnum.MoveNext())
					{
						byte[] colormapBytes = colormapTexture.Lock();
						for(int x = Math.Max(0, (int)Math.Floor(colormapUpdateStart * colormapTexture.width)), end = Math.Min(colormapTexture.width - 1, (int)Math.Ceiling(colormapUpdateEnd * colormapTexture.width)); x < end; ++x)
						{
							float xr = ((float)x + 0.5f) / (float)colormapTexture.width;

							/*if(xr >= sectionEnum.Current.end.pos && !sectionEnum.MoveNext())
							break;*/
							sectionEnum = sections.GetEnumerator();
							sectionEnum.MoveNext();
							while(xr < sectionEnum.Current.start.pos || xr >= sectionEnum.Current.end.pos)
								if(!sectionEnum.MoveNext())
									goto endColormapCreation;

							xr = (xr - sectionEnum.Current.start.pos) / (sectionEnum.Current.end.pos - sectionEnum.Current.start.pos);
							sectionEnum.Current.colorMap.tex.Interpolate(xr, out colormapBytes[x * 3 + 0], out colormapBytes[x * 3 + 1], out colormapBytes[x * 3 + 2]);
						}
						endColormapCreation:
						colormapTexture.Unlock();
					}
					colormapTexture.Unlock();

					// Reset colormap update region
					colormapUpdateStart = float.MaxValue;
					colormapUpdateEnd = float.MinValue;
				}

				Matrix4 trans;
				trans = Matrix4.CreateScale(2.0f * Bounds.Width / BackbufferSize.Width, (2.0f * Bounds.Height * 0.1f - 4.0f) / BackbufferSize.Height, 1.0f);
				trans *= Matrix4.CreateTranslation(-1.0f + 2.0f * (Bounds.Left + 0.5f) / BackbufferSize.Width, 1.0f - 2.0f * (Bounds.Bottom + 0.5f) / BackbufferSize.Height, 0.0f);

				trans *= Matrix4.CreateScale((float)BackbufferSize.Width / (float)Bounds.Width, 1.0f, 1.0f);
				trans *= transform;
				trans *= Matrix4.CreateScale((float)Bounds.Width / (float)BackbufferSize.Width, 1.0f, 1.0f);

				cmpreviewshader.Bind(trans);
				Common.meshQuad.Bind(cmpreviewshader, colormapTexture);
				Common.meshQuad.Draw();


				trans = Matrix4.CreateScale(2.0f * Bounds.Width / BackbufferSize.Width, (2.0f * Bounds.Height - 4.0f) / BackbufferSize.Height, 1.0f);
				trans *= Matrix4.CreateTranslation(-1.0f + 2.0f * (Bounds.Left + 0.5f) / BackbufferSize.Width, 1.0f - 2.0f * (Bounds.Bottom + 0.5f) / BackbufferSize.Height, 0.0f);

				trans *= Matrix4.CreateScale((float)BackbufferSize.Width / (float)Bounds.Width, 1.0f, 1.0f);
				trans *= transform;
				trans *= Matrix4.CreateScale((float)Bounds.Width / (float)BackbufferSize.Width, 1.0f, 1.0f);

				lineshader.Bind(trans);
				meshLines.Bind(lineshader, null);
				meshLines.Draw();
				if(meshHistogram != null)
				{
					meshHistogram.Bind(lineshader, null);
					meshHistogram.Draw();
				}

				foreach(Splitter splitter in splitters)
					if(!splitter.isfixed)
					{
						if(splitter.left == null)
							drawSplitter(splitter.pos, texInterjectorLeft);
						else if(splitter.right == null)
							drawSplitter(splitter.pos, texInterjectorRight);
						else
							drawSplitter(splitter.pos, texSplitter);
					}

				//Common.fontText.DrawString(Bounds.Left - 10, Bounds.Top - Bounds.Height, "0%", BackbufferSize);
				//Common.fontText.DrawString(Bounds.Right - 20, Bounds.Top - Bounds.Height, "100%", BackbufferSize);
				for(int i = 0; i <= 100; i += 10)
				{
					Vector3 vpos = Vector3.Transform(new Vector3((float)i / 100.0f, 0.0f, 0.0f), trans);
					font.DrawString((int)((vpos.X + 1.0f) * BackbufferSize.Width / 2.0f), Bounds.Top, i.ToString() + "%", BackbufferSize);
				}

				GL.Disable(EnableCap.ScissorTest);
			}

			private void drawSplitter(float pos, GLTexture2D tex) // pos = 0.0f ... 1.0f
			{
				Vector3 vpos = Vector3.Transform(new Vector3(2.0f * pos - 1.0f, -1.0f, 0.0f), transform);
				vpos.X *= (float)Bounds.Width / (float)BackbufferSize.Width;
				//vpos.X += (float)Bounds.Left / (float)BackbufferSize.Width;

				vpos.Y *= (float)Bounds.Height / (float)BackbufferSize.Height;
				vpos.Y -= (float)Bounds.Top / (float)BackbufferSize.Height;

				Matrix4 mattrans;
				mattrans = Matrix4.Identity;
				mattrans *= Matrix4.CreateTranslation(-0.5f, 0.3f, 0.0f);
				mattrans *= Matrix4.CreateScale((float)tex.width / (float)BackbufferSize.Width, (float)tex.height / (float)BackbufferSize.Height, 1.0f);
				mattrans *= Matrix4.CreateTranslation(vpos);
				Common.sdrTextured.Bind(mattrans);

				Common.meshQuad.Bind(Common.sdrTextured, tex);
				Common.meshQuad.Draw();
			}

			private Vector3 tmd0 = new Vector3();
			public new bool MouseDown(MouseEventArgs e)
			{
				if(!Bounds.Contains(e.Location))
					return false;

				float xr = Math.Min(Math.Max((float)(e.X - Bounds.X) / (float)Bounds.Width, 0.0f), 1.0f);

				if(colorTableMgr.GetCursor() == CursorType.MovePin)
				{
					Vector3 vpos = Vector3.Transform(new Vector3(2.0f * xr - 1.0f, 0.0f, 0.0f), invtransform);
					xr = vpos.X / 2.0f + 0.5f;

					//EDIT: Show trash button

					float dragSplitterDistance = float.MaxValue;
					int splitterIndex = 0;
					foreach(Splitter splitter in splitters)
						if(!splitter.isfixed && Math.Abs(splitter.pos - xr) < dragSplitterDistance)
						{
							dragSplitterDistance = Math.Abs(splitter.pos - xr);
							dragSplitterIndex = splitterIndex++;
						} else
							++splitterIndex;
				}
				else if(colorTableMgr.GetCursor() == CursorType.InsertSplitterPin || colorTableMgr.GetCursor() == CursorType.InsertNestedPin)
				{
					Vector3 vpos = Vector3.Transform(new Vector3(2.0f * xr - 1.0f, 0.0f, 0.0f), invtransform);
					xr = vpos.X / 2.0f + 0.5f;

					// Find section containing xr
					int sectionIdx;
					for(sectionIdx = 0; sectionIdx < sections.Count; ++sectionIdx)
						if(sections[sectionIdx].start.pos < xr && xr <= sections[sectionIdx].end.pos)
							break;
					if(sectionIdx == sections.Count)
						return true;

					if(colorTableMgr.GetCursor() == CursorType.InsertSplitterPin)
						ActionManager.Do(InsertSplitterPinAction, new object[] { xr });
					else
						ActionManager.Do(InsertNestedPinAction, new object[] { xr });

					colorTableMgr.SetCursor(CursorType.Default);
				}
				else
				{
					xr = 1.0f - 2.0f * xr;

					colorTableMgr.SetCursor(CursorType.MoveView);

					Matrix4 transform_noscale = transform;
					transform_noscale.M11 = 1.0f;

					tmd0 = Vector3.Transform(new Vector3(xr, 0.0f, 0.0f), transform_noscale);
				}

				return true;
			}
			public bool MouseMove(MouseEventArgs e)
			{
				float xr = Math.Min(Math.Max((float)(e.X - Bounds.X) / (float)Bounds.Width, 0.0f), 1.0f);

				if(dragSplitterIndex != -1)
				{
					Vector3 vpos = Vector3.Transform(new Vector3(2.0f * xr - 1.0f, 0.0f, 0.0f), invtransform);
					xr = vpos.X / 2.0f + 0.5f;

					ActionManager.Do(MovePinAction, new object[] { dragSplitterIndex, xr });
				}
				else if(colorTableMgr.GetCursor() == CursorType.DragPoint)
				{
					Vector3 vpos = Vector3.Transform(new Vector3(2.0f * xr - 1.0f, 0.0f, 0.0f), invtransform);
					xr = vpos.X / 2.0f + 0.5f;

					xr = (xr - dragPointSection.start.pos) / (dragPointSection.end.pos - dragPointSection.start.pos);
					xr -= dragOffset;

					if(dragPoint == DragPoint.Start)
					{
						xr = Math.Min(xr, 0.0f);
						dragPointSection.startValue = xr;
					}
					else if(dragPoint == DragPoint.End)
					{
						xr = Math.Max(xr, 1.0f);
						dragPointSection.endValue = xr;
					}
					ColormapChanged(dragPointSection.start.pos, dragPointSection.end.pos);
				}
				else if(colorTableMgr.GetCursor() != CursorType.InsertSplitterPin && colorTableMgr.GetCursor() != CursorType.InsertNestedPin)
				{
					if(colorTableMgr.GetCursor() == CursorType.MoveView)
					{
						xr = 1.0f - 2.0f * xr;

						Matrix4 transform_noscale = transform;
						transform_noscale.M11 = 1.0f;

						Vector3 t0, tm0 ;
						tm0 = Vector3.Transform(new Vector3(xr, 0.0f, 0.0f), transform_noscale);
						t0 = Vector3.Transform(new Vector3(0.0f, 0.0f, 0.0f), transform_noscale);


						tm0 = Vector3.Subtract(tmd0, tm0);
						tm0 = Vector3.Add(tm0, t0);

						tm0 = Vector3.Transform(tm0, invtransform);

						transform *= Matrix4.CreateTranslation(tm0);
						restrictTransform(ref transform);
						Matrix4.Invert(ref transform, out invtransform);

						//					repositionHistogramLabels();
					}
					else
					{
						Vector3 vpos = Vector3.Transform(new Vector3(2.0f * xr - 1.0f, 0.0f, 0.0f), invtransform);
						xr = vpos.X / 2.0f + 0.5f;

						dragPointSection = null;
						dragPoint = DragPoint.None;

						// Set move mouse cursor if a splitter is close to xr
						var tolearance = 10.0f * invtransform.M11 / (float)BackbufferSize.Width; // Grab tolerance is +- 10 pixels
						int splitterIdx;
						for(splitterIdx = 0; splitterIdx < splitters.Count; ++splitterIdx)
							if(!splitters[splitterIdx].isfixed && Math.Abs(splitters[splitterIdx].pos - xr) < tolearance)
								break;

						if(splitterIdx == splitters.Count)
						{
							// Show drag points on section underneath mouse cursor
							foreach(Section section in sections)
								if(dragPointSection == null && section.start.pos < xr && xr < section.end.pos)
									dragPointSection = section;

							if(dragPointSection != null)
							{
								// Compute visible section width in pixels
								float sectionwidth = (dragPointSection.end.pos - dragPointSection.start.pos) * (float)BackbufferSize.Width / invtransform.M11;
								if(sectionwidth >= 56.0f) // 56 = 2 * drag_point_width (16 pixels) + 3 * drag_point_distance (8 pixels)
								{
									float yr = (float)e.Y / (float)BackbufferSize.Height;
									float tolearance_x = 16.0f * invtransform.M11 / (float)BackbufferSize.Width, tolearance_y = 16.0f / (2.0f * (float)BackbufferSize.Height); // Grab tolerance is +- 16 pixels

									// Set move grab cursor if a drag point is close to xr
									if(Math.Abs(dragPointSection.end.pos - xr - 8.0f * invtransform.M11 / (float)BackbufferSize.Width) < tolearance_x && Math.Abs(0.95f - yr) < tolearance_y)
									{
										dragPoint = DragPoint.End;
										colorTableMgr.SetCursor(CursorType.HoverOverPoint);
									}
									else if(Math.Abs(dragPointSection.start.pos - xr + 8.0f * invtransform.M11 / (float)BackbufferSize.Width) < tolearance_x && Math.Abs(0.95f - yr) < tolearance_y)
									{
										dragPoint = DragPoint.Start;
										colorTableMgr.SetCursor(CursorType.HoverOverPoint);
									}
									else
										colorTableMgr.SetCursor(CursorType.Default);
								}
								else
								{
									dragPointSection = null;
									colorTableMgr.SetCursor(CursorType.Default);
								}
							}
							else
								colorTableMgr.SetCursor(CursorType.Default);
						}
						else
							colorTableMgr.SetCursor(CursorType.MovePin);

						/*if(dragPointSection != oldDragPointSection || dragPoint != oldDragPoint)
						requestAnimFrame(render);*/
					}
				}

				return Bounds.Contains(e.Location);
			}
			public bool MouseUp(MouseEventArgs e)
			{
				if(!Bounds.Contains(e.Location))
				{
					colorTableMgr.draggedColormap = null;
					return false;
				}

				if(colorTableMgr.draggedColormap != null)
				{
					OnColormapDrop(colorTableMgr.draggedColormap, e);

					/*sections[0].colorMap = colorTableMgr.draggedColormap;
				ColormapChanged();*/
					colorTableMgr.draggedColormap = null;
				}

				if(dragSplitterIndex != -1)
				{
					//EDIT: Hide trash button

					/*if(false)//(target == document.getElementById('cmdTrash')) // If splitter is released above trash
				{
					// Remove dragSplitter
					splitters.splice(splitters.indexOf(dragSplitter), 1);

					if(dragSplitter.left === null)
					{
						// Iteratively remove section dragSplitter.right and splitter dragSplitter.right.end
						while(dragSplitter.right !== null)
						{
							sections.splice(sections.indexOf(dragSplitter.right), 1);
							splitters.splice(splitters.indexOf(dragSplitter.right.end), 1);

							dragSplitter = dragSplitter.right.end;
						}
					}
					else if(dragSplitter.right === null)
					{
						// Iteratively remove section dragSplitter.left and splitter dragSplitter.left.start
						while(dragSplitter.left !== null)
						{
							sections.splice(sections.indexOf(dragSplitter.left), 1);
							splitters.splice(splitters.indexOf(dragSplitter.left.start), 1);

							dragSplitter = dragSplitter.left.start;
						}
					}
					else
					{
						// Remove section dragSplitter.left and connect splitter dragSplitter.left.start to section dragSplitter.right
						dragSplitter.left.start.right = dragSplitter.right;
						dragSplitter.right.start = dragSplitter.left.start;
						sections.splice(sections.indexOf(dragSplitter.left), 1);
					}

					requestAnimFrame(render);
					onColorTableChanged(sections);
				}*/
					dragSplitterIndex = -1;
				}
				else
					colorTableMgr.SetCursor(CursorType.Default);

				return true;
			}

			public bool MouseWheel(MouseEventArgs e)
			{
				if(!Bounds.Contains(e.Location))
					return false;

				float xr = Math.Min(Math.Max((float)(e.X - Bounds.X) / (float)Bounds.Width, 0.0f), 1.0f);
				xr = 1.0f - 2.0f * xr; // xr = mouse position in device space ([-1, 1])

				// >>> Mouse centered zoom
				// tm0 = vector from coordinate system center to mouse position in transform space
				// Algorithm:
				// 1) Translate image center to mouse cursor
				// 2) Zoom
				// 3) Translate back

				Matrix4 transform_noscale = transform;
				transform_noscale.M11 = 1.0f; transform_noscale.M22 = 1.0f;

				Vector3 t0, tm, tm0, tm0n;
				t0 = Vector3.Transform(new Vector3(0.0f, 0.0f, 0.0f), transform_noscale);
				tm = Vector3.Transform(new Vector3(xr, 0.0f, 0.0f), transform_noscale);
				tm0 = Vector3.Subtract(t0, tm);
				tm0 = Vector3.Transform(tm0, invtransform);
				tm0n = Vector3.Multiply(tm0, -1.0f);

				foo = tm.X.ToString();

				float zoom = 1.0f + (float)Math.Sign(e.Delta) / 50.0f;
				transform *= Matrix4.CreateTranslation(tm0n);
				transform *= Matrix4.CreateScale(new Vector3(zoom, 1.0f, 1.0f));
				transform *= Matrix4.CreateTranslation(tm0);
				restrictTransform(ref transform);
				invtransform = Matrix4.Invert(transform);
				/*requestAnimFrame(render);
			repositionHistogramLabels();*/

				// Trigger mouse move event, because the relative mouse position might have changed
				this.MouseMove(e);

				return true;
			}

			private void OnColormapDrop(NamedColorTable colormap, MouseEventArgs e)
			{
				float xr = Math.Min(Math.Max((float)(e.X - Bounds.X) / (float)Bounds.Width, 0.0f), 1.0f);

				Vector3 vpos = Vector3.Transform(new Vector3(2.0f * xr - 1.0f, 0.0f, 0.0f), invtransform);
				xr = vpos.X / 2.0f + 0.5f;

				//			highlightArea = null;
				/*bool done = false; 
			foreach(Section section in sections)
				if(!done && section.start.pos <= xr && xr < section.end.pos)
				{
					section.colorMap = colormap;
					section.flipped = false;//colormap.flipped;
					section.startValue = 0.0f;
					section.endValue = 1.0f;
					done = true;
				}
			ColormapChanged();*/
				for(int sectionIndex = 0; sectionIndex < sections.Count; ++sectionIndex)
					if(sections[sectionIndex].start.pos <= xr && xr < sections[sectionIndex].end.pos)
					{
						ActionManager.Do(SetSectionColormapAction, new object[] { sectionIndex, colormap.name });
						break;
					}
			}

			private void restrictTransform(ref Matrix4 transform)
			{
				// Restrict transformations to keep the whole canvas (-1 ... 1) filled with (part of) the color map

				if(transform.M11 < 1.0f) // If zoomed out more than zoom == 1.0f
					transform.M11 = 1.0f;
				if(-1.0f * transform.M11 + transform.M41 > -1.0f) // If out of bounds left
					transform.M41 = 1.0f * transform.M11 - 1.0f;
				else if(1.0 * transform.M11 + transform.M41 < 1.0f) // If out of bounds right
					transform.M41 = 1.0f - 1.0f * transform.M11;
			}

			private void ColormapChanged(float updateRegion_start, float updateRegion_end)
			{
				colormapUpdateStart = Math.Min(colormapUpdateStart, updateRegion_start);
				colormapUpdateEnd = Math.Max(colormapUpdateEnd, updateRegion_end);
			}

			private void InsertSplitterPin(float splitterPosition)
			{
				// Find section containing splitterPosition
				int sectionIdx;
				for(sectionIdx = 0; sectionIdx < sections.Count; ++sectionIdx)
					if(sections[sectionIdx].start.pos < splitterPosition && splitterPosition <= sections[sectionIdx].end.pos)
						break;
				if(sectionIdx == sections.Count)
					return;

				// >>> Insert splitter at splitterPosition

				Splitter newSplitter = new Splitter {
					isfixed = false,
					interjector = false,
					pos = splitterPosition,
					left = null,
					right = null
				};
				splitters.Add(newSplitter);
				Section newSection = new Section {
					colorMap = sections[sectionIdx].colorMap,
					start = sections[sectionIdx].start,
					end = newSplitter,
					startValue = sections[sectionIdx].startValue,
					endValue = sections[sectionIdx].endValue,
					flipped = sections[sectionIdx].flipped,
					interjector = false
				};
				sections[sectionIdx].start.right = newSection;
				sections[sectionIdx].start = newSplitter;
				newSplitter.left = newSection;
				newSplitter.right = sections[sectionIdx];
				sections.Insert(sectionIdx, newSection);

				ColormapChanged(newSplitter.left.start.pos, newSplitter.right.end.pos);
			}
			private void InsertNestedPin(float splitterPosition)
			{
				// Find section containing splitterPosition
				int sectionIdx;
				for(sectionIdx = 0; sectionIdx < sections.Count; ++sectionIdx)
					if(sections[sectionIdx].start.pos < splitterPosition && splitterPosition <= sections[sectionIdx].end.pos)
						break;
				if(sectionIdx == sections.Count)
					return;

				// >>> Insert interjector at splitterPosition

				Vector3 vpos = Vector3.Transform(new Vector3(2.0f * 10.0f / BackbufferSize.Width - 1.0f, 0.0f, 0.0f), invtransform);
				var delta = vpos.X / 2.0f + 0.5f;

				Splitter leftSplitter = new Splitter {
					isfixed = false,
					interjector = false,
					pos = Math.Max(splitterPosition - delta, sections[sectionIdx].start.pos + 1e-5f),
					left = null,
					right = null
				};
				Splitter rightSplitter = new Splitter {
					isfixed = false,
					interjector = false,
					pos = Math.Min(splitterPosition + delta, sections[sectionIdx].end.pos - 1e-5f),
					left = null,
					right = null
				};
				splitters.Add(leftSplitter);
				splitters.Add(rightSplitter);
				Section newSection = new Section {
					colorMap = sections[sectionIdx].colorMap,
					start = leftSplitter,
					end = rightSplitter,
					startValue = sections[sectionIdx].startValue,
					endValue = sections[sectionIdx].endValue,
					flipped = sections[sectionIdx].flipped,
					interjector = true
				};
				sections.Insert(sectionIdx, newSection);

				leftSplitter.right = rightSplitter.left = newSection;

				ColormapChanged(newSection.start.pos, newSection.end.pos);
			}
			private void MovePin(int splitterIndex, float splitterPosition)
			{
				if(splitterIndex < 0 || splitterIndex >= splitters.Count || splitters[splitterIndex].isfixed)
					return;
				Splitter dragSplitter = splitters[splitterIndex];

				// >>> Move pin to splitterPosition

				float dragBoundsLeft = dragSplitter.left != null ? dragSplitter.left.start.pos : 0.0f;
				float dragBoundsRight = dragSplitter.right != null ? dragSplitter.right.end.pos : 1.0f;

				//xr = Math.min(Math.max(xr, 0.0), 1.0);
				/*if(dragSplitter.left !== null)
					xr = Math.max(xr, dragSplitter.left.start.pos);
				if(dragSplitter.right !== null)
				{
					xr = Math.min(xr, dragSplitter.right.end.pos);
					console.log(dragSplitter.right.end.pos);
				}*/

				splitterPosition = Math.Max(splitterPosition, dragBoundsLeft);
				splitterPosition = Math.Min(splitterPosition, dragBoundsRight);

				ColormapChanged(dragSplitter.left == null ? dragSplitter.pos : dragSplitter.left.start.pos, dragSplitter.right == null ? dragSplitter.pos : dragSplitter.right.end.pos);
				dragSplitter.pos = splitterPosition;
				ColormapChanged(dragSplitter.left == null ? dragSplitter.pos : dragSplitter.left.start.pos, dragSplitter.right == null ? dragSplitter.pos : dragSplitter.right.end.pos);
			}
			private void SetSectionColormap(int sectionIndex, string colormapName)
			{
				if(sectionIndex < 0 || sectionIndex >= sections.Count)
					return;
				Section section = sections[sectionIndex];

				NamedColorTable colormap;
				if(!colorTableMgr.colormaps.TryGetValue(colormapName, out colormap))
					return;

				section.colorMap = colormap;
				section.flipped = false;//colormap.flipped;
				section.startValue = 0.0f;
				section.endValue = 1.0f;

				ColormapChanged(section.start.pos, section.end.pos);
			}
			private void ResetColorTable()
			{
				splitters = new List<Splitter>() {
					new Splitter {isfixed=true, interjector=false, pos=0.0f, left=null, right=null},
					new Splitter {isfixed=true, interjector=false, pos=1.0f, left=null, right=null}
				};
				sections = new List<Section>() {
					new Section {colorMap=(NamedColorTable)colorTableMgr.colormaps["_default"], start=splitters[0], end=splitters[1], startValue=0.0f, endValue=1.0f, flipped=false, interjector=false}
				};
				ColormapChanged(0.0f, 1.0f);
			}
		}
		private InputSection input;

		private readonly GLWindow glcontrol;
		private readonly GLButton[] buttons;
		private readonly GLCursor[] cursors;
		private GLCursor activecursor = null;

		public Dictionary<string, NamedColorTable> colormaps = new Dictionary<string, NamedColorTable>();

		public GLTexture1D Colormap { get { return input.Colormap;} }

		private Action ShowColormapPickerAction, HideColormapPickerAction;

		public ColorTableManager(GLWindow glcontrol)
		{
			this.glcontrol = glcontrol;
			this.Bounds = new Rectangle(Point.Empty, glcontrol.Size);
			SetCursor(CursorType.Default);

			ShowColormapPickerAction = ActionManager.CreateAction("Show colormap picker", "show picker", delegate(object[] parameters) {
				pickerVisible = true;
				return null;
			});
			HideColormapPickerAction = ActionManager.CreateAction("Hide colormap picker", "hide picker", delegate(object[] parameters) {
				pickerVisible = false;
				return null;
			});

			// Create shaders as singleton
			/*if(colortableshader == null)
				colortableshader = new GLShader(new string[] {COLOR_TABLE_SHADER.VS}, new string[] {COLOR_TABLE_SHADER.FS});*/
			if(lineshader == null)
				lineshader = new GLShader(new string[] {LINE_SHADER.VS}, new string[] {LINE_SHADER.FS});
			if(cmpreviewshader == null)
				cmpreviewshader = new GLShader(new string[] {COLORMAP_PREVIEW_SHADER.VS}, new string[] {COLORMAP_PREVIEW_SHADER.FS});

			// Create input section
			input = new InputSection(this, new GLTextFont2(new Font("Lucida Sans Unicode", 12.0f)));
			input.Bounds = new Rectangle(130, 60, glcontrol.Width - 260, 200);//new Rectangle(100, glcontrol.Height - 60, glcontrol.Width - 400, 40);
			input.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
			this.Controls.Add(input);

			// Create colormap picker
			picker = new ColorMapPicker(new GLTextFont2(new Font("Lucida Sans Unicode", 12.0f/*, FontStyle.Bold*/)));
			picker.bounds = new Rectangle(0, 0, glcontrol.Width, input.Bounds.Top);
			picker.ColormapDragStart += ColorMapPicker_ColormapDragStart;

			// Create/load colormaps
			AddColormap(ColorTableFromSolidColor(Color4.Red, Vector3.Zero, "Red"));
			AddColormap(ColorTableFromSolidColor(Color4.Orange, Vector3.Zero, "Orange"));
			AddColormap(ColorTableFromSolidColor(Color4.Yellow, Vector3.Zero, "Yellow"));
			AddColormap(ColorTableFromSolidColor(Color4.Green, Vector3.Zero, "Green"));
			AddColormap(ColorTableFromSolidColor(Color4.Blue, Vector3.Zero, "Blue"));
			AddColormap(ColorTableFromSolidColor(Color4.White, Vector3.Zero, "White"));
			AddColormap(ColorTableFromSolidColor(Color4.Gray, Vector3.Zero, "Gray"));
			AddColormap(ColorTableFromSolidColor(Color4.Black, Vector3.Zero, "Black"));
			/*ColorMapCreator.Vector3 C0 = new ColorMapCreator.Vector3(58.650f, 76.245f, 192.270f);
			ColorMapCreator.Vector3 C1 = new ColorMapCreator.Vector3(180.030f, 4.080f, 38.250f);
			AddColormap(ColorTableFromRange(C0, C1, new Vector3(65.0f / 255.0f, 68.0f / 255.0f, 91.0f / 255.0f), "Moreland cool/warm", "Divergent"));*/
			AddColormaps(ColorTableFromXml("ColorMaps.xml"));
			AddColormap(NamedColorTable.None);

			ActionManager.Do(HideColormapPickerAction);
			Reset();

			// Create buttons
			buttons = new GLButton[4];
			buttons[0] = new GLButton("splitterButton.png", new Rectangle(4, 100, 0, 0), AnchorStyles.Bottom | AnchorStyles.Left, "CreateSplitter", "Create colormap splitter");
			buttons[0].Click = SplitterButton_Click;
			buttons[1] = new GLButton("interjectorButton.png", new Rectangle(4, 100 - buttons[0].Bounds.Height, 0, 0), AnchorStyles.Bottom | AnchorStyles.Left, "CreateInterjector", "Create colormap interjector");
			buttons[1].Click = InterjectorButton_Click;
			buttons[2] = new GLButton("colorMapButton.png", new Rectangle(4, 100, 0, 0), AnchorStyles.Bottom | AnchorStyles.Right, "ShowColormapPicker", "Show colormap picker");
			buttons[2].Click = ColorMapButton_Click;
			buttons[3] = new GLButton("saveColorMapButton.png", new Rectangle(4, 100 - buttons[2].Bounds.Height, 0, 0), AnchorStyles.Bottom | AnchorStyles.Right, "SaveColormap", "Save colormap to disk");
			buttons[3].Click = SplitterButton_Click;

			// Create cursors
			cursors = new GLCursor[2];
			cursors[0] = new GLCursor("splitterCursor.png", new Point(2, 54));
			cursors[1] = new GLCursor("interjectorCursor.png", new Point(8, 51));

			/*HISTORY_PATH = System.Reflection.Assembly.GetEntryAssembly().Location;
			HISTORY_PATH = HISTORY_PATH.Substring(0, Math.Max(HISTORY_PATH.LastIndexOf('/'), HISTORY_PATH.LastIndexOf('\\')) + 1);
			HISTORY_PATH += ".colormap";

			if(System.IO.File.Exists(HISTORY_PATH))
			{
				System.IO.StreamReader sr = new System.IO.StreamReader(HISTORY_PATH);
				while(sr.Peek() != -1)
					history.Add(sr.ReadLine());
				sr.Close();
				history_idx = history.Count;
			}*/
		}
		/*~ColorTableManager()
		{
			System.IO.StreamWriter sw = new System.IO.StreamWriter(HISTORY_PATH);
			foreach(string h in history)
				sw.WriteLine(h);
			sw.Close();
		}*/

		private void AddColormap(NamedColorTable colormap)
		{
			colormaps.Add(colormap.name, colormap);
			if(colormap.groupname != "Solid" && !colormaps.ContainsKey("_default"))
				colormaps.Add("_default", colormap);

			picker.AddColorMap(colormap);
		}
		private void AddColormaps(IEnumerable<NamedColorTable> colormaps)
		{
			NamedColorTable defaultColormap;
			this.colormaps.TryGetValue("_default", out defaultColormap);

			foreach(NamedColorTable colormap in colormaps)
			{
				this.colormaps.Add(colormap.name, colormap);
				if(defaultColormap == null && colormap.groupname != "Solid")
				{
					defaultColormap = colormap;
					this.colormaps.Add("_default", colormap);
				}

				picker.AddColorMap(colormap);
			}
		}

		/*public void SelectImage(HashSet<int>[] selection, Dictionary<int[], TransformedImage> images)
		{
			TransformedImage selection;
			images.TryGetValue(selectionkey, out selection);

			if(selection != null && selection.meta.ContainsKey("histogram"))
			{
				List<Vector3> positions = new List<Vector3>();
				Newtonsoft.Json.Linq.JContainer histogram = (Newtonsoft.Json.Linq.JContainer)selection.meta["histogram"];
				const float xscale = -50.0f;
				float yscale = (float)1.0f / (float)histogram.Count, i = 0.0f;
				positions.Add(new Vector3(0.0f, 1.0f, 0.0f));
				foreach(double value in histogram)
					positions.Add(new Vector3((float)value * xscale, 1.0f - i++ * yscale, 0.0f));
				positions.Add(new Vector3(0.0f, 0.0f, 0.0f));

				meshHistogram = new GLMesh(positions.ToArray(), null, null, null, null, null, PrimitiveType.LineLoop);
			}
			else
				meshHistogram = null;
		}*/

		private void SplitterButton_Click(object sender, EventArgs e)
		{
			SetCursor(CursorType.InsertSplitterPin);
		}
		private void InterjectorButton_Click(object sender, EventArgs e)
		{
			SetCursor(CursorType.InsertNestedPin);
		}
		private void ColorMapButton_Click(object sender, EventArgs e)
		{
			if(pickerVisible)
				ActionManager.Do(HideColormapPickerAction);
			else
				ActionManager.Do(ShowColormapPickerAction);
		}

		private void ColorMapPicker_ColormapDragStart(NamedColorTable colormap)
		{
			draggedColormap = colormap;
		}

		public void OnSizeChanged(Size backbuffersize)
		{
			foreach(GLButton button in buttons)
				button.OnParentSizeChanged(backbuffersize, backbuffersize);
			foreach(GLCursor cursor in cursors)
				cursor.OnSizeChanged(backbuffersize);
			picker.OnSizeChanged(backbuffersize);
		}

		//public new void Draw(float dt)
		protected override void Draw(float dt, Matrix4 _transform)
		{
			foreach(GLButton button in buttons)
				button.Draw(dt);

			if(pickerVisible)
				picker.Draw();

			if(activecursor != null)
				activecursor.Draw(glcontrol.PointToClient(Control.MousePosition));
		}

		public enum CursorType
		{
			Default, InsertSplitterPin, InsertNestedPin, MovePin, MoveView, HoverOverPoint, DragPoint
		}
		private CursorType currentCursor = CursorType.Default;
public static string foo = "";
		public CursorType GetCursor()
		{
			return currentCursor;
		}
		public void SetCursor(CursorType cursor)
		{
			if(cursor != currentCursor)
			{
				switch(cursor)
				{
				case CursorType.InsertSplitterPin:
					Cursor.Current = Cursors.Default;
					activecursor = cursors[0];
					activecursor.visible = true;
					break;
				case CursorType.InsertNestedPin:
					Cursor.Current = Cursors.Default;
					activecursor = cursors[1];
					activecursor.visible = true;
					break;
				case CursorType.MovePin:
					Cursor.Current = Cursors.VSplit;
					activecursor = null;
					break;
				case CursorType.MoveView:
					Cursor.Current = Cursors.SizeAll;
					activecursor = null;
					break;
				case CursorType.HoverOverPoint:
					Cursor.Current = Cursors.Hand;
					activecursor = null;
					break;
				case CursorType.DragPoint:
					Cursor.Current = Cursors.Hand;
					activecursor = null;
					break;
				default:
					Cursor.Current = Cursors.Default;
					activecursor = null;
					break;
				}
				currentCursor = cursor;
			}
		}

		public new bool MouseDown(MouseEventArgs e)
		{
			if(pickerVisible && picker.MouseDown(e))
				return true;
			foreach(GLButton button in buttons)
				if(button.OnMouseDown(e))
					return true;

			return input.MouseDown(e);
		}
		public bool MouseMove(MouseEventArgs e)
		{
			if(pickerVisible && picker.MouseMove(e))
				return true;

			/*bool inside = bounds.Contains(e.Location);

			if(inside && activecursor != null)
			{
				//Cursor.Hide();
				activecursor.visible = true;
			}
			else if(!inside)
			{
				//Cursor.Show();
				if(activecursor != null)
					activecursor.visible = false;
			}*/
			
			return input.MouseMove(e);
		}
		public bool MouseUp(MouseEventArgs e)
		{
			if(pickerVisible && draggedColormap != null)
				ActionManager.Do(HideColormapPickerAction);
			
			return input.MouseUp(e);
		}

		public bool MouseWheel(MouseEventArgs e)
		{
			return input.MouseWheel(e);
		}

		public void Reset()
		{
			ActionManager.Do(input.ResetColorTableAction, new object[] {});
		}

#region "ColorTable creation/loading"
		private static NamedColorTable ColorTableFromSolidColor(Color4 color, Vector3 nanColor, string name)
		{
			byte[] colormapBytes = new byte[3];
			colormapBytes[0] = (byte)(color.R * 255.0f);
			colormapBytes[1] = (byte)(color.G * 255.0f);
			colormapBytes[2] = (byte)(color.B * 255.0f);
			return new NamedColorTable(new GLTexture1D(colormapBytes, 1, false), nanColor, name, "Solid");
		}
		private static NamedColorTable ColorTableFromRange(ColorMapCreator.Vector3 cmin, ColorMapCreator.Vector3 cmax, Vector3 nanColor, string name, string groupname)
		{
			byte[] colorTable = new byte[COLOR_TABLE_SIZE * 3];
			float xscale = 1.0f / (float)(COLOR_TABLE_SIZE - 1);
			for(int x = 0; x < COLOR_TABLE_SIZE; ++x)
			{
				ColorMapCreator.Vector3 clr = ColorMapCreator.interpolateColor(cmin, cmax, (float)x * xscale);
				colorTable[3 * x + 0] = (byte)clr.x;
				colorTable[3 * x + 1] = (byte)clr.y;
				colorTable[3 * x + 2] = (byte)clr.z;
			}
			return new NamedColorTable(new GLTexture1D(colorTable, COLOR_TABLE_SIZE, false), nanColor, name, groupname);
		}

		private static NamedColorTable[] ColorTableFromXml(string filename)
		{
			List<NamedColorTable> colorTables = new List<NamedColorTable>();

			XmlReader reader = XmlReader.Create(filename);
			while(reader.Read() && (reader.NodeType != XmlNodeType.Element || reader.Name != "ColorMaps")) {}

			//XmlElement root = new XmlElement();
			bool breakwhile = false;
			while(reader.Read() && !breakwhile)
			{
				switch(reader.NodeType)
				{
				case XmlNodeType.Element:
					if(reader.Name == "ColorMap")
					{
						string colorMapName = GetXmlAttribute(reader, "name");
						if(colorMapName == null)
							colorMapName = "Color table " + (colorTables.Count + 1);

						string colorMapGroupName = GetXmlAttribute(reader, "group");
						if(colorMapGroupName == null)
							colorMapGroupName = "Custom";

						ColorMapCreator.InterpolatedColorMap colorTable = new ColorMapCreator.InterpolatedColorMap();
						Vector3 nanColor = Vector3.Zero;

						while(reader.Read() && !breakwhile)
						{
							switch(reader.NodeType)
							{
							case XmlNodeType.Element:
								if(reader.Name == "Point")
								{
									float x = float.NaN, r = -1.0f, g = -1.0f, b = -1.0f;
									while(reader.MoveToNextAttribute())
									{
										if(reader.Name == "x")
											x = float.Parse(reader.Value);
										if(reader.Name == "r")
											r = float.Parse(reader.Value);
										if(reader.Name == "g")
											g = float.Parse(reader.Value);
										if(reader.Name == "b")
											b = float.Parse(reader.Value);
									}
									if(x != float.NaN && r >= 0.0f && r <= 1.0f && g >= 0.0f && g <= 1.0f && b >= 0.0f && b <= 1.0f)
										colorTable.AddColor(x, r * 255.0f, g * 255.0f, b * 255.0f);
								}
								else if(reader.Name == "NaN")
								{
									float r = -1.0f, g = -1.0f, b = -1.0f;
									while(reader.MoveToNextAttribute())
									{
										if(reader.Name == "r")
											r = float.Parse(reader.Value);
										if(reader.Name == "g")
											g = float.Parse(reader.Value);
										if(reader.Name == "b")
											b = float.Parse(reader.Value);
									}
									if(r >= 0.0f && r <= 1.0f && g >= 0.0f && g <= 1.0f && b >= 0.0f && b <= 1.0f)
										nanColor = new Vector3(r, g, b);
								}
								break;

							case XmlNodeType.EndElement:
								if(reader.Name == "ColorMap")
									breakwhile = true;
								break;
							}
						}
						breakwhile = false;


						byte[] bytes = colorTable.Create(COLOR_TABLE_SIZE);
/*Bitmap bmp = new Bitmap(COLOR_TABLE_SIZE, 1, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
System.Drawing.Imaging.BitmapData data = bmp.LockBits(new Rectangle(Point.Empty, bmp.Size), System.Drawing.Imaging.ImageLockMode.WriteOnly, bmp.PixelFormat);
System.Runtime.InteropServices.Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
bmp.UnlockBits(data);
bmp.Save(colorMapName + ".png");*/
						colorTables.Add(new NamedColorTable(new GLTexture1D(bytes, COLOR_TABLE_SIZE, false), nanColor, colorMapName, colorMapGroupName));
					}
					break;

				case XmlNodeType.EndElement:
					if(reader.Name == "ColorMaps")
						breakwhile = true;
					break;
				}
			}
			reader.Close();
			return colorTables.ToArray();
		}

		private static string GetXmlAttribute(XmlReader reader, string name)
		{
			if(reader.MoveToAttribute(name))
				return reader.Value;
			else
				return null;
		}
#endregion
	}
}

