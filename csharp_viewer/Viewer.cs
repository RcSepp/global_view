#define DISABLE_DATAVIZ

using System;
using System.Windows.Forms;
using System.Drawing;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace csharp_viewer
{
	public class Viewer : Form
	{
		//public static string DATABASE_PATH = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "/work/mpas_060km/";
		//public static string DATABASE_PATH = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "/work/mpas_test/";
		//public static string DATABASE_PATH = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "/work/cinema_v3/";
		//public static string DATABASE_PATH = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "/work/cinema_v5/";

		//public static string DATABASE_PATH = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "/work/db/debug/";
		//public static string DATABASE_PATH = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "/work/db/cosmo/";
		//public static string DATABASE_PATH = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "/work/db/mag-figs-cmap/";
		//public static string DATABASE_PATH = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "/work/db/mpas/";
		//public static string DATABASE_PATH = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "/work/db/mpas_ani/";
		//public static string DATABASE_PATH = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "/work/db/mpas_view/";

		public static int IMAGE_DIV = 1;

		GLControl glImageCloud;
		bool glImageCloud_loaded = false, form_closing = false;
		//ImageCloud imageCloud = new SimpleImageCloud();
		//ImageCloud imageCloud = new ThetaPhiImageCloud();
		ArgumentIndex argIndex = new ArgumentIndex();
		ImageCloud imageCloud;
		FlowLayoutPanel pnlImageControls;
		Control ctrlConsole = null;
		DimensionMapper dimMapper;
		ActionManager actMgr = new ActionManager();
		ScriptingConsole scrCle = new ScriptingConsole();

#if !DISABLE_DATAVIZ
		Panel pnlPCView;
		DataVisualization dataviz = null;
#endif

		System.Diagnostics.Stopwatch timer;

		readonly string[] cmdline;
		string image_pixel_format = null;
		//TrackBar[] tbArguments;
		//Label[] lblArgumentValues;

		// Main database collections
		Dictionary<int[], TransformedImage> images = new Dictionary<int[], TransformedImage>(new IntArrayEqualityComparer()); // A hashmap of images accessed by an index array (consisting of one index per dimension)
		public static Mutex images_mutex = new Mutex();
		Cinema.CinemaArgument[] arguments; // An array of descriptors for each dimension
		//HashSet<string> valueset = new HashSet<string>(); // A set of all value types appearing in the metadata of at least one image
		Dictionary<string, HashSet<object>> valuerange = new Dictionary<string, HashSet<object>>(); // A set of all value types, containing a set of all possible values in the metadata all images

		private Selection selection = null;

		bool closing = false;
		string name_pattern, depth_name_pattern;

		private Action LoadDatabaseAction, UnloadDatabaseAction, ExitProgramAction;
		private Action OnSelectionChangedAction, OnTransformationAddedAction, ClearTransformsAction;

		public static bool KeyEquals(int[] x, int[] y)
		{
			if(x.Length != y.Length)
				return false;
			for(int i = 0; i < x.Length; i++)
				if(!x[i].Equals(y[i]))
					return false;
			return true;
		}
		public class IntArrayEqualityComparer : IEqualityComparer<int[]>
		{
			public bool Equals(int[] x, int[] y)
			{
				if(x.Length != y.Length)
					return false;
				for(int i = 0; i < x.Length; i++)
					if(!x[i].Equals(y[i]))
						return false;
				return true;
			}

			public int GetHashCode(int[] obj)
			{
				int hash = 17;
				for(int i = 0; i < obj.Length; i++)
					hash = hash * 23 + obj[i];
				return hash;
			}
		}

		[STAThread]
		static public void Main(string[] args)
		{
			/*OpenTK.Matrix4 foo = OpenTK.Matrix4.CreatePerspectiveFieldOfView(60.0f * MathHelper.Pi / 180.0f, 1.0f, 1.0f, 1000.0f).Inverted();
			OpenTK.Vector4 bar1 = OpenTK.Vector4.Transform(new OpenTK.Vector4(0.0f, 0.0f, -1.0f, 1.0f), foo); bar1.X /= bar1.W; bar1.Y /= bar1.W; bar1.Z /= bar1.W;
			OpenTK.Vector4 bar2 = OpenTK.Vector4.Transform(new OpenTK.Vector4(0.0f, 0.0f, 0.0f, 1.0f), foo); bar2.X /= bar2.W; bar2.Y /= bar2.W; bar2.Z /= bar2.W;
			OpenTK.Vector4 bar3 = OpenTK.Vector4.Transform(new OpenTK.Vector4(0.0f, 0.0f, 1.0f, 1.0f), foo); bar3.X /= bar3.W; bar3.Y /= bar3.W; bar3.Z /= bar3.W;
			OpenTK.Vector4 bar4 = OpenTK.Vector4.Transform(new OpenTK.Vector4(0.0f, 0.0f, 10.0f, 1.0f), foo); bar4.X /= bar4.W; bar4.Y /= bar4.W; bar4.Z /= bar4.W;
			int abc = 0;*/

			/*// Parse meta data from info.json
			Cinema.CinemaArgument[] arguments;
			string name_pattern;
			string image_pixel_format;
			Cinema.ParseCinemaDescriptor(DATABASE_PATH, out arguments, out name_pattern, out image_pixel_format);*/

			try {
				Application.Run(new Viewer(args));
			//Application.Run(new DimensionMapper(arguments));
			} catch(Exception ex) {
				MessageBox.Show(ex.Message, ex.TargetSite.ToString());
			}
		}

		public Viewer(string[] cmdline)
		{
			this.cmdline = cmdline;

			OnSelectionChangedAction = ActionManager.CreateAction("Change Selection", this, "OnSelectionChanged");
			OnTransformationAddedAction = ActionManager.CreateAction("Add Transformation", this, "OnTransformationAdded");

			// >>> Initialize Components

			Rectangle screenbounds = Screen.PrimaryScreen.Bounds;

			this.Text = "csharp_viewer";
			this.StartPosition = FormStartPosition.Manual;
			this.Bounds = new Rectangle(screenbounds.Left, screenbounds.Top + 22, screenbounds.Width * 2 / 3, screenbounds.Height - 200);
			this.BackColor = Color.White;
			this.FormClosing += form_Closing;

			glImageCloud = new GLControl(new GraphicsMode(32, 24, 8, 1), 3, 2, GraphicsContextFlags.Default);
			glImageCloud.Load += glImageCloud_Load;
			//glImageCloud.Paint += glImageCloud_Paint;
			glImageCloud.TabIndex = 0;
			this.Controls.Add(glImageCloud);
			imageCloud = new ImageCloud();

			pnlImageControls = new FlowLayoutPanel();
			pnlImageControls.BackColor = SystemColors.Control;
			pnlImageControls.TabIndex = 1;
			this.Controls.Add(pnlImageControls);

			#if !DISABLE_DATAVIZ
			pnlPCView = new Panel();
			pnlPCView.BackColor = Color.Black;
			pnlPCView.TabIndex = 2;
			this.Controls.Add(pnlPCView);
			#endif

			this.SizeChanged += this_SizeChanged;
			this.FormClosing += this_FormClosing;

			//tbArguments = new TrackBar[arguments.Length];
			//lblArgumentValues = new Label[arguments.Length];

			#if !DISABLE_DATAVIZ
			// Create data visualization
			dataviz = new DataVisualization(images, arguments, valueset, pnlPCView);
			#endif

			// Create scripting console
			/*ctrlConsole = scrCle.Create();
			scrCle.MethodCall += actMgr.Invoke;
			this.Controls.Add(ctrlConsole);*/
			Form frmConsole = new Form();
			frmConsole.StartPosition = FormStartPosition.Manual;
			frmConsole.Bounds = new Rectangle(this.Left + this.Width, this.Top, screenbounds.Width - this.Width, 512);
			Control ctrlConsole = scrCle.Create();
			ctrlConsole.Dock = DockStyle.Fill;
			scrCle.MethodCall += actMgr.Invoke;
			frmConsole.Controls.Add(ctrlConsole);
			frmConsole.Show();

			this_SizeChanged(null, null);

			// Create dimension mapper
			dimMapper = new DimensionMapper();
			dimMapper.TransformAdded += dimMapper_TransformAdded;
			dimMapper.TransformRemoved += dimMapper_TransformRemoved;





			LoadDatabaseAction = ActionManager.CreateAction("Load database", "load", this, "LoadDatabase");
			LoadDatabaseAction = ActionManager.CreateAction("Unload database", "unload", this, "UnloadDatabase");
			ClearTransformsAction = ActionManager.CreateAction("Clear Transformations", "clear", this, "ClearTransforms");
			ActionManager.CreateAction("Exit program", "exit", this, "Exit");

			ActionManager.CreateAction("Select all", "all", argIndex, "SelectAll");
			ActionManager.CreateAction("Clear selection", "none", delegate(object[] parameters) {
				images_mutex.WaitOne();
				OnSelectionChanged(null);
				images_mutex.ReleaseMutex();
			});
			ActionManager.CreateAction("Focus selection", "focus", imageCloud, "FocusSelection");
			ActionManager.CreateAction("Select and focus all", "focus all", delegate(object[] parameters) {
				argIndex.SelectAll();
				imageCloud.FocusSelection();
			});

			ActionManager.CreateAction<int>("Apply x transform to %a of selection", "x %a", delegate(object[] parameters) {
				int argidx = (int)parameters[0];
				if(argidx < arguments.Length)
				{
					XTransform transform = new XTransform();
					transform.SetArguments(arguments);
					transform.SetIndex(0, argidx);
					OnTransformationAdded(transform);
				}
			});
			ActionManager.CreateAction<int>("Apply y transform to %a of selection", "y %a", delegate(object[] parameters) {
				int argidx = (int)parameters[0];
				if(argidx < arguments.Length)
				{
					YTransform transform = new YTransform();
					transform.SetArguments(arguments);
					transform.SetIndex(0, argidx);
					OnTransformationAdded(transform);
				}
			});
			ActionManager.CreateAction<int>("Apply z transform to %a of selection", "z %a", delegate(object[] parameters) {
				int argidx = (int)parameters[0];
				if(argidx < arguments.Length)
				{
					ZTransform transform = new ZTransform();
					transform.SetArguments(arguments);
					transform.SetIndex(0, argidx);
					OnTransformationAdded(transform);
				}
			});

			ActionManager.CreateAction<int>("Animate %a of selection", "animate %a", delegate(object[] parameters) {
				int argidx = (int)parameters[0];
				if(argidx < arguments.Length)
				{
					ImageTransform transform = new AnimationTransform();
					transform.SetArguments(arguments);
					transform.SetIndex(0, argidx);
					OnTransformationAdded(transform);
				}
			});

			ActionManager.CreateAction<int, int>("Apply theta-phi-view transform", "theta-phi-view %a %a", delegate(object[] parameters) {
				int thetaidx = (int)parameters[0];
				int phiidx = (int)parameters[1];
				if(thetaidx < arguments.Length && phiidx < arguments.Length)
				{
					ImageTransform transform = new ThetaPhiViewTransform();
					transform.SetArguments(arguments);
					transform.SetIndex(0, thetaidx);
					transform.SetIndex(1, phiidx);
					OnTransformationAdded(transform);
				}
			});

			ActionManager.CreateAction<int, int>("Apply theta-phi transform", "theta-phi %a %a", delegate(object[] parameters) {
				int thetaidx = (int)parameters[0];
				int phiidx = (int)parameters[1];
				if(thetaidx < arguments.Length && phiidx < arguments.Length)
				{
					ImageTransform transform = new ThetaPhiTransform();
					transform.SetArguments(arguments);
					transform.SetIndex(0, thetaidx);
					transform.SetIndex(1, phiidx);
					OnTransformationAdded(transform);
				}
			});

			ActionManager.CreateAction("Spread out all dimensions", "spread", delegate(object[] parameters) {
				WheelTransform transform = new WheelTransform();
				transform.SetArguments(arguments);
				for(int i = 0; i < arguments.Length; ++i)
					transform.SetIndex(i, i);
				OnTransformationAdded(transform);
			});
			ActionManager.CreateAction("Select all images and spread out all dimensions", "spread all", delegate(object[] parameters) {
				argIndex.SelectAll();

				WheelTransform transform = new WheelTransform();
				transform.SetArguments(arguments);
				for(int i = 0; i < arguments.Length; ++i)
					transform.SetIndex(i, i);
				OnTransformationAdded(transform);
			});
		}

		private void form_Closing(object sender, FormClosingEventArgs e)
		{
			form_closing = true;
		}

		private void LoadDatabase(string filename)
		{
			if(images.Count != 0)
				UnloadDatabase(); // Only one database can be loaded at a time

			if(!System.IO.Directory.Exists(filename))
				throw new System.IO.DirectoryNotFoundException(filename);

			// Parse meta data from info.json
			Cinema.ParseCinemaDescriptor(filename, out arguments, out name_pattern, out depth_name_pattern, out image_pixel_format);

			// >>> Load images and image meta

			if(false)
			{
				Thread inSituThread = new Thread(new ParameterizedThreadStart(SimulateInSituThread));
				inSituThread.Start((object)filename);
			}
			else
			{
				// Load images and image meta data by iterating over all argument combinations
				int[] argidx = new int[arguments.Length];
				bool done;
				float floatvalue;
				do {
					// Construct CinemaImage key and image file path from argidx[]
					object[] imagevalues = new object[arguments.Length];
					String imagepath = name_pattern;
					for(int i = 0; i < arguments.Length; ++i)
					{
						imagevalues[i] = arguments[i].values[argidx[i]];
						imagepath = imagepath.Replace("{" + arguments[i].name + "}", imagevalues[i].ToString());

						if(imagevalues[i].GetType() == typeof(string) && float.TryParse((string)imagevalues[i], out floatvalue))
							imagevalues[i] = floatvalue;
						else if(imagevalues[i].GetType() == typeof(long))
							imagevalues[i] = (float)(long)imagevalues[i];
					}
					imagepath = filename + "image/" + imagepath;

					String depthpath = depth_name_pattern;
					if(depth_name_pattern != null)
					{
						// Construct depth image file path from argidx[]
						for(int i = 0; i < arguments.Length; ++i)
							depthpath = depthpath.Replace("{" + arguments[i].name + "}", arguments[i].values[argidx[i]].ToString());
						depthpath = filename + "image/" + depthpath;
					}

					// Load CinemaImage
					TransformedImage cimg = new TransformedImage();
					cimg.LocationChanged += imageCloud.InvalidateOverallBounds;
					cimg.values = imagevalues;
					cimg.filename = imagepath;
					cimg.depth_filename = depthpath;
					Cinema.ParseImageDescriptor(imagepath.Substring(0, imagepath.Length - "png".Length) + "json", out cimg.meta, out cimg.invview);
					cimg.key = new int[argidx.Length]; Array.Copy(argidx, cimg.key, argidx.Length);
					images_mutex.WaitOne();
					images.Add(cimg.key, cimg);
					images_mutex.ReleaseMutex();

					if(cimg.meta != null)
						foreach(KeyValuePair<string, object> meta in cimg.meta)
						{
							HashSet<object> range;
							if(!valuerange.TryGetValue(meta.Key, out range))
							{
								range = new HashSet<object>();
								valuerange.Add(meta.Key, range);
							}
							range.Add(meta.Value);
						}

					// Get next argument combination -> argidx[]
					done = true;
					for(int i = 0; i < arguments.Length; ++i) {
						if(++argidx[i] == arguments[i].values.Length)
							argidx[i] = 0;
						else {
							done = false;
							break;
						}
					}
				} while(!done);
				#if !DISABLE_DATAVIZ
				if(dataviz != null)
				dataviz.ImagesAdded();
				#endif
			}

			/*// Create selection array and populate it with the default values
			selection = new IndexProductSelection(arguments.Length, valuerange.Count, images);
			for(int i = 0; i < arguments.Length; ++i)
			{
				if(arguments[i].defaultValue != null)
					selection[i].Add(Array.IndexOf(arguments[i].values, arguments[i].defaultValue));
			}*/

			images_mutex.WaitOne();

			if(imageCloud != null)
			{
				// Get image size
				String imagepath = name_pattern;
				for(int i = 0; i < arguments.Length; ++i)
					imagepath = imagepath.Replace("{" + arguments[i].name + "}", arguments[i].values[0].ToString());
				imagepath = filename + "image/" + imagepath;
				Image img = Image.FromFile(imagepath);
				Size imageSize = new Size(img.Width / IMAGE_DIV, img.Height / IMAGE_DIV);
				img.Dispose();

				try {
					imageCloud.Load(pnlImageControls, arguments, images, imageSize, image_pixel_format != null && image_pixel_format.Equals("I24"), depth_name_pattern != null);
				} catch(Exception ex) {
					MessageBox.Show(ex.Message, ex.TargetSite.ToString());
					throw ex;
				}

				/*// >>> Define heuristic to choose transformations based on argument names

				Dictionary<string, int> argnames = new Dictionary<string, int>();
				int idx = 0;
				foreach(Cinema.CinemaArgument argument in arguments)
					argnames.Add(argument.name, idx++);

				if(argnames.ContainsKey("theta") && argnames.ContainsKey("phi"))
				{
					imageCloud.transforms.Add(new XYTransform(argnames["theta"], argnames["phi"], arguments));
					//imageCloud.transforms.Add(new ThetaPhiTransform(argnames["theta"], argnames["phi"]));
					imageCloud.transforms.Add(new HighlightSelectionTransform(Color4.Azure));
					argnames.Remove("theta");
					argnames.Remove("phi");
				}
				//imageCloud.transforms.Add(new XYTransform(2, 1, arguments));

				if(argnames.ContainsKey("time"))
				{
					imageCloud.transforms.Add(new XTransform(argnames["time"], arguments));
					//imageCloud.transforms.Add(new AnimationTransform(argnames["time"], arguments));
					argnames.Remove("time");
				}*/
			}

			argIndex.Load(images, arguments, valuerange);
			argIndex.SelectionChanged += CallSelectionChangedHandlers;
			argIndex.ArgumentLabelMouseDown += ArgumentIndex_ArgumentLabelMouseDown;

			imageCloud.SelectionChanged += CallSelectionChangedHandlers;
			imageCloud.TransformAdded += dimMapper_TransformAdded;

			dimMapper.Load(arguments);

			actMgr.Load(arguments);

			images_mutex.ReleaseMutex();

			/*// Load textures for all images in images
			images_mutex.WaitOne();
			foreach(TransformedImage img in images.Values)
				img.LoadTexture();
			images_mutex.ReleaseMutex();*/


			//ActionManager.Do(ClearTransformsAction);
			//CallSelectionChangedHandlers(selection);

			/*IndexProductSelection foo = new IndexProductSelection(arguments.Length, valuerange.Count, images);
			for(int i = 0; i < arguments.Length; ++i)
				for(int j = 0; j < arguments[i].values.Length; ++j)
					foo[i].Add(j);
			ActionManager.Do(OnSelectionChangedAction, new object[] { foo });

			ImageTransform bar = new ThetaPhiViewTransform();
			bar.SetArguments(arguments); bar.SetIndex(0, 0); bar.SetIndex(1, 1);
			imageCloud.AddTransform(bar);
			ActionManager.Do(OnTransformationAddedAction, new object[] { bar });

			bar = new AnimationTransform();
			bar.SetArguments(arguments); bar.SetIndex(0, 2);
			imageCloud.AddTransform(bar);
			ActionManager.Do(OnTransformationAddedAction, new object[] { bar });*/
		}

		private void SimulateInSituThread(object parameters)
		{
			string filename = (string)parameters;

			// Create a list of all available indices
			List<int[]> indexlist = new List<int[]>();
			int[] argidx = new int[arguments.Length];
			bool done;
			do {
				// Add argidx to indexqueue
				int[] argidx_copy = new int[arguments.Length];
				Array.Copy(argidx, argidx_copy, argidx.Length);
				indexlist.Add(argidx_copy);

				// Get next argument combination -> argidx[]
				done = true;
				for(int i = 0; i < arguments.Length; ++i) {
					if(++argidx[i] == arguments[i].values.Length)
						argidx[i] = 0;
					else {
						done = false;
						break;
					}
				}
			} while(!done);

			Random rand = new Random();

//imageCloud.AddTransform(new AnimationTransform(0, arguments));
//imageCloud.AddTransform(new ThetaPhiTransform(2, 1));
//ThetaPhiTransform foo = new ThetaPhiTransform();
//foo.SetArguments(arguments); foo.SetIndex(0, 2); foo.SetIndex(1, 1);

/*GraphTransform foo = new GraphTransform();
foo.SetArguments(arguments); foo.SetIndex(0, 2); foo.SetIndex(1, 1);
imageCloud.AddTransform(foo);*/

/*WheelTransform foo = new WheelTransform();
foo.SetArguments(arguments); for(int i = 0; i < arguments.Length; ++i) foo.SetIndex(i, i);
imageCloud.AddTransform(foo);*/

			int indexlist_length = indexlist.Count;
			//int[] selectedimagekey = new int[argidx.Length];
			float floatvalue;
			while(indexlist_length != 0 && closing == false)
			{
				// >>> Load random image by poping a random index from the index list

				int r = rand.Next(indexlist_length);
				argidx = indexlist[r];
				indexlist[r] = indexlist[indexlist_length - 1];
				--indexlist_length;

				// Construct CinemaImage key and image file path from argidx[]
				object[] imagevalues = new object[arguments.Length];
				String imagepath = name_pattern;
				for(int i = 0; i < arguments.Length; ++i)
				{
					imagevalues[i] = arguments[i].values[argidx[i]];
					imagepath = imagepath.Replace("{" + arguments[i].name + "}", imagevalues[i].ToString());

					if(imagevalues[i].GetType() == typeof(string) && float.TryParse((string)imagevalues[i], out floatvalue))
						imagevalues[i] = floatvalue;
					else if(imagevalues[i].GetType() == typeof(long))
						imagevalues[i] = (float)(long)imagevalues[i];
				}
				imagepath = filename + "image/" + imagepath;

				// Load CinemaImage
				TransformedImage cimg = new TransformedImage();
				cimg.LocationChanged += imageCloud.InvalidateOverallBounds;
				cimg.values = imagevalues;
				cimg.filename = imagepath;

				Cinema.ParseImageDescriptor(imagepath.Substring(0, imagepath.Length - "png".Length) + "json", out cimg.meta, out cimg.invview);
				cimg.key = new int[argidx.Length]; Array.Copy(argidx, cimg.key, argidx.Length);
foreach(ImageTransform transform in imageCloud.transforms)
	cimg.AddTransform(transform);

				images_mutex.WaitOne();

				images.Add(cimg.key, cimg);

				// If the loaded images is the one currently selected, call SelectImage functions to update the image in each dependent class
				if(selection != null && selection.Contains(cimg.key))
					CallSelectionChangedHandlers(selection);

				images_mutex.ReleaseMutex();

				if(cimg.meta != null)
					foreach(KeyValuePair<string, object> meta in cimg.meta)
					{
						HashSet<object> range;
						if(!valuerange.TryGetValue(meta.Key, out range))
						{
							range = new HashSet<object>();
							valuerange.Add(meta.Key, range);
						}
						range.Add(meta.Value);
					}

#if !DISABLE_DATAVIZ
				if(dataviz != null)
					dataviz.ImagesAdded();
#endif

				Thread.Sleep(10);
			}
		}

		private void UnloadDatabase()
		{
			images_mutex.WaitOne();

			imageCloud.Unload();
			argIndex.Unload();
			actMgr.Unload();

			arguments = null;
			images.Clear();

			images_mutex.ReleaseMutex();

			OnSelectionChanged(null);
			ClearTransforms();
		}

		private void Exit()
		{
			this.Close();
		}

		private static void SetControlSize(Control control, int x, int y, int width, int height)
		{
			control.SetBounds(x, y, width > 0 ? width : 0, height > 0 ? height : 0);
		}
		private void this_SizeChanged(object sender, EventArgs e)
		{
			// >>> Apply UI logic manually since anchors and docking are useless on Mono Forms for OsX

			int w = this.ClientSize.Width, h = this.ClientSize.Height;
#if DISABLE_DATAVIZ
			SetControlSize(glImageCloud, 0, 0, w, h - 200);
			if(ctrlConsole != null)
			{
				SetControlSize(pnlImageControls, 0, h - 200, w, 32);
				SetControlSize(ctrlConsole, 0, h - 168, w, 168);
			}
			else
				SetControlSize(pnlImageControls, 0, h - 200, w, 200);
#else
			SetControlSize(glImageCloud, 0, 0, (w - 4) / 2, h - 200);
			if(ctrlConsole != null)
			{
				SetControlSize(pnlImageControls, 0, h - 200, (w - 4) / 2, 32);
				SetControlSize(ctrlConsole, 0, h - 168, (w - 4) / 2, 168);
			}
			else
				SetControlSize(pnlImageControls, 0, h - 200, (w - 4) / 2, 200);
			SetControlSize(pnlPCView, (w - 208) / 2 + 4, 0, (w - 4) / 2, h);
#endif
			// Invalidate all controls
			foreach(Control control in this.Controls)
				control.Hide();
			Application.DoEvents();
			System.Threading.Thread.Sleep(100);
			foreach(Control control in this.Controls)
				control.Show();

			if(glImageCloud_loaded)
			{
				//GL.Viewport(glImageCloud.Height > glImageCloud.Width ? new Rectangle(0, (glImageCloud.Height - glImageCloud.Width) / 2, glImageCloud.Width, glImageCloud.Width) : new Rectangle((glImageCloud.Width - glImageCloud.Height) / 2, 0, glImageCloud.Height, glImageCloud.Height));
				GL.Viewport(glImageCloud.Size);
				imageCloud.OnSizeChanged(glImageCloud.Size);
			}
			/*Application.DoEvents();
			tbArgument_ValueChanged(null, null); // OsX bugfix*/
		}

		private void this_FormClosing(object sender, FormClosingEventArgs e)
		{
			closing = true;
		}

		private void CallSelectionChangedHandlers(Selection _selection)
		{
			ActionManager.Do(OnSelectionChangedAction, new object[] {_selection == null ? new ArraySelection(images) : _selection.Clone()});
		}
		private void OnSelectionChanged(Selection _selection)
		{
			selection = _selection;

			/*int[] imagekey = new int[arguments.Length];
			for(int i = 0; i < arguments.Length; ++i)
			{
				var e = selection[i].GetEnumerator(); e.MoveNext();
				imagekey[i] = e.Current;
			}*/

			images_mutex.WaitOne();

			if(imageCloud != null)
				imageCloud.OnSelectionChanged(selection);
			if(argIndex != null)
				argIndex.OnSelectionChanged(selection);

#if !DISABLE_DATAVIZ
			if(dataviz != null)
				dataviz.OnSelectionChanged(imagekey, images);
#endif

			images_mutex.ReleaseMutex();
		}

		private void ArgumentIndex_ArgumentLabelMouseDown(Cinema.CinemaArgument argument, int argumentIndex)
		{
			dimMapper.Show();
			dimMapper.BringToFront();
		}

		private void dimMapper_TransformAdded(ImageTransform newtransform)
		{
			ActionManager.Do(OnTransformationAddedAction, new object[] {newtransform});
		}
		private void OnTransformationAdded(ImageTransform newtransform)
		{
			if(selection == null)
				return;

			images_mutex.WaitOne();
			imageCloud.AddTransform(newtransform);

			foreach(KeyValuePair<int[], TransformedImage> selectedimage in selection)
				selectedimage.Value.AddTransform(newtransform);
			images_mutex.ReleaseMutex();

			// Update selection (bounds may have changed due to added transform)
			CallSelectionChangedHandlers(selection);
		}

		private void dimMapper_TransformRemoved(ImageTransform transform)
		{
			imageCloud.RemoveTransform(transform);

			foreach(KeyValuePair<int[], TransformedImage> selectedimage in selection)
				selectedimage.Value.RemoveTransform(transform);

			// Update selection (bounds may have changed due to removed transform)
			CallSelectionChangedHandlers(selection);
		}

		private void ClearTransforms()
		{
			images_mutex.WaitOne();
			imageCloud.ClearTransforms();

			foreach(TransformedImage image in images.Values)
			{
				image.ClearTransforms();
				image.skipPosAnimation();
			}
			images_mutex.ReleaseMutex();

			// Update selection (bounds may have changed due to removed transforms)
			CallSelectionChangedHandlers(selection);
		}

		private void glImageCloud_Load(object sender, EventArgs e)
		{
			glImageCloud.Context.MakeCurrent(null);
			Thread renderThread = new Thread(RenderThread);
			renderThread.Priority = ThreadPriority.Lowest;
			renderThread.Start();
		}
		private void RenderThread()
		{
			glImageCloud.MakeCurrent();
			GL.ClearColor(0.8f, 0.8f, 0.8f, 1.0f);
			//GL.ClearColor(0.0f, 0.1f, 0.3f, 1.0f);
			//GL.Viewport(glImageCloud.Height > glImageCloud.Width ? new Rectangle(0, (glImageCloud.Height - glImageCloud.Width) / 2, glImageCloud.Width, glImageCloud.Width) : new Rectangle((glImageCloud.Width - glImageCloud.Height) / 2, 0, glImageCloud.Height, glImageCloud.Height));
			GL.Viewport(glImageCloud.Size);
			GL.Enable(EnableCap.DepthTest);
			GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
			GL.Enable(EnableCap.Blend);
			//GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);

			Common.CreateCommonMeshes();
			Common.CreateCommonFonts();
			try {
				Common.CreateCommonShaders();
			} catch(Exception ex) {
				MessageBox.Show(ex.Message, "Error creating shaders");
			}

			imageCloud.Init(glImageCloud);
			imageCloud.OnSizeChanged(glImageCloud.Size);

			argIndex.Init();

			glImageCloud.MouseDown += glImageCloud_MouseDown;
			glImageCloud.MouseUp += glImageCloud_MouseUp;
			glImageCloud.MouseMove += glImageCloud_MouseMove;
			glImageCloud.KeyDown += glImageCloud_KeyDown;

			// Start timer
			timer = new System.Diagnostics.Stopwatch();
			timer.Start();

			glImageCloud_loaded = true;

			if(cmdline.Length == 1)
				LoadDatabase(cmdline[0]);

			while(!form_closing)
			{
				if (images_mutex.WaitOne(1) == false)
					continue;
				glImageCloud.MakeCurrent();

				InputDevices.Update();

				//glImageCloud.MakeCurrent(); //TODO: Uncomment when using multiple GLControl's

				GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

				float dt = (float)timer.Elapsed.TotalSeconds;
				timer.Restart();

				actMgr.Update(ref dt);

				imageCloud.Draw(dt);
				argIndex.Draw(glImageCloud.PointToClient(Control.MousePosition), glImageCloud.Size);

				images_mutex.ReleaseMutex();

				glImageCloud.SwapBuffers();

				actMgr.PostRender(glImageCloud);
			}
		}

		private bool mouseDownInsideArgIndex = false;
		private void glImageCloud_MouseDown(object sender, MouseEventArgs e)
		{
			images_mutex.WaitOne();
			if(argIndex.MouseDown(glImageCloud.Size, e))
				mouseDownInsideArgIndex = true;
			else
				imageCloud.MouseDown(sender, e);
			images_mutex.ReleaseMutex();
		}
		private void glImageCloud_MouseUp(object sender, MouseEventArgs e)
		{
			mouseDownInsideArgIndex = false;
			images_mutex.WaitOne();
			if(!argIndex.MouseUp(glImageCloud.Size, e))
				imageCloud.MouseUp(sender, e);
			images_mutex.ReleaseMutex();
		}
		private void glImageCloud_MouseMove(object sender, MouseEventArgs e)
		{
			images_mutex.WaitOne();
			if(!argIndex.MouseMove(glImageCloud.Size, e) && !mouseDownInsideArgIndex)
				imageCloud.MouseMove(sender, e);
			images_mutex.ReleaseMutex();
		}
		private void glImageCloud_KeyDown(object sender, KeyEventArgs e)
		{
			if(e.KeyCode == Keys.P)
				actMgr.Play(4.0);
			else if(e.KeyCode == Keys.R)
				actMgr.CaptureFrames(20.0);
			else if(e.KeyCode == Keys.X)
				actMgr.Clear();
			else
			{
				images_mutex.WaitOne();
				imageCloud.KeyDown(sender, e);
				images_mutex.ReleaseMutex();
			}
		}
	}
}