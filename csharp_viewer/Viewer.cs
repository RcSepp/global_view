#define DISABLE_DATAVIZ
//#define USE_STD_IO
#define EMBED_CONSOLE

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
	public static class Global
	{
		public static float time = 0.0f;
	}

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

		GLWindow glImageCloud; //OpenTK.GLControl glImageCloud;
		bool glImageCloud_loaded = false, form_closing = false, renderThread_finished = false;
		//ImageCloud imageCloud = new SimpleImageCloud();
		//ImageCloud imageCloud = new ThetaPhiImageCloud();
		ImageCloud imageCloud;
		Control ctrlConsole = null;
		ActionManager actMgr = new ActionManager();
#if USE_STD_IO
		StdConsole scrCle = new StdConsole();
#else
		ScriptingConsole scrCle = new ScriptingConsole();
#endif
		Control scrCle_Invoker = null;
		public static ImageBrowser browser = new SimpleBrowser();
		//public static ImageBrowser browser = new MPASBrowser();

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
		//Dictionary<int[], TransformedImage> images = new Dictionary<int[], TransformedImage>(new IntArrayEqualityComparer()); // A hashmap of images accessed by an index array (consisting of one index per dimension)
		public static TransformedImageCollection images = new TransformedImageCollection();
		public static Mutex image_render_mutex = new Mutex();
		public static Cinema.CinemaArgument[] arguments; // An array of descriptors for each dimension
		//HashSet<string> valueset = new HashSet<string>(); // A set of all value types appearing in the metadata of at least one image
		Dictionary<string, HashSet<object>> valuerange = new Dictionary<string, HashSet<object>>(); // A set of all value types, containing a set of all possible values in the metadata all images

		public static Selection selection = new Selection(images);
		public static HashSet<TransformedImage> visible = new HashSet<TransformedImage>();
		public static Dictionary<string, IEnumerable<TransformedImage>> groups = new Dictionary<string, IEnumerable<TransformedImage>>();

		bool closing = false;
		string name_pattern, depth_name_pattern;

		private Action AppStartAction, ExitProgramAction;
		private Action OnSelectionChangedAction, OnSelectionMovedAction, OnTransformationAddedAction, ClearTransformsAction;
		//private Action FocusAction, MoveAction, ShowAction, HideAction, ClearAction, CountAction, GroupAction;

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

		//[STAThread]
		static public void Main(string[] args)
		{
			/*string name_pattern, depth_name_pattern, image_pixel_format;
			Cinema.ParseCinemaDescriptor(args[0], out arguments, out name_pattern, out depth_name_pattern, out image_pixel_format);

			string code = "select where $theta > 100";
			ISQL.Compiler compiler = new ISQL.Compiler();
			compiler.Execute(code);*/



			try {
				Application.Run(new Viewer(args));
			//Application.Run(new DimensionMapper(arguments));
			} catch(Exception ex) {
				MessageBox.Show(ex.Message, ex.TargetSite.ToString());
			}
		}

		/*GLWindow foo;
		public Viewer(string[] cmdline)
		{
			foo = new GLWindow();
			foo.Dock = DockStyle.Fill;
			this.Controls.Add(foo);
		}*/
		public Viewer(string[] cmdline)
		{
			//Control.CheckForIllegalCrossThreadCalls = false;

			this.cmdline = cmdline;

			AppStartAction = ActionManager.CreateAction(null, this, "ResetAll");
			OnSelectionChangedAction = ActionManager.CreateAction("Change Selection", this, "OnSelectionChanged");
			OnSelectionMovedAction = ActionManager.CreateAction("Move Selection", this, "OnSelectionMoved");
			OnTransformationAddedAction = ActionManager.CreateAction("Add Transformation", this, "OnTransformationAdded");

			groups.Add("all", images);
			groups.Add("none", new List<TransformedImage>());
			groups.Add("selection", selection);
			foreach(TransformedImage image in images)
				visible.Add(image);
			groups.Add("visible", visible);

			// >>> Initialize Components

			Rectangle screenbounds = Screen.PrimaryScreen.WorkingArea;

			if(Environment.OSVersion.Platform == PlatformID.MacOSX || Environment.OSVersion.Platform == PlatformID.Unix)
				screenbounds = new Rectangle(screenbounds.Left, screenbounds.Top + 22, screenbounds.Width + 8, screenbounds.Height - 100);

			this.Text = "csharp_viewer";
			this.StartPosition = FormStartPosition.Manual;
			#if EMBED_CONSOLE
			this.Bounds = screenbounds;
			#else
			this.Bounds = new Rectangle(screenbounds.Left, screenbounds.Top, screenbounds.Width * 2 / 3, screenbounds.Height - 100);
			//this.Bounds = new Rectangle(0, 0, 1608, 1251); // Results in backbuffersize == (1600, 1024)
			#endif
			//this.BackColor = Color.White;
			this.FormClosing += form_Closing;

			glImageCloud = new GLWindow();//new OpenTK.GLControl(new GraphicsMode(32, 24, 8, 1), 3, 0, GraphicsContextFlags.Default);
			glImageCloud.Load += glImageCloud_Load;
			//glImageCloud.Paint += glImageCloud_Paint;
			glImageCloud.TabIndex = 0;
			glImageCloud.AllowDrop = true;
			this.Controls.Add(glImageCloud);
			imageCloud = new ImageCloud();
			imageCloud.Dock = DockStyle.Fill;
			glImageCloud.Controls.Add(imageCloud);

			#if !DISABLE_DATAVIZ
			pnlPCView = new Panel();
			pnlPCView.BackColor = Color.Black;
			pnlPCView.TabIndex = 2;
			this.Controls.Add(pnlPCView);
			#endif

			this.SizeChanged += this_SizeChanged;

			//tbArguments = new TrackBar[arguments.Length];
			//lblArgumentValues = new Label[arguments.Length];

#if !DISABLE_DATAVIZ
			// Create data visualization
			dataviz = new DataVisualization(images, arguments, valueset, pnlPCView);
#endif

#if !USE_STD_IO
			// Create scripting console
			#if EMBED_CONSOLE
			ctrlConsole = scrCle.Create();
			scrCle.ExecuteCommand += Console_Execute;
			this.Controls.Add(ctrlConsole);
			#else
			Form frmConsole = new Form();
			frmConsole.StartPosition = FormStartPosition.Manual;
			frmConsole.Bounds = new Rectangle(this.Left + this.Width, this.Top, screenbounds.Width - this.Width, 512);
			Control ctrlConsole = scrCle.Create();
			ctrlConsole.Dock = DockStyle.Fill;
			scrCle.MethodCall += actMgr.Invoke;
			frmConsole.Controls.Add(ctrlConsole);
			frmConsole.Show();
			#endif
#endif

			if(Directory.Exists("/Users/sklaassen/Desktop/work/db"))
				scrCle.workingDirectory = "/Users/sklaassen/Desktop/work/db";

			this_SizeChanged(null, null);



			ActionManager.CreateAction<string>("Load database", "load", delegate(object[] parameters) {
				LoadAny((string)parameters[0]);
				return null;
			});
			ActionManager.CreateAction("Unload database", "unload", this, "UnloadDatabase");
			//ClearTransformsAction = ActionManager.CreateAction("Clear Transformations", "clear", this, "ClearTransforms");
			ActionManager.CreateAction("Exit program", "exit", this, "Exit");

			ActionManager.CreateAction<IEnumerable<TransformedImage>>("Select images", "select", delegate(object[] parameters) {
				IEnumerable<TransformedImage> scope = (IEnumerable<TransformedImage>)parameters[0];
				image_render_mutex.WaitOne();
				imageCloud.Select(scope);
				image_render_mutex.ReleaseMutex();
				return null;
			});
			ActionManager.CreateAction<IEnumerable<TransformedImage>>("Focus images", "focus", delegate(object[] parameters) {
				IEnumerable<TransformedImage> scope = (IEnumerable<TransformedImage>)parameters[0];
				image_render_mutex.WaitOne();
				imageCloud.Focus(scope, true);
				image_render_mutex.ReleaseMutex();
				return null;
			});
			ActionManager.CreateAction<Vector3, IEnumerable<TransformedImage>>("Move images", "move", delegate(object[] parameters) {
				Vector3 deltapos = (Vector3)parameters[0];
				IEnumerable<TransformedImage> scope = (IEnumerable<TransformedImage>)parameters[1];
				image_render_mutex.WaitOne();
				imageCloud.Move(deltapos, scope);
				image_render_mutex.ReleaseMutex();
				return null;
			});
			ActionManager.CreateAction<IEnumerable<TransformedImage>>("Show images", "show", delegate(object[] parameters) {
				IEnumerable<TransformedImage> scope = (IEnumerable<TransformedImage>)parameters[0];
				image_render_mutex.WaitOne();
				browser.Show(scope);
				image_render_mutex.ReleaseMutex();
				return null;
			});
			ActionManager.CreateAction<IEnumerable<TransformedImage>>("Hide images", "hide", delegate(object[] parameters) {
				IEnumerable<TransformedImage> scope = (IEnumerable<TransformedImage>)parameters[0];
				image_render_mutex.WaitOne();
				browser.Hide(scope);
				image_render_mutex.ReleaseMutex();
				return null;
			});
			ActionManager.CreateAction<IEnumerable<TransformedImage>>("Clear image transforms", "clear", delegate(object[] parameters) {
				IEnumerable<TransformedImage> scope = (IEnumerable<TransformedImage>)parameters[0];
				image_render_mutex.WaitOne();
				imageCloud.Clear(scope);
				image_render_mutex.ReleaseMutex();
				return null;
			});
			ActionManager.CreateAction<IEnumerable<TransformedImage>>("Count images", "count", delegate(object[] parameters) {
				IEnumerable<TransformedImage> scope = (IEnumerable<TransformedImage>)parameters[0];
				image_render_mutex.WaitOne();
				string result = imageCloud.Count(scope).ToString();
				image_render_mutex.ReleaseMutex();
				return result;
			});
			ActionManager.CreateAction<string, IEnumerable<TransformedImage>>("Create image group", "form", delegate(object[] parameters) {
				string groupname = (string)parameters[0];
				IEnumerable<TransformedImage> scope = (IEnumerable<TransformedImage>)parameters[1];
				if(groups.ContainsKey(groupname))
					return "Group " + groupname + " already exists";
				image_render_mutex.WaitOne();
				groups.Add(groupname, imageCloud.CreateGroup(scope));
				image_render_mutex.ReleaseMutex();
				return null;
			});
			ActionManager.CreateAction("Create translation transform in x-direction", "x", this, "CreateTransformX");
			ActionManager.CreateAction("Create translation transform in y-direction", "y", this, "CreateTransformY");
			ActionManager.CreateAction("Create translation transform in z-direction", "z", this, "CreateTransformZ");
			ActionManager.CreateAction("Create theta-phi transform", "thetaPhi", this, "CreateTransformThetaPhi");
			ActionManager.CreateAction("Create star-coordinates transform", "star", this, "CreateTransformStar");
			ActionManager.CreateAction("Create transform that only shows the image whose view angle most closly matches the cameras view angle", "look", this, "CreateTransformLookAt");
			ActionManager.CreateAction("Create skip transform", "skip", this, "CreateTransformSkip");

			ActionManager.CreateAction("Clear selection", "none", delegate(object[] parameters) {
				image_render_mutex.WaitOne();
				selection.Clear();
				OnSelectionChanged(selection);
				image_render_mutex.ReleaseMutex();
				return null;
			});

			/*ActionManager.CreateAction<int>("Apply x transform to %a of selection", "x %a", delegate(object[] parameters) {
				int argidx = (int)parameters[0];
				if(argidx < arguments.Length)
				{
					XTransform transform = new XTransform();
					transform.SetArguments(arguments);
					transform.SetIndex(0, argidx);
					OnTransformationAdded(transform, selection);
				}
				return null;
			});
			ActionManager.CreateAction<int>("Apply y transform to %a of selection", "y %a", delegate(object[] parameters) {
				int argidx = (int)parameters[0];
				if(argidx < arguments.Length)
				{
					YTransform transform = new YTransform();
					transform.SetArguments(arguments);
					transform.SetIndex(0, argidx);
					OnTransformationAdded(transform, selection);
				}
				return null;
			});
			ActionManager.CreateAction<int>("Apply z transform to %a of selection", "z %a", delegate(object[] parameters) {
				int argidx = (int)parameters[0];
				if(argidx < arguments.Length)
				{
					ZTransform transform = new ZTransform();
					transform.SetArguments(arguments);
					transform.SetIndex(0, argidx);
					OnTransformationAdded(transform, selection);
				}
				return null;
			});

			ActionManager.CreateAction<int>("Animate %a of selection", "animate %a", delegate(object[] parameters) {
				int argidx = (int)parameters[0];
				if(argidx < arguments.Length)
				{
					ImageTransform transform = new AnimationTransform();
					transform.SetArguments(arguments);
					transform.SetIndex(0, argidx);
					OnTransformationAdded(transform, selection);
				}
				return null;
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
					OnTransformationAdded(transform, selection);
				}
				return null;
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
					OnTransformationAdded(transform, selection);
				}
				return null;
			});*/

			ActionManager.CreateAction<IEnumerable<TransformedImage>>("Spread out all dimensions", "spread", delegate(object[] parameters) {
				IEnumerable<TransformedImage> scope = (IEnumerable<TransformedImage>)parameters[0];

				if(arguments != null)
				{
					/*WheelTransform transform = new WheelTransform();
					transform.SetArguments(arguments);
					for(int i = 0; i < arguments.Length; ++i)
						transform.SetIndex(i, i);
					OnTransformationAdded(transform, scope);*/

					string byExpr = "0";
					for(int argidx = 0; argidx < arguments.Length; ++argidx)
						if(byExpr == "0")
							byExpr = "2.0f * Array.IndexOf(image.args[" + argidx.ToString() + "].values, image.values[" + argidx.ToString() + "])";
						else
							byExpr += ", 2.0f * Array.IndexOf(image.args[" + argidx.ToString() + "].values, image.values[" + argidx.ToString() + "])";
					CreateTransformStar(byExpr, null, scope);
				}
				return null;
			});
			ActionManager.CreateAction<string, HashSet<int>>("Animate the given argument", "animate", delegate(object[] parameters) {
				string byExpr = (string)parameters[0];
				HashSet<int> indices = (HashSet<int>)parameters[1];
				HashSet<int>.Enumerator indices_enum = indices.GetEnumerator();
				indices_enum.MoveNext();
				int index = indices_enum.Current;

				if(arguments != null)
				{
					string output, warnings;
					Console_Execute(string.Format("skip all BY " + byExpr + " != (int)(time * 10.0f) % " + arguments[index].values.Length.ToString()), out output, out warnings);
					return output + warnings;
				}
				return null;
			});

			ActionManager.CreateAction<IEnumerable<TransformedImage>>("Spread images randomly", "rspread", delegate(object[] parameters) {
				IEnumerable<TransformedImage> scope = (IEnumerable<TransformedImage>)parameters[0];

				if(arguments != null)
				{
					int numimages = imageCloud.Count(scope);
					float ext = (float)Math.Sqrt(numimages), halfext = ext / 2.0f;

					Random rand = new Random();
					image_render_mutex.WaitOne();
					foreach(TransformedImage image in scope)
						image.pos = new Vector3((float)rand.NextDouble() * ext - halfext, (float)rand.NextDouble() * ext - halfext, (float)0.0f);
					image_render_mutex.ReleaseMutex();

					imageCloud.InvalidateOverallBounds();

					// Update selection (bounds may have changed due to added transform)
					CallSelectionChangedHandlers();
				}
				return null;
			});
			ActionManager.CreateAction<IEnumerable<TransformedImage>>("Spread images randomly in 3 dimensions", "rspread3d", delegate(object[] parameters) {
				IEnumerable<TransformedImage> scope = (IEnumerable<TransformedImage>)parameters[0];

				if(arguments != null)
				{
					int numimages = imageCloud.Count(scope);
					float ext = (float)Math.Pow(numimages, 1.0 / 3.0), halfext = ext / 2.0f;

					Random rand = new Random();
					image_render_mutex.WaitOne();
					foreach(TransformedImage image in scope)
						image.pos = new Vector3((float)rand.NextDouble() * ext - halfext, (float)rand.NextDouble() * ext - halfext, (float)rand.NextDouble() * ext - halfext);
					image_render_mutex.ReleaseMutex();

					imageCloud.InvalidateOverallBounds();

					// Update selection (bounds may have changed due to added transform)
					CallSelectionChangedHandlers();
				}
				return null;
			});

#if USE_STD_IO
			scrCle.MethodCall += actMgr.Invoke;
			scrCle_Invoker = this;
			scrCle.Run();
#endif
		}

		private void ResetAll()
		{
			image_render_mutex.WaitOne();

			selection.Clear();
			foreach(TransformedImage image in images)
				visible.Add(image);

			image_render_mutex.ReleaseMutex();

			OnSelectionChanged(selection);
			ClearTransforms();
		}

		private void form_Closing(object sender, FormClosingEventArgs e)
		{
			form_closing = true;
			while(!renderThread_finished) {Thread.Sleep(1);}

			UnloadDatabase(); // Important: Do this only after renderThread has finished
			imageCloud.Free();
		}

		private void Console_Execute(string command, out string output, out string warnings)
		{
			ISQL.Compiler.Execute(command, compiler_MethodCall, TransformCompiled, out output, out warnings);
		}
		private string compiler_MethodCall(string method, object[] args)
		{
			string stdout;
			if(scrCle_Invoker != null)
			{
				IAsyncResult invokeResult = scrCle_Invoker.BeginInvoke(new ISQL.Compiler.MethodCallDelegate(actMgr.Invoke), new object[] { method, args });
				invokeResult.AsyncWaitHandle.WaitOne();
				stdout = (string)scrCle_Invoker.EndInvoke(invokeResult);
			}
			else
				stdout = actMgr.Invoke(method, args);
			return stdout;
		}

		private void FindFileOrDirectory(ref string path)
		{
			bool isdir;
			if(!(isdir = System.IO.Directory.Exists(path)) && !System.IO.File.Exists(path))
			{
				string relativePath = scrCle.workingDirectory + Path.DirectorySeparatorChar + path;
				if(!(isdir = System.IO.Directory.Exists(relativePath)) && !System.IO.File.Exists(relativePath))
					throw new System.IO.FileNotFoundException(path);
				path = relativePath;
			}

			if(isdir && !path.EndsWith("/") && !path.EndsWith("\\"))
				path += Path.DirectorySeparatorChar;
		}

		private void PreLoad()
		{
			if(images.Count != 0)
				UnloadDatabase(); // Only one database can be loaded at a time
		}
		private void PostLoad(Size imageSize)
		{
			/*// Create selection array and populate it with the default values
			selection = new IndexProductSelection(arguments.Length, valuerange.Count, images);
			for(int i = 0; i < arguments.Length; ++i)
			{
				if(arguments[i].defaultValue != null)
					selection[i].Add(Array.IndexOf(arguments[i].values, arguments[i].defaultValue));
			}*/

			image_render_mutex.WaitOne();

			if(imageCloud != null)
			{
				try {
					imageCloud.Load(arguments, images, valuerange, imageSize, image_pixel_format != null && image_pixel_format.Equals("I24"), depth_name_pattern != null);
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

			imageCloud.SelectionChanged += CallSelectionChangedHandlers;
			imageCloud.SelectionMoved += CallSelectionMovedHandlers;
			imageCloud.TransformAdded += ImageCloud_TransformAdded;

			actMgr.Load(arguments);
			actMgr.FrameCaptureFinished += actMgr_FrameCaptureFinished;

			browser.SelectionChanged += CallSelectionChangedHandlers;
			browser.SelectionMoved += CallSelectionMovedHandlers;

			image_render_mutex.ReleaseMutex();


			//ActionManager.Do(ClearTransformsAction);
			//CallSelectionChangedHandlers();

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



			/*imageCloud.SelectAll();
			ImageTransform bar = new XYTransform();
			bar.SetArguments(arguments); bar.SetIndex(0, 0); bar.SetIndex(1, 1);
			imageCloud.AddTransform(bar);
			ActionManager.Do(OnTransformationAddedAction, new object[] { bar });*/
		}

		private void LoadFromCommandLine(string[] argv)
		{
			if(argv == null | argv.Length == 0)
				return;

			List<string> filenames = new List<string>();
			bool recursive = false;
			string name_pattern = null;
			for(int i = 0; i < argv.Length; ++i)
			{
				string arg = argv[i];
				if(arg.StartsWith("-"))
				{
					if(arg == "-r" || arg == "--recursive")
						recursive = true;
					else if(arg == "-p" || arg == "--pattern")
					{
						if(i == argv.Length - 1)
							throw new ArgumentException("missing argument after " + arg);
						name_pattern = argv[++i];
					}
				}
				else
					filenames.Add(arg);
			}

			if(filenames.Count == 0)
				throw new ArgumentException("no files specified");

			if(filenames.Count == 1)
				LoadAny(argv[0], recursive, name_pattern);
			else
				throw new NotImplementedException("Multiple file load not yet implemented");
		}
		private void LoadAny(string filename, bool recursive = false, string name_pattern = null)
		{
			FindFileOrDirectory(ref filename);

			if(Directory.Exists(filename))
			{
				if(Cinema.IsCinemaDB(filename))
					LoadCinemaDatabase(filename);
				else
					LoadDatabaseFromDirectory(filename, name_pattern, recursive);
			}
			else if(File.Exists(filename))
			{
				if(filename.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || filename.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
					LoadDatabaseFromImages(new string[] { filename }, name_pattern);
				else
					throw new FileLoadException(filename + " is not a PNG image");
			}
			else
				throw new FileNotFoundException(filename + " not found");
		}

		private void LoadCinemaDatabase(string filename)
		{
			FindFileOrDirectory(ref filename);

			PreLoad();

			// Parse meta data from info.json
			Cinema.ParseCinemaDescriptor(filename, out arguments, out name_pattern, out depth_name_pattern, out image_pixel_format);

			// >>> Load images and image meta

			string imagepath;
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
				do {
					// Construct CinemaImage key and image file path from argidx[]
					float[] imagevalues = new float[arguments.Length];
					string[] imagestrvalues = new string[arguments.Length];
					imagepath = name_pattern;
					for(int i = 0; i < arguments.Length; ++i)
					{
						imagevalues[i] = arguments[i].values[argidx[i]];
						imagestrvalues[i] = arguments[i].strValues[argidx[i]];
						imagepath = imagepath.Replace("{" + arguments[i].name + "}", arguments[i].strValues[argidx[i]].ToString());

						/*if(imagevalues[i].GetType() == typeof(string) && float.TryParse((string)imagevalues[i], out floatvalue))
							imagevalues[i] = floatvalue;
						else if(imagevalues[i].GetType() == typeof(long))
							imagevalues[i] = (float)(long)imagevalues[i];*/
					}
					imagepath = filename + "image/" + imagepath;

					String depthpath = depth_name_pattern;
					if(depth_name_pattern != null)
					{
						// Construct depth image file path from argidx[]
						for(int i = 0; i < arguments.Length; ++i)
							depthpath = depthpath.Replace("{" + arguments[i].name + "}", arguments[i].strValues[argidx[i]].ToString());
						depthpath = filename + "image/" + depthpath;
					}

					// Load CinemaImage
					TransformedImage cimg = new TransformedImage();
					cimg.LocationChanged += imageCloud.InvalidateOverallBounds;
					cimg.values = imagevalues;
					cimg.strValues = imagestrvalues;
					cimg.args = arguments;
					cimg.filename = imagepath;
					cimg.depth_filename = depthpath;
					Cinema.ParseImageDescriptor(imagepath.Substring(0, imagepath.Length - "png".Length) + "json", out cimg.meta, out cimg.invview);
					cimg.key = new int[argidx.Length]; Array.Copy(argidx, cimg.key, argidx.Length);
					image_render_mutex.WaitOne();
					//images.Add(cimg.key, cimg);
					images.Add(cimg);
					visible.Add(cimg);
					image_render_mutex.ReleaseMutex();

					for(int i = 0; i < arguments.Length; ++i)
					{
						List<TransformedImage> valueimages;
						arguments[i].images.TryGetValue(imagevalues[i], out valueimages);
						if(valueimages == null)
							valueimages = new List<TransformedImage>();
						valueimages.Add(cimg);
					}

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

			// Get image size
			imagepath = name_pattern;
			for(int i = 0; i < arguments.Length; ++i)
				imagepath = imagepath.Replace("{" + arguments[i].name + "}", arguments[i].strValues[0].ToString());
			imagepath = filename + "image/" + imagepath;
			Image img = Image.FromFile(imagepath);
			Size imageSize = new Size(img.Width, img.Height);
			img.Dispose();

			PostLoad(imageSize);
		}


		private void FindImagesRecursive(string dirname, ref List<string> filenames)
		{
			filenames.AddRange(Directory.GetFiles(dirname, "*.png"));
			foreach(string subdirname in Directory.GetDirectories(dirname))
				FindImagesRecursive(subdirname, ref filenames);
		}
		private void LoadDatabaseFromDirectory(string dirname, string name_pattern = null, bool recursive = false)
		{
			FindFileOrDirectory(ref dirname);

			PreLoad();

			List<string> filenames = new List<string>();
			foreach(string filename in Directory.EnumerateFiles(dirname, "*.*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
				if(filename.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || filename.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
					filenames.Add(filename);
			LoadDatabaseFromImages(filenames.ToArray(), dirname + name_pattern);
		}

		private void LoadDatabaseFromImages(string[] filenames, string name_pattern = null)
		{
			if(filenames.Length == 0)
				return;

			PreLoad();

			// Parse meta data from info.json
			string[] name_pattern_splitters = null;
			Dictionary<string, int>[] strValueIndices = null;
			if(name_pattern != null && name_pattern != "")
			{
				System.Text.RegularExpressions.MatchCollection matches = System.Text.RegularExpressions.Regex.Matches(name_pattern, "{\\w*}");
				arguments = new Cinema.CinemaArgument[matches.Count];
				name_pattern_splitters = new string[matches.Count + 1];
				strValueIndices = new Dictionary<string, int>[matches.Count];
				int last_match_end = 0, i = 0;
				foreach(System.Text.RegularExpressions.Match match in matches)
				{
					string argumentStr = match.Value.Substring(1, match.Value.Length - 2);
					name_pattern_splitters[i] = name_pattern.Substring(last_match_end, match.Index - last_match_end);
					last_match_end = match.Index + match.Length;

					Cinema.CinemaArgument carg = arguments[i] = new Cinema.CinemaArgument();
					carg.name = argumentStr;
					carg.label = argumentStr;
					carg.strValues = new string[0];
					carg.values = new float[0];
					carg.defaultValue = float.NaN;

					strValueIndices[i++] = new Dictionary<string, int>();
				}
				name_pattern_splitters[i] = name_pattern.Substring(last_match_end);
				this.name_pattern = name_pattern;
			}
			else
			{
				arguments = new Cinema.CinemaArgument[0];
				this.name_pattern = "";
			}
			depth_name_pattern = "";
			image_pixel_format = "";//"I24";

			// >>> Load images and image meta

			// Load images and image meta data by iterating over all argument combinations
			foreach(string imagepath in filenames)
			{
				// >>> Load CinemaImage

				// Get values through name_pattern
				string[] strValues = new string[arguments.Length];
				float[] values = new float[arguments.Length];
				int[] key = new int[arguments.Length];
				if(arguments.Length > 0)
				{
					// Make sure name_pattern starts with name_pattern_splitters[0]
					if(!imagepath.StartsWith(name_pattern_splitters[0]))
					{
						System.Console.Error.WriteLine("image path " + imagepath + " does not match pattern " + name_pattern);
						continue; // Skip image
					}

					bool err = false;
					int valueend = 0;
					for(int i = 0; i < arguments.Length; ++i)
					{
						// Find start of next name pattern splitter (== end of value)
						int valuestart = valueend + name_pattern_splitters[i].Length;
						valueend = name_pattern_splitters[i + 1] == "" ? imagepath.Length : imagepath.IndexOf(name_pattern_splitters[i + 1], valuestart);
						if(valueend == -1)
						{
							System.Console.Error.WriteLine("image path " + imagepath + " does not match pattern " + name_pattern);
							err = true;
							break;
						}

						strValues[i] = imagepath.Substring(valuestart, valueend - valuestart);
						float.TryParse(strValues[i], out values[i]);
					}
					if(err)
						continue; // Skip image

					for(int i = 0; i < arguments.Length; ++i)
					{
						int strValueindex;
						if(!strValueIndices[i].TryGetValue(strValues[i], out strValueindex))
						{
							strValueIndices[i].Add(strValues[i], strValueindex = strValueIndices[i].Count);

							for(int j = i + 1; j < arguments.Length; ++j) //DELETE
								strValueIndices[j].Clear(); //DELETE
						}
						key[i] = strValueindex;
					}
				}

				TransformedImage cimg = new TransformedImage();
				cimg.LocationChanged += imageCloud.InvalidateOverallBounds;

				cimg.values = values;
				cimg.strValues = strValues;
				cimg.args = arguments;
				cimg.filename = imagepath;
				cimg.depth_filename = null;
				Cinema.ParseImageDescriptor(imagepath.Substring(0, imagepath.Length - "png".Length) + "json", out cimg.meta, out cimg.invview);

				cimg.key = key;
				image_render_mutex.WaitOne();
				images.Add(cimg);
				visible.Add(cimg);
				image_render_mutex.ReleaseMutex();

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
			}

			// >>> Update argument values
			for(int i = 0; i < arguments.Length; ++i)
			{
				arguments[i].strValues = new string[strValueIndices[i].Count];
				arguments[i].values = new float[strValueIndices[i].Count];
				foreach(KeyValuePair<string, int> pair in strValueIndices[i])
				{
					arguments[i].strValues[pair.Value] = pair.Key;
					float.TryParse(pair.Key, out arguments[i].values[pair.Value]);
				}
			}

			#if !DISABLE_DATAVIZ
			if(dataviz != null)
			dataviz.ImagesAdded();
			#endif

			// Get image size
			Image img = Image.FromFile(filenames[0]);
			Size imageSize = new Size(img.Width, img.Height);
			img.Dispose();

			PostLoad(imageSize);
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

			Thread.Sleep(5000);

			Random rand = new Random();

			int indexlist_length = indexlist.Count;
			//int[] selectedimagekey = new int[argidx.Length];
			while(indexlist_length != 0 && closing == false)
			{
				// >>> Load random image by poping a random index from the index list

				int r = rand.Next(indexlist_length);
				argidx = indexlist[r];
				indexlist[r] = indexlist[indexlist_length - 1];
				--indexlist_length;

				// Construct CinemaImage key and image file path from argidx[]
				float[] imagevalues = new float[arguments.Length];
				string[] imagestrvalues = new string[arguments.Length];
				String imagepath = name_pattern;
				for(int i = 0; i < arguments.Length; ++i)
				{
					imagevalues[i] = arguments[i].values[argidx[i]];
					imagestrvalues[i] = arguments[i].strValues[argidx[i]];
					imagepath = imagepath.Replace("{" + arguments[i].name + "}", arguments[i].strValues[argidx[i]].ToString());
				}
				imagepath = filename + "image/" + imagepath;

				// Load CinemaImage
				TransformedImage cimg = new TransformedImage();
				cimg.LocationChanged += imageCloud.InvalidateOverallBounds;
				cimg.values = imagevalues;
				cimg.strValues = imagestrvalues;
				cimg.args = arguments;
				cimg.filename = imagepath;

				for(int i = 0; i < arguments.Length; ++i)
				{
					List<TransformedImage> valueimages;
					arguments[i].images.TryGetValue(imagevalues[i], out valueimages);
					if(valueimages == null)
						valueimages = new List<TransformedImage>();
					valueimages.Add(cimg);
				}

				Cinema.ParseImageDescriptor(imagepath.Substring(0, imagepath.Length - "png".Length) + "json", out cimg.meta, out cimg.invview);
				cimg.key = new int[argidx.Length]; Array.Copy(argidx, cimg.key, argidx.Length);
foreach(ImageTransform transform in imageCloud.transforms)
	cimg.AddTransform(transform);

				image_render_mutex.WaitOne();

				//images.Add(cimg.key, cimg);
				images.Add(cimg);
				visible.Add(cimg);

				// If the loaded images is the one currently selected, call SelectImage functions to update the image in each dependent class
				if(selection != null && selection.Contains(cimg))
					CallSelectionChangedHandlers();

				image_render_mutex.ReleaseMutex();

				imageCloud.AddImage(cimg);

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

				Thread.Sleep(1000);
			}
		}

		private void UnloadDatabase()
		{
			image_render_mutex.WaitOne();

			imageCloud.Unload();
			actMgr.Unload();

			arguments = null;
			selection.Clear();
			visible.Clear();
			images.Clear();

			image_render_mutex.ReleaseMutex();

			OnSelectionChanged(selection);
			ClearTransforms();
		}

		private void Exit()
		{
#if USE_STD_IO
			scrCle.Close();
#endif
			this.Close();
		}

		private static void SetControlSize(Control control, int x, int y, int width, int height)
		{
			if(control != null)
				control.SetBounds(x, y, width > 0 ? width : 0, height > 0 ? height : 0);
		}
		private void this_SizeChanged(object sender, EventArgs e)
		{
			// >>> Apply UI logic manually since anchors and docking are useless on Mono Forms for OsX

			int w = this.ClientSize.Width, h = this.ClientSize.Height;
#if DISABLE_DATAVIZ
			SetControlSize(glImageCloud, 0, 0, w, ctrlConsole == null ? h : h - 256);
			if(ctrlConsole != null)
				SetControlSize(ctrlConsole, 0, h - 256, w, 256);
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

		private void CallSelectionChangedHandlers()
		{
			ActionManager.Do(OnSelectionChangedAction, selection.Clone());
		}
		private void CallSelectionMovedHandlers()
		{
			ActionManager.Do(OnSelectionMovedAction);
		}
		private void OnSelectionChanged(Selection _selection)
		{
			if(!_selection.IsClone(selection))
			{
				selection.Clear();
				foreach(TransformedImage selectedimage in _selection)
					selection.Add(selectedimage);
			}

			/*int[] imagekey = new int[arguments.Length];
			for(int i = 0; i < arguments.Length; ++i)
			{
				var e = selection[i].GetEnumerator(); e.MoveNext();
				imagekey[i] = e.Current;
			}*/

			image_render_mutex.WaitOne();

			if(imageCloud != null)
				imageCloud.OnSelectionChanged();

#if !DISABLE_DATAVIZ
			if(dataviz != null)
				dataviz.OnSelectionChanged(imagekey, images);
#endif

			image_render_mutex.ReleaseMutex();
		}
		private void OnSelectionMoved()
		{
			image_render_mutex.WaitOne();

			if(imageCloud != null)
				imageCloud.OnSelectionMoved();

			image_render_mutex.ReleaseMutex();
		}

		private void ArgumentIndex_ArgumentLabelMouseDown(Cinema.CinemaArgument argument, int argumentIndex)
		{
		}

		private void ImageCloud_TransformAdded(ImageTransform newtransform)
		{
			if(selection != null)
				ActionManager.Do(OnTransformationAddedAction, newtransform, selection);
		}
		private string TransformCompiled(ImageTransform transform, IEnumerable<TransformedImage> images)
		{
			ActionManager.Do(OnTransformationAddedAction, transform, selection);
			return "";
		}
		private void OnTransformationAdded(ImageTransform newtransform, IEnumerable<TransformedImage> images)
		{
			if(images == null)
				return;

			image_render_mutex.WaitOne();
			imageCloud.AddTransform(newtransform);

			foreach(TransformedImage image in images)
				image.AddTransform(newtransform);
			image_render_mutex.ReleaseMutex();

			// Update selection (bounds may have changed due to added transform)
			CallSelectionChangedHandlers();
		}

		private void dimMapper_TransformRemoved(ImageTransform transform)
		{
			imageCloud.RemoveTransform(transform);

			foreach(TransformedImage selectedimage in selection)
				selectedimage.RemoveTransform(transform);

			// Update selection (bounds may have changed due to removed transform)
			CallSelectionChangedHandlers();
		}

		private void CreateTransformX(string byExpr, HashSet<int> byExpr_usedArgumentIndices, IEnumerable<TransformedImage> images)
		{
			string warnings = "";
			ImageTransform transform = CompiledTransform.CompileTranslationTransform(byExpr, "0.0f", "0.0f", true, ref warnings);

			OnTransformationAdded(transform, images);
		}
		private void CreateTransformY(string byExpr, HashSet<int> byExpr_usedArgumentIndices, IEnumerable<TransformedImage> images)
		{
			string warnings = "";
			ImageTransform transform = CompiledTransform.CompileTranslationTransform("0.0f", byExpr, "0.0f", true, ref warnings);

			OnTransformationAdded(transform, images);
		}
		private void CreateTransformZ(string byExpr, HashSet<int> byExpr_usedArgumentIndices, IEnumerable<TransformedImage> images)
		{
			string warnings = "";
			ImageTransform transform = CompiledTransform.CompileTranslationTransform("0.0f", "0.0f", byExpr, true, ref warnings);

			OnTransformationAdded(transform, images);
		}
		private void CreateTransformThetaPhi(string byExpr, HashSet<int> byExpr_usedArgumentIndices, IEnumerable<TransformedImage> images)
		{
			string warnings = "";
			ImageTransform transform = CompiledTransform.CompilePolarTransform(byExpr, true, ref warnings);

			OnTransformationAdded(transform, images);
		}
		private void CreateTransformStar(string byExpr, HashSet<int> byExpr_usedArgumentIndices, IEnumerable<TransformedImage> images)
		{
			string warnings = "";
			ImageTransform transform = CompiledTransform.CompileStarTransform(byExpr, true, ref warnings);

			OnTransformationAdded(transform, images);
		}
		private void CreateTransformLookAt(string byExpr, HashSet<int> byExpr_usedArgumentIndices, IEnumerable<TransformedImage> images)
		{
			string indices = null;
			foreach(int index in byExpr_usedArgumentIndices)
				if(indices == null)
					indices = index.ToString();
				else
					indices += ", " + index.ToString();

			string warnings = "";
			ImageTransform transform = CompiledTransform.CreateTransformLookAt(byExpr, indices, ref warnings);

			transform.SetArguments(arguments);
			OnTransformationAdded(transform, images);
		}
		private void CreateTransformSkip(string byExpr, HashSet<int> byExpr_usedArgumentIndices, IEnumerable<TransformedImage> images)
		{
			string warnings = "";
			ImageTransform transform = CompiledTransform.CompileSkipTransform(byExpr, true, ref warnings);

			OnTransformationAdded(transform, images);
		}

		private void ClearTransforms()
		{
			image_render_mutex.WaitOne();
			imageCloud.ClearTransforms();

			foreach(TransformedImage image in images.Values)
			{
				image.ClearTransforms();
				image.skipPosAnimation();
			}
			image_render_mutex.ReleaseMutex();

			// Update selection (bounds may have changed due to removed transforms)
			CallSelectionChangedHandlers();
		}

		private void glImageCloud_Load(object sender, EventArgs e)
		{
			Thread renderThread = new Thread(RenderThread);
			renderThread.Priority = ThreadPriority.Lowest;
			renderThread.Start();
		}
		private void RenderThread()
		{
			glImageCloud.Init();
			//GL.ClearColor(0.8f, 0.8f, 0.8f, 1.0f);
			GL.ClearColor(0.0f, 0.1f, 0.3f, 1.0f);
			//GL.Viewport(glImageCloud.Height > glImageCloud.Width ? new Rectangle(0, (glImageCloud.Height - glImageCloud.Width) / 2, glImageCloud.Width, glImageCloud.Width) : new Rectangle((glImageCloud.Width - glImageCloud.Height) / 2, 0, glImageCloud.Height, glImageCloud.Height));
			GL.Viewport(glImageCloud.Size);
			GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
			GL.Enable(EnableCap.Blend);
			GL.DepthFunc(DepthFunction.Lequal);
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

			glImageCloud.DragEnter += glImageCloud_DragEnter;
			glImageCloud.DragDrop += glImageCloud_DragDrop;

			glImageCloud.MouseDown += glImageCloud_MouseDown;
			glImageCloud.MouseUp += glImageCloud_MouseUp;
			glImageCloud.MouseMove += glImageCloud_MouseMove;
			glImageCloud.MouseWheel += glImageCloud_MouseWheel;
			glImageCloud.DoubleClick += glImageCloud_DoubleClick;
			glImageCloud.KeyDown += glImageCloud_KeyDown;

			glImageCloud_loaded = true;

			ActionManager.Do(AppStartAction);

			//if(cmdline.Length == 1)
			//	LoadCinemaDatabase(cmdline[0]);
			LoadFromCommandLine(cmdline);
//LoadDatabaseFromImages(new string[] {"/Users/sklaassen/Desktop/work/db/cinema_debug/image/1.000000/-30/-30.png"});
//if(cmdline.Length == 1)
//	LoadDatabaseFromDirectory(cmdline[0], false);

			browser.Init(imageCloud);
			browser.OnLoad();

			// Start timer
			timer = new System.Diagnostics.Stopwatch();
			timer.Start();

			float averageDt = 0.0f;
			int frameCounter = 0;
			while(!form_closing)
			{
				if (image_render_mutex.WaitOne(1) == false)
					continue;

				InputDevices.Update();

				float dt = (float)timer.Elapsed.TotalSeconds;
				timer.Restart();

				//dt = Math.Min(0.1f, dt); // Avoid high dt during lags

				if(dt < 1.0f)
				{
					averageDt = dt + (float)frameCounter * averageDt;
					averageDt /= (float)++frameCounter;
				}
				else
				{
					dt = averageDt;
					++frameCounter;
				}

				actMgr.Update(ref dt);

				glImageCloud.Render(dt);

				image_render_mutex.ReleaseMutex();

				glImageCloud.SwapBuffers();

				actMgr.PostRender(glImageCloud, scrCle as ScriptingConsole);

				Global.time += dt;
			}
			renderThread_finished = true;
		}

		void glImageCloud_DragEnter(object sender, DragEventArgs e)
		{
			if(e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
		}

		void glImageCloud_DragDrop(object sender, DragEventArgs e)
		{
			string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
			if(files.Length == 1)
				LoadAny(files[0], true);
			else if(files.Length > 1)
				LoadDatabaseFromImages(files);
		}

		private bool mouseDownInsideArgIndex = false;
		private void glImageCloud_MouseDown(object sender, MouseEventArgs e)
		{
			image_render_mutex.WaitOne();
			#if USE_ARG_IDX
			if(argIndex.MouseDown(glImageCloud.Size, e))
				mouseDownInsideArgIndex = true;
			else
			#endif
				imageCloud.MouseDown(sender, e);
			image_render_mutex.ReleaseMutex();
		}
		private void glImageCloud_MouseUp(object sender, MouseEventArgs e)
		{
			mouseDownInsideArgIndex = false;
			image_render_mutex.WaitOne();
			#if USE_ARG_IDX
			if(!argIndex.MouseUp(glImageCloud.Size, e))
			#endif
				imageCloud.MouseUp(sender, e);
			image_render_mutex.ReleaseMutex();
		}
		private void glImageCloud_MouseMove(object sender, MouseEventArgs e)
		{
			image_render_mutex.WaitOne();
			#if USE_ARG_IDX
			if(!argIndex.MouseMove(glImageCloud.Size, e) && !mouseDownInsideArgIndex)
			#endif
				imageCloud.MouseMove(sender, e);
			image_render_mutex.ReleaseMutex();
		}
		private void glImageCloud_MouseWheel(object sender, MouseEventArgs e)
		{
			image_render_mutex.WaitOne();
			imageCloud.MouseWheel(sender, e);
			image_render_mutex.ReleaseMutex();
		}
		private void glImageCloud_DoubleClick(object sender, EventArgs e)
		{
			image_render_mutex.WaitOne();
			imageCloud.DoubleClick(sender, this.PointToClient(MousePosition));
			image_render_mutex.ReleaseMutex();
		}
		private delegate void bar(object sender, EventArgs e);
		private void glImageCloud_KeyDown(object sender, KeyEventArgs e)
		{
			switch(e.KeyCode)
			{
			case Keys.P:
				actMgr.Play(2.0);
				break;
			case Keys.F12:
				actMgr.SaveScreenshot("screenshot.png");
				ImageCloud.Status("Screenshot saved as \"screenshot.png\"");
				break;
			case Keys.R:
				// Switch to video-friendly resolution
				this.ClientSize = new Size(1920, 1200);
				this_SizeChanged(null, null);

				actMgr.CaptureFrames(20.0);
				break;
			case Keys.X:
				actMgr.Clear();
				break;
			default:
				image_render_mutex.WaitOne();
				imageCloud.KeyDown(sender, e);
				browser.OnKeyDown(e);
				image_render_mutex.ReleaseMutex();
				break;
			}
		}

		private void actMgr_FrameCaptureFinished()
		{
			ImageCloud.Status("Frame capture finished");
		}
	}
}