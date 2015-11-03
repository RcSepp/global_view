using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace csharp_viewer
{
	public abstract class ImageBrowser
	{
		// Public events
		public event Selection.ChangedDelegate SelectionChanged;
		public event Selection.MovedDelegate SelectionMoved;

		// Private variables
		private ImageCloud imageCloud;

		public void Init(ImageCloud imageCloud)
		{
			this.imageCloud = imageCloud;
		}

		// Options
		protected enum Option
		{
			BackColor, ViewControl, ShowCoordinateSystem
		}
		protected void SetOption(Option option, object value)
		{
			switch(option)
			{
			case Option.BackColor:
				GL.ClearColor((Color4)value);
				break;
			case Option.ViewControl:
				imageCloud.viewControl = (ImageCloud.ViewControl)value;
				break;
			case Option.ShowCoordinateSystem:
				imageCloud.showCoordinateSystem = (bool)value;
				break;
			}
		}


		// Event handlers
		public virtual void OnLoad() {}
		public virtual void OnImageMouseDown(TransformedImage image) {}
		public virtual void OnImageClick(TransformedImage image) {}
		public virtual void OnImageRightClick(TransformedImage image) {}
		public virtual void OnImageDoubleClick(TransformedImage image) {}
		public virtual void OnImageMove(TransformedImage image, Vector3 delta) {}
		public virtual void OnKeyDown(KeyEventArgs e) {}


		// Control functions

		protected void ShowContextMenu(ImageContextMenu.MenuGroup cm)
		{
			imageCloud.ShowContextMenu(cm);
		}

		protected void Select(TransformedImage image)
		{
			Viewer.selection.Clear();
			Viewer.selection.Add(image);
			SelectionChanged();
		}
		protected void AddToSelection(TransformedImage image)
		{
			Viewer.selection.Add(image);
			SelectionChanged();
		}
		protected void ClearSelection()
		{
			Viewer.selection.Clear();
		}

		protected void Focus(TransformedImage image)
		{
			imageCloud.FocusSingle(image);
		}
		protected void Focus(IEnumerable<TransformedImage> images)
		{
			imageCloud.Focus(images);
		}
		protected void FocusSelection()
		{
			imageCloud.Focus(Viewer.selection);
		}

		public void Move(IEnumerable<TransformedImage> images, Vector3 deltapos)
		{
			bool selectionmoved = false;
			foreach(TransformedImage image in images)
			{
				image.pos += deltapos;
				image.skipPosAnimation();

				if(image.selected)
					selectionmoved = true;
			}
			if(selectionmoved)
				SelectionMoved();
		}
		public void Move(TransformedImage image, Vector3 deltapos)
		{
			image.pos += deltapos;
			image.skipPosAnimation();

			if(image.selected)
				SelectionMoved();
		}
		public void MoveSelection(Vector3 deltapos)
		{
			Move(Viewer.selection, deltapos);
		}

		public void MoveIntoView(TransformedImage image)
		{
			imageCloud.MoveIntoView(image);
		}

		public void Show(IEnumerable<TransformedImage> images)
		{
			foreach(TransformedImage image in images)
			{
				image.visible = true;
				Viewer.visible.Add(image);
			}
		}
		public void Show(TransformedImage image)
		{
			image.visible = true;
			Viewer.visible.Add(image);
		}
		public void ShowSelection()
		{
			Show(Viewer.selection);
		}

		public void Hide(IEnumerable<TransformedImage> images)
		{
			foreach(TransformedImage image in images)
			{
				image.visible = false;
				Viewer.visible.Remove(image);
			}
		}
		public void Hide(TransformedImage image)
		{
			image.visible = false;
			Viewer.visible.Remove(image);
		}
		public void HideSelection()
		{
			Hide(Viewer.selection);
		}
	}

	public class SimpleBrowser : ImageBrowser
	{
		private ImageContextMenu.MenuGroup cmImage;

		public override void OnLoad()
		{
			SetOption(Option.BackColor, Color4.LightSlateGray);
			//SetOption(Option.ViewControl, ImageCloud.ViewControl.TwoDimensional);
			SetOption(Option.ShowCoordinateSystem, false);

			cmImage = new ImageContextMenu.MenuGroup("");
			cmImage.controls.Add(new ImageContextMenu.MenuButton("test"));
			cmImage.ComputeSize();

			//string output, warnings;
			//ISQL.Compiler.Execute(string.Format("x all BY #theta * 3.0f"), ActionManager.mgr.Invoke, out output, out warnings);
		}

		public override void OnImageMouseDown(TransformedImage image)
		{
			if(!Viewer.selection.Contains(image))
			{
				if(InputDevices.kbstate.IsKeyDown(OpenTK.Input.Key.LWin))
					AddToSelection(image);
				else
					Select(image);
			}
		}
		public override void OnImageRightClick(TransformedImage image)
		{
			ShowContextMenu(cmImage);
		}
		public override void OnImageDoubleClick(TransformedImage image)
		{
			ClearSelection();
			MoveIntoView(image);
		}
		public override void OnImageMove(TransformedImage image, Vector3 delta)
		{
			MoveSelection(delta);
		}

		public override void OnKeyDown(KeyEventArgs e)
		{
			switch(e.KeyCode)
			{
			case Keys.C:
				/*ViewControl[] values = (ViewControl[])Enum.GetValues(typeof(ViewControl));
				ActionManager.Do(SetViewControlAction, (int)viewControl + 1 == values.Length ? values[0] : values[(int)viewControl + 1]);*/
				break;

			case Keys.O:
				/*if(depthRenderingEnabled)
					ActionManager.Do(DisableDepthRenderingAction);
				else
					ActionManager.Do(EnableDepthRenderingAction);*/
				break;

			case Keys.F:
				FocusSelection();
				break;

			case Keys.Delete:
				HideSelection();
				break;
			}
		}
	}
}

