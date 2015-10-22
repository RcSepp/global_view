using System;
using System.Drawing;
using System.Collections.Generic;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace csharp_viewer
{
	public class ImageContextMenu
	{
		private static int MIN_WIDTH = 200;
		private static Color4 BACKGROUND_COLOR = new Color4(236, 236, 236, 255);
		private static Color4 HIGHLIGHT_COLOR = new Color4(50, 132, 254, 255);

		public delegate void MenuButtonClickDelegate(MenuButton sender);

		private MenuGroup menu;
		private bool shown = false;
		private float alpha = 0.0f;
		private Rectangle bounds;

		public abstract class MenuControl
		{
			public string text;
			public object tag;
		}
		public class MenuButton : MenuControl
		{
			public MenuButtonClickDelegate onclick;

			public MenuButton(string text, MenuButtonClickDelegate onclick = null)
			{
				this.text = text;
				this.onclick = onclick;
			}
		}
		public class MenuGroup : MenuControl
		{
			public List<MenuControl> controls = new List<MenuControl>();
			public Size size;
			public MenuControl mouseOverControl = null;

			public MenuGroup(string text)
			{
				this.text = text;
			}

			public void ComputeSize()
			{
				size = Size.Empty;
				foreach(MenuControl ctrl in controls)
				{
					Vector2 ctrlsize = Common.fontText.MeasureString(ctrl.text);
					size.Width = Math.Max(size.Width, (int)Math.Ceiling(ctrlsize.X));
					size.Height += (int)Math.Ceiling(ctrlsize.Y);
				}
				size.Width = Math.Max(ImageContextMenu.MIN_WIDTH, size.Width + 8);
				size.Height += 8;
			}

			private int ControlIndexFromPos(int y)
			{
				float oldy, newy = 2.0f, fy = (float)y;
				int index = 0;

				if(fy < newy)
					return 0;
				foreach(MenuControl ctrl in controls)
				{
					oldy = newy;
					newy += Common.fontText.MeasureString(ctrl.text).Y;

					if(oldy <= fy && fy < newy)
						return index;
					
					++index;
				}
				return controls.Count - 1;
			}
			public void MouseDown(int y)
			{
			}
			public void MouseUp(int y, out bool hidemenu)
			{
				MenuControl ctrl = controls[ControlIndexFromPos(y)];
				if(ctrl is MenuButton)
				{
					MenuButton button = (MenuButton)ctrl;
					if(button.onclick != null)
						button.onclick(button);
					hidemenu = true;
				}
				else
					hidemenu = false;
			}
			public void MouseMove(int y)
			{
				mouseOverControl = controls[ControlIndexFromPos(y)];
			}
			public void MouseLeave()
			{
				mouseOverControl = null;
			}
		}

		public void Draw(float dt, Size backbufferSize)
		{
			if(alpha == 0.0f)
				return;
			else if(!shown)
				alpha = Math.Max(alpha - 5.0f * dt, 0.0f);
			
			Matrix4 trans = Matrix4.Identity;
			trans *= Matrix4.CreateScale(2.0f * bounds.Width / backbufferSize.Width, (2.0f * bounds.Height - 4.0f) / backbufferSize.Height, 1.0f);
			trans *= Matrix4.CreateTranslation(-1.0f + 2.0f * (bounds.Left + 0.5f) / backbufferSize.Width, 1.0f - 2.0f * (bounds.Bottom - 0.5f) / backbufferSize.Height, 0.0f);

			// Draw background shadow
			trans *= Matrix4.CreateTranslation(8.0f / backbufferSize.Width, -8.0f / backbufferSize.Height, 0.0f);
			Common.sdrSolidColor.Bind(trans);
			GL.Uniform4(Common.sdrSolidColor_colorUniform, 0.2f, 0.2f, 0.2f, 0.5f * alpha);
			Common.meshQuad.Bind(Common.sdrSolidColor, null);
			Common.meshQuad.Draw();
			trans *= Matrix4.CreateTranslation(-8.0f / backbufferSize.Width, 8.0f / backbufferSize.Height, 0.0f);

			// Draw background
			Common.sdrSolidColor.Bind(trans);
			GL.Uniform4(Common.sdrSolidColor_colorUniform, BACKGROUND_COLOR.R, BACKGROUND_COLOR.G, BACKGROUND_COLOR.B, alpha);
			Common.meshQuad.Bind(Common.sdrSolidColor, null);
			Common.meshQuad.Draw();

			// Draw border
			Common.sdrSolidColor.Bind(trans);
			GL.Uniform4(Common.sdrSolidColor_colorUniform, 0.0f, 0.0f, 0.0f, alpha);
			Common.meshLineQuad2.Bind(Common.sdrSolidColor, null);
			Common.meshLineQuad2.Draw();

			// Draw controls
			float x = (float)(bounds.Left + 4), y = (float)(bounds.Top + 4);
			foreach(MenuControl ctrl in menu.controls)
			{
				float ctrlheight = Common.fontText.MeasureString(ctrl.text).Y;
				if(ctrl == menu.mouseOverControl)
				{
					trans *= Matrix4.CreateTranslation(0.0f, 2.0f * ((float)(menu.size.Height + bounds.Top) - y - ctrlheight) / backbufferSize.Height, 0.0f);
					trans.M22 *= ctrlheight / (float)menu.size.Height;

					Common.sdrSolidColor.Bind(trans);
					GL.Uniform4(Common.sdrSolidColor_colorUniform, HIGHLIGHT_COLOR.R, HIGHLIGHT_COLOR.G, HIGHLIGHT_COLOR.B, alpha);
					Common.meshQuad.Bind(Common.sdrSolidColor, null);
					Common.meshQuad.Draw();
				}

				Common.fontText.DrawString(x, y, ctrl.text, backbufferSize, new Color4(0.0f, 0.0f, 0.0f, alpha));
				y += ctrlheight;
			}
		}

		public void Show(MenuGroup menu, Point pos, Size backbufferSize)
		{
			if(backbufferSize.Width - pos.X < menu.size.Width)
				pos.X -= menu.size.Width;

			if(backbufferSize.Height - pos.Y < menu.size.Height)
				pos.Y = backbufferSize.Height - menu.size.Height - 8;

			bounds = new Rectangle(pos.X, pos.Y, menu.size.Width, menu.size.Height);

			this.menu = menu;
			shown = true;
			alpha = 1.0f;
		}
		public void Hide()
		{
			shown = false;
		}

		public bool MouseDown(object sender, System.Windows.Forms.MouseEventArgs e, Size backbuffersize)
		{
			if(shown == true && bounds.Contains(e.Location))
			{
				menu.MouseDown(e.Y - bounds.Y);
				return true;
			}

			Hide();
			return false;
		}
		public bool MouseUp(object sender, System.Windows.Forms.MouseEventArgs e, Size backbuffersize)
		{
			if(shown == true && bounds.Contains(e.Location))
			{
				bool hidemenu;
				menu.MouseUp(e.Y - bounds.Y, out hidemenu);
				if(hidemenu)
					Hide();
				return true;
			}
			return false;
		}
		public bool MouseMove(object sender, System.Windows.Forms.MouseEventArgs e, Size backbuffersize)
		{
			if(shown == true)
			{
				if(bounds.Contains(e.Location))
				{
					menu.MouseMove(e.Y - bounds.Y);
					return true;
				}
				else
					menu.MouseLeave();
			}
			return false;
		}
	}
}

