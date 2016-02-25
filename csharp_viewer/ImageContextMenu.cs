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

		private List<MenuGroup> menus = new List<MenuGroup>();

		public abstract class MenuControl
		{
			public ImageContextMenu cm;
			public string text;
			public object tag;
			public MenuGroup parent = null;
		}
		public class MenuButton : MenuControl
		{
			public MenuButtonClickDelegate onclick;

			private MenuButton(ImageContextMenu cm, string text, MenuButtonClickDelegate onclick = null)
			{
				this.cm = cm;
				this.text = text;
				this.onclick = onclick;
			}
			public static MenuButton Create(ImageContextMenu cm, string text, MenuButtonClickDelegate onclick = null)
			{
				MenuButton mb = new MenuButton(cm, text, onclick);
				return mb;
			}
		}
		public class MenuGroup : MenuControl
		{
			private List<MenuControl> controls = new List<MenuControl>();
			public Size size;
			public MenuControl mouseOverControl = null;
			private Size backbufferSize;

			private bool shown = false;
			public bool Visible {get { return shown; } }
			private float alpha = 0.0f;
			private Rectangle bounds;
			public Rectangle Bounds {get { return bounds; } }

			private MenuGroup(ImageContextMenu cm, string text)
			{
				this.cm = cm;
				this.text = text;
			}
			public static MenuGroup Create(ImageContextMenu cm, string text)
			{
				MenuGroup mg = new MenuGroup(cm, text);
				cm.menus.Add(mg);
				return mg;
			}

			public void AddControl(MenuControl ctrl)
			{
				controls.Add(ctrl);
				ctrl.parent = this;
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

				foreach(MenuControl ctrl in controls)
					if(ctrl is MenuGroup)
						((MenuGroup)ctrl).ComputeSize();
			}

			private int ControlIndexFromPos(ref int y)
			{
				float oldy, newy = 2.0f, fy = (float)y;
				int index = 0;

				if(fy < newy)
					return 0;
				foreach(MenuControl ctrl in controls)
				{
					oldy = newy;
					newy += Common.fontText.MeasureString(ctrl.text).Y;

					y = (int)oldy - 2;

					if(oldy <= fy && fy < newy)
						return index;
					
					++index;
				}
				return controls.Count - 1;
			}
			public void MouseDown(int y)
			{
			}
			public void MouseUp(int y)
			{
				MenuControl ctrl = controls[ControlIndexFromPos(ref y)];
				if(ctrl is MenuButton)
				{
					MenuButton button = (MenuButton)ctrl;
					if(button.onclick != null)
						button.onclick(button);
					Hide();
				}
				else if(ctrl is MenuGroup)
				{
					MenuGroup submenu = (MenuGroup)ctrl;
					submenu.Show(new Point(bounds.Right, bounds.Top + y), backbufferSize);
				}
			}
			public void MouseMove(int y)
			{
				mouseOverControl = controls[ControlIndexFromPos(ref y)];
			}
			public void MouseLeave()
			{
				mouseOverControl = null;
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
				foreach(MenuControl ctrl in controls)
				{
					float ctrlheight = Common.fontText.MeasureString(ctrl.text).Y;
					if(ctrl == mouseOverControl)
					{
						trans *= Matrix4.CreateTranslation(0.0f, 2.0f * ((float)(size.Height + bounds.Top) - y - ctrlheight) / backbufferSize.Height, 0.0f);
						trans.M22 *= ctrlheight / (float)size.Height;

						Common.sdrSolidColor.Bind(trans);
						GL.Uniform4(Common.sdrSolidColor_colorUniform, HIGHLIGHT_COLOR.R, HIGHLIGHT_COLOR.G, HIGHLIGHT_COLOR.B, alpha);
						Common.meshQuad.Bind(Common.sdrSolidColor, null);
						Common.meshQuad.Draw();
					}

					Common.fontText.DrawString(x, y, ctrl.text, backbufferSize, new Color4(0.0f, 0.0f, 0.0f, alpha));
					y += ctrlheight;
				}
			}

			public void Show(Point pos, Size backbufferSize)
			{
				if(backbufferSize.Width - pos.X < size.Width)
					pos.X -= size.Width;

				if(backbufferSize.Height - pos.Y < size.Height)
					pos.Y = backbufferSize.Height - size.Height - 8;

				bounds = new Rectangle(pos.X, pos.Y, size.Width, size.Height);
				this.backbufferSize = backbufferSize;

				shown = true;
				alpha = 1.0f;
			}
			public void Hide()
			{
				shown = false;
				foreach(MenuControl ctrl in controls)
					if(ctrl is MenuGroup)
						((MenuGroup)ctrl).Hide();
				if(parent != null)
					parent.Hide_Reverse();
			}
			private void Hide_Reverse()
			{
				shown = false;
				if(parent != null)
					parent.Hide_Reverse();
			}
			public void HideImmediately()
			{
				shown = false;
				alpha = 0.0f;
				foreach(MenuControl ctrl in controls)
					if(ctrl is MenuGroup)
						((MenuGroup)ctrl).HideImmediately();
				if(parent != null)
					parent.HideImmediately_Reverse();
			}
			private void HideImmediately_Reverse()
			{
				shown = false;
				alpha = 0.0f;
				if(parent != null)
					parent.HideImmediately_Reverse();
			}

			public bool Contains(Point pos)
			{
				return bounds.Contains(pos);
			}
			public bool Contains_Recursive(Point pos)
			{
				if(bounds.Contains(pos))
					return true;

				bool result = false;
				foreach(MenuControl ctrl in controls)
					if(result == false && ctrl is MenuGroup)
						result |= ((MenuGroup)ctrl).Contains_Recursive(pos);
				if(result == false && Contains_Reverse(pos))
					return true;
				return result;
			}
			public bool Contains_Reverse(Point pos)
			{
				return bounds.Contains(pos) || (parent != null && parent.Contains_Reverse(pos));
			}
		}

		public void Draw(float dt, Size backbufferSize)
		{
			foreach(MenuGroup menu in menus)
				menu.Draw(dt, backbufferSize);
		}

		public bool MouseDown(object sender, System.Windows.Forms.MouseEventArgs e, Size backbuffersize)
		{
			bool handled = false;
			foreach(MenuGroup menu in menus)
			{
				if(menu.Visible && menu.Contains(e.Location))
				{
					menu.MouseDown(e.Y - menu.Bounds.Y);
					handled = true;
				}
				else if(!menu.Contains_Recursive(e.Location))
					menu.Hide();
			}
			return handled;
		}
		public bool MouseUp(object sender, System.Windows.Forms.MouseEventArgs e, Size backbuffersize)
		{
			bool handled = false;
			foreach(MenuGroup menu in menus)
				if(menu.Visible && menu.Contains(e.Location))
				{
					menu.MouseUp(e.Y - menu.Bounds.Y);
					handled = true;
				}
			return handled;
		}
		public bool MouseMove(object sender, System.Windows.Forms.MouseEventArgs e, Size backbuffersize)
		{
			bool handled = false;
			foreach(MenuGroup menu in menus)
				if(menu.Visible)
				{
					if(menu.Contains(e.Location))
					{
						menu.MouseMove(e.Y - menu.Bounds.Y);
						handled = true;
					}
					else
						menu.MouseLeave();
				}
			return handled;
		}
	}
}

