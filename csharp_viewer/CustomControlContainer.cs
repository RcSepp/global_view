using System;
using System.Windows.Forms;
using System.Drawing;
using System.Collections.Generic;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace csharp_viewer
{
	public class CustomControlContainer : GLControl
	{
		public delegate void CustomControlValueChangedDelegate(int controlIdx, float value);
		public event CustomControlValueChangedDelegate CustomControlValueChanged;

		private static Color4 BORDER_COLOR = new Color4(64, 64, 64, 255);
		private static Color4 TICK_COLOR = new Color4(128, 100, 162, 255);
		private static Color4 SLIDER_COLOR = new Color4(142, 180, 247, 100);

		private GLMesh meshBorders, meshTick, meshSlider;

		private Action CustomControlValueChangedAction, RemoveCustomControlsAction;

		public class Slider
		{
			public string label = "";
			private Rectangle bounds, labelBounds;
			private GLMesh meshBorders, meshTick, meshSlider;
			public readonly float[] values;
			private readonly float minValue, valueRange;
			private float currentValue;
			public float Value {
				get {
					return currentValue;
				}
				set {
					if(currentValue != value && CustomControlValueChanged != null)
						CustomControlValueChanged(controlIdx, currentValue = value);
					else
						currentValue = value;
				}
			}
			public readonly int controlIdx;

			public event CustomControlValueChangedDelegate CustomControlValueChanged;

			public Slider(int controlIdx, float[] values, GLMesh meshBorders, GLMesh meshTick, GLMesh meshSlider)
			{
				this.controlIdx = controlIdx;

				this.values = values;
				currentValue = values.Length > 0 ? values[0] : 0.0f;
				minValue = float.MaxValue;
				float maxValue = float.MinValue;
				foreach(float v in values)
				{
					minValue = Math.Min(minValue, v);
					maxValue = Math.Max(maxValue, v);
				}
				valueRange = maxValue - minValue;

				this.meshBorders = meshBorders;
				this.meshTick = meshTick;
				this.meshSlider = meshSlider;
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

				GL.Uniform4(colorUniform, SLIDER_COLOR);
				meshSlider.Bind(sdr, null);
				y = 0.0f;//(2.0f * bounds.Height - 4.0f) / backbufferSize.Height;
				//int xi = 0;
				float sliderWidth = 20.0f;

				//x = (float)(xi + 1) / (float)(values.Length + 1);
				x = (currentValue - minValue) / valueRange;
				x *= (2.0f * bounds.Width) / backbufferSize.Width;
				x = (sliderWidth + x * (bounds.Width - sliderWidth)) / bounds.Width;
				sdr.Bind(Matrix4.CreateScale(sliderWidth / (2.0f * bounds.Width), 1.0f, 1.0f) * trans * Matrix4.CreateTranslation(x, y, 0.0f));
				meshSlider.Draw();
					
				GL.LineWidth(2.0f);
				sdr.Bind(trans);
				GL.Uniform4(colorUniform, TICK_COLOR);
				meshTick.Bind(sdr, null);

				for(int i = 0; i < values.Length; ++i)
				{
					//x = (float)(i + 1) / (float)(values.Length + 1);
					x = (values[i] - minValue) / valueRange;
					sdr.Bind(trans * Matrix4.CreateTranslation((x * 2.0f * (bounds.Width - sliderWidth) + sliderWidth) / backbufferSize.Width, 0.0f, 0.0f));
					meshTick.Draw();
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
					if(mousepos.X < bounds.X || mousepos.X >= bounds.Right)
						return false;
					
					float mousevalue = (float)(mousepos.X - bounds.X) / (float)bounds.Width;
					mousevalue = mousevalue * valueRange + minValue;

					int closestValueIdx = -2;
					float closestValueDelta = float.MaxValue; // Tolerance = float.MaxValue
					for(int idx = 0; idx < values.Length; ++idx)
						if(Math.Abs(values[idx] - mousevalue) < closestValueDelta)
						{
							closestValueIdx = idx;
							closestValueDelta = Math.Abs(values[idx] - mousevalue);
						}
					if(closestValueIdx == -2)
						return false;
					tick = closestValueIdx;
					return true;
				}

				return false;
			}
			public int TickFromMousePosition2(Point mousepos)
			{
				if(mousepos.X < bounds.X)
					return 0;
				if(mousepos.X >= bounds.Right)
					return values.Length - 1;

				float mousevalue = (float)(mousepos.X - bounds.X) / (float)bounds.Width;
				mousevalue = mousevalue * valueRange + minValue;

				int closestValueIdx = -2;
				float closestValueDelta = float.MaxValue; // Tolerance = float.MaxValue
				for(int idx = 0; idx < values.Length; ++idx)
					if(Math.Abs(values[idx] - mousevalue) < closestValueDelta)
					{
						closestValueIdx = idx;
						closestValueDelta = Math.Abs(values[idx] - mousevalue);
					}
				return closestValueIdx;
			}

			public bool LabelContainsPoint(Point pt) { return labelBounds.Contains(pt); }
		}
		private List<Slider> sliders = new List<Slider>();

		public void Init()
		{
			Vector3[] border_positions = {
				new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 1.0f, 0.0f),
				new Vector3(0.0f, 1.0f, 0.0f), new Vector3(1.0f, 1.0f, 0.0f),
				new Vector3(1.0f, 1.0f, 0.0f), new Vector3(1.0f, 0.0f, 0.0f),
				new Vector3(1.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f)
			};
			meshBorders = new GLMesh(border_positions, null, null, null, null, null, PrimitiveType.Lines);

			Vector3[] tick_positions = {
				new Vector3(0.0f, 0.2f, 0.0f),
				new Vector3(0.0f, 0.8f, 0.0f)
			};
			meshTick = new GLMesh(tick_positions, null, null, null, null, null, PrimitiveType.Lines);

			meshSlider = new GLMesh(new Vector3[] {new Vector3(-1.0f, -0.1f, 0.0f), new Vector3(-1.0f, 1.15f, 0.0f), new Vector3(1.0f, 1.15f, 0.0f), new Vector3(1.0f, -0.1f, 0.0f)}, null, null, null, null, null, PrimitiveType.TriangleFan);

			CustomControlValueChangedAction = ActionManager.CreateAction<int, float>("Called when a custom control's value has changed", "", delegate(object[] p) {
				int controlIdx = (int)p[0];
				float value = (float)p[1];
				if(CustomControlValueChanged != null)
					CustomControlValueChanged(controlIdx, sliders[controlIdx].Value = value);
				else
					sliders[controlIdx].Value = value;
				return null;
			});
			RemoveCustomControlsAction = ActionManager.CreateAction("Remove all custom controls", "", delegate(object[] p) {
				sliders.Clear();
				return null;
			});
			ActionManager.Do(RemoveCustomControlsAction);
		}

		public Slider CreateSlider(string label, float[] values)
		{
			Slider slider = new Slider(sliders.Count, values, meshBorders, meshTick, meshSlider);
			slider.label = label;
			sliders.Add(slider);
			ActionManager.Do(CustomControlValueChangedAction, slider.controlIdx, slider.Value);
			return slider;
		}

		public void Unload()
		{
			sliders.Clear();
		}

		protected override void Draw(float dt, Matrix4 _transform)
		{
			GL.Disable(EnableCap.DepthTest);

			int bounds_y = Bounds.Y;

			int paramidx = 0;
			foreach(Slider slider in sliders)
			{
				slider.Draw(Bounds, paramidx++, BackbufferSize, Common.sdrSolidColor, Common.sdrSolidColor_colorUniform); //TODO: make constraint sets indexable. Otherwise selection[i++] will crash for meta data trackbars
				Bounds = new Rectangle(Bounds.X, Bounds.Y - Bounds.Height * 3 / 2, Bounds.Width, Bounds.Height);// Bounds.Y += Bounds.Height * 3 / 2;
			}

			Bounds = new Rectangle(Bounds.X, bounds_y, Bounds.Width, Bounds.Height);

			GL.Enable(EnableCap.DepthTest);
		}

		private bool TickFromMousePosition(Point mousepos, out Point tick)
		{
			tick = Point.Empty;

			int x, y = 0;
			foreach(Slider slider in sliders)
			{
				if(slider.TickFromMousePosition(mousepos, out x))
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
			for(int i = 0; i < sliders.Count; ++i)
				if(sliders[i].LabelContainsPoint(e.Location))
				{
					/*if(ArgumentParameterMouseDown != null)
						ArgumentParameterMouseDown(parameters[i], i);*/
					return true;
				}

			Point tick;
			if(TickFromMousePosition(e.Location, out tick))
			{
				ActionManager.Do(CustomControlValueChangedAction, tick.Y, sliders[tick.Y].values[tick.X]);
				capturedTick = tick;
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
				int y = capturedTick.Value.Y;
				Slider slider = sliders[y];

				int x = slider.TickFromMousePosition2(e.Location);
				if(x >= 0 && x != capturedTick.Value.X)
				{
					ActionManager.Do(CustomControlValueChangedAction, y, slider.values[x]);
					capturedTick = new Point(x, y);
				}
			}
			return false;
		}
	}
}

