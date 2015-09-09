using System;
using System.Windows.Forms;
using System.Drawing;
using System.Collections.Generic;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace csharp_viewer
{
	public static class ARGUMENT_INDEX_SHADER
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
			uniform vec4 Color;

			void main()
			{
				gl_FragColor = Color;
			}
		";
	}

	public class ArgumentIndex
	{
		public event Selection.ChangedDelegate SelectionChanged;
		public delegate void ArgumentLabelMouseDelegate(Cinema.CinemaArgument argument, int argumentIndex);
		public event ArgumentLabelMouseDelegate ArgumentLabelMouseDown;

		private static Color4 BORDER_COLOR = new Color4(64, 64, 64, 255);
		private static Color4 TICK_COLOR = new Color4(128, 100, 162, 255);
		private static Color4 SELECTION_COLOR = new Color4(142, 180, 247, 255);
		private static Color4 PRESELECTION_COLOR = new Color4(142, 180, 247, 128);

		private Dictionary<int[], TransformedImage> images;
		private Cinema.CinemaArgument[] arguments;
		private GLMesh meshSelection;

		private IndexProductSelection selection;

		private class TrackBar
		{
			public string label = "";
			private Rectangle bounds, labelBounds;
			int numvalues;
			private GLMesh meshSelection, meshBorders, meshTicks;

			public TrackBar(int numvalues, GLMesh meshSelection)
			{
				this.numvalues = numvalues;
				this.meshSelection = meshSelection;

				// Create border mesh
				List<Vector3> border_positions = new List<Vector3>();
				Vector3[] tick_positions = new Vector3[2 * numvalues];

				/*border_positions.Add(new Vector3(1.0f, 0.0f, 0.0f));
				border_positions.Add(new Vector3(1.0f, 1.0f, 0.0f));
				border_positions.Add(new Vector3(0.0f, 1.0f, 0.0f));
				border_positions.Add(new Vector3(0.0f, 0.0f, 0.0f));
				border_positions.Add(new Vector3(1.06f, 0.0f, 0.0f));
				border_positions.Add(new Vector3(1.06f, 1.0f, 0.0f));
				border_positions.Add(new Vector3(1.0f, 1.0f, 0.0f));*/

				border_positions.Add(new Vector3(0.0f, 0.0f, 0.0f)); border_positions.Add(new Vector3(0.0f, 1.0f, 0.0f));
				border_positions.Add(new Vector3(0.0f, 1.0f, 0.0f)); border_positions.Add(new Vector3(1.0f, 1.0f, 0.0f));
				border_positions.Add(new Vector3(1.0f, 1.0f, 0.0f)); border_positions.Add(new Vector3(1.0f, 0.0f, 0.0f));
				border_positions.Add(new Vector3(1.0f, 0.0f, 0.0f)); border_positions.Add(new Vector3(0.0f, 0.0f, 0.0f));

				border_positions.Add(new Vector3(1.01f, 0.0f, 0.0f)); border_positions.Add(new Vector3(1.01f, 1.0f, 0.0f));
				border_positions.Add(new Vector3(1.01f, 1.0f, 0.0f)); border_positions.Add(new Vector3(1.06f, 1.0f, 0.0f));
				border_positions.Add(new Vector3(1.06f, 1.0f, 0.0f)); border_positions.Add(new Vector3(1.06f, 0.0f, 0.0f));
				border_positions.Add(new Vector3(1.06f, 0.0f, 0.0f)); border_positions.Add(new Vector3(1.01f, 0.0f, 0.0f));

				for(int i = 0; i < numvalues; ++i)
				{
					float x = (float)(i + 1) / (float)(numvalues + 1);
					tick_positions[2 * i + 0] = new Vector3(x, 0.2f, 0.0f);
					tick_positions[2 * i + 1] = new Vector3(x, 0.8f, 0.0f);
				}

				meshBorders = new GLMesh(border_positions.ToArray(), null, null, null, null, null, PrimitiveType.Lines);
				meshTicks = new GLMesh(tick_positions, null, null, null, null, null, PrimitiveType.Lines);
			}

			public void Draw(Rectangle bounds, HashSet<int> selection, Size backbufferSize, GLShader sdr, int colorUniform)
			{
				float x, y;

				this.bounds = bounds;

				Matrix4 trans = Matrix4.Identity;
				trans *= Matrix4.CreateScale(2.0f * bounds.Width / backbufferSize.Width, (2.0f * bounds.Height - 4.0f) / backbufferSize.Height, 1.0f);
				trans *= Matrix4.CreateTranslation(-1.0f + 2.0f * (bounds.Left + 0.5f) / backbufferSize.Width, 1.0f - 2.0f * (bounds.Bottom - 0.5f) / backbufferSize.Height, 0.0f);

				sdr.Bind(trans);
				GL.Uniform4(colorUniform, BORDER_COLOR);
				meshBorders.Bind(sdr, null);
				meshBorders.Draw();

				GL.LineWidth(6.0f);
				if(selection != null)
				{
					GL.Uniform4(colorUniform, SELECTION_COLOR);
					meshSelection.Bind(sdr, null);
					y = 0.0f;//(2.0f * bounds.Height - 4.0f) / backbufferSize.Height;
					foreach(int xi in selection)
					{
						x = (float)(xi + 1) / (float)(numvalues + 1);
						x *= 2.0f * bounds.Width / backbufferSize.Width;
						sdr.Bind(trans * Matrix4.CreateTranslation(x, y, 0.0f));
						meshSelection.Draw();
					}
				}

				/*Point preSelectedTick;
				if(TickFromMousePosition(mousepos, backbufferSize, out preSelectedTick))
				{
					GL.Uniform4(sdrColored_colorParam, PRESELECTION_COLOR);
					if(preSelectedTick.X == -1)
						for(preSelectedTick.X = 0; preSelectedTick.X < arguments[preSelectedTick.Y].values.Length; ++preSelectedTick.X)
						{
							x = (float)(preSelectedTick.X + 1) / (float)(arguments[preSelectedTick.Y].values.Length + 1);
							sdrColored.Bind(trans * Matrix4.CreateTranslation(x * 2.0f * bounds.Width / backbufferSize.Width, preSelectedTick.Y * -1.5f * (2.0f * bounds.Height - 4.0f) / backbufferSize.Height, 0.0f));
							meshSelection.Draw();
						}
					else
					{
						x = (float)(preSelectedTick.X + 1) / (float)(arguments[preSelectedTick.Y].values.Length + 1);
						sdrColored.Bind(trans * Matrix4.CreateTranslation(x * 2.0f * bounds.Width / backbufferSize.Width, preSelectedTick.Y * -1.5f * (2.0f * bounds.Height - 4.0f) / backbufferSize.Height, 0.0f));
						meshSelection.Draw();
					}
				}*/

				GL.LineWidth(2.0f);
				sdr.Bind(trans);
				GL.Uniform4(colorUniform, TICK_COLOR);
				meshTicks.Bind(sdr, null);
				meshTicks.Draw();

				GL.LineWidth(1.0f);

				Vector2 strsize = Common.fontText.MeasureString(label);
				Common.fontText.DrawString(bounds.X - strsize.X - 2, bounds.Y - 1, label, backbufferSize);
				this.labelBounds = new Rectangle((int)(bounds.X - strsize.X - 2), bounds.Y - 1, (int)strsize.X, (int)strsize.Y);
			}

			public bool TickFromMousePosition(Point mousepos, out int tick)
			{
				tick = -2;

				if(bounds.Contains(mousepos))
				{
					int x = (int)Math.Round((float)(mousepos.X - bounds.X) * (float)(numvalues + 1) / (float)bounds.Width) - 1;
					if(x < 0 || x >= numvalues)
						return false;
					tick = x;
					return true;
				}
				if(mousepos.Y >= bounds.Top && mousepos.Y < bounds.Bottom && mousepos.X > bounds.Right)
				{
					tick = -1;
					return true;
				}

				return false;
			}

			public bool LabelContainsPoint(Point pt) { return labelBounds.Contains(pt); }
		}
		private List<TrackBar> trackbars = new List<TrackBar>();

		public void Init()
		{
			// Load shader
			Common.sdrSolidColor = new GLShader(new string[] {ARGUMENT_INDEX_SHADER.VS}, new string[] {ARGUMENT_INDEX_SHADER.FS});
			Common.sdrSolidColor.Bind();

			meshSelection = new GLMesh(new Vector3[] {new Vector3(0.0f, -0.1f, 0.0f), new Vector3(0.0f, 1.15f, 0.0f)}, null, null, null, null, null, PrimitiveType.Lines);
		}

		public void Load(Dictionary<int[], TransformedImage> images, Cinema.CinemaArgument[] arguments, Dictionary<string, HashSet<object>> valuerange)
		{
			this.images = images;
			this.arguments = arguments;

			selection = new IndexProductSelection(arguments.Length, valuerange.Count, images);

			// Create track bars for each argument
			foreach(Cinema.CinemaArgument argument in arguments)
			{
				TrackBar newtrackbar = new TrackBar(argument.values.Length, meshSelection);
				newtrackbar.label = argument.label;
				trackbars.Add(newtrackbar);
			}

			// Create track bars for meta data value
			foreach(KeyValuePair<string, HashSet<object>> range in valuerange)
			{
				TrackBar newtrackbar = new TrackBar(range.Value.Count, meshSelection);
				newtrackbar.label = range.Key;
				trackbars.Add(newtrackbar);
			}
		}

		public void Unload()
		{
			images = null;
			arguments = null;
			selection = null;
			trackbars.Clear();
		}
			
		public void Draw(Point mousepos, Size backbufferSize)
		{
			GL.Disable(EnableCap.DepthTest);

			Rectangle bounds = new Rectangle(backbufferSize.Width - 330, 10, 300, 16);

			int i = 0;
			foreach(TrackBar trackbar in trackbars)
			{
				trackbar.Draw(bounds, selection[i++], backbufferSize, Common.sdrSolidColor, Common.sdrSolidColor_colorUniform); //TODO: make constraint sets indexable. Otherwise selection[i++] will crash for meta data trackbars
				bounds.Y += bounds.Height * 3 / 2;
			}

			GL.Enable(EnableCap.DepthTest);
		}

		private bool TickFromMousePosition(Point mousepos, out Point tick)
		{
			tick = Point.Empty;

			int x, y = 0;
			foreach(TrackBar trackbar in trackbars)
			{
				if(trackbar.TickFromMousePosition(mousepos, out x))
				{
					tick = new Point(x, y);
					return true;
				}
				++y;
			}
			return false;
		}

		private Point? capturedTick = null;
		public bool MouseDown(Size backbufferSize, MouseEventArgs e)
		{
			for(int i = 0; i < trackbars.Count; ++i)
				if(trackbars[i].LabelContainsPoint(e.Location))
				{
					if(ArgumentLabelMouseDown != null)
						ArgumentLabelMouseDown(arguments[i], i);
					return true;
				}

			Point tick;
			if(TickFromMousePosition(e.Location, out tick))
			{
if(tick.X == -1)
{
	for(tick.X = 0; tick.X < arguments[tick.Y].values.Length; ++tick.X)
		selection[tick.Y].Add(tick.X);
	capturedTick = null;
}
else
{
				if(InputDevices.kbstate.IsKeyUp(OpenTK.Input.Key.LControl))
					selection[tick.Y].Clear();
				selection[tick.Y].Add(tick.X);
				capturedTick = tick;
}
				if(SelectionChanged != null)
					SelectionChanged(selection);
				return true;
			}
			capturedTick = null;
			return false;
		}
		public bool MouseUp(Size backbufferSize, MouseEventArgs e)
		{
			capturedTick = null;
			return false;
		}
		public bool MouseMove(Size backbufferSize, MouseEventArgs e)
		{
			if(capturedTick.HasValue)
			{
				Rectangle bounds = new Rectangle(backbufferSize.Width - 330, 10, 300, 16);
				int y = capturedTick.Value.Y;
				Cinema.CinemaArgument argument = arguments[y];
				int x = (int)Math.Round((float)(e.Location.X - bounds.X) * (float)(argument.values.Length + 1) / (float)bounds.Width) - 1;
				if(x < 0)
					x = 0;
				else if(x >= argument.values.Length)
					x = argument.values.Length - 1;
				if(x != capturedTick.Value.X)
				{
					if(InputDevices.kbstate.IsKeyUp(OpenTK.Input.Key.LControl))
						selection[y].Clear();
					selection[y].Add(x);
					if(SelectionChanged != null)
						SelectionChanged(selection);
				}
			}
			return false;
		}

		public void OnSelectionChanged(Selection _selection)
		{
			if(!(_selection is IndexProductSelection))
				for(int i = 0; i < arguments.Length; ++i)
					selection[i].Clear();
		}

		public void SelectAll()
		{
			for(int i = 0; i < arguments.Length; ++i)
				for(int j = 0; j < arguments[i].values.Length; ++j)
					selection[i].Add(j);
			if(SelectionChanged != null)
				SelectionChanged(selection);
		}
	}
}

