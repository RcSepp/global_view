using System;
using System.Windows.Forms;
using System.Drawing;
using System.Collections.Generic;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace csharp_viewer
{
	public class ParameterIndex : GLControl
	{
		private const int MAX_NUM_PARAMS_SHOWING_VALUES = 20;

		public delegate void ParameterChangedDelegate(Cinema.CinemaStore.Parameter parameter, int paramidx);
		public event ParameterChangedDelegate ParameterChanged;
		public delegate void ParameterLabelMouseDelegate(Cinema.CinemaArgument argument, int argumentIndex);
		public event ParameterLabelMouseDelegate ArgumentParameterMouseDown;

		private static Color4 BORDER_COLOR = new Color4(64, 64, 64, 255);
		private static Color4 TICK_COLOR = new Color4(128, 100, 162, 255);
		private static Color4 CHECK_COLOR = new Color4(142, 180, 247, 100);

		private TransformedImageCollection images;
		private Cinema.CinemaStore.Parameter[] parameters;
		private GLMesh meshBorders, meshTick, meshCheck;

		private Action ParameterChangedAction;

		private class CheckBar
		{
			public string label = "";
			private Rectangle bounds, labelBounds;
			Cinema.CinemaStore.Parameter parameter;
			public bool multicheck;
			private GLMesh meshBorders, meshTick, meshCheck;

			public CheckBar(Cinema.CinemaStore.Parameter parameter, GLMesh meshBorders, GLMesh meshTick, GLMesh meshCheck)
			{
				this.parameter = parameter;
				this.meshBorders = meshBorders;
				this.meshTick = meshTick;
				this.meshCheck = meshCheck;
			}

			public void Draw(Rectangle bounds, int argidx, Size backbufferSize, GLShader sdr, int colorUniform)
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

				GL.Uniform4(colorUniform, CHECK_COLOR);
				meshCheck.Bind(sdr, null);
				y = 0.0f;//(2.0f * bounds.Height - 4.0f) / backbufferSize.Height;
				int xi = 0;
				foreach(bool isChecked in parameter.isChecked)
				{
					if(isChecked)
					{
						float halfwidth = parameter.strValues.Length <= MAX_NUM_PARAMS_SHOWING_VALUES ? Common.fontTextSmall.MeasureString(parameter.strValues[xi]).X / 2.0f + 2.0f : 3.0f;

						x = (float)(xi + 1) / (float)(parameter.strValues.Length + 1);
						x *= 2.0f * bounds.Width / backbufferSize.Width;
						sdr.Bind(Matrix4.CreateScale(halfwidth / bounds.Width, 1.0f, 1.0f) * trans * Matrix4.CreateTranslation(x, y, 0.0f));
						meshCheck.Draw();
					}
					++xi;
				}

				if(parameter.strValues.Length > MAX_NUM_PARAMS_SHOWING_VALUES)
				{
					GL.LineWidth(2.0f);
					sdr.Bind(trans);
					GL.Uniform4(colorUniform, TICK_COLOR);
					meshTick.Bind(sdr, null);

					for(int i = 0; i < parameter.strValues.Length; ++i)
					{
						//x = (float)(i + 1) / (float)(parameter.strValues.Length + 1);
						x = parameter.values[i] / 10.0f;
						sdr.Bind(trans * Matrix4.CreateTranslation(x * 2.0f * bounds.Width / backbufferSize.Width, 0.0f, 0.0f));
						meshTick.Draw();
					}
				}
				else
				{
					y = bounds.Bottom - Common.fontTextSmall.MeasureString(" ").Y + 2.0f;
					for(int i = 0; i < parameter.values.Length; ++i)
					{
						x = (float)(i + 1) / (float)(parameter.strValues.Length + 1);
						Common.fontTextSmall.DrawString(bounds.Left + x * bounds.Width - Common.fontTextSmall.MeasureString(parameter.strValues[i]).X / 2.0f, y, parameter.strValues[i], backbufferSize);
					}
				}

				GL.LineWidth(1.0f);

				Vector2 strsize = Common.fontText2.MeasureString(label);
				Common.fontText2.DrawString(bounds.X - strsize.X - 2, bounds.Y - 1, label, backbufferSize);
				this.labelBounds = new Rectangle((int)(bounds.X - strsize.X - 2), bounds.Y - 1, (int)strsize.X, (int)strsize.Y);
			}

			public bool TickFromMousePosition(Point mousepos, out int tick)
			{
				tick = -2;

				if(bounds.Contains(mousepos))
				{
					int x = (int)Math.Round((float)(mousepos.X - bounds.X) * (float)(parameter.strValues.Length + 1) / (float)bounds.Width) - 1;
					if(x < 0 || x >= parameter.strValues.Length)
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
		private List<CheckBar> checkbars = new List<CheckBar>();

		public void Init()
		{
			Vector3[] border_positions = {
				new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 1.0f, 0.0f),
				new Vector3(0.0f, 1.0f, 0.0f), new Vector3(1.0f, 1.0f, 0.0f),
				new Vector3(1.0f, 1.0f, 0.0f), new Vector3(1.0f, 0.0f, 0.0f),
				new Vector3(1.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f),

				new Vector3(1.01f, 0.0f, 0.0f), new Vector3(1.01f, 1.0f, 0.0f),
				new Vector3(1.01f, 1.0f, 0.0f), new Vector3(1.06f, 1.0f, 0.0f),
				new Vector3(1.06f, 1.0f, 0.0f), new Vector3(1.06f, 0.0f, 0.0f),
				new Vector3(1.06f, 0.0f, 0.0f), new Vector3(1.01f, 0.0f, 0.0f)
			};
			meshBorders = new GLMesh(border_positions, null, null, null, null, null, PrimitiveType.Lines);

			Vector3[] tick_positions = {
				new Vector3(0.0f, 0.2f, 0.0f),
				new Vector3(0.0f, 0.8f, 0.0f)
			};
			meshTick = new GLMesh(tick_positions, null, null, null, null, null, PrimitiveType.Lines);

			meshCheck = new GLMesh(new Vector3[] {new Vector3(-1.0f, -0.1f, 0.0f), new Vector3(-1.0f, 1.15f, 0.0f), new Vector3(1.0f, 1.15f, 0.0f), new Vector3(1.0f, -0.1f, 0.0f)}, null, null, null, null, null, PrimitiveType.TriangleFan);

			ParameterChangedAction = ActionManager.CreateAction<int, bool[]>("Called when the checked states of the values of a parameter have changed", "", delegate(object[] p) {
				int paramidx = (int)p[0];
				bool[] isChecked = (bool[])p[1];
				Array.Copy(isChecked, parameters[paramidx].isChecked, isChecked.Length);
				ParameterChanged(parameters[paramidx], paramidx);
				return null;
			});
		}

		public void Load()
		{
			this.images = Viewer.images;
			this.parameters = Global.parameters;

			// Create check bars for each parameter
			checkbars.Clear();
			int paramidx = 0;
			foreach(Cinema.CinemaStore.Parameter parameter in parameters)
			{
				CheckBar newcheckbar = new CheckBar(parameter, meshBorders, meshTick, meshCheck);
				newcheckbar.label = parameter.label;
				newcheckbar.multicheck = parameter.type == "option";
				checkbars.Add(newcheckbar);

				if(ParameterChanged != null)
				{
					bool[] initialIsChecked = new bool[parameter.isChecked.Length];
					Array.Copy(parameter.isChecked, initialIsChecked, initialIsChecked.Length);
					ActionManager.Do(ParameterChangedAction, paramidx++, initialIsChecked);
				}
			}
		}

		public void Unload()
		{
			images = null;
			parameters = null;
			checkbars.Clear();
		}

		protected override void Draw(float dt, Matrix4 _transform)
		{
			GL.Disable(EnableCap.DepthTest);

			int bounds_y = Bounds.Y;

			int paramidx = 0;
			foreach(CheckBar checkbar in checkbars)
			{
				/*HashSet<int> selected_ticks = new HashSet<int>();
				foreach(TransformedImage selectedimage in selection)
				{
					int idx = Array.IndexOf(selectedimage.args[i].values, selectedimage.values[i]);
					selected_ticks.Add(idx);
				}
				++i;*/

				checkbar.Draw(Bounds, paramidx++, BackbufferSize, Common.sdrSolidColor, Common.sdrSolidColor_colorUniform); //TODO: make constraint sets indexable. Otherwise selection[i++] will crash for meta data trackbars
				Bounds = new Rectangle(Bounds.X, Bounds.Y + Bounds.Height * 3 / 2, Bounds.Width, Bounds.Height);// Bounds.Y += Bounds.Height * 3 / 2;
			}

			Bounds = new Rectangle(Bounds.X, bounds_y, Bounds.Width, Bounds.Height);

			GL.Enable(EnableCap.DepthTest);
		}

		private bool TickFromMousePosition(Point mousepos, out Point tick)
		{
			tick = Point.Empty;

			int x, y = 0;
			foreach(CheckBar checkbar in checkbars)
			{
				if(checkbar.TickFromMousePosition(mousepos, out x))
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
			for(int i = 0; i < checkbars.Count; ++i)
				if(checkbars[i].LabelContainsPoint(e.Location))
				{
					if(ArgumentParameterMouseDown != null)
						ArgumentParameterMouseDown(parameters[i], i);
					return true;
				}

			Point tick;
			if(TickFromMousePosition(e.Location, out tick))
			{
				bool[] newIsChecked = new bool[parameters[tick.Y].isChecked.Length];

				if(tick.X == -1)
				{
					for(int i = 0; i < parameters[tick.Y].values.Length; ++i)
						newIsChecked[i] = true;
					capturedTick = null;
				}
				else
				{
					if(InputDevices.kbstate.IsKeyUp(OpenTK.Input.Key.LControl))
						for(int i = 0; i < parameters[tick.Y].values.Length; ++i)
							newIsChecked[i] = false;
					else
						for(int i = 0; i < parameters[tick.Y].values.Length; ++i)
							newIsChecked[i] = parameters[tick.Y].isChecked[i];
					newIsChecked[tick.X] = true;
				}

				if(ParameterChanged != null)
					ActionManager.Do(ParameterChangedAction, tick.Y, newIsChecked);
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
				Cinema.CinemaStore.Parameter parameter = parameters[y];
				int x = (int)Math.Round((float)(e.Location.X - bounds.X) * (float)(parameter.values.Length + 1) / (float)bounds.Width) - 1;
				if(x < 0)
					x = 0;
				else if(x >= parameter.values.Length)
					x = parameter.values.Length - 1;
				if(x != capturedTick.Value.X)
				{
					/*if(InputDevices.kbstate.IsKeyUp(OpenTK.Input.Key.LControl))
						selection[y].Clear();
					selection[y].Add(x);
					if(SelectionChanged != null)
						SelectionChanged(selection);*/
				}
			}
			return false;
		}
	}
}

