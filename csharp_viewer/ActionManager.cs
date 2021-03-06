﻿using System;
using System.Collections.Generic;
using System.Reflection;

using System.Drawing;
using OpenTK.Graphics.OpenGL;

namespace csharp_viewer
{
	public class ActionManager
	{
		public static ActionManager mgr = null;
		public delegate void FrameCaptureFinishedDelegate();
		public event FrameCaptureFinishedDelegate FrameCaptureFinished;

		private struct PerformedAction
		{
			public readonly double time;
			public readonly string cmdString;
			public readonly Action action;
			public readonly object[] parameters;

			public PerformedAction(double time, string cmdString, Action action, object[] parameters)
			{
				this.time = time;
				this.cmdString = cmdString;
				this.action = action;
				this.parameters = parameters;
			}
		}

		private Cinema.CinemaArgument[] arguments;
		private double time = 0.0;
		private LinkedList<PerformedAction> actions = new LinkedList<PerformedAction>();
		private Dictionary<string, Action> registered_actions = new Dictionary<string, Action>();

		private bool playing = false, playing_captureframes, executing = false;
		private double playback_speed, playback_invfps;
		private int playback_framecounter;
		private LinkedList<PerformedAction>.Enumerator playback_next_action;
		private List<string>.Enumerator script_next_command;

		private string screenshot_filename = null;

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
				} else
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
							playback_next_action.Current.action.Do(playback_next_action.Current.parameters, playback_next_action.Current.cmdString);
						} catch(Exception ex) {
							playing = false;
							if(ex.InnerException != null)
								Global.cle.PrintOutput(ex.InnerException.Message);
							else
								Global.cle.PrintOutput(ex.Message);
							return;
						}
						if(!playback_next_action.MoveNext())
						{
							playing = false;
							if(FrameCaptureFinished != null)
								FrameCaptureFinished();
							return;
						}
					} while(playback_next_action.Current.time == lastActionTime);
				}
			}
			else
			{
				if(executing)
				{
					if(script_next_command.MoveNext())
					{
						try {
							string output, warnings;
							ISQL.Compiler.Execute(script_next_command.Current, ActionManager.mgr.Invoke, null, out output, out warnings);
							Invoke("PrintCommand", new object[] { script_next_command.Current, output + warnings });
							//return output + warnings;
						} catch(Exception ex) {
							executing = false;
							if(ex.InnerException != null)
								Global.cle.PrintOutput(ex.InnerException.Message);
							else
								Global.cle.PrintOutput(ex.Message);
							return;
						}
					}
					else
						executing = false;
				}

				time += (double)dt;
			}
		}

		public void PostRender(GLWindow gl, ScriptingConsole cle = null)
		{
			if(screenshot_filename != null || (playing && playing_captureframes))
			{
				Bitmap bmpGL = new Bitmap(gl.ClientSize.Width, gl.ClientSize.Height);
				System.Drawing.Imaging.BitmapData data = bmpGL.LockBits(new Rectangle(Point.Empty, gl.ClientSize), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
				GL.ReadPixels(0, 0, gl.ClientSize.Width, gl.ClientSize.Height, PixelFormat.Bgr, PixelType.UnsignedByte, data.Scan0);
				bmpGL.UnlockBits(data);
				bmpGL.RotateFlip(RotateFlipType.RotateNoneFlipY);

				Bitmap bmp;
				if(cle != null)
				{
					Bitmap bmpCle = new Bitmap(gl.ClientSize.Width, cle.Height);
					Graphics gfx = Graphics.FromImage(bmpCle);
					cle.DrawToGraphics(gfx);
					gfx.Flush();
					gfx.Dispose();

					bmp = new Bitmap(gl.ClientSize.Width, gl.ClientSize.Height + cle.Height);
					gfx = Graphics.FromImage(bmp);
					gfx.DrawImageUnscaled(bmpGL, 0, 0);
					gfx.DrawImageUnscaled(bmpCle, 0, bmpGL.Height);
					gfx.Flush();
					gfx.Dispose();
					bmpGL.Dispose();
					bmpCle.Dispose();
				}
				else
					bmp = bmpGL;

				if(screenshot_filename != null)
				{
					bmp.Save(screenshot_filename);
					screenshot_filename = null;
				}
				if(playing && playing_captureframes)
				{
					string framestr = playback_framecounter.ToString();
					for(int i = 0, numzeros = 5 - framestr.Length; i < numzeros; ++i)
						framestr = '0' + framestr;
					bmp.Save("frames/frame" + framestr + ".png");
				}
				bmp.Dispose();
			}
		}

		private static MethodInfo GetMethod(Type clstype, string method)
		{
			MethodInfo mi;
			do
			{
				if((mi = clstype.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)) != null)
					return mi;
				if((mi = clstype.GetMethod(method, BindingFlags.Public | BindingFlags.Instance)) != null)
					return mi;
			} while((clstype = clstype.BaseType) != null);
			return null;
		}

		public static Action CreateAction(string desc, object instance, string method)
		{
			if(desc == null || desc == "")
				desc = method;

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
				string name = action.name.ToLower();
				if(mgr.registered_actions.ContainsKey(name))
					mgr.registered_actions.Remove(name);
				mgr.registered_actions.Add(name, action);
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
				string name = action.name.ToLower();
				if(mgr.registered_actions.ContainsKey(name))
					mgr.registered_actions.Remove(name);
				mgr.registered_actions.Add(name, action);
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
				name = action.name.ToLower();
				if(mgr.registered_actions.ContainsKey(name.ToLower()))
					mgr.registered_actions.Remove(name);
				mgr.registered_actions.Add(name, action);
			}
			return action;
		}

		public static Action CreateAction(string desc, string method, Action.CallbackActionDelegate func)
		{
			Action action = new CallbackAction(method, desc, new Type[] {}, func);
			if(mgr != null)
			{
				string name = action.name.ToLower();
				if(mgr.registered_actions.ContainsKey(name.ToLower()))
					mgr.registered_actions.Remove(name);
				mgr.registered_actions.Add(name, action);
			}
			return action;
		}
		public static Action CreateAction<T>(string desc, string method, Action.CallbackActionDelegate func)
		{
			Action action = new CallbackAction(method, desc, new Type[] {typeof(T)}, func);
			if(mgr != null)
			{
				string name = action.name.ToLower();
				if(mgr.registered_actions.ContainsKey(name.ToLower()))
					mgr.registered_actions.Remove(name);
				mgr.registered_actions.Add(name, action);
			}
			return action;
		}
		public static Action CreateAction<T1, T2>(string desc, string method, Action.CallbackActionDelegate func)
		{
			Action action = new CallbackAction(method, desc, new Type[] {typeof(T1), typeof(T2)}, func);
			if(mgr != null)
			{
				string name = action.name.ToLower();
				if(mgr.registered_actions.ContainsKey(name.ToLower()))
					mgr.registered_actions.Remove(name);
				mgr.registered_actions.Add(name, action);
			}
			return action;
		}
		public static Action CreateAction<T1, T2, T3>(string desc, string method, Action.CallbackActionDelegate func)
		{
			Action action = new CallbackAction(method, desc, new Type[] {typeof(T1), typeof(T2), typeof(T3)}, func);
			if(mgr != null)
			{
				string name = action.name.ToLower();
				if(mgr.registered_actions.ContainsKey(name.ToLower()))
					mgr.registered_actions.Remove(name);
				mgr.registered_actions.Add(name, action);
			}
			return action;
		}
		public static Action CreateAction<T1, T2, T3, T4>(string desc, string method, Action.CallbackActionDelegate func)
		{
			Action action = new CallbackAction(method, desc, new Type[] {typeof(T1), typeof(T2), typeof(T3), typeof(T4)}, func);
			if(mgr != null)
			{
				string name = action.name.ToLower();
				if(mgr.registered_actions.ContainsKey(name.ToLower()))
					mgr.registered_actions.Remove(name);
				mgr.registered_actions.Add(name, action);
			}
			return action;
		}
		public static Action CreateAction<T1, T2, T3, T4, T5>(string desc, string method, Action.CallbackActionDelegate func)
		{
			Action action = new CallbackAction(method, desc, new Type[] {typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5)}, func);
			if(mgr != null)
			{
				string name = action.name.ToLower();
				if(mgr.registered_actions.ContainsKey(name.ToLower()))
					mgr.registered_actions.Remove(name);
				mgr.registered_actions.Add(name, action);
			}
			return action;
		}

		public static string Do(Action action, params object[] parameters)
		{
			if(!mgr.playing)
				mgr.actions.AddLast(new PerformedAction(mgr.time, null, action, parameters));
			return action.Do(parameters);
		}

		/*public void SaveAs(string filename)
		{
			
		}*/

		public void RunScript(string filename)
		{
			if(playing)
				return;

			System.IO.StreamReader sr = new System.IO.StreamReader(new System.IO.FileStream(filename, System.IO.FileMode.Open, System.IO.FileAccess.Read));
			List<string> commandlist = new List<string>();
			while(sr.Peek() != -1)
			{
				string readline = sr.ReadLine();
				if(!readline.StartsWith("//"))
					commandlist.Add(readline);
			}
			sr.Close();

			script_next_command = commandlist.GetEnumerator();
			executing = true;
		}

		public void Clear()
		{
			actions.Clear();
			time = 0.0;
			playing = false;
		}

		public void Play(double playback_speed = 1.0)
		{
			if(executing)
				return;

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
			if(executing)
				return;

			playback_next_action = actions.GetEnumerator();
			if(!playback_next_action.MoveNext())
				return;
			time = 0.0;
			playback_invfps = 1.0 / fps;
			playback_framecounter = 0;
			playing_captureframes = true;
			playing = true;
			System.IO.Directory.CreateDirectory("frames" + System.IO.Path.DirectorySeparatorChar);
		}

		public void SaveScreenshot(string filename = "screenshot.png")
		{
			screenshot_filename = filename;
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

		/// <summary>
		/// Call a registered action
		/// </summary>
		/// <param name="action_name">Action name</param>
		/// <param name="args">List of arguments</param>
		public string Invoke(string action_name, object[] args, string cmdString = null)
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
			if(!registered_actions.TryGetValue(action_name.ToLower(), out action))
			{
				if(arguments == null)
				{
					stdout += "Command not found " + action_name;
					return stdout;
				}

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

				if(!registered_actions.TryGetValue(new_action_name.ToLower(), out action))
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
					
					//string output = Do(action, args);
					if(!playing)
						actions.AddLast(new PerformedAction(time, cmdString, action, args));
					string output = action.Do(args, cmdString);
					if(output != null)
						stdout += output;
				} catch(Exception ex) {
					stdout += ex.InnerException != null ? ex.InnerException.ToString() : ex.ToString();
				}
			}

			return stdout;
		}

		private static object Cast<T>(object o)
		{
			return (object)(T)(dynamic)o;
		}

		public static string activeCmdString = null;

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

			public override string Do(object[] parameters = null, string cmdString = null)
			{
				activeCmdString = cmdString;
				string result = (string)_do.Invoke(instance, parameters != null ? parameters : new object[] {});
				activeCmdString = null;
				return result;
			}
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

			public override string Do(object[] parameters = null, string cmdString = null)
			{
				activeCmdString = cmdString;
				string result = cbk(parameters);
				activeCmdString = null;
				return result;
			}
		}
	}

	public abstract class Action
	{
		public delegate string CallbackActionDelegate(object[] parameters);

		public readonly string name, desc;
		public readonly Type[] argtypes;

		public Action(string name, string desc, Type[] argtypes)
		{
			this.name = name;
			this.desc = desc;
			this.argtypes = argtypes;
		}

		public abstract string Do(object[] parameters, string cmdString = null);
		public virtual void Undo(object[] parameters = null) {}
		public virtual bool CanUndo() { return false; }
	}
}

