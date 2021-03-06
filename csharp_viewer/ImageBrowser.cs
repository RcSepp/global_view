﻿using System;
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
		private Viewer viewer;
		private ImageCloud imageCloud;

		public void Init(Viewer viewer, ImageCloud imageCloud)
		{
			this.viewer = viewer;
			this.imageCloud = imageCloud;
		}

		// Options
		protected enum Option
		{
			BackColor, ViewControl, ViewRotationCenter, ShowCoordinateSystem, ShowLineGrid, ShowArgumentIndex, ShowParameterIndex, ShowColormap, ShowConsole, EnableMouseRect, FullScreen, ForceOriginalImageSize
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
			case Option.ShowArgumentIndex:
				imageCloud.showArgumentIndex = (bool)value;
				break;
			case Option.ShowParameterIndex:
				imageCloud.showParameterIndex = (bool)value;
				break;
			case Option.ShowColormap:
				imageCloud.showColormap = (bool)value;
				break;
			case Option.ShowConsole:
				viewer.ConsoleVisible = (bool)value;
				break;
			case Option.EnableMouseRect:
				imageCloud.enableMouseRect = (bool)value;
				break;
			case Option.FullScreen:
				if ((bool)value)
				{
					viewer.FormBorderStyle = FormBorderStyle.None;
					viewer.WindowState = FormWindowState.Maximized;
				}
				else
				{
					viewer.FormBorderStyle = FormBorderStyle.Sizable;
					viewer.WindowState = FormWindowState.Normal;
				}
				break;
				case Option.ForceOriginalImageSize:
				imageCloud.forceOriginalImageSize = (bool)value;
				break;
			}
		}


		// Event handlers
		public virtual void OnLoad() {}
		public virtual void OnImageMouseDown(MouseButtons button, TransformedImage image, Vector2 uv, out bool enableDrag) { enableDrag = false; }
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

		protected ImageContextMenu.MenuButton CreateContextMenuButton(string text, ImageContextMenu.MenuButtonClickDelegate onclick = null)
		{
			return imageCloud.CreateContextMenuButton(text, onclick);
		}
		protected ImageContextMenu.MenuGroup CreateContextMenuGroup(string text)
		{
			return imageCloud.CreateContextMenuGroup(text);
		}
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
				image.Position += deltapos;
				image.skipPosAnimation();

				if(image.Selected)
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
			image.Position += deltapos;
			image.skipPosAnimation();

			if(image.Selected)
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
				image.Position += deltapos;
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

		public void TakeScreenshot(string filename = "screenshot.png")
		{
			Viewer.image_render_mutex.WaitOne();
			ActionManager.mgr.SaveScreenshot(filename);
			ImageCloud.Status(string.Format("Screenshot saved as \"{0}\"", filename));
			Viewer.image_render_mutex.ReleaseMutex();
		}
		public void ChangeResolution(int width, int height)
		{
			viewer.ChangeResolution(width, height);
		}
		public void Playback(double playback_speed = 1.0)
		{
			ActionManager.mgr.Play(playback_speed);
		}
		public void ClearPlayback()
		{
			ActionManager.mgr.Clear();
		}
		public void CapturePlayback(int width = 1920, int height = 1200, double fps = 25.0)
		{
			// Switch to video-friendly resolution
			viewer.ChangeResolution(width, height);

			ActionManager.mgr.CaptureFrames(fps);
		}
		public void Exit()
		{
			viewer.Close();
		}
	}

	public class SimpleBrowser : ImageBrowser
	{
		private ImageContextMenu.MenuGroup cmImage;

		Color4 bgcolor = Color4.DarkSlateGray;
		public override void OnLoad()
		{
			SetOption(Option.BackColor, new Color4(102, 101, 96, 255));
			//SetOption(Option.BackColor, bgcolor);
			//SetOption(Option.ViewControl, ImageCloud.ViewControl.TwoDimensional);
			//SetOption(Option.ShowCoordinateSystem, false);
			//SetOption(Option.ShowLineGrid, false);
			//SetOption(Option.ShowColormap, false);
			SetOption(Option.ShowArgumentIndex, false);

			//SetOption(Option.ForceOriginalImageSize, true);

			cmImage = CreateContextMenuGroup("");

			ImageContextMenu.MenuGroup cmPlayback = CreateContextMenuGroup("Playback");
			cmImage.AddControl(cmPlayback);
			cmPlayback.AddControl(CreateContextMenuButton("TakeScreenShot", delegate(ImageContextMenu.MenuButton sender) {
				cmImage.HideImmediately();

				// Wait for the context menu to disappear
				Viewer.image_render_mutex.ReleaseMutex();
				System.Threading.Thread.Sleep(1000);
				Viewer.image_render_mutex.WaitOne();

				TakeScreenshot();
			}));
			cmPlayback.AddControl(CreateContextMenuButton("Play", delegate(ImageContextMenu.MenuButton sender) {
				Playback(2.0f);
			}));
			cmPlayback.AddControl(CreateContextMenuButton("Clear", delegate(ImageContextMenu.MenuButton sender) {
				ClearPlayback();
			}));
			cmPlayback.AddControl(CreateContextMenuButton("Record", delegate(ImageContextMenu.MenuButton sender) {
				CapturePlayback();
			}));

			ImageContextMenu.MenuGroup cmShow = CreateContextMenuGroup("Show");
			cmImage.AddControl(cmShow);
			cmShow.AddControl(CreateContextMenuButton("Argument Index", delegate(ImageContextMenu.MenuButton sender) {
				SetOption(Option.ShowArgumentIndex, true);
			}));
			cmShow.AddControl(CreateContextMenuButton("Parameter Index", delegate(ImageContextMenu.MenuButton sender) {
				SetOption(Option.ShowParameterIndex, true);
			}));
			cmShow.AddControl(CreateContextMenuButton("Colormap", delegate(ImageContextMenu.MenuButton sender) {
				SetOption(Option.ShowColormap, true);
			}));
			cmShow.AddControl(CreateContextMenuButton("Coordinate System", delegate(ImageContextMenu.MenuButton sender) {
				SetOption(Option.ShowCoordinateSystem, true);
			}));
			cmShow.AddControl(CreateContextMenuButton("Line Grid", delegate(ImageContextMenu.MenuButton sender) {
				SetOption(Option.ShowLineGrid, true);
			}));
			cmShow.AddControl(CreateContextMenuButton("Console", delegate(ImageContextMenu.MenuButton sender) {
				SetOption(Option.ShowConsole, true);
			}));

			ImageContextMenu.MenuGroup cmHide = CreateContextMenuGroup("Hide");
			cmImage.AddControl(cmHide);
			cmHide.AddControl(CreateContextMenuButton("Argument Index", delegate(ImageContextMenu.MenuButton sender) {
				SetOption(Option.ShowArgumentIndex, false);
			}));
			cmHide.AddControl(CreateContextMenuButton("Parameter Index", delegate(ImageContextMenu.MenuButton sender) {
				SetOption(Option.ShowParameterIndex, false);
			}));
			cmHide.AddControl(CreateContextMenuButton("Colormap", delegate(ImageContextMenu.MenuButton sender) {
				SetOption(Option.ShowColormap, false);
			}));
			cmHide.AddControl(CreateContextMenuButton("Coordinate System", delegate(ImageContextMenu.MenuButton sender) {
				SetOption(Option.ShowCoordinateSystem, false);
			}));
			cmHide.AddControl(CreateContextMenuButton("Line Grid", delegate(ImageContextMenu.MenuButton sender) {
				SetOption(Option.ShowLineGrid, false);
			}));
			cmHide.AddControl(CreateContextMenuButton("Console", delegate(ImageContextMenu.MenuButton sender) {
				SetOption(Option.ShowConsole, false);
			}));

			ImageContextMenu.MenuGroup cmViewMode = CreateContextMenuGroup("View Control");
			cmImage.AddControl(cmViewMode);
			cmViewMode.AddControl(CreateContextMenuButton("View Centric", delegate(ImageContextMenu.MenuButton sender) {
				SetOption(Option.ViewControl, ImageCloud.ViewControl.ViewCentric);
			}));
			cmViewMode.AddControl(CreateContextMenuButton("Coordinate System Centric", delegate(ImageContextMenu.MenuButton sender) {
				SetOption(Option.ViewControl, ImageCloud.ViewControl.CoordinateSystemCentric);
			}));
			cmViewMode.AddControl(CreateContextMenuButton("Two Dimensional", delegate(ImageContextMenu.MenuButton sender) {
				SetOption(Option.ViewControl, ImageCloud.ViewControl.TwoDimensional);
			}));

			cmImage.ComputeSize();

			//ExecuteISQL(string.Format("x all BY #theta * 3.0f"));
		}

		public override void OnImageMouseDown(MouseButtons button, TransformedImage image, Vector2 uv, out bool enableDrag)
		{
			if(button == MouseButtons.Left)
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
			//ClearSelection();
			//MoveIntoView(image);
		}
		public override void OnImageDrag(TransformedImage image, Vector3 delta)
		{
			MoveSelection(delta);
		}

		bool argidx_visible = true;
		public override void OnKeyDown(KeyEventArgs e)
		{
			switch(e.KeyCode)
			{
			case Keys.C:
				/*ViewControl[] values = (ViewControl[])Enum.GetValues(typeof(ViewControl));
				ActionManager.Do(SetViewControlAction, (int)viewControl + 1 == values.Length ? values[0] : values[(int)viewControl + 1]);*/
				break;

			case Keys.O:
				SetOption(Option.ShowArgumentIndex, argidx_visible = !argidx_visible);
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

			case Keys.Space:
				/*if(bgcolor == Color4.Red)
					bgcolor = Color4.Blue;
				else
					bgcolor = Color4.Red;
				SetOption(Option.BackColor, bgcolor);*/
				break;


			case Keys.P:
				Playback(2.0);
				break;
			case Keys.F12:
				TakeScreenshot();
				break;
			case Keys.R:
				CapturePlayback();
				break;
			case Keys.X:
				ClearPlayback();
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

		public override void OnImageMouseDown(MouseButtons button, TransformedImage image, Vector2 uv, out bool enableDrag)
		{
			enableDrag = false;

			if(button != MouseButtons.Left || image.Selected == true)
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

	public class PhotoBrowser : ImageBrowser
	{
		//private ImageContextMenu.MenuGroup cmImage;

		public override void OnLoad()
		{
			SetOption(Option.BackColor, Color4.Black);
			SetOption(Option.ViewControl, ImageCloud.ViewControl.TwoDimensional);
			SetOption(Option.ShowCoordinateSystem, false);
			SetOption(Option.ShowLineGrid, false);
			SetOption(Option.ShowArgumentIndex, false);
			SetOption(Option.ShowParameterIndex, false);
			SetOption(Option.ShowConsole, false);
			SetOption(Option.EnableMouseRect, false);
			SetOption(Option.FullScreen, true);
			SetOption(Option.ForceOriginalImageSize, true);

			//cmImage = new ImageContextMenu.MenuGroup("");
			//cmImage.controls.Add(new ImageContextMenu.MenuButton("test"));
			//cmImage.ComputeSize();

			ExecuteISQL("x all BY #filename * 3.0f");

			Select(Viewer.images[0]);
			FocusSelection();
			ActionManager.mgr.Invoke("SkipViewAnimation", new object[] { });
		}

		public override void OnImageMouseDown(System.Windows.Forms.MouseButtons button, TransformedImage image, Vector2 uv, out bool enableDrag)
		{
			enableDrag = button == MouseButtons.Left;
		}
		public override void OnNonImageMouseDown()
		{
		}
		public override void OnImageRightClick(TransformedImage image)
		{
			//ShowContextMenu(cmImage);
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
			switch (e.KeyCode)
			{
				case Keys.Left:
					{
						IEnumerator<TransformedImage> imageenum = Viewer.selection.GetEnumerator();
						if (!imageenum.MoveNext())
							break;
						int imageIndex = imageenum.Current.key[0];
						if (imageIndex > 0)
						{
							ExecuteISQL("SELECT WHERE #filename == " + (--imageIndex).ToString());
							FocusSelection();
							ActionManager.mgr.Invoke("SkipViewAnimation", new object[] { });
						}
					}
					break;

				case Keys.Right:
					{
						IEnumerator<TransformedImage> imageenum = Viewer.selection.GetEnumerator();
						if (!imageenum.MoveNext())
							break;
						int imageIndex = imageenum.Current.key[0];
						if (imageIndex < Viewer.images.Count - 1)
						{
							ExecuteISQL("SELECT WHERE #filename == " + (++imageIndex).ToString());
							FocusSelection();
							ActionManager.mgr.Invoke("SkipViewAnimation", new object[] { });
						}
					}
					break;

				case Keys.Y:
					{
						/*IEnumerator<TransformedImage> imageenum = Viewer.selection.GetEnumerator();
						if (!imageenum.MoveNext())
							break;
						TransformedImage.ImageLayer layer = imageenum.Current.FirstLayer;
						layer.bmp.RotateFlip(RotateFlipType.Rotate90FlipNone);
						layer.TriggerReload();
						layer.originalWidth = layer.bmp.Width;
						layer.originalHeight = layer.bmp.Height;
						layer.originalAspectRatio = (float)layer.originalWidth / (float)layer.originalHeight;*/
					}
					break;

				case Keys.Escape:
					Exit();
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

	public class CinemaConverterBrowser : ImageBrowser
	{
		public override void OnLoad()
		{
			SetOption(Option.BackColor, Color4.DarkSlateBlue);

			SetOption(Option.ShowCoordinateSystem, false);
			SetOption(Option.ShowLineGrid, false);
			SetOption(Option.ShowColormap, false);
			SetOption(Option.ShowArgumentIndex, false);
			SetOption(Option.ShowParameterIndex, false);

			SetOption(Option.ForceOriginalImageSize, true);

			Hide(Viewer.images);
			//ExecuteISQL("rspread all");
			//ExecuteISQL("z all by - #theta - 1.5f * #phi");
			ExecuteISQL("z all by - #phi - #theta");
			Focus(Viewer.images);
			foreach(TransformedImage image in Viewer.images)
			{
				image.skipPosAnimation();

				string filename = image.metaFilename;
				filename = filename.Substring(0, filename.Length - ".meta".Length) + ".png";
				if(!filename.Contains("RV_15km_18_10_3_1_2048x2048"))
					throw new Exception();
				filename = filename.Replace("RV_15km_18_10_3_1_2048x2048", "RV_15km_18_10_3_1_2048x2048_SpecA");
				if(!filename.Contains("/mpas/"))
					throw new Exception();
				filename = filename.Replace("/mpas/", "/");
				System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filename));

				image.SaveAssembledImageToFile(filename, OnImageSaved);
			}
			Show(Viewer.images);
		}

		private int imageIdx = 0;
		private void OnImageSaved(TransformedImage sender)
		{
			Hide(sender);

			System.Console.WriteLine(string.Format("{0} of {1} done", imageIdx++, Viewer.images.Count));
		}
	}
}

