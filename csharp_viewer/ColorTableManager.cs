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
	public class ColorTableManager
	{
		private static int COLOR_TABLE_SIZE = 1024;

		private static GLShader colortableshader = null;
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
					value = 1.0 - vtexcoord.y;
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
		}

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

		public class NamedColorTable
		{
			public GLTexture1D tex;
			public Vector3 nanColor;
			public string name;

			public NamedColorTable(GLTexture1D tex, Vector3 nanColor, string name)
			{
				this.name = name;
				this.nanColor = nanColor;
				this.tex = tex;
			}
			public static NamedColorTable None = new NamedColorTable(null, Vector3.Zero, "none");

			public override string ToString()
			{
				return name;
			}
		}

		public class ColorTableSettings
		{
			public GLTexture1D innerColorTable, outerColorTable;
			public bool hasOuterColorTable;
			public float minValue, maxValue;
			public Vector3 nanColor;
		}
		private ColorTableSettings settings = new ColorTableSettings();

		public delegate void ColorTableSettingsChangedDelegate(ColorTableSettings settings);

		private readonly Rectangle bounds;
		private readonly GLMesh meshquad;
		private ComboBox cbInnerColorTable, cbOuterColorTable;
		private ColorTableSettingsChangedDelegate OnColorTableSettingsChanged;
		private GLMesh meshLines, meshHistogram;
		private bool settingsChanged = true;

		private Action SetInnerColorTableAction, SetOuterColorTableAction, SetMinValueAction, SetMaxValueAction, ResetColorTableAction;

		public ColorTableManager(Rectangle bounds, GLMesh meshquad, FlowLayoutPanel pnlImageControls, ColorTableSettingsChangedDelegate OnColorTableSettingsChanged)
		{
			this.bounds = bounds;
			this.meshquad = meshquad;
			this.OnColorTableSettingsChanged = OnColorTableSettingsChanged;

			SetInnerColorTableAction = ActionManager.CreateAction("Set inner color table", this, "SetInnerColorTable");
			SetOuterColorTableAction = ActionManager.CreateAction("Set outer color table", this, "SetOuterColorTable");
			SetMinValueAction = ActionManager.CreateAction("Set nested color table start", this, "SetMinValue");
			SetMaxValueAction = ActionManager.CreateAction("Set nested color table end", this, "SetMaxValue");
			ResetColorTableAction = ActionManager.CreateAction("Reset color table", this, "ResetColorTable");

			// Create shaders as singleton
			if(colortableshader == null)
				colortableshader = new GLShader(new string[] {COLOR_TABLE_SHADER.VS}, new string[] {COLOR_TABLE_SHADER.FS});
			if(lineshader == null)
				lineshader = new GLShader(new string[] {LINE_SHADER.VS}, new string[] {LINE_SHADER.FS});

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
			cbInnerColorTable.Items.Add(ColorTableFromRange(C0, C1, new Vector3(65.0f / 255.0f, 68.0f / 255.0f, 91.0f / 255.0f), "Moreland cool/warm"));
			cbInnerColorTable.Items.AddRange(ColorTableFromXml("ColorMaps.xml"));
			cbOuterColorTable.Items.Add(NamedColorTable.None);
			foreach(NamedColorTable item in cbInnerColorTable.Items)
				cbOuterColorTable.Items.Add(item);
			//cbInnerColorTable.SelectedIndex = cbOuterColorTable.SelectedIndex = 0;

			//ResetColorTable();

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
				float y = (float)i / 40.0f;
				positions.Add(new Vector3(1.0f, y, 0.0f));
				positions.Add(new Vector3(i % 10 == 0 ? 1.6f : 1.3f, y, 0.0f));
			}
			meshLines = new GLMesh(positions.ToArray(), null, null, null, null, null, PrimitiveType.Lines);

			// Create number font
			//font = new GLNumberFont("HelveticaNeue_12.png", new FontDefinition(new int[] {0, 14, 26, 39, 53, 67, 80, 93, 106, 120, 133}, new int[] {0, 19}), meshquad, true);
			//font = new GLNumberFont("HelveticaNeue_16.png", new FontDefinition(new int[] {0, 18, 34, 53, 71, 89, 106, 124, 142, 160, 178}, new int[] {0, 25}), true);
			//font = new GLTextFont("fntDefault.png", new Vector2(19.0f, 32.0f), meshquad);
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

		public void Draw(Size backbufferSize)
		{
			if(settings.innerColorTable == null)
				return;

			if(settingsChanged)
			{
				settingsChanged = false;

				colortableshader.Bind();

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

				OnColorTableSettingsChanged(settings);
			}

			if(settings.outerColorTable != null)
			{
				GL.ActiveTexture(TextureUnit.Texture1);
				settings.outerColorTable.Bind();
			}
			GL.ActiveTexture(TextureUnit.Texture0);
			settings.innerColorTable.Bind();

			Matrix4 trans = Matrix4.Identity;
			trans *= Matrix4.CreateScale(2.0f * bounds.Width / backbufferSize.Width, (2.0f * bounds.Height - 4.0f) / backbufferSize.Height, 1.0f);
			trans *= Matrix4.CreateTranslation(-1.0f + 2.0f * (bounds.Left + 0.5f) / backbufferSize.Width, 1.0f - 2.0f * (bounds.Bottom - 0.5f) / backbufferSize.Height, 0.0f);

			colortableshader.Bind(trans);
			meshquad.Bind(colortableshader, null);
			meshquad.Draw();

			lineshader.Bind(trans);
			meshLines.Bind(lineshader, null);
			meshLines.Draw();
			if(meshHistogram != null)
			{
				meshHistogram.Bind(lineshader, null);
				meshHistogram.Draw();
			}

			Common.fontText.DrawString(bounds.Right + 12, bounds.Top - 10, "100%", backbufferSize);
			Common.fontText.DrawString(bounds.Right + 12, bounds.Bottom - 10, "0%", backbufferSize);
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
			if(!bounds.Contains(e.Location))
				return false;

			float mousevalue = 1.0f - Math.Min(Math.Max((float)(e.Y - bounds.Y) / (float)bounds.Height, 0.0f), 1.0f);
			float mousetomin = Math.Abs(mousevalue - settings.minValue);
			float mousetomax = Math.Abs(mousevalue - settings.maxValue);

			if(mousetomin < mousetomax)
			{
				if(mousetomin < 10.0f / (float)bounds.Height) // 10.0 ... snap distance [pixel]
				{
					trackMinValueSlider = true;

					ActionManager.Do(SetMinValueAction, new object[] { mousevalue });
				}
			}
			else
			{
				if(mousetomax < 10.0f / (float)bounds.Height) // 10.0 ... snap distance [pixel]
				{
					trackMaxValueSlider = true;

					ActionManager.Do(SetMaxValueAction, new object[] { mousevalue });
				}
			}

			return true;
		}
		public bool MouseMove(MouseEventArgs e)
		{
			float mousevalue = 1.0f - Math.Min(Math.Max((float)(e.Y - bounds.Y) / (float)bounds.Height, 0.0f), 1.0f);
			if(trackMinValueSlider)
				ActionManager.Do(SetMinValueAction, new object[] { mousevalue });
			if(trackMaxValueSlider)
				ActionManager.Do(SetMaxValueAction, new object[] { mousevalue });

			return bounds.Contains(e.Location);
		}
		public bool MouseUp(MouseEventArgs e)
		{
			trackMinValueSlider = trackMaxValueSlider = false;

			return bounds.Contains(e.Location);
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
		private static NamedColorTable ColorTableFromRange(ColorMapCreator.Vector3 cmin, ColorMapCreator.Vector3 cmax, Vector3 nanColor, string name)
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
			return new NamedColorTable(new GLTexture1D(colorTable, COLOR_TABLE_SIZE, false), nanColor, name);
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
						colorTables.Add(new NamedColorTable(new GLTexture1D(bytes, COLOR_TABLE_SIZE, false), nanColor, colorMapName));
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
			while(reader.MoveToNextAttribute())
				if(reader.Name == name)
					return reader.Value;
			return null;
		}
#endregion
	}
}

