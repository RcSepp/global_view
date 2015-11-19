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
			BackColor, ViewControl, ViewRotationCenter, ShowCoordinateSystem, ShowLineGrid, EnableMouseRect
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
			case Option.ViewRotationCenter:
				imageCloud.viewControl = ImageCloud.ViewControl.PointCentric;
				imageCloud.viewRotationCenter = (Vector3)value;
				break;
			case Option.ShowCoordinateSystem:
				imageCloud.showCoordinateSystem = (bool)value;
				break;
			case Option.ShowLineGrid:
				imageCloud.showLineGrid = (bool)value;
				break;
			case Option.EnableMouseRect:
				imageCloud.enableMouseRect = (bool)value;
				break;
			}
		}


		// Event handlers
		public virtual void OnLoad() {}
		public virtual void OnImageMouseDown(TransformedImage image, out bool enableDrag) { enableDrag = false; }
		public virtual void OnNonImageMouseDown() {}
		public virtual void OnImageClick(TransformedImage image) {}
		public virtual void OnImageRightClick(TransformedImage image) {}
		public virtual void OnImageDoubleClick(TransformedImage image) {}
		public virtual void OnImageDrag(TransformedImage image, Vector3 delta) {}
		public virtual void OnKeyDown(KeyEventArgs e) {}


		private Action MoveImagesAction, MoveImageAction, MoveSelectionAction;
		private Action ShowImagesAction, ShowImageAction, ShowSelectionAction;
		private Action HideImagesAction, HideImageAction, HideSelectionAction;
		public ImageBrowser()
		{
			// Create actions for control functions

			// Selection functions don't need to call an action, because SelectionChanged() calls an action

			// Focus functions don't need to call an action, because changing the view calls an action

			MoveImagesAction = ActionManager.CreateAction("", (ImageBrowser)this, "DoMoveImages");
			MoveImageAction = ActionManager.CreateAction("", (ImageBrowser)this, "DoMoveImage");
			MoveSelectionAction = ActionManager.CreateAction("", this, "DoMoveSelection");

			ShowImagesAction = ActionManager.CreateAction("", (ImageBrowser)this, "DoShowImages");
			ShowImageAction = ActionManager.CreateAction("", (ImageBrowser)this, "DoShowImage");
			ShowSelectionAction = ActionManager.CreateAction("", this, "DoShowSelection");

			HideImagesAction = ActionManager.CreateAction("", (ImageBrowser)this, "DoHideImages");
			HideImageAction = ActionManager.CreateAction("", (ImageBrowser)this, "DoHideImage");
			HideSelectionAction = ActionManager.CreateAction("", this, "DoHideSelection");
		}


		// Control functions

		protected void ShowContextMenu(ImageContextMenu.MenuGroup cm)
		{
			imageCloud.ShowContextMenu(cm);
		}

		protected string ExecuteISQL(string command)
		{
			string output, warnings;
			ISQL.Compiler.Execute(command, ActionManager.mgr.Invoke, null, out output, out warnings);
			return output + warnings;
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
			if(Viewer.selection.Count > 0)
			{
				Viewer.selection.Clear();
				SelectionChanged();
			}
		}

		protected void Focus(TransformedImage image, bool animate = true)
		{
			imageCloud.FocusSingle(image, animate);
		}
		protected void Focus(IEnumerable<TransformedImage> images, bool animate = true)
		{
			imageCloud.Focus(images, animate);
		}
		protected void FocusSelection(bool animate = true)
		{
			imageCloud.Focus(Viewer.selection, animate);
		}
			
		public void Move(IEnumerable<TransformedImage> images, Vector3 deltapos)
		{
			ActionManager.Do(MoveImagesAction, images, deltapos); //EDIT: Should be images.Clone()
		}
		private void DoMoveImages(IEnumerable<TransformedImage> images, Vector3 deltapos)
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
			ActionManager.Do(MoveImageAction, image, deltapos);
		}
		private void DoMoveImage(TransformedImage image, Vector3 deltapos)
		{
			image.pos += deltapos;
			image.skipPosAnimation();

			if(image.selected)
				SelectionMoved();
		}
		public void MoveSelection(Vector3 deltapos)
		{
			ActionManager.Do(MoveSelectionAction, deltapos);
		}
		private void DoMoveSelection(Vector3 deltapos)
		{
			foreach(TransformedImage image in Viewer.selection)
			{
				image.pos += deltapos;
				image.skipPosAnimation();
			}
			SelectionMoved();
		}

		public void MoveIntoView(TransformedImage image)
		{
			imageCloud.MoveIntoView(image);
		}

		public void Show(IEnumerable<TransformedImage> images)
		{
			ActionManager.Do(ShowImagesAction, images); //EDIT: Should be images.Clone()
		}
		private void DoShowImages(IEnumerable<TransformedImage> images)
		{
			foreach(TransformedImage image in images)
			{
				image.Visible = true;
				Viewer.visible.Add(image);
			}
		}
		public void Show(TransformedImage image)
		{
			ActionManager.Do(ShowImageAction, image);
		}
		private void DoShowImage(TransformedImage image)
		{
			image.Visible = true;
			Viewer.visible.Add(image);
		}
		public void ShowSelection()
		{
			ActionManager.Do(ShowSelectionAction);
		}
		public void DoShowSelection()
		{
			foreach(TransformedImage selectedimage in Viewer.selection)
			{
				selectedimage.Visible = true;
				Viewer.visible.Add(selectedimage);
			}
		}

		public void Hide(IEnumerable<TransformedImage> images)
		{
			ActionManager.Do(HideImagesAction, images); //EDIT: Should be images.Clone()
		}
		private void DoHideImages(IEnumerable<TransformedImage> images)
		{
			//bool selectionhidden = false;
			foreach(TransformedImage image in images)
			{
				image.Visible = false;
				Viewer.visible.Remove(image);

				//if(image.selected)
				//	selectionhidden = true;
			}
			//if(selectionhidden)
			//	SelectionMoved();
		}
		public void Hide(TransformedImage image)
		{
			ActionManager.Do(HideImageAction, image);
		}
		private void DoHideImage(TransformedImage image)
		{
			image.Visible = false;
			Viewer.visible.Remove(image);
		}
		public void HideSelection()
		{
			ActionManager.Do(HideSelectionAction);
		}
		private void DoHideSelection()
		{
			foreach(TransformedImage selectedimage in Viewer.selection)
			{
				selectedimage.Visible = false;
				Viewer.visible.Remove(selectedimage);
			}
			//SelectionMoved();
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

		public override void OnImageMouseDown(TransformedImage image, out bool enableDrag)
		{
			if(Control.MouseButtons == MouseButtons.Left)
			{
				enableDrag = true;

				if(!Viewer.selection.Contains(image))
				{
					if(InputDevices.kbstate.IsKeyDown(OpenTK.Input.Key.LWin))
						AddToSelection(image);
					else
						Select(image);
				}
			}
			else
				enableDrag = false;
		}
		public override void OnNonImageMouseDown()
		{
			// Clear selection if Windows key (command key on Mac) isn't pressed
			if(InputDevices.kbstate.IsKeyUp(OpenTK.Input.Key.LWin))
				ClearSelection();
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
		public override void OnImageDrag(TransformedImage image, Vector3 delta)
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

	public class MPASBrowser : ImageBrowser
	{
		int expandedTimeIdx = -1;

		public override void OnLoad()
		{
			SetOption(Option.BackColor, Color4.DarkSlateBlue);
			//SetOption(Option.ViewControl, ImageCloud.ViewControl.CoordinateSystemCentric);
			SetOption(Option.ViewControl, ImageCloud.ViewControl.TwoDimensional);
			SetOption(Option.ShowCoordinateSystem, false);
			SetOption(Option.ShowLineGrid, false);
			SetOption(Option.EnableMouseRect, false);

			ExecuteISQL(string.Format("hide WHERE (int) $phi < -90 || (int) $phi > 90"));

			ExecuteISQL(string.Format("x all BY #time * 3.0f"));
			ExecuteISQL(string.Format("look all BY pi - $theta * pi / 180.0f, pi / 2.0f - $phi * pi / 180.0f"));

			Focus(Viewer.images, false);
		}

		public override void OnImageMouseDown(TransformedImage image, out bool enableDrag)
		{
			enableDrag = false;

			if(Control.MouseButtons != MouseButtons.Left || image.selected == true)
				return;

			int timeidx = Array.IndexOf(image.args[0].values, image.values[0]);

			int selection_timeidx;
			if(Viewer.selection.Count == 0)
				selection_timeidx = -1;
			else
			{
				IEnumerator<TransformedImage> selection_enum = Viewer.selection.GetEnumerator();
				selection_enum.MoveNext();
				selection_timeidx = Array.IndexOf(selection_enum.Current.args[0].values, selection_enum.Current.values[0]);
			}

			if(timeidx == expandedTimeIdx)
			{
				Select(image);

				if(selection_timeidx != expandedTimeIdx)
					ExecuteISQL(string.Format("focus WHERE #time == {0}", timeidx));

				SetOption(Option.ViewRotationCenter, new Vector3((float)timeidx * 3.0f + 5.0f, 0.0f, 0.0f));
			}
			else
			{
				SetOption(Option.ViewControl, ImageCloud.ViewControl.CoordinateSystemCentric);

				ExecuteISQL(string.Format("select WHERE #time == {0}", timeidx));
				FocusSelection();
			}
		}
		public override void OnImageDoubleClick(TransformedImage image)
		{
			int timeidx = Array.IndexOf(image.args[0].values, image.values[0]);

			if(timeidx == expandedTimeIdx)
			{
				expandedTimeIdx = -1;

				SetOption(Option.ViewControl, ImageCloud.ViewControl.CoordinateSystemCentric);

				ExecuteISQL(string.Format("clear all"));
				ExecuteISQL(string.Format("x all BY #time * 3.0f"));
				ExecuteISQL(string.Format("look all BY pi - $theta * pi / 180.0f, pi / 2.0f - $phi * pi / 180.0f"));

				ExecuteISQL(string.Format("select WHERE #time == {0}", timeidx));
				FocusSelection();
			}
			else
			{
				expandedTimeIdx = timeidx;

				ExecuteISQL(string.Format("clear all"));
				ExecuteISQL(string.Format("x BY #time * 3.0f WHERE #time < {0}", timeidx));
				ExecuteISQL(string.Format("x BY #time * 3.0f + 5.0f WHERE #time == {0}", timeidx));
				ExecuteISQL(string.Format("x BY #time * 3.0f + 10.0f WHERE #time > {0}", timeidx));

				FocusSelection();
				SetOption(Option.ViewRotationCenter, new Vector3((float)timeidx * 3.0f + 5.0f, 0.0f, 0.0f));
				//SetOption(Option.ViewControl, ImageCloud.ViewControl.PointCentric);

				ExecuteISQL(string.Format("thetaPhi BY pi - $theta * pi / 180.0f, pi / 2.0f - $phi * pi / 180.0f, 5.0f WHERE #time == {0}", timeidx));

				FocusSelection();
				Select(image);
			}
		}
	}
}

