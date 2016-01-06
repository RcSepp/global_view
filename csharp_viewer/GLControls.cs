using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace csharp_viewer
{
	public class GLControl
	{
		public ControlCollection Controls = new ControlCollection();

		/*private GLControl parent;
		public GLControl Parent
		{
			get { return parent; }
			set { parent = value; OnParentSizeChanged(); }
		}*/
		private Size parentSize = Size.Empty;

		private Size backbufferSize = Size.Empty;
		public Size BackbufferSize
		{
			get { return backbufferSize; }
		}

		private Rectangle bounds = new Rectangle(0, 0, 100, 100);
		public Rectangle Bounds
		{
			get { return bounds; }
			set
			{
				bounds = value;
				foreach(GLControl control in Controls)
					OnParentSizeChanged(backbufferSize, bounds.Size);
			}
		}
		public Size Size { get { return bounds.Size; } }
		public int Width { get { return bounds.Width; } }
		public int Height { get { return bounds.Height; } }

		private AnchorStyles anchor = AnchorStyles.Top | AnchorStyles.Left;
		public AnchorStyles Anchor
		{
			get { return anchor; }
			set { anchor = value; OnParentSizeChanged(backbufferSize, parentSize); }
		}

		private DockStyle dock = DockStyle.None;
		public DockStyle Dock
		{
			get { return dock; }
			set { dock = value; OnParentSizeChanged(backbufferSize, parentSize); }
		}

		public bool Visible = true;

		protected virtual void Draw(float dt, Matrix4 transform) {}
		protected virtual void SizeChanged() {}
		protected virtual void MouseDown(MouseEventArgs e) {}

		public GLControl()
		{
			Controls.ControlAdded += Controls_ControlAdded;
		}
		private void Controls_ControlAdded(GLControl control)
		{
			control.OnParentSizeChanged(backbufferSize, this.Size);
		}

		public void Draw(float dt)
		{
			if(!Visible || parentSize.Width == 0)
				return;

			Matrix4 transform = Matrix4.Identity;
			transform *= Matrix4.CreateScale(2.0f * bounds.Width / parentSize.Width, (2.0f * bounds.Height - 4.0f) / parentSize.Height, 1.0f);
			transform *= Matrix4.CreateTranslation(-1.0f + 2.0f * (bounds.Left + 0.5f) / parentSize.Width, 1.0f - 2.0f * (bounds.Bottom - 0.5f) / parentSize.Height, 0.0f);
			Draw(dt, transform);

			foreach(GLControl control in Controls)
			{
				if(control.BackbufferSize.Width != backbufferSize.Width || control.BackbufferSize.Height != backbufferSize.Height) //EDIT: Might be slow
					control.OnParentSizeChanged(backbufferSize, parentSize); //EDIT: Might be slow
				control.Draw(dt);
			}
		}

		public void OnParentSizeChanged(Size backbufferSize, Size parentSize)
		{
			this.backbufferSize = backbufferSize;
			if(parentSize.Width == this.parentSize.Width && parentSize.Height == this.parentSize.Height)
				return;

			Size oldSize = bounds.Size;

			if(this.parentSize.Width > 0 && this.parentSize.Height > 0)
			{
				// Adjust bounds to top-left anchor
				if((anchor & AnchorStyles.Right) != 0)
				{
					if((anchor & AnchorStyles.Left) != 0)
						bounds.Width += parentSize.Width - this.parentSize.Width;
					else
						bounds.X = this.parentSize.Width - bounds.Right;
				}
				if((anchor & AnchorStyles.Bottom) != 0)
					bounds.Y = this.parentSize.Height - bounds.Bottom;
			}

			this.parentSize = parentSize;

			switch(dock)
			{
			case DockStyle.None:
				// Adjust bounds to anchor
				if((anchor & AnchorStyles.Right) != 0 && (anchor & AnchorStyles.Left) == 0)
					bounds.X = parentSize.Width - bounds.Right;
				if((anchor & AnchorStyles.Bottom) != 0)
					bounds.Y = parentSize.Height - bounds.Bottom;
				break;

			case DockStyle.Top:
				bounds.X = 0;
				bounds.Y = 0;
				bounds.Width = parentSize.Width;
				break;
			case DockStyle.Bottom:
				bounds.X = 0;
				bounds.Y = parentSize.Height - bounds.Height;
				bounds.Width = parentSize.Width;
				break;
			case DockStyle.Left:
				bounds.X = 0;
				bounds.Y = 0;
				bounds.Height = parentSize.Height;
				break;
			case DockStyle.Right:
				bounds.X = parentSize.Width - bounds.Width;
				bounds.Y = 0;
				bounds.Height = parentSize.Height;
				break;
			case DockStyle.Fill:
				bounds.X = 0;
				bounds.Y = 0;
				bounds.Width = parentSize.Width;
				bounds.Height = parentSize.Height;
				break;
			}

			if(bounds.Width != oldSize.Width || bounds.Height != oldSize.Height)
			{
				SizeChanged();
				foreach(GLControl control in Controls)
					control.OnParentSizeChanged(backbufferSize, this.Size);
			}
		}

		public bool OnMouseDown(MouseEventArgs e)
		{
			if(bounds.Contains(e.Location))
			{
				MouseDown(e);
				return true;
			}
			else
				return false;
		}
	}

	public class ControlCollection : List<GLControl>
	{
		public delegate void ControlAddedDelegate(GLControl control);
		public ControlAddedDelegate ControlAdded;

		public new void Add(GLControl control)
		{
			base.Add(control);
			if(ControlAdded != null)
				ControlAdded(control);
		}
	}

	/*public class GLPanel : GLControl
	{
		private List<GLControl> controls = new List<GLControl>();

		public void Add(GLControl control)
		{
			controls.Add(control);
			control.OnParentSizeChanged(this.Size);
		}

		protected override void Draw(Matrix4 transform)
		{
			foreach(GLControl control in controls)
				control.Draw();
		}

		protected override void SizeChanged()
		{
			foreach(GLControl control in controls)
				control.OnParentSizeChanged(this.Size);
		}

		protected override void MouseDown(MouseEventArgs e)
		{
			foreach(GLControl control in controls)
				control.OnMouseDown(e);
		}
	}*/

	public class GLWindow : OpenTK.GLControl
	{
		public new List<GLControl> Controls;
		public new event EventHandler Load;

		public GLWindow()
			: base(new GraphicsMode(32, 32, 0, 4), 3, 0, GraphicsContextFlags.Default)
		{
			Controls = new List<GLControl>();
			base.Load += OnLoad;
			SizeChanged += OnSizeChanged;
			base.SizeChanged += OnSizeChanged;
			MouseDown += OnMouseDown;
			MouseUp += OnMouseUp;
			MouseMove += OnMouseMove;
			MouseWheel += OnMouseWheel;
			KeyDown += OnKeyDown;
		}

		private void OnLoad(object sender, EventArgs e)
		{
			Context.MakeCurrent(null);
			if(this.Load != null)
				this.Load(this, e);
		}
		private void OnSizeChanged(object sender, EventArgs e)
		{
			foreach(GLControl control in Controls)
				control.OnParentSizeChanged(this.Size, this.Size);
		}
		private void OnMouseDown(object sender, MouseEventArgs e)
		{
			foreach(GLControl control in Controls)
				control.OnMouseDown(e);
		}
		private void OnMouseUp(object sender, MouseEventArgs e)
		{
		}
		private void OnMouseMove(object sender, MouseEventArgs e)
		{
		}
		private void OnMouseWheel(object sender, MouseEventArgs e)
		{
		}
		private void OnDoubleClick(object sender, EventArgs e)
		{
		}
		private void OnKeyDown(object sender, KeyEventArgs e)
		{
		}

		public void Init()
		{
			MakeCurrent();
		}

		public void Render(float dt)
		{
			MakeCurrent();
			GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
			foreach(GLControl control in Controls)
			{
				if(control.BackbufferSize.Width != this.Width || control.BackbufferSize.Height != this.Height) //EDIT: Might be slow
					control.OnParentSizeChanged(this.Size, this.Size); //EDIT: Might be slow
				control.Draw(dt);
			}
		}
	}

	public class GLButton : GLControl
	{
		private GLTexture2D tex;

		private Action clickAction = null;
		public EventHandler Click;

		public GLButton(string texfilename, Rectangle bounds, AnchorStyles anchor = AnchorStyles.Top | AnchorStyles.Left, string actionname = null, string actiondesc = null)
		{
			this.Anchor = anchor;

			this.tex = GLTexture2D.FromFile(texfilename, false);
			if(actionname != null)
				clickAction = ActionManager.CreateAction(actiondesc == null ? "" : actiondesc, actionname, this, "ClickInternal");

			if(bounds.Width <= 0)
				bounds.Width = tex.width;
			if(bounds.Height <= 0)
				bounds.Height = tex.height;
			this.Bounds = bounds;
		}
		public GLButton(GLTexture2D texture, Rectangle bounds, AnchorStyles anchor = AnchorStyles.Top | AnchorStyles.Left, string actionname = null, string actiondesc = null)
		{
			this.Anchor = anchor;

			this.tex = texture;
			if(actionname != null)
				clickAction = ActionManager.CreateAction(actiondesc == null ? "" : actiondesc, actionname, this, "ClickInternal");

			if(bounds.Width <= 0)
				bounds.Width = tex.width;
			if(bounds.Height <= 0)
				bounds.Height = tex.height;
			this.Bounds = bounds;
		}

		protected override void Draw(float dt, Matrix4 transform)
		{
			transform *= Matrix4.CreateTranslation(0.5f / BackbufferSize.Width, 0.5f / BackbufferSize.Height, 0.0f);
			Common.sdrTextured.Bind(transform);
			Common.meshQuad.Bind(Common.sdrTextured, tex);
			Common.meshQuad.Draw();
		}

		protected override void MouseDown(MouseEventArgs e)
		{
			PerformClick();
		}

		public void PerformClick()
		{
			if(clickAction != null)
				ActionManager.Do(clickAction);
			else
				ClickInternal();
		}
		private void ClickInternal()
		{
			if(Click != null)
				Click(this, null);
		}
	}

	public class GLLabel : GLControl
	{
		public string Text = "";
		public GLFont Font = Common.fontText;

		protected override void Draw(float dt, Matrix4 transform)
		{
			Font.DrawString(Bounds.Left, Bounds.Top, Text, BackbufferSize);
		}
	}
}

