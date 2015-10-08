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
		private static int COLOR_TABLE_SIZE = 1024;

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
		private NamedColorTable draggedColormap = null;

		public class ColorTableSettings
		{
			public GLTexture1D innerColorTable, outerColorTable;
			public bool hasOuterColorTable;
			public float minValue, maxValue;
			public Vector3 nanColor;
		}
		private ColorTableSettings settings = new ColorTableSettings();

		private class Section
		{
			public NamedColorTable colorMap;
			public Splitter start, end;
			public bool flipped, interjector;
		}
		private List<Section> sections;
		private class Splitter
		{
			public bool isfixed, interjector;
			public float pos;
			public Section left, right;
		}
		private List<Splitter> splitters;

		public delegate void ColorTableSettingsChangedDelegate(ColorTableSettings settings);

		private readonly GLWindow glcontrol;
		private readonly Rectangle bounds;
		private readonly GLMesh meshquad;
		private readonly GLButton[] buttons;
		private readonly GLCursor[] cursors;
		private GLCursor activecursor = null;
		private ComboBox cbInnerColorTable, cbOuterColorTable;
		private ColorTableSettingsChangedDelegate OnColorTableSettingsChanged;
		private GLMesh meshLines, meshHistogram;
		private GLTexture2D texSplitter, texInterjectorLeft, texInterjectorRight;
		private Matrix4 transform = Matrix4.Identity, invtransform = Matrix4.Identity;
		private bool settingsChanged = true;
		private Size backbuffersize;

		private GLTexture1D colormapTexture;
		private Color4 colormapNanColor;

		private Action SetInnerColorTableAction, SetOuterColorTableAction, SetMinValueAction, SetMaxValueAction, ResetColorTableAction;

		public ColorTableManager(GLWindow glcontrol, Rectangle bounds, GLMesh meshquad, FlowLayoutPanel pnlImageControls, ColorTableSettingsChangedDelegate OnColorTableSettingsChanged)
		{
			this.glcontrol = glcontrol;
			this.bounds = bounds;
			this.meshquad = meshquad;
			this.OnColorTableSettingsChanged = OnColorTableSettingsChanged;

			SetInnerColorTableAction = ActionManager.CreateAction("Set inner color table", this, "SetInnerColorTable");
			SetOuterColorTableAction = ActionManager.CreateAction("Set outer color table", this, "SetOuterColorTable");
			SetMinValueAction = ActionManager.CreateAction("Set nested color table start", this, "SetMinValue");
			SetMaxValueAction = ActionManager.CreateAction("Set nested color table end", this, "SetMaxValue");
			ResetColorTableAction = ActionManager.CreateAction("Reset color table", this, "ResetColorTable");

			// Create shaders as singleton
			/*if(colortableshader == null)
				colortableshader = new GLShader(new string[] {COLOR_TABLE_SHADER.VS}, new string[] {COLOR_TABLE_SHADER.FS});*/
			if(lineshader == null)
				lineshader = new GLShader(new string[] {LINE_SHADER.VS}, new string[] {LINE_SHADER.FS});
			if(cmpreviewshader == null)
				cmpreviewshader = new GLShader(new string[] {COLORMAP_PREVIEW_SHADER.VS}, new string[] {COLORMAP_PREVIEW_SHADER.FS});

			// Create colormap
			colormapTexture = new GLTexture1D(new byte[3 * COLOR_TABLE_SIZE], COLOR_TABLE_SIZE, false);

			// Create colormap picker
			picker = new ColorMapPicker(new GLFont(new Font("Lucida Grande", 12.0f, FontStyle.Bold)));
			picker.bounds = new Rectangle(0, 0, glcontrol.Width, bounds.Top);
			picker.ColormapDragStart += ColorMapPicker_ColormapDragStart;

			// Create ComboBoxes containing all loaded color tables
			cbInnerColorTable = new ComboBox();
			cbOuterColorTable = new ComboBox();
			cbInnerColorTable.DropDownStyle = cbOuterColorTable.DropDownStyle = ComboBoxStyle.DropDownList;
			pnlImageControls.Controls.Add(cbInnerColorTable);
			pnlImageControls.Controls.Add(cbOuterColorTable);
			cbInnerColorTable.SelectedIndexChanged += cbInnerColorTable_SelectedIndexChanged;
			cbOuterColorTable.SelectedIndexChanged += cbOuterColorTable_SelectedIndexChanged;

			// Create/load color tables
			ColorMapCreator.Vector3 C0 = new ColorMapCreator.Vector3(58.650f, 76.245f, 192.270f);
			ColorMapCreator.Vector3 C1 = new ColorMapCreator.Vector3(180.030f, 4.080f, 38.250f);
			cbInnerColorTable.Items.Add(ColorTableFromRange(C0, C1, new Vector3(65.0f / 255.0f, 68.0f / 255.0f, 91.0f / 255.0f), "Moreland cool/warm", "Divergent"));
			cbInnerColorTable.Items.AddRange(ColorTableFromXml("ColorMaps.xml"));
			cbOuterColorTable.Items.Add(NamedColorTable.None);
			foreach(NamedColorTable item in cbInnerColorTable.Items)
			{
				cbOuterColorTable.Items.Add(item);
				picker.AddColorMap(item);
			}
			//cbInnerColorTable.SelectedIndex = cbOuterColorTable.SelectedIndex = 0;

			splitters = new List<Splitter>() {
				new Splitter {isfixed=true, interjector=false, pos=0.0f, left=null, right=null},
				new Splitter {isfixed=true, interjector=false, pos=1.0f, left=null, right=null}
			};
			sections = new List<Section>() {
				new Section {colorMap=(NamedColorTable)cbInnerColorTable.Items[0], start=splitters[0], end=splitters[1], flipped=false, interjector=false}
			};

			// Create table outline and tick marks as line list mesh
			List<Vector3> positions = new List<Vector3>();
			positions.AddRange(new Vector3[] {
				// Outline
				new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 1.0f, 0.0f),
				new Vector3(0.0f, 1.0f, 0.0f), new Vector3(1.0f, 1.0f, 0.0f),
				new Vector3(1.0f, 1.0f, 0.0f), new Vector3(1.0f, 0.0f, 0.0f),
				new Vector3(1.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f)
			});
			for(int i = 0; i <= 40; ++i)
			{
				float x = (float)i / 40.0f;
				positions.Add(new Vector3(x, 1.0f, 0.0f));
				positions.Add(new Vector3(x, i % 10 == 0 ? 1.6f : 1.3f, 0.0f));
			}
			meshLines = new GLMesh(positions.ToArray(), null, null, null, null, null, PrimitiveType.Lines);

			// Create textures
			texSplitter = GLTexture2D.FromFile("splitter.png", false);
			texInterjectorLeft = GLTexture2D.FromFile("InterjectorLeft.png", false);
			texInterjectorRight = GLTexture2D.FromFile("InterjectorRight.png", false);

			// Create number font
			//font = new GLNumberFont("HelveticaNeue_12.png", new FontDefinition(new int[] {0, 14, 26, 39, 53, 67, 80, 93, 106, 120, 133}, new int[] {0, 19}), meshquad, true);
			//font = new GLNumberFont("HelveticaNeue_16.png", new FontDefinition(new int[] {0, 18, 34, 53, 71, 89, 106, 124, 142, 160, 178}, new int[] {0, 25}), true);
			//font = new GLTextFont("fntDefault.png", new Vector2(19.0f, 32.0f), meshquad);

			// Create buttons
			buttons = new GLButton[4];
			buttons[0] = new GLButton("splitterButton.png", new Rectangle(4, 100, 0, 0), AnchorStyles.Bottom | AnchorStyles.Left, "CreateSplitter", "Create colormap splitter");
			buttons[0].Click = SplitterButton_Click;
			buttons[1] = new GLButton("interjectorButton.png", new Rectangle(4, 100 - buttons[0].bounds.Height, 0, 0), AnchorStyles.Bottom | AnchorStyles.Left, "CreateInterjector", "Create colormap interjector");
			buttons[1].Click = InterjectorButton_Click;
			buttons[2] = new GLButton("colorMapButton.png", new Rectangle(4, 100, 0, 0), AnchorStyles.Bottom | AnchorStyles.Right, "ShowColormapPicker", "Show colormap picker");
			buttons[2].Click = ColorMapButton_Click;
			buttons[3] = new GLButton("saveColorMapButton.png", new Rectangle(4, 100 - buttons[2].bounds.Height, 0, 0), AnchorStyles.Bottom | AnchorStyles.Right, "SaveColormap", "Save colormap to disk");
			buttons[3].Click = SplitterButton_Click;

			// Create cursors
			cursors = new GLCursor[2];
			cursors[0] = new GLCursor("splitterCursor.png", new Point(2, 54));
			cursors[1] = new GLCursor("interjectorCursor.png", new Point(8, 51));
		}

		public void Free()
		{
			cbInnerColorTable.Parent.Controls.Remove(cbInnerColorTable);
			cbOuterColorTable.Parent.Controls.Remove(cbOuterColorTable);
		}

		public void SelectImage(HashSet<int>[] selection, Dictionary<int[], TransformedImage> images)
		{
			/*TransformedImage selection;
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
				meshHistogram = null;*/
		}

		private void SplitterButton_Click(object sender, EventArgs e)
		{
			activecursor = cursors[0];
		}
		private void InterjectorButton_Click(object sender, EventArgs e)
		{
			activecursor = cursors[1];
		}
		private void ColorMapButton_Click(object sender, EventArgs e)
		{
			pickerVisible = !pickerVisible;
		}

		private void ColorMapPicker_ColormapDragStart(NamedColorTable colormap)
		{
			draggedColormap = colormap;
		}

		public void OnSizeChanged(Size backbuffersize)
		{
			this.backbuffersize = backbuffersize;
			foreach(GLButton button in buttons)
				button.OnParentSizeChanged(backbuffersize);
			foreach(GLCursor cursor in cursors)
				cursor.OnSizeChanged(backbuffersize);
			picker.OnSizeChanged(backbuffersize);
		}

		public void Draw(float dt)
		{
			if(settings.innerColorTable == null)
				return;

			if(settingsChanged)
			{
				settingsChanged = false;

				/*colortableshader.Bind();

				GL.ActiveTexture(TextureUnit.Texture0);
				settings.innerColorTable.Bind();
				GL.Uniform1(colortableshader.GetUniformLocation("InnerColorTable"), 0);

				GL.Uniform1(colortableshader.GetUniformLocation("HasOuterColorTable"), (settings.hasOuterColorTable = (settings.outerColorTable != null)) ? 1 : 0);
				if(settings.hasOuterColorTable)
				{
					GL.ActiveTexture(TextureUnit.Texture1);
					settings.outerColorTable.Bind();
					GL.Uniform1(colortableshader.GetUniformLocation("OuterColorTable"), 1);
				}

				GL.Uniform1(colortableshader.GetUniformLocation("MinValue"), settings.minValue);
				GL.Uniform1(colortableshader.GetUniformLocation("MaxValue"), settings.maxValue);

				OnColorTableSettingsChanged(settings);*/

				// Create colormap from sections
				List<Section>.Enumerator sectionEnum = sections.GetEnumerator();
				if(sectionEnum.MoveNext())
				{
					byte[] colormapBytes = colormapTexture.Lock();
					for(int x = 0; x < COLOR_TABLE_SIZE; ++x)
					{
						float xr = (float)x / (float)COLOR_TABLE_SIZE;
						if(xr >= sectionEnum.Current.end.pos && !sectionEnum.MoveNext())
							break;
						xr = (xr - sectionEnum.Current.start.pos) / (sectionEnum.Current.end.pos - sectionEnum.Current.start.pos);
						sectionEnum.Current.colorMap.tex.Interpolate(xr, out colormapBytes[x * 3 + 0], out colormapBytes[x * 3 + 1], out colormapBytes[x * 3 + 2]);
					}
					colormapTexture.Unlock();
				}
			}

			if(settings.outerColorTable != null)
			{
				GL.ActiveTexture(TextureUnit.Texture1);
				settings.outerColorTable.Bind();
			}
			GL.ActiveTexture(TextureUnit.Texture0);
			settings.innerColorTable.Bind();

			Matrix4 trans = Matrix4.Identity;
			trans *= Matrix4.CreateScale(2.0f * bounds.Width / backbuffersize.Width, (2.0f * bounds.Height - 4.0f) / backbuffersize.Height, 1.0f);
			trans *= Matrix4.CreateTranslation(-1.0f + 2.0f * (bounds.Left + 0.5f) / backbuffersize.Width, 1.0f - 2.0f * (bounds.Bottom - 0.5f) / backbuffersize.Height, 0.0f);

			/*colortableshader.Bind(trans);
			meshquad.Bind(colortableshader, null);
			meshquad.Draw();*/

			cmpreviewshader.Bind(trans);
			meshquad.Bind(cmpreviewshader, colormapTexture);
			meshquad.Draw();

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

			foreach(GLButton button in buttons)
				button.Draw(dt);

			Common.fontText.DrawString(bounds.Left - 10, bounds.Top - bounds.Height, "0%", backbuffersize);
			Common.fontText.DrawString(bounds.Right - 20, bounds.Top - bounds.Height, "100%", backbuffersize);

			if(pickerVisible)
				picker.Draw();

			if(activecursor != null)
				activecursor.Draw(glcontrol.PointToClient(Control.MousePosition));
		}

		private void drawSplitter(float pos, GLTexture2D tex) // pos = 0.0f ... 1.0f
		{
			Vector3 vpos = Vector3.Transform(new Vector3(2.0f * pos - 1.0f, -1.0f, 0.0f), transform);

			Matrix4 mattrans;
			mattrans = Matrix4.Identity;
			mattrans *= Matrix4.CreateTranslation(-0.5f, 0.0f, 0.0f);
			mattrans *= Matrix4.CreateScale((float)tex.width / (float)backbuffersize.Width, (float)tex.height / (float)backbuffersize.Height, 1.0f);
			mattrans *= Matrix4.CreateTranslation(vpos);
			Common.sdrTextured.Bind(mattrans);

			Common.meshQuad.Bind(Common.sdrTextured, tex);
			Common.meshQuad.Draw();
		}

		private void cbInnerColorTable_SelectedIndexChanged(object sender, EventArgs e)
		{
			ActionManager.Do(SetInnerColorTableAction, new object[] { ((ComboBox)sender).SelectedIndex });
		}
		private void cbOuterColorTable_SelectedIndexChanged(object sender, EventArgs e)
		{
			ActionManager.Do(SetOuterColorTableAction, new object[] { ((ComboBox)sender).SelectedIndex });
		}

		private bool trackMinValueSlider = false, trackMaxValueSlider = false;
		public bool MouseDown(MouseEventArgs e)
		{
			if(pickerVisible && picker.MouseDown(e))
				return true;
			foreach(GLButton button in buttons)
				if(button.OnMouseDown(e))
					return true;

			if(!bounds.Contains(e.Location))
				return false;

			if(activecursor != null)
			{
				float xr = Math.Min(Math.Max((float)(e.X - bounds.X) / (float)bounds.Width, 0.0f), 1.0f);

				// Find section containing xr
				int sectionIdx;
				for(sectionIdx = 0; sectionIdx < sections.Count; ++sectionIdx)
					if(sections[sectionIdx].start.pos < xr && xr <= sections[sectionIdx].end.pos)
						break;
				if(sectionIdx == sections.Count)
					return true;

				if(activecursor == cursors[0])
				{
					// Insert splitter at xr
					Splitter newSplitter = new Splitter {
						isfixed = false,
						interjector = false,
						pos = xr,
						left = null,
						right = null
					};
					splitters.Add(newSplitter);
					Section newSection = new Section {
						colorMap = sections[sectionIdx].colorMap,
						start = sections[sectionIdx].start,
						end = newSplitter,
						flipped = sections[sectionIdx].flipped,
						interjector = false
					};
					sections[sectionIdx].start.right = newSection;
					sections[sectionIdx].start = newSplitter;
					newSplitter.left = newSection;
					newSplitter.right = sections[sectionIdx];
					sections.Insert(sectionIdx, newSection);
				}
				else
				{
					/*vec3.transformMat4(vpos, [2.0 * 10.0 / canvas.width - 1.0, 0.0, 0.0], invtransform);
					var delta = vpos[0] / 2.0 + 0.5;

					// Insert interjector at xr
					var leftSplitter = {
						fixed: false,
						interjector: false,
						pos: Math.max(xr - delta, sections[sectionIdx].start.pos + 1e-5),
						left: null,
						right: null
					};
					var rightSplitter = {
						fixed: false,
						interjector: false,
						pos: Math.min(xr + delta, sections[sectionIdx].end.pos - 1e-5),
						left: null,
						right: null
					};
					splitters.push(leftSplitter, rightSplitter);
					var newSection = {
						colorMap: sections[sectionIdx].colorMap,
						start: leftSplitter,
						end: rightSplitter,
						flipped: sections[sectionIdx].flipped,
						interjector: true
					};
					sections.splice(sectionIdx, 0, newSection);

					leftSplitter.right = rightSplitter.left = newSection;*/
				}

				ColormapChanged();
			}

			return true;
		}
		public bool MouseMove(MouseEventArgs e)
		{
			if(pickerVisible && picker.MouseMove(e))
				return true;
			
			bool inside = bounds.Contains(e.Location);

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
			}

			float mousevalue = Math.Min(Math.Max((float)(e.X - bounds.X) / (float)bounds.Width, 0.0f), 1.0f);
			if(trackMinValueSlider)
				ActionManager.Do(SetMinValueAction, new object[] { mousevalue });
			if(trackMaxValueSlider)
				ActionManager.Do(SetMaxValueAction, new object[] { mousevalue });

			return inside;
		}
		public bool MouseUp(MouseEventArgs e)
		{
			trackMinValueSlider = trackMaxValueSlider = false;

			if(draggedColormap != null)
			{
				sections[0].colorMap = draggedColormap;
				ColormapChanged();
				draggedColormap = null;
			}

			return bounds.Contains(e.Location);
		}

		private void ColormapChanged()
		{
			settingsChanged = true;
		}

		private void SetInnerColorTable(int index)
		{
			if(cbInnerColorTable.SelectedIndex != index)
				cbInnerColorTable.SelectedIndex = index;

			settings.innerColorTable = ((NamedColorTable)((ComboBox)cbInnerColorTable).SelectedItem).tex;
			settings.nanColor = ((NamedColorTable)((ComboBox)cbInnerColorTable).SelectedItem).nanColor;

			settingsChanged = true;
		}
		private void SetOuterColorTable(int index)
		{
			if(cbOuterColorTable.SelectedIndex != index)
				cbOuterColorTable.SelectedIndex = index;

			settings.outerColorTable = ((NamedColorTable)((ComboBox)cbOuterColorTable).SelectedItem).tex;

			settingsChanged = true;
		}
		private void SetMinValue(float value)
		{
			settings.minValue = value;
			if(settings.minValue > settings.maxValue)
				settings.maxValue = value;

			settingsChanged = true;
		}
		private void SetMaxValue(float value)
		{
			settings.maxValue = value;
			if(settings.maxValue < settings.minValue)
				settings.minValue = value;

			settingsChanged = true;
		}
		private void ResetColorTable()
		{
			SetMinValue(0.0f);
			SetMaxValue(1.0f);
			SetOuterColorTable(0);
			SetInnerColorTable(0);
		}
		public void Reset()
		{
			ActionManager.Do(ResetColorTableAction, new object[] {});
		}

#region "ColorTable creation/loading"
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

