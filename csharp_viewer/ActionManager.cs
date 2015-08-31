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
							System.Windows.Forms.MessageBox.Show(ex.InnerException.ToString(), ex.InnerException.TargetSite.ToString());
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
			}
		}

		public static Action CreateAction(string desc, object instance, string method)
		{
			Type clstype = instance.GetType();
			MethodInfo methodinfo = clstype.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
			if(methodinfo == null)
				throw new Exception("Method not found: " + desc);

			ParameterInfo[] paraminfo = methodinfo.GetParameters();
			Type[] paramtypes = new Type[paraminfo.Length];
			for(int i = 0; i < paraminfo.Length; ++i)
				paramtypes[i] = paraminfo[i].ParameterType;

			Action action = new Action(method, desc, paramtypes, instance, methodinfo, null);
			if(mgr != null)
				mgr.registered_actions.Add(action.name, action);
			return action;
		}
		public static Action CreateAction(string desc, object instance, string _do, string _undo)
		{
			Type clstype = instance.GetType();
			MethodInfo _do_info = clstype.GetMethod(_do, BindingFlags.NonPublic | BindingFlags.Instance);
			if(_do_info == null)
				throw new Exception("Method not found: " + desc);
			MethodInfo _undo_info = clstype.GetMethod(_undo, BindingFlags.NonPublic | BindingFlags.Instance);
			if(_undo_info == null)
				throw new Exception("Method not found: " + desc);

			ParameterInfo[] paraminfo = _do_info.GetParameters();
			Type[] paramtypes = new Type[paraminfo.Length];
			for(int i = 0; i < paraminfo.Length; ++i)
				paramtypes[i] = paraminfo[i].ParameterType;

			Action action = new Action(_do, desc, paramtypes, instance, _do_info, _undo_info);
			if(mgr != null)
				mgr.registered_actions.Add(action.name, action);
			return action;
		}

		public static void Do(Action action, object[] parameters = null)
		{
			if(!mgr.playing)
			{
				mgr.actions.AddLast(new PerformedAction(mgr.time, action, parameters));
				action.Do(parameters);
			}
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

		public void Invoke(string action_name)
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
		}
		public void Invoke(string action_name, object[] args, ref string stdout)
		{
			if(action_name == "commands")
			{
				foreach(KeyValuePair<string, Action> registered_action in registered_actions)
					stdout += registered_action.Key + "() -> " + registered_action.Value.desc + '\n';
				return;
			}

			Action action;
			if(registered_actions.TryGetValue(action_name, out action))
			{
				if(args.Length < action.argtypes.Length)
					stdout += "Not enough arguments for " + action_name;
				else if(args.Length > action.argtypes.Length)
					stdout += "Too many arguments for " + action_name;
				else
				{
					for(int i = 0; i < args.Length; ++i)
					{
						if(args[i].GetType() != action.argtypes[i])
						{
							MethodInfo castMethod = this.GetType().GetMethod("Cast", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(action.argtypes[i]);
							try {
								args[i] = castMethod.Invoke(null, new object[] { args[i] });
							} catch {
								stdout += "Cannot cast argument " + (i + 1).ToString() + " from " + args[i].GetType() + " to " + action.argtypes[i];
								return;
							}
						}
					}

					try {
						//action.Do(args);
						Do(action, args);
					} catch(Exception ex) {
						stdout += ex.InnerException.ToString();
					}
				}
			}
			else
				stdout += "Command not found " + action_name;
		}

		private static object Cast<T>(object o)
		{
			return (object)(T)(dynamic)o;
		}
	}

	public class Action
	{
		public readonly string name, desc;
		public readonly Type[] argtypes;

		private readonly object instance;
		private readonly MethodInfo _do, _undo;

		public Action(string name, string desc, Type[] argtypes, object instance, MethodInfo _do, MethodInfo _undo)
		{
			this.name = name;
			this.desc = desc;
			this.argtypes = argtypes;
			this.instance = instance;
			this._do = _do;
			this._undo = _undo;
		}
			
		public void Do(object[] parameters = null) { _do.Invoke(instance, parameters != null ? parameters : new object[] {}); }
		public void Undo(object[] parameters = null) { _undo.Invoke(instance, parameters != null ? parameters : new object[] {}); }
		public bool CanUndo() { return _undo != null; }
	}
}

