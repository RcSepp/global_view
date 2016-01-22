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
		public static decimal OPENGL_VERSION = -1;
		public static string EXE_DIR = Path.GetDirectoryName(Application.ExecutablePath) + Path.DirectorySeparatorChar;

		public static float time = 0.0f;
		public static Cinema.CinemaArgument[] arguments = new Cinema.CinemaArgument[0]; // An array of descriptors for each dimension
		public static Cinema.CinemaStore.Parameter[] parameters = new Cinema.CinemaStore.Parameter[0]; // An array of descriptors for each parameter

#if USE_STD_IO
		public static StdConsole cle = new StdConsole();
#else
		public static ScriptingConsole cle = new ScriptingConsole();
#endif
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
		bool form_closing = false, renderThread_finished = false;
		//ImageCloud imageCloud = new SimpleImageCloud();
		//ImageCloud imageCloud = new ThetaPhiImageCloud();
		ImageCloud imageCloud;
		Control ctrlConsole = null;
		ActionManager actMgr = new ActionManager();
		Control cle_Invoker = null;
		public static ImageBrowser browser = new SimpleBrowser();
		//public static ImageBrowser browser = new MPASBrowser();
		//public static ImageBrowser browser = new PhotoBrowser();

#if !DISABLE_DATAVIZ
		Panel pnlPCView;
		DataVisualization dataviz = null;
#endif

		System.Diagnostics.Stopwatch timer;

		private bool consoleVisible = true;
		public bool ConsoleVisible
		{
			get { return consoleVisible; }
			set
			{
				if (consoleVisible != value)
				{
					consoleVisible = value;
					this_SizeChanged(this, null);
				}
			}
		}

		readonly string[] cmdline;
		string image_pixel_format = null;
		//TrackBar[] tbArguments;
		//Label[] lblArgumentValues;

		// Main database collections
		//Dictionary<int[], TransformedImage> images = new Dictionary<int[], TransformedImage>(new IntArrayEqualityComparer()); // A hashmap of images accessed by an index array (consisting of one index per dimension)
		public static TransformedImageCollection images = new TransformedImageCollection();
		public static Mutex image_render_mutex = new Mutex();
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

		[STAThread]
		static public void Main(string[] args)
		{
			/*string name_pattern, depth_name_pattern, image_pixel_format;
			Cinema.ParseCinemaDescriptor(args[0], out Global.arguments, out name_pattern, out depth_name_pattern, out image_pixel_format);

			string code = "select where $theta > 100";
			ISQL.Compiler compiler = new ISQL.Compiler();
			compiler.Execute(code);*/



			try {
				Application.Run(new Viewer(args));
			//Application.Run(new DimensionMapper(Global.arguments));
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
			Control.CheckForIllegalCrossThreadCalls = false;

			this.cmdline = cmdline;

			AppStartAction = ActionManager.CreateAction(null, this, "ResetAll");
			OnSelectionChangedAction = ActionManager.CreateAction("Change Selection", this, "OnSelectionChanged");
			OnSelectionMovedAction = ActionManager.CreateAction("Move Selection", this, "OnSelectionMoved");
			OnTransformationAddedAction = ActionManager.CreateAction("Add Transformation", this, "OnTransformationAdded");

			groups.Add("all", images);
			groups.Add("none", new List<TransformedImage>());
			groups.Add("selection", selection); groups.Add("selected", selection);
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

			//tbArguments = new TrackBar[Global.arguments.Length];
			//lblArgumentValues = new Label[Global.arguments.Length];

#if !DISABLE_DATAVIZ
			// Create data visualization
			dataviz = new DataVisualization(images, Global.arguments, valueset, pnlPCView);
#endif

#if !USE_STD_IO
			// Create scripting console
			#if EMBED_CONSOLE
			ctrlConsole = Global.cle.Create();
			Global.cle.ExecuteCommand += Console_Execute;
			this.Controls.Add(ctrlConsole);
			#else
			Form frmConsole = new Form();
			frmConsole.StartPosition = FormStartPosition.Manual;
			frmConsole.Bounds = new Rectangle(this.Left + this.Width, this.Top, screenbounds.Width - this.Width, 512);
			Control ctrlConsole = Global.cle.Create();
			ctrlConsole.Dock = DockStyle.Fill;
			Global.cle.MethodCall += actMgr.Invoke;
			frmConsole.Controls.Add(ctrlConsole);
			frmConsole.Show();
			#endif
#endif

			if(Directory.Exists("/Users/sklaassen/Desktop/work/db"))
				Global.cle.workingDirectory = "/Users/sklaassen/Desktop/work/db";

			this_SizeChanged(null, null);



			ActionManager.CreateAction<string>("Load database", "load", delegate(object[] parameters) {
				LoadAny((string)parameters[0], true);
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
			ActionManager.CreateAction<IEnumerable<TransformedImage>>("Count images", "count", delegate(object[] parameters) {
				IEnumerable<TransformedImage> scope = (IEnumerable<TransformedImage>)parameters[0];
				image_render_mutex.WaitOne();
				string result = imageCloud.Count(scope).ToString();
				image_render_mutex.ReleaseMutex();
				return result;
			});
			ActionManager.CreateAction<ImageTransform.Id>("Remove image transform by id", "remove", delegate(object[] parameters) {
				ImageTransform.Id transformId = (ImageTransform.Id)parameters[0];
				image_render_mutex.WaitOne();
				foreach(ImageTransform transform in imageCloud.transforms)
				{
					if(transform.id == transformId)
					{
						imageCloud.RemoveTransform(transform);
						image_render_mutex.ReleaseMutex();
						return null;
					}
				}
				image_render_mutex.ReleaseMutex();
				return string.Format("Transform id {0} not found", transformId);
			});
			ActionManager.CreateAction<IEnumerable<TransformedImage>>("Clear image transforms", "clear", delegate(object[] parameters) {
				IEnumerable<TransformedImage> scope = (IEnumerable<TransformedImage>)parameters[0];
				image_render_mutex.WaitOne();
				imageCloud.Clear(scope);
				image_render_mutex.ReleaseMutex();
				return null;
			});
			ActionManager.CreateAction<IEnumerable<TransformedImage>>("List image transforms", "list", delegate(object[] parameters) {
				IEnumerable<TransformedImage> scope = (IEnumerable<TransformedImage>)parameters[0];
				image_render_mutex.WaitOne();

				string output = "";
				if(scope == Viewer.images)
				{
					foreach(ImageTransform transform in imageCloud.transforms)
						output += string.Format("{0}: {1}\n", transform.id, transform.description);
				}
				else
				{
					SortedDictionary<ImageTransform.Id, ImageTransform> transforms = new SortedDictionary<ImageTransform.Id, ImageTransform>();
					foreach(TransformedImage image in scope)
						foreach(ImageTransform transform in image.transforms)
							if(!transforms.ContainsKey(transform.id))
								transforms.Add(transform.id, transform);

					foreach(ImageTransform transform in transforms.Values)
						output += string.Format("{0}: {1}\n", transform.id, transform.description);
				}
				
				image_render_mutex.ReleaseMutex();
				return output;
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
			ActionManager.CreateAction("Create transform that only shows the image whose view angle most closly matches a view angle modified by dragging with the mouse", "sphere", this, "CreateTransformSphericalView");
			ActionManager.CreateAction("Create skip transform", "skip", this, "CreateTransformSkip");

			ActionManager.CreateAction("Clear selection", "none", delegate(object[] parameters) {
				image_render_mutex.WaitOne();
				selection.Clear();
				OnSelectionChanged(selection);
				image_render_mutex.ReleaseMutex();
				return null;
			});

			ActionManager.CreateAction<IEnumerable<TransformedImage>>("Spread out all dimensions", "spread", delegate(object[] parameters) {
				IEnumerable<TransformedImage> scope = (IEnumerable<TransformedImage>)parameters[0];

				if(Global.arguments != null)
				{
					/*WheelTransform transform = new WheelTransform();
					transform.SetArguments(Global.arguments);
					for(int i = 0; i < Global.arguments.Length; ++i)
						transform.SetIndex(i, i);
					OnTransformationAdded(transform, scope);*/

					string[] byExpr = new string[Global.arguments.Length];
					HashSet<int> byExpr_usedArgumentIndices = new HashSet<int>();
					for(int argidx = 0; argidx < Global.arguments.Length; ++argidx)
					{
						byExpr[argidx] = "2.0f * Array.IndexOf(Global.arguments[" + argidx.ToString() + "].values, image.values[image.globalargindices[" + argidx.ToString() + "]])";
						byExpr_usedArgumentIndices.Add(argidx);
					}

					CreateTransformStar(byExpr, byExpr_usedArgumentIndices, false, scope);
				}
				return null;
			});
			ActionManager.CreateAction<string[], HashSet<int>, bool, IEnumerable<TransformedImage>>("Animate the given argument", "animate", delegate(object[] parameters) {
				string[] byExpr = (string[])parameters[0];
				if(byExpr.Length != 2)
					return "usage: animate SCOPE by VARIABLE, FPS";
				HashSet<int> indices = (HashSet<int>)parameters[1];
				HashSet<int>.Enumerator indices_enum = indices.GetEnumerator();
				//bool isTemporal = (bool)parameters[2];
				IEnumerable<TransformedImage> scope = (IEnumerable<TransformedImage>)parameters[3];

				indices_enum.MoveNext();
				int index = indices_enum.Current;

				if(Global.arguments != null)
				{
					string warnings = "";
					ImageTransform transform = CompiledTransform.CompileSkipTransform(string.Format("{0} != (int)(Global.time * {1}) % {2}", byExpr[0], byExpr[1], Global.arguments[index].values.Length), true, ref warnings);
					if(ActionManager.activeCmdString != null)
						transform.description = ActionManager.activeCmdString;

					OnTransformationAdded(transform, images);
					return warnings;
				}
				return null;
			});

			ActionManager.CreateAction<IEnumerable<TransformedImage>>("Spread images randomly", "rspread", delegate(object[] parameters) {
				IEnumerable<TransformedImage> scope = (IEnumerable<TransformedImage>)parameters[0];

				if(Global.arguments != null)
				{
					int numimages = imageCloud.Count(scope);
					float ext = (float)Math.Sqrt(numimages), halfext = ext / 2.0f;

					Random rand = new Random();
					image_render_mutex.WaitOne();
					foreach(TransformedImage image in scope)
						image.pos += new Vector3((float)rand.NextDouble() * ext - halfext, (float)rand.NextDouble() * ext - halfext, (float)0.0f);
					image_render_mutex.ReleaseMutex();

					imageCloud.InvalidateOverallBounds();

					// Update selection (bounds may have changed due to added transform)
					CallSelectionChangedHandlers();
				}
				return null;
			});
			ActionManager.CreateAction<IEnumerable<TransformedImage>>("Spread images randomly in 3 dimensions", "rspread3d", delegate(object[] parameters) {
				IEnumerable<TransformedImage> scope = (IEnumerable<TransformedImage>)parameters[0];

				if(Global.arguments != null)
				{
					int numimages = imageCloud.Count(scope);
					float ext = (float)Math.Pow(numimages, 1.0 / 3.0), halfext = ext / 2.0f;

					ext *= 10.0f;
					halfext *= 10.0f;

					Random rand = new Random();
					image_render_mutex.WaitOne();
					foreach(TransformedImage image in scope)
						image.pos += new Vector3((float)rand.NextDouble() * ext - halfext, (float)rand.NextDouble() * ext - halfext, (float)rand.NextDouble() * ext - halfext);
					image_render_mutex.ReleaseMutex();

					imageCloud.InvalidateOverallBounds();

					// Update selection (bounds may have changed due to added transform)
					CallSelectionChangedHandlers();
				}
				return null;
			});

			ActionManager.CreateAction<IEnumerable<TransformedImage>>("Feature detection", "detect", delegate(object[] parameters) {
				IEnumerable<TransformedImage> scope = (IEnumerable<TransformedImage>)parameters[0];

				IEnumerator<TransformedImage> se = scope.GetEnumerator();
				if(!se.MoveNext())
					return null;

				string args = string.Format("\"{0}\" ", se.Current.FirstLayer.filename);
				foreach(TransformedImage image in scope)
					args += string.Format("\"{0}\" ", image.FirstLayer.filename);

				var process = new System.Diagnostics.Process
				{
					StartInfo = new System.Diagnostics.ProcessStartInfo
					{
						FileName = "./ImageSearchIntegrate",
						Arguments = args,
						UseShellExecute = false,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
					}
				};
				process.OutputDataReceived += (sender, e) => Global.cle.PrintOutput(e.Data);
				process.ErrorDataReceived += (sender, e) => Global.cle.PrintOutput(e.Data);

				process.Start();
				process.BeginOutputReadLine();
				process.BeginErrorReadLine();

				process.WaitForExit();


				System.IO.StreamReader sr = new StreamReader(new System.IO.FileStream("parsedFile.txt", FileMode.Open, FileAccess.Read));
				se = scope.GetEnumerator();
				while(sr.Peek() != -1)
				{
					// Get value and filename
					string readline = sr.ReadLine();
					float value;
					string strValue, filename;
					if(readline.StartsWith("found: "))
					{
						value = 0.0f;
						strValue = "found";
						filename = readline.Substring("found: ".Length);
					}
					else if(readline.StartsWith("possible: "))
					{
						value = 1.0f;
						strValue = "possible";
						filename = readline.Substring("possible: ".Length);
					}
					else if(readline.StartsWith("notFound: "))
					{
						value = 2.0f;
						strValue = "notFound";
						filename = readline.Substring("notFound: ".Length);
					}
					else
						continue;

					/*// Find image by filename //EDIT: Images are in order of scope! Don't find images manually!
					foreach(TransformedImage image in images)
						if(image.filename.Equals(filename))
						{
							GLTextureStream.ImageMetaData[] meta = new GLTextureStream.ImageMetaData[1];
							meta[0].name = "feature";
							meta[0].value = value;
							meta[0].strValue = strValue;
							texstream_ReadImageMetaData(image, meta);

							break;
						}*/
					if(!se.MoveNext() || !se.Current.FirstLayer.filename.Equals(filename))
						Global.cle.PrintOutput(string.Format("Error: Unexpected filename ({0})", filename));

					GLTextureStream.ImageMetaData[] meta = new GLTextureStream.ImageMetaData[1];
					meta[0].name = "feature";
					meta[0].value = value;
					meta[0].strValue = strValue;
					texstream_ReadImageMetaData(se.Current, meta);
				}
				sr.Close();

				return null;
			});

#if USE_STD_IO
			Global.cle.MethodCall += actMgr.Invoke;
			Global.cle_Invoker = this;
			Global.cle.Run();
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
		private string compiler_MethodCall(string method, object[] args, string isqlString)
		{
			string stdout;
			if(cle_Invoker != null)
			{
				IAsyncResult invokeResult = cle_Invoker.BeginInvoke(new ISQL.Compiler.MethodCallDelegate(actMgr.Invoke), new object[] { method, args, isqlString });
				invokeResult.AsyncWaitHandle.WaitOne();
				stdout = (string)cle_Invoker.EndInvoke(invokeResult);
			}
			else
				stdout = actMgr.Invoke(method, args, isqlString);
			return stdout;
		}

		private void AddArguments<T>(ref T[] arguments, T[] newargs, out int[] globalargindices) where T : Cinema.CinemaArgument
		{
			int oldnumargs = arguments.Length, newnumargs = arguments.Length;

			globalargindices = new int[arguments.Length + newargs.Length];
			Array.Resize(ref arguments, arguments.Length + newargs.Length);

			for(int i = 0; i < arguments.Length; ++i)
				globalargindices[i] = -1;
			for(int i = 0; i < newargs.Length; ++i)
			{
				int newargindex = -1;
				for(int j = 0; j < oldnumargs; ++j)
					if(arguments[j].label.Equals(newargs[i].label))
					{
						newargindex = j;
						break;
					}

				if(newargindex == -1)
				{
					arguments[newnumargs] = newargs[i];
					globalargindices[newnumargs++] = i;
				}
				else
					globalargindices[newargindex] = i;
			}
				
			if(newnumargs != arguments.Length)
			{
				Array.Resize(ref globalargindices, newnumargs);
				Array.Resize(ref arguments, newnumargs);
			}
		}

		private void FindFileOrDirectory(ref string path)
		{
			bool isdir;
			if(!(isdir = System.IO.Directory.Exists(path)) && !System.IO.File.Exists(path))
			{
				string relativePath = Global.cle.workingDirectory + Path.DirectorySeparatorChar + path;
				if(!(isdir = System.IO.Directory.Exists(relativePath)) && !System.IO.File.Exists(relativePath))
					throw new System.IO.FileNotFoundException(path);
				path = relativePath;
			}

			if(isdir && !path.EndsWith("/") && !path.EndsWith("\\"))
				path += Path.DirectorySeparatorChar;
		}

		private void PreLoad()
		{
			//if(images.Count != 0)
			//	UnloadDatabase(); // Only one database can be loaded at a time
		}
		private void PostLoad(IEnumerable<TransformedImage> newimages, Size imageSize, bool hasFloatImages)
		{
			/*// Create selection array and populate it with the default values
			selection = new IndexProductSelection(Global.arguments.Length, valuerange.Count, images);
			for(int i = 0; i < Global.arguments.Length; ++i)
			{
				if(Global.arguments[i].defaultValue != null)
					selection[i].Add(Array.IndexOf(Global.arguments[i].values, Global.arguments[i].defaultValue));
			}*/

			image_render_mutex.WaitOne();

			if(imageCloud != null)
			{
				//try {
					imageCloud.Load(newimages, valuerange, imageSize, hasFloatImages/*image_pixel_format != null && image_pixel_format.Equals("I24")*/, false/*depth_name_pattern != null*/);
				/*} catch(Exception ex) {
					MessageBox.Show(ex.Message, ex.TargetSite.ToString());
					throw ex;
				}*/

				/*// >>> Define heuristic to choose transformations based on argument names

				Dictionary<string, int> argnames = new Dictionary<string, int>();
				int idx = 0;
				foreach(Cinema.CinemaArgument argument in Global.arguments)
					argnames.Add(argument.name, idx++);

				if(argnames.ContainsKey("theta") && argnames.ContainsKey("phi"))
				{
					imageCloud.transforms.Add(new XYTransform(argnames["theta"], argnames["phi"], Global.arguments));
					//imageCloud.transforms.Add(new ThetaPhiTransform(argnames["theta"], argnames["phi"]));
					imageCloud.transforms.Add(new HighlightSelectionTransform(Color4.Azure));
					argnames.Remove("theta");
					argnames.Remove("phi");
				}
				//imageCloud.transforms.Add(new XYTransform(2, 1, Global.arguments));

				if(argnames.ContainsKey("time"))
				{
					imageCloud.transforms.Add(new XTransform(argnames["time"], Global.arguments));
					//imageCloud.transforms.Add(new AnimationTransform(argnames["time"], Global.arguments));
					argnames.Remove("time");
				}*/
			}

			imageCloud.SelectionChanged += CallSelectionChangedHandlers;
			imageCloud.SelectionMoved += CallSelectionMovedHandlers;
			imageCloud.TransformAdded += ImageCloud_TransformAdded;

			actMgr.Load(Global.arguments);
			actMgr.FrameCaptureFinished += actMgr_FrameCaptureFinished;

			browser.SelectionChanged += CallSelectionChangedHandlers;
			browser.SelectionMoved += CallSelectionMovedHandlers;

			image_render_mutex.ReleaseMutex();


			//ActionManager.Do(ClearTransformsAction);
			//CallSelectionChangedHandlers();

			/*IndexProductSelection foo = new IndexProductSelection(Global.arguments.Length, valuerange.Count, images);
			for(int i = 0; i < Global.arguments.Length; ++i)
				for(int j = 0; j < Global.arguments[i].values.Length; ++j)
					foo[i].Add(j);
			ActionManager.Do(OnSelectionChangedAction, new object[] { foo });

			ImageTransform bar = new ThetaPhiViewTransform();
			bar.SetArguments(Global.arguments); bar.SetIndex(0, 0); bar.SetIndex(1, 1);
			imageCloud.AddTransform(bar);
			ActionManager.Do(OnTransformationAddedAction, new object[] { bar });

			bar = new AnimationTransform();
			bar.SetArguments(Global.arguments); bar.SetIndex(0, 2);
			imageCloud.AddTransform(bar);
			ActionManager.Do(OnTransformationAddedAction, new object[] { bar });*/



			/*imageCloud.SelectAll();
			ImageTransform bar = new XYTransform();
			bar.SetArguments(Global.arguments); bar.SetIndex(0, 0); bar.SetIndex(1, 1);
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
				else if(filename.EndsWith(".isql", StringComparison.OrdinalIgnoreCase))
					ActionManager.mgr.RunScript(filename);
				else
					throw new FileLoadException(filename + " is not a recognized image or ISQL sript");
			}
			else
				throw new FileNotFoundException(filename + " not found");
		}

		private void LoadCinemaDatabase(string filename)
		{
			FindFileOrDirectory(ref filename);

			PreLoad();

			// Parse meta data from info.json
			Cinema.CinemaArgument[] newargs;
			Cinema.ParseCinemaDescriptor(filename, out newargs, out name_pattern, out depth_name_pattern, out image_pixel_format);
			bool useOnlyFloatImages = image_pixel_format != null && image_pixel_format.Equals("I24");
			bool useFloatImages = useOnlyFloatImages;

			Cinema.CinemaStore.Parameter[] newparams;
			Cinema.CinemaStore store = Cinema.CinemaStore.Load(filename + "image/info.json");
			newargs = store.arguments;
			newparams = store.parameters;

			image_render_mutex.WaitOne();
			int[] newargindices;
			AddArguments(ref Global.arguments, newargs, out newargindices);
			int[] newparamindices;
			AddArguments(ref Global.parameters, newparams, out newparamindices);
			image_render_mutex.ReleaseMutex();

			// >>> Load images and image meta

			//string imagepath;
			List<TransformedImage> newimages = null;
			#pragma warning disable 162
			if(false)
			{
				Thread inSituThread = new Thread(new ParameterizedThreadStart(SimulateInSituThread));
				inSituThread.Start((object)filename);
				return;
			}
			else
			{
				image_render_mutex.WaitOne();

				/*// Load images and image meta data by iterating over all argument combinations
				int[] argidx = new int[newargs.Length];
				newimages = new List<TransformedImage>();
				bool done;
				do {
					// Construct CinemaImage key and image file path from argidx[]
					float[] imagevalues = new float[newargs.Length];
					string[] imagestrvalues = new string[newargs.Length];
					imagepath = name_pattern;
					for(int i = 0; i < newargs.Length; ++i)
					{
						imagevalues[i] = newargs[i].values[argidx[i]];
						imagestrvalues[i] = newargs[i].strValues[argidx[i]];
						imagepath = imagepath.Replace("{" + newargs[i].name + "}", newargs[i].strValues[argidx[i]].ToString());
					}
					imagepath = filename + "image/" + imagepath;

					String depthpath = depth_name_pattern;
					if(depth_name_pattern != null)
					{
						// Construct depth image file path from argidx[]
						for(int i = 0; i < newargs.Length; ++i)
							depthpath = depthpath.Replace("{" + newargs[i].name + "}", newargs[i].strValues[argidx[i]].ToString());
						depthpath = filename + "image/" + depthpath;
					}

					// Load CinemaImage
					TransformedImage cimg = new TransformedImage();
					cimg.LocationChanged += imageCloud.InvalidateOverallBounds;
					cimg.values = imagevalues;
					cimg.strValues = imagestrvalues;
					cimg.args = newargs;
					cimg.globalargindices = newargindices;
					cimg.filename = imagepath;
					cimg.depth_filename = depthpath;
					Cinema.ParseImageDescriptor(imagepath.Substring(0, imagepath.Length - "png".Length) + "json", out cimg.meta, out cimg.invview);

					cimg.key = new int[argidx.Length]; Array.Copy(argidx, cimg.key, argidx.Length);
					newimages.Add(cimg);

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
					for(int i = 0; i < newargs.Length; ++i) {
						if(++argidx[i] == newargs[i].values.Length)
							argidx[i] = 0;
						else {
							done = false;
							break;
						}
					}
				} while(!done);*/

				// Load images and image meta data by iterating over all argument combinations
				newimages = new List<TransformedImage>();
				foreach(int[] argidx in store.iterateKeys())
				{
					float[] imagevalues = store.GetImageValues(argidx);
					string[] imagestrvalues = store.GetImageStrValues(argidx);
					Cinema.CinemaStore.Association[] dependentAssociations = store.GetDependentAssociations(argidx);

					/*string imagepath, depthpath, lumpath;
					bool isFloatImage;
					store.GetImageFilePath(argidx, dependentAssociations, out imagepath, out depthpath, out lumpath, out isFloatImage);
					imagepath = filename + imagepath;
					if(depthpath != null)
						depthpath = filename + depthpath;
					if(lumpath != null)
						lumpath = filename + lumpath;*/

					// Load CinemaImage
					TransformedImage cimg = new TransformedImage();
					cimg.LocationChanged += imageCloud.InvalidateOverallBounds;
					cimg.values = imagevalues;
					cimg.strValues = imagestrvalues;
					cimg.args = newargs;
					cimg.globalargindices = newargindices;

					foreach(Cinema.CinemaStore.LayerDescription layerdesc in store.iterateLayers(argidx, dependentAssociations))
					{
						TransformedImage.ImageLayer layer = new TransformedImage.ImageLayer(
							cimg,
							filename + layerdesc.imagepath,
							layerdesc.imageDepthPath == null ? null : filename + layerdesc.imageDepthPath,
							layerdesc.imageLumPath == null ? null : filename + layerdesc.imageLumPath,
							useOnlyFloatImages || layerdesc.isFloatImage
						);
						//layer.filename = filename + layerdesc.imagepath;
						//layer.depth_filename = layerdesc.imageDepthPath == null ? null : filename + layerdesc.imageDepthPath;
						//layer.lum_filename = layerdesc.imageLumPath == null ? null : filename + layerdesc.imageLumPath;
						//layer.isFloatImage = useOnlyFloatImages || layerdesc.isFloatImage;
						useFloatImages |= layer.isFloatImage;
						layer.key = layerdesc.paramidx;
						layer.keymask = layerdesc.paramvalid;
						layer.parameters = newparams;
						layer.globalparamindices = newparamindices;
						cimg.AddLayer(layer);
					}
					if(cimg.activelayers.Count + cimg.inactivelayers.Count == 0)
						throw new Exception();

					//Cinema.ParseImageDescriptor(imagepath.Substring(0, imagepath.Length - "png".Length) + "json", out cimg.meta, out cimg.invview);
					cimg.invview = Matrix4.Identity;

					cimg.key = argidx;
					newimages.Add(cimg);

					//for(int i = 0; i < newargs.Length; ++i)
					//{
					//	List<TransformedImage> valueimages;
					//	newargs[i].images.TryGetValue(imagevalues[i], out valueimages);
					//	if(valueimages == null)
					//		valueimages = new List<TransformedImage>();
					//	valueimages.Add(cimg);
					//}

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

				#if !DISABLE_DATAVIZ
				if(dataviz != null)
				dataviz.ImagesAdded();
				#endif
			}
			#pragma warning restore 162

			//image_render_mutex.WaitOne();
			images.AddRange(newimages);
			foreach(TransformedImage newimage in newimages)
				visible.Add(newimage);
			image_render_mutex.ReleaseMutex();

			if(newimages.Count == 0)
				return;

			// Get image size
			Size imageSize = new Size(256, 256);
			if(File.Exists(newimages[0].FirstLayer.filename))
			{
				Image img = Image.FromFile(newimages[0].FirstLayer.filename);
				imageSize = new Size(img.Width, img.Height);
				img.Dispose();
			}

			PostLoad(newimages, imageSize, useFloatImages);

			string startupscriptfilename = filename + Path.DirectorySeparatorChar + "startup.isql";
			if(File.Exists(startupscriptfilename))
			{
				Global.cle.PrintOutput("Executing startup script (startup.isql)");
				actMgr.RunScript(startupscriptfilename);
			}
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
					filenames.Add(/*dirname +*/ filename);
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
			Cinema.CinemaArgument[] newargs;
			if(name_pattern != null && name_pattern != "")
			{
				System.Text.RegularExpressions.MatchCollection matches = System.Text.RegularExpressions.Regex.Matches(name_pattern, "{\\w*}");
				newargs = new Cinema.CinemaArgument[matches.Count];
				name_pattern_splitters = new string[matches.Count + 1];
				strValueIndices = new Dictionary<string, int>[matches.Count];
				int last_match_end = 0, i = 0;
				foreach(System.Text.RegularExpressions.Match match in matches)
				{
					string argumentStr = match.Value.Substring(1, match.Value.Length - 2);
					name_pattern_splitters[i] = name_pattern.Substring(last_match_end, match.Index - last_match_end);
					last_match_end = match.Index + match.Length;

					Cinema.CinemaArgument carg = newargs[i] = new Cinema.CinemaArgument();
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
				newargs = new Cinema.CinemaArgument[0];
				this.name_pattern = "";
			}
			depth_name_pattern = null;
			image_pixel_format = "";//"I24";

			image_render_mutex.WaitOne();
			int[] newargindices;
			AddArguments(ref Global.arguments, newargs, out newargindices);
			image_render_mutex.ReleaseMutex();

			// >>> Load images and image meta

			// Load images and image meta data by iterating over all argument combinations
			List<TransformedImage> newimages = new List<TransformedImage>();
			foreach(string imagepath in filenames)
			{
				string lpath = imagepath.ToLower();
				if(!lpath.EndsWith(".png") && !lpath.EndsWith(".jpg"))
					continue;

				// >>> Load CinemaImage

				// Get values through name_pattern
				string[] strValues = new string[newargs.Length];
				float[] values = new float[newargs.Length];
				int[] key = new int[newargs.Length];
				if(newargs.Length > 0)
				{
					// Make sure name_pattern starts with name_pattern_splitters[0]
					if(!imagepath.StartsWith(name_pattern_splitters[0]))
					{
						System.Console.Error.WriteLine("image path " + imagepath + " does not match pattern " + name_pattern);
						continue; // Skip image
					}

					bool err = false;
					int valueend = 0;
					for(int i = 0; i < newargs.Length; ++i)
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

					for(int i = 0; i < newargs.Length; ++i)
					{
						int strValueindex;
						if(!strValueIndices[i].TryGetValue(strValues[i], out strValueindex))
						{
							strValueIndices[i].Add(strValues[i], strValueindex = strValueIndices[i].Count);

							for(int j = i + 1; j < newargs.Length; ++j) //DELETE
								strValueIndices[j].Clear(); //DELETE
						}
						key[i] = strValueindex;
						values[i] = (float)key[i];
					}
				}

				TransformedImage cimg = new TransformedImage();
				cimg.LocationChanged += imageCloud.InvalidateOverallBounds;

				cimg.values = values;
				cimg.strValues = strValues;
				cimg.args = newargs;
				cimg.globalargindices = newargindices;

				TransformedImage.ImageLayer layer = new TransformedImage.ImageLayer(cimg, imagepath);
				//layer.filename = imagepath;
				//layer.depth_filename = null;
				//layer.lum_filename = null;
				//layer.isFloatImage = false;
				cimg.AddLayer(layer);

				Cinema.ParseImageDescriptor(imagepath.Substring(0, imagepath.Length - "png".Length) + "json", out cimg.meta, out cimg.invview);

				cimg.key = key;
				newimages.Add(cimg);

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

			if(newimages.Count == 0)
				return;

			image_render_mutex.WaitOne();

			images.AddRange(newimages);
			foreach(TransformedImage newimage in newimages)
				visible.Add(newimage);

			// >>> Update argument values
			for(int i = 0; i < newargs.Length; ++i)
			{
				Cinema.CinemaArgument arg = newargs[i];
				arg.strValues = new string[strValueIndices[i].Count];
				arg.values = new float[strValueIndices[i].Count];
				foreach(KeyValuePair<string, int> pair in strValueIndices[i])
				{
					arg.strValues[pair.Value] = pair.Key;
					if(!float.TryParse(pair.Key, out arg.values[pair.Value]))
						arg.values[pair.Value] = (float)pair.Value;
				}
			}

			image_render_mutex.ReleaseMutex();

			#if !DISABLE_DATAVIZ
			if(dataviz != null)
			dataviz.ImagesAdded();
			#endif

			// Get image size
			Size imageSize = new Size(256, 256);
			if(File.Exists(newimages[0].FirstLayer.filename))
			{
				Image img = Image.FromFile(newimages[0].FirstLayer.filename);
				imageSize = new Size(img.Width, img.Height);
				img.Dispose();
			}
			PostLoad(newimages, imageSize, image_pixel_format == "I24");
		}

		private void SimulateInSituThread(object parameters)
		{
			string filename = (string)parameters;

			// Create a list of all available indices
			List<int[]> indexlist = new List<int[]>();
			int[] argidx = new int[Global.arguments.Length];
			bool done;
			do {
				// Add argidx to indexqueue
				int[] argidx_copy = new int[Global.arguments.Length];
				Array.Copy(argidx, argidx_copy, argidx.Length);
				indexlist.Add(argidx_copy);

				// Get next argument combination -> argidx[]
				done = true;
				for(int i = 0; i < Global.arguments.Length; ++i) {
					if(++argidx[i] == Global.arguments[i].values.Length)
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
				float[] imagevalues = new float[Global.arguments.Length];
				string[] imagestrvalues = new string[Global.arguments.Length];
				String imagepath = name_pattern;
				for(int i = 0; i < Global.arguments.Length; ++i)
				{
					imagevalues[i] = Global.arguments[i].values[argidx[i]];
					imagestrvalues[i] = Global.arguments[i].strValues[argidx[i]];
					imagepath = imagepath.Replace("{" + Global.arguments[i].name + "}", Global.arguments[i].strValues[argidx[i]].ToString());
				}
				imagepath = filename + "image/" + imagepath;

				// Load CinemaImage
				TransformedImage cimg = new TransformedImage();
				cimg.LocationChanged += imageCloud.InvalidateOverallBounds;
				cimg.values = imagevalues;
				cimg.strValues = imagestrvalues;
				cimg.args = Global.arguments;

				TransformedImage.ImageLayer layer = new TransformedImage.ImageLayer(cimg, imagepath);
				//layer.filename = imagepath;
				cimg.AddLayer(layer);

				/*for(int i = 0; i < Global.arguments.Length; ++i)
				{
					List<TransformedImage> valueimages;
					Global.arguments[i].images.TryGetValue(imagevalues[i], out valueimages);
					if(valueimages == null)
						valueimages = new List<TransformedImage>();
					valueimages.Add(cimg);
				}*/

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

			Global.arguments = new Cinema.CinemaArgument[0];
			selection.Clear();
			visible.Clear();
			images.Clear();

			image_render_mutex.ReleaseMutex();

			OnSelectionChanged(selection);
			ClearTransforms();
		}

		private bool texstream_ReadImageMetaData(TransformedImage image, GLTextureStream.ImageMetaData[] meta)
		{
			if(!image_render_mutex.WaitOne(500)) // Without this timeout image_render_mutex and addImageMutex can deadlock when this function is called during database unload
				return false;

			Cinema.CinemaArgument[] newargs = new Cinema.CinemaArgument[image.args.Length + meta.Length];
			int i;
			for(i = 0; i < image.args.Length; ++i)
				newargs[i] = image.args[i];
			foreach(GLTextureStream.ImageMetaData m in meta)
			{
				Cinema.CinemaArgument newarg = newargs[i++] = new Cinema.CinemaArgument();
				newarg.name = newarg.label = m.name;
				newarg.values = new float[0];
				newarg.strValues = new string[0];
				newarg.defaultValue = m.value;
			}

			int[] globalargindices;
			AddArguments(ref Global.arguments, newargs, out globalargindices);

			int oldargslen = image.args.Length;
			Array.Resize(ref image.args, image.args.Length + meta.Length);
			Array.Resize(ref image.values, image.values.Length + meta.Length);
			Array.Resize(ref image.strValues, image.strValues.Length + meta.Length);

			i = 0;
			foreach(Cinema.CinemaArgument arg in Global.arguments)
			{
				if(globalargindices[i] >= oldargslen)
				{
					image.args[globalargindices[i]] = newargs[globalargindices[i]];
					float value = image.values[globalargindices[i]] = meta[globalargindices[i] - oldargslen].value;
					string strValue = image.strValues[globalargindices[i]] = meta[globalargindices[i] - oldargslen].strValue;
					if(Array.IndexOf<float>(arg.values, value) == -1)
					{
						/*Array.Resize(ref arg.values, arg.values.Length + 1);
						Array.Resize(ref arg.strValues, arg.strValues.Length + 1);
						arg.values[arg.values.Length - 1] = value;
						arg.strValues[arg.strValues.Length - 1] = strValue;*/
						arg.AddValue(value, strValue);
					}
				}
				++i;
			}

			image.globalargindices = globalargindices;
			image.InvalidateLocation();

			//try {
				imageCloud.Load(new TransformedImage[0], valuerange, new Size(100, 100), image_pixel_format != null && image_pixel_format.Equals("I24"), depth_name_pattern != null);
			//} catch(Exception ex) {
			//	MessageBox.Show(ex.Message, ex.TargetSite.ToString());
			//	throw ex;
			//}

			image_render_mutex.ReleaseMutex();

			return true;
		}

		private void Exit(string isqlCommand)
		{
#if USE_STD_IO
			Global.cle.Close();
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
			// >>> Apply UI logic manually since anchors and docking aren't working on Mono Forms for OsX

			int w = this.ClientSize.Width, h = this.ClientSize.Height;
#if DISABLE_DATAVIZ
			SetControlSize(glImageCloud, 0, 0, w, !consoleVisible || ctrlConsole == null ? h : h - 256);
			if(ctrlConsole != null)
			{
				ctrlConsole.Visible = consoleVisible;
				if (consoleVisible)
					SetControlSize(ctrlConsole, 0, h - 256, w, 256);
			}
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
			/*// Invalidate all controls
			foreach(Control control in this.Controls)
				control.Hide();
			Application.DoEvents();
			System.Threading.Thread.Sleep(100);
			foreach(Control control in this.Controls)
				control.Show();*/
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

			/*int[] imagekey = new int[Global.arguments.Length];
			for(int i = 0; i < Global.arguments.Length; ++i)
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
		private string TransformCompiled(ImageTransform transform, IEnumerable<TransformedImage> images, string isqlString)
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
			imageCloud.InvalidateOverallBounds();
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

		public static string GetSkipImageExpr(HashSet<int> byExpr_usedArgumentIndices)
		{
			string skipImageExpr = "";
			int highestIdx = -1;
			foreach(int argIdx in byExpr_usedArgumentIndices)
			{
				highestIdx = Math.Max(highestIdx, argIdx);
				skipImageExpr += " || image.globalargindices[" + argIdx + "] == -1";
			}
			if(highestIdx == -1)
				skipImageExpr = "false";
			else
				skipImageExpr = "image.globalargindices.Length <= " + highestIdx + skipImageExpr;
			return skipImageExpr;
		}
		private string CreateTransformX(string[] byExpr, HashSet<int> byExpr_usedArgumentIndices, bool byExpr_isTemporal, IEnumerable<TransformedImage> images)
		{
			if(byExpr.Length != 1)
				return "usage: x SCOPE by X";
			
			string warnings = "";
			ImageTransform transform = CompiledTransform.CompileTranslationTransform(byExpr[0], "0.0f", "0.0f", GetSkipImageExpr(byExpr_usedArgumentIndices), byExpr_isTemporal, ref warnings);
			if( ActionManager.activeCmdString != null)
				transform.description = ActionManager.activeCmdString;

			OnTransformationAdded(transform, images);
			return warnings;
		}
		private string CreateTransformY(string[] byExpr, HashSet<int> byExpr_usedArgumentIndices, bool byExpr_isTemporal, IEnumerable<TransformedImage> images)
		{
			if(byExpr.Length != 1)
				return "usage: y SCOPE by Y";

			string warnings = "";
			ImageTransform transform = CompiledTransform.CompileTranslationTransform("0.0f", byExpr[0], "0.0f", GetSkipImageExpr(byExpr_usedArgumentIndices), byExpr_isTemporal, ref warnings);
			if( ActionManager.activeCmdString != null)
				transform.description = ActionManager.activeCmdString;

			OnTransformationAdded(transform, images);
			return warnings;
		}
		private string CreateTransformZ(string[] byExpr, HashSet<int> byExpr_usedArgumentIndices, bool byExpr_isTemporal, IEnumerable<TransformedImage> images)
		{
			if(byExpr.Length != 1)
				return "usage: z SCOPE by Z";

			string warnings = "";
			ImageTransform transform = CompiledTransform.CompileTranslationTransform("0.0f", "0.0f", byExpr[0], GetSkipImageExpr(byExpr_usedArgumentIndices), byExpr_isTemporal, ref warnings);
			if( ActionManager.activeCmdString != null)
				transform.description = ActionManager.activeCmdString;

			OnTransformationAdded(transform, images);
			return warnings;
		}
		private string CreateTransformThetaPhi(string[] byExpr, HashSet<int> byExpr_usedArgumentIndices, bool byExpr_isTemporal, IEnumerable<TransformedImage> images)
		{
			if(byExpr.Length != 3)
				return "usage: thetaPhi SCOPE by THETA, PHI, RADIUS";

			string warnings = "";
			ImageTransform transform = CompiledTransform.CompilePolarTransform(byExpr[0], byExpr[1], byExpr[2], byExpr_isTemporal, ref warnings);
			if( ActionManager.activeCmdString != null)
				transform.description = ActionManager.activeCmdString;

			OnTransformationAdded(transform, images);
			return warnings;
		}
		private string CreateTransformStar(string[] byExpr, HashSet<int> byExpr_usedArgumentIndices, bool byExpr_isTemporal, IEnumerable<TransformedImage> images)
		{
			string warnings = "";
			ImageTransform transform = CompiledTransform.CompileStarTransform(byExpr, GetSkipImageExpr(byExpr_usedArgumentIndices), byExpr_isTemporal, ref warnings);
			if( ActionManager.activeCmdString != null)
				transform.description = ActionManager.activeCmdString;

			OnTransformationAdded(transform, images);
			return warnings;
		}
		private string CreateTransformLookAt(string[] byExpr, HashSet<int> byExpr_usedArgumentIndices, bool byExpr_isTemporal, IEnumerable<TransformedImage> images)
		{
			if(byExpr.Length != 2)
				return "usage: look SCOPE by THETA, PHI";
			
			string indices = null;
			foreach(int index in byExpr_usedArgumentIndices)
				if(indices == null)
					indices = index.ToString();
				else
					indices += ", " + index.ToString();

			string warnings = "";
			ImageTransform transform = CompiledTransform.CreateTransformLookAt(byExpr[0], byExpr[1], indices, byExpr_isTemporal, ref warnings);
			if( ActionManager.activeCmdString != null)
				transform.description = ActionManager.activeCmdString;

			transform.OnArgumentsChanged();
			OnTransformationAdded(transform, images);
			return warnings;
		}
		private string CreateTransformSphericalView(string[] byExpr, HashSet<int> byExpr_usedArgumentIndices, bool byExpr_isTemporal, IEnumerable<TransformedImage> images)
		{
			if(byExpr.Length != 2)
				return "usage: sphere SCOPE by THETA, PHI";

			string indices = null;
			foreach(int index in byExpr_usedArgumentIndices)
				if(indices == null)
					indices = index.ToString();
				else
					indices += ", " + index.ToString();

			string warnings = "";
			ImageTransform transform = CompiledTransform.CreateTransformSphericalView(byExpr[0], byExpr[1], indices, byExpr_isTemporal, ref warnings);
			if( ActionManager.activeCmdString != null)
				transform.description = ActionManager.activeCmdString;

			transform.OnArgumentsChanged();
			OnTransformationAdded(transform, images);
			return warnings;
		}
		private string CreateTransformSkip(string[] byExpr, HashSet<int> byExpr_usedArgumentIndices, bool byExpr_isTemporal, IEnumerable<TransformedImage> images)
		{
			if(byExpr.Length != 1)
				return "usage: skip SCOPE by CONDITION";
			
			string warnings = "";
			ImageTransform transform = CompiledTransform.CompileSkipTransform(byExpr[0], byExpr_isTemporal, ref warnings);
			if( ActionManager.activeCmdString != null)
				transform.description = ActionManager.activeCmdString;

			OnTransformationAdded(transform, images);
			return warnings;
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
			string openglVersionStr = GL.GetString(StringName.Version);
			int idx = openglVersionStr.IndexOf('.'); // idx = first occurence of '.'
			idx = openglVersionStr.IndexOf('.', ++idx); // idx = second occurence of '.'
			if (idx != -1)
				openglVersionStr = openglVersionStr.Substring(0, idx); // Turn "X.Y.Z" into "X.Y"
			Global.OPENGL_VERSION = decimal.Parse(openglVersionStr.Split(' ')[0]);

			//GL.ClearColor(0.8f, 0.8f, 0.8f, 1.0f);
			//GL.ClearColor(0.0f, 0.1f, 0.3f, 1.0f); // Set inside image cload!
			//GL.Viewport(glImageCloud.Height > glImageCloud.Width ? new Rectangle(0, (glImageCloud.Height - glImageCloud.Width) / 2, glImageCloud.Width, glImageCloud.Width) : new Rectangle((glImageCloud.Width - glImageCloud.Height) / 2, 0, glImageCloud.Height, glImageCloud.Height));
			GL.Viewport(glImageCloud.Size);
			GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
			GL.Enable(EnableCap.Blend);
			GL.DepthFunc(DepthFunction.Less); // Should be 'Less', not 'Lequal', to make sure fragment counting results in as many occluded pixels as possible to accurately determine which images are unoccluded
			//GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);

			GL.ClampColor(ClampColorTarget.ClampReadColor, ClampColorMode.False);
			GL.ClampColor(ClampColorTarget.ClampVertexColor, ClampColorMode.False);
			GL.ClampColor(ClampColorTarget.ClampFragmentColor, ClampColorMode.False);

			Common.CreateCommonMeshes();
			Common.CreateCommonFonts();
			try {
				Common.CreateCommonShaders();
			} catch(Exception ex) {
				MessageBox.Show(ex.Message, "Error creating shaders");
			}

			imageCloud.Init(glImageCloud, texstream_ReadImageMetaData);
			imageCloud.OnSizeChanged(glImageCloud.Size);

			glImageCloud.DragEnter += glImageCloud_DragEnter;
			glImageCloud.DragDrop += glImageCloud_DragDrop;

			glImageCloud.MouseDown += glImageCloud_MouseDown;
			glImageCloud.MouseUp += glImageCloud_MouseUp;
			glImageCloud.MouseMove += glImageCloud_MouseMove;
			glImageCloud.MouseWheel += glImageCloud_MouseWheel;
			glImageCloud.DoubleClick += glImageCloud_DoubleClick;
			//glImageCloud.KeyDown += glImageCloud_KeyDown;

			ActionManager.Do(AppStartAction);

			//if(cmdline.Length == 1)
			//	LoadCinemaDatabase(cmdline[0]);
			LoadFromCommandLine(cmdline);
//LoadDatabaseFromImages(new string[] {"/Users/sklaassen/Desktop/work/db/cinema_debug/image/1.000000/-30/-30.png"});
//if(cmdline.Length == 1)
//	LoadDatabaseFromDirectory(cmdline[0], false);

			browser.Init(this, imageCloud);
			browser.OnLoad();

			// Start timer
			timer = new System.Diagnostics.Stopwatch();
			timer.Start();

			float averageDt = 0.0f;
			int frameCounter = 0;
			Size glImageCloud_Size = Size.Empty;
			while(!form_closing)
			{
				if (image_render_mutex.WaitOne(1) == false)
					continue;

				if(glImageCloud.Size != glImageCloud_Size)
				{
					//GL.Viewport(glImageCloud.Height > glImageCloud.Width ? new Rectangle(0, (glImageCloud.Height - glImageCloud.Width) / 2, glImageCloud.Width, glImageCloud.Width) : new Rectangle((glImageCloud.Width - glImageCloud.Height) / 2, 0, glImageCloud.Height, glImageCloud.Height));
					GL.Viewport(glImageCloud_Size = glImageCloud.Size);
					imageCloud.OnSizeChanged(glImageCloud.Size);
				}

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

				actMgr.PostRender(glImageCloud, Global.cle as ScriptingConsole);

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
		protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
		{
			if (glImageCloud.Focused)
				glImageCloud_KeyDown(glImageCloud, new KeyEventArgs(keyData));
			return base.ProcessCmdKey(ref msg, keyData);
		}

		private void actMgr_FrameCaptureFinished()
		{
			ImageCloud.Status("Frame capture finished");
		}
	}
}