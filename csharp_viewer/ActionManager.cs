using System;
using System.Collections.Generic;
using System.Reflection;

using System.Drawing;
using OpenTK.Graphics.OpenGL;

namespace csharp_viewer
{
	public class ActionManager
	{
		public static ActionManager mgr = null;

		private struct PerformedAction
		{
			public readonly double time;
			public readonly Action action;
			public readonly object[] parameters;

			public PerformedAction(double time, Action action, object[] parameters)
			{
				this.time = time;
				this.action = action;
				this.parameters = parameters;
			}
		}

		private Cinema.CinemaArgument[] arguments;
		private double time = 0.0;
		private LinkedList<PerformedAction> actions = new LinkedList<PerformedAction>();
		private Dictionary<string, Action> registered_actions = new Dictionary<string, Action>();

		private bool playing = false, playing_captureframes;
		private double playback_speed, playback_invfps;
		private int playback_framecounter;
		private LinkedList<PerformedAction>.Enumerator playback_next_action;

		public ActionManager()
		{
			mgr = this;
		}

		public void Load(Cinema.CinemaArgument[] arguments)
		{
			this.arguments = arguments;
		}
		public void Unload()
		{
			arguments = null;
		}

		public void Update(ref float dt)
		{
			if(playing)
			{
				if(playback_invfps != 0.0)
				{
					time += playback_invfps;
					dt = (float)playback_invfps;
				}
				else
				{
					time += playback_speed * (double)dt;
					dt *= (float)playback_speed;
				}

				++playback_framecounter;

				if(time >= playback_next_action.Current.time)
				{
					double lastActionTime = playback_next_action.Current.time;
					do
					{
						try {
							playback_next_action.Current.action.Do(playback_next_action.Current.parameters);
						} catch(Exception ex) {
							playing = false;
							if(ex.InnerException != null)
								System.Windows.Forms.MessageBox.Show(ex.InnerException.ToString(), ex.InnerException.TargetSite.ToString());
							else
								System.Windows.Forms.MessageBox.Show(ex.ToString(), ex.TargetSite.ToString());
							return;
						}
						if(!playback_next_action.MoveNext())
						{
							playing = false;
//System.Windows.Forms.Application.Exit();
							return;
						}
					} while(playback_next_action.Current.time == lastActionTime);
				}
			}
			else
				time += (double)dt;
		}

		public void PostRender(OpenTK.GLControl gl)
		{
			if(playing && playing_captureframes)
			{
				Bitmap bmp = new Bitmap(gl.ClientSize.Width, gl.ClientSize.Height);
				System.Drawing.Imaging.BitmapData data = bmp.LockBits(new Rectangle(Point.Empty, gl.ClientSize), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
				GL.ReadPixels(0, 0, gl.ClientSize.Width, gl.ClientSize.Height, PixelFormat.Bgr, PixelType.UnsignedByte, data.Scan0);
				bmp.UnlockBits(data);

				bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);

				string framestr = playback_framecounter.ToString();
				for(int i = 0, numzeros = 5 - framestr.Length; i < numzeros; ++i)
					framestr = '0' + framestr;
				bmp.Save("frames/frame" + framestr + ".png");
				bmp.Dispose();
			}
		}

		private static MethodInfo GetMethod(Type clstype, string method)
		{
			MethodInfo mi;
			if((mi = clstype.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)) != null)
				return mi;
			return clstype.GetMethod(method, BindingFlags.Public | BindingFlags.Instance);
		}

		public static Action CreateAction(string desc, object instance, string method)
		{
			Type clstype = instance.GetType();
			MethodInfo methodinfo = GetMethod(clstype, method);
			if(methodinfo == null)
				throw new Exception("Method not found: " + desc);

			ParameterInfo[] paraminfo = methodinfo.GetParameters();
			Type[] paramtypes = new Type[paraminfo.Length];
			for(int i = 0; i < paraminfo.Length; ++i)
				paramtypes[i] = paraminfo[i].ParameterType;

			Action action = new InvokeAction(method, desc, paramtypes, instance, methodinfo, null);
			if(mgr != null)
			{
				if(mgr.registered_actions.ContainsKey(action.name))
					mgr.registered_actions.Remove(action.name);
				mgr.registered_actions.Add(action.name, action);
			}
			return action;
		}
		public static Action CreateAction(string desc, object instance, string _do, string _undo)
		{
			Type clstype = instance.GetType();
			MethodInfo _do_info = GetMethod(clstype, _do);
			if(_do_info == null)
				throw new Exception("Method not found: " + desc);
			MethodInfo _undo_info = GetMethod(clstype, _undo);
			if(_undo_info == null)
				throw new Exception("Method not found: " + desc);

			ParameterInfo[] paraminfo = _do_info.GetParameters();
			Type[] paramtypes = new Type[paraminfo.Length];
			for(int i = 0; i < paraminfo.Length; ++i)
				paramtypes[i] = paraminfo[i].ParameterType;

			Action action = new InvokeAction(_do, desc, paramtypes, instance, _do_info, _undo_info);
			if(mgr != null)
			{
				if(mgr.registered_actions.ContainsKey(action.name))
					mgr.registered_actions.Remove(action.name);
				mgr.registered_actions.Add(action.name, action);
			}
			return action;
		}

		public static Action CreateAction(string desc, string name, object instance, string method)
		{
			Type clstype = instance.GetType();
			MethodInfo methodinfo = GetMethod(clstype, method);
			if(methodinfo == null)
				throw new Exception("Method not found: " + desc);

			ParameterInfo[] paraminfo = methodinfo.GetParameters();
			Type[] paramtypes = new Type[paraminfo.Length];
			for(int i = 0; i < paraminfo.Length; ++i)
				paramtypes[i] = paraminfo[i].ParameterType;

			Action action = new InvokeAction(name, desc, paramtypes, instance, methodinfo, null);
			if(mgr != null)
			{
				if(mgr.registered_actions.ContainsKey(action.name))
					mgr.registered_actions.Remove(action.name);
				mgr.registered_actions.Add(action.name, action);
			}
			return action;
		}

		public static Action CreateAction(string desc, string method, Action.CallbackActionDelegate func)
		{
			Action action = new CallbackAction(method, desc, new Type[] {}, func);
			if(mgr != null)
			{
				if(mgr.registered_actions.ContainsKey(action.name))
					mgr.registered_actions.Remove(action.name);
				mgr.registered_actions.Add(action.name, action);
			}
			return action;
		}
		public static Action CreateAction<T>(string desc, string method, Action.CallbackActionDelegate func)
		{
			Action action = new CallbackAction(method, desc, new Type[] {typeof(T)}, func);
			if(mgr != null)
			{
				if(mgr.registered_actions.ContainsKey(action.name))
					mgr.registered_actions.Remove(action.name);
				mgr.registered_actions.Add(action.name, action);
			}
			return action;
		}
		public static Action CreateAction<T1, T2>(string desc, string method, Action.CallbackActionDelegate func)
		{
			Action action = new CallbackAction(method, desc, new Type[] {typeof(T1), typeof(T2)}, func);
			if(mgr != null)
			{
				if(mgr.registered_actions.ContainsKey(action.name))
					mgr.registered_actions.Remove(action.name);
				mgr.registered_actions.Add(action.name, action);
			}
			return action;
		}

		public static void Do(Action action, object[] parameters = null)
		{
			if(!mgr.playing)
				mgr.actions.AddLast(new PerformedAction(mgr.time, action, parameters));
			action.Do(parameters);
		}

		/*public void SaveAs(string filename)
		{
			
		}*/

		public void Clear()
		{
			actions.Clear();
			time = 0.0;
			playing = false;
		}

		public void Play(double playback_speed = 1.0)
		{
			playback_next_action = actions.GetEnumerator();
			if(!playback_next_action.MoveNext())
				return;
			time = 0.0;
			this.playback_speed = playback_speed;
			playback_invfps = 0.0;
			playback_framecounter = 0;
			playing_captureframes = false;
			playing = true;
		}

		public void CaptureFrames(double fps = 20.0)
		{
			playback_next_action = actions.GetEnumerator();
			if(!playback_next_action.MoveNext())
				return;
			time = 0.0;
			playback_invfps = 1.0 / fps;
			playback_framecounter = 0;
			playing_captureframes = true;
			playing = true;
		}

		private static int String_ReverseReplaceEx(ref string str, string[] oldStr, string newStr)
		{
			for(int c = str.Length - 1; c >= 0; --c)
				for(int i = 0; i < oldStr.Length; ++i)
					if(str.Length >= c + oldStr[i].Length && oldStr[i].Equals(str.Substring(c, oldStr[i].Length)))
					{
						str = str.Substring(0, c) + newStr + str.Substring(c + oldStr[i].Length);
						return i;
					}
			return -1;
		}

		/*public void Invoke(string action_name)
		{
			Action action;
			if(registered_actions.TryGetValue(action_name, out action))
				action.Do();
		}
		public void Invoke(string action_name, object[] args)
		{
			Action action;
			if(registered_actions.TryGetValue(action_name, out action))
				action.Do(args);
		}*/
		public string Invoke(string action_name, object[] args)
		{
			string stdout = "";

			if(action_name == "help")
			{
				foreach(KeyValuePair<string, Action> registered_action in registered_actions)
				{
					if(registered_action.Key.Contains("%a"))
					{
						if(arguments != null)
							foreach(Cinema.CinemaArgument argument in arguments)
								stdout += registered_action.Key.Replace("%a", argument.label) + "() -> " + registered_action.Value.desc.Replace("%a", argument.label) + '\n';
					}
					else
						stdout += registered_action.Key + "() -> " + registered_action.Value.desc + '\n';
				}
				return stdout;
			}

			Action action;
			if(!registered_actions.TryGetValue(action_name, out action))
			{
				if(arguments == null)
				{
					stdout += "Command not found " + action_name;
					return stdout;
				}

				/*string new_action_name = null;
				foreach(Cinema.CinemaArgument arg in arguments)
					if(action_name.Contains(arg.label))
					{
						new_action_name = action_name.Replace(arg.label, "%a");

						// Add arg to args as first argument
						if(args == null)
							args = new object[] { arg };
						else
						{
							object[] newargs = new object[args.Length + 1];
							Array.Copy(args, 0, newargs, 1, args.Length);
							newargs[0] = arg;
							args = newargs;
						}
						break;
					}

				if(new_action_name == null || !registered_actions.TryGetValue(new_action_name, out action))
				{
					stdout += "Command not found " + action_name;
return stdout;
				}*/

				/*string[] labels = new string[arguments.Length];
				for(int i = 0; i < arguments.Length; ++i)
					labels[i] = "$" + arguments[i].label;

				string new_action_name = action_name;
				int index;
				while((index = String_ReverseReplaceEx(ref new_action_name, labels, "%a")) != -1)
				{
					// Add index to args as first argument
					if(args == null)
						args = new object[] { index };
					else
					{
						object[] newargs = new object[args.Length + 1];
						Array.Copy(args, 0, newargs, 1, args.Length);
						newargs[0] = index;
						args = newargs;
					}
				}

				if(!registered_actions.TryGetValue(new_action_name, out action))
				{
					stdout += "Command not found " + action_name;
					return stdout;
				}*/

				string new_action_name = action_name;
				int c;
				while((c = new_action_name.LastIndexOf('$')) != -1)
				{
					int spacepos = new_action_name.IndexOf(' ', ++c);
					string argname = spacepos == -1 ? new_action_name.Substring(c) : new_action_name.Substring(c, spacepos - c);

					int index;
					for(index = 0; index < arguments.Length && !argname.Equals(arguments[index].label); ++index) {}
					if(index == arguments.Length)
					{
						stdout += "Unknown argument " + argname;
						return stdout;
					}

					new_action_name = new_action_name.Substring(0, --c) + "%a" + (spacepos == -1 ? "" : new_action_name.Substring(spacepos));

					// Add index to args as first argument
					if(args == null)
						args = new object[] { index };
					else
					{
						object[] newargs = new object[args.Length + 1];
						Array.Copy(args, 0, newargs, 1, args.Length);
						newargs[0] = index;
						args = newargs;
					}
				}

				if(!registered_actions.TryGetValue(new_action_name, out action))
				{
					stdout += "Command not found " + action_name;
					return stdout;
				}
			}


			if(args.Length < action.argtypes.Length)
				stdout += "Not enough arguments for " + action_name;
			else if(args.Length > action.argtypes.Length)
				stdout += "Too many arguments for " + action_name;
			else
			{
				// Perform argument type check and perform dynamic type cast if available (i.e. int to float)
				for(int i = 0; i < args.Length; ++i)
					if(args[i].GetType() != action.argtypes[i])
					{
						MethodInfo castMethod = this.GetType().GetMethod("Cast", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(action.argtypes[i]);
						try {
							args[i] = castMethod.Invoke(null, new object[] { args[i] });
						} catch {
							stdout += "Cannot cast argument " + (i + 1).ToString() + " from " + args[i].GetType() + " to " + action.argtypes[i];
							return stdout;
						}
					}

				// Perform action
				try {
					//action.Do(args);
					Do(action, args);
				} catch(Exception ex) {
					stdout += ex.InnerException.ToString();
				}
			}

			return stdout;
		}

		private static object Cast<T>(object o)
		{
			return (object)(T)(dynamic)o;
		}


		private class InvokeAction : Action
		{
			private readonly object instance;
			private readonly MethodInfo _do, _undo;

			public InvokeAction(string name, string desc, Type[] argtypes, object instance, MethodInfo _do, MethodInfo _undo)
				: base(name, desc, argtypes)
			{
				this.instance = instance;
				this._do = _do;
				this._undo = _undo;
			}

			public override void Do(object[] parameters = null) { _do.Invoke(instance, parameters != null ? parameters : new object[] {}); }
			public override void Undo(object[] parameters = null) { _undo.Invoke(instance, parameters != null ? parameters : new object[] {}); }
			public override bool CanUndo() { return _undo != null; }
		}

		private class CallbackAction : Action
		{
			private readonly CallbackActionDelegate cbk;

			public CallbackAction(string name, string desc, Type[] argtypes, CallbackActionDelegate func)
				: base(name, desc, argtypes)
			{
				this.cbk = func;
			}

			public override void Do(object[] parameters = null) { cbk(parameters); }
		}
	}

	public abstract class Action
	{
		public delegate void CallbackActionDelegate(object[] parameters);

		public readonly string name, desc;
		public readonly Type[] argtypes;

		public Action(string name, string desc, Type[] argtypes)
		{
			this.name = name;
			this.desc = desc;
			this.argtypes = argtypes;
		}

		public abstract void Do(object[] parameters = null);
		public virtual void Undo(object[] parameters = null) {}
		public virtual bool CanUndo() { return false; }
	}
}

