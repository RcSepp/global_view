﻿using System;
using System.IO;
using System.Collections.Generic;

using System.CodeDom.Compiler;
using Microsoft.CSharp;
using System.Text;

namespace csharp_viewer
{
	public abstract class Console
	{
		// Command history
		private static string HISTORY_PATH;
		private List<string> history = new List<string>();
		private int history_idx = 0;
		private string history_current;

		// Recordable macros
		private static string MACRO_PATH;
		private string[] macros = new string[10];

		// Execution delegate
		public delegate void ExecuteDelegate(string command, out string output, out string warnings);
		public event ExecuteDelegate ExecuteCommand;

		// Working directory
		public string workingDirectory = Directory.GetCurrentDirectory();

		public Console()
		{
			ActionManager.CreateAction("List files and directories of the current working directory", "ls", delegate(object[] parameters) {
				return ListFiles();
			});
			ActionManager.CreateAction<string>("Change working directory", "cd", delegate(object[] parameters) {
				return ChangeWorkingDirectory((string)parameters[0]);
			});

			HISTORY_PATH = System.Reflection.Assembly.GetEntryAssembly().Location;
			MACRO_PATH = HISTORY_PATH = HISTORY_PATH.Substring(0, Math.Max(HISTORY_PATH.LastIndexOf('/'), HISTORY_PATH.LastIndexOf('\\')) + 1);
			HISTORY_PATH += ".history";
			MACRO_PATH += ".macros";

			if(File.Exists(HISTORY_PATH))
			{
				StreamReader sr = new StreamReader(HISTORY_PATH);
				while(sr.Peek() != -1)
					history.Add(sr.ReadLine());
				sr.Close();
				history_idx = history.Count;
			}
			if(File.Exists(MACRO_PATH))
			{
				StreamReader sr = new StreamReader(MACRO_PATH);
				int i = 0;
				while(i < macros.Length && sr.Peek() != -1)
					macros[i++] = sr.ReadLine();
				while(i < macros.Length)
					macros[i++] = "";
				sr.Close();
			}
		}
		~Console()
		{
			StreamWriter sw = new StreamWriter(HISTORY_PATH);
			foreach(string h in history)
				sw.WriteLine(h);
			sw.Close();

			sw = new StreamWriter(MACRO_PATH);
			foreach(string m in macros)
				sw.WriteLine(m);
			sw.Close();
		}

		protected bool HistoryUp(ref string current)
		{
			if(history_idx > 0)
			{
				if(history_idx == history.Count)
					history_current = current;
				current = history[--history_idx];
				return true;
			}
			return false;
		}
		protected bool HistoryDown(ref string current)
		{
			if(history_idx == history.Count - 1)
			{
				++history_idx;
				current = history_current;
				return true;
			}
			else if(history_idx < history.Count - 1)
			{
				current = history[++history_idx];
				return true;
			}
			return false;
		}
		private void HistoryAdd(string current)
		{
			history.Add(current);
			history_idx = history.Count;
		}

		protected void StoreMacro(int slot, string macro)
		{
			if(slot < 0 || slot >= macros.Length)
				throw new IndexOutOfRangeException(string.Format("Invalid macro slot: {0} (should be between 0 and {1})", slot, macros.Length));
			macros[slot] = macro;
		}
		protected string RecallMacro(int slot)
		{
			if(slot < 0 || slot >= macros.Length)
				throw new IndexOutOfRangeException(string.Format("Invalid macro slot: {0} (should be between 0 and {1})", slot, macros.Length));
			return macros[slot];
		}

		/*protected string Execute(string command)
		{
			string method = command;

			HistoryAdd(method);

			string argsstr = "";
			int obpos = method.IndexOf('('), cbpos = method.LastIndexOf(')');
			if(obpos != -1 && cbpos != -1)
			{
				argsstr = method.Substring(obpos + 1, cbpos - obpos - 1);
				method = method.Substring(0, obpos);
			}

			object[] args;
			CompilerErrorCollection errors;
			if(Eval(argsstr, out args, out errors, new string[] { "OpenTK", "OpenTK.Graphics" }, new string[] { "OpenTK.dll" }))
			{
				if(MethodCall != null)
				{
					string stdout;
					if(!ConsoleMethodCall(method, args, out stdout))
					{
						if(MethodCallInvoker != null)
						{
							IAsyncResult invokeResult = MethodCallInvoker.BeginInvoke(MethodCall, new object[] { method, args });
							invokeResult.AsyncWaitHandle.WaitOne();
							stdout = (string)MethodCallInvoker.EndInvoke(invokeResult);
						}
						else
							stdout = MethodCall(method, args);
					}
					stdout.TrimEnd(new char[] { ' ', '\t', '\n' });
					return stdout;
				}
			}
			else
			{
				string stdout = "";
				foreach(CompilerError error in errors)
					stdout += error.ErrorText + '\n';
				stdout.TrimEnd(new char[] { '\n' });
				return stdout;
			}

			return null;
		}*/

		protected string Execute(string command)
		{
			HistoryAdd(command);

			try {
				string output, warnings;
				ExecuteCommand(command, out output, out warnings);
				return (warnings + output).TrimEnd(new char[] { ' ', '\t', '\n' });
			}
			catch(Exception ex) {
				return ex.Message;
			}
		}

		private string ListFiles()
		{
			string stdout = workingDirectory + ":\n";
			int workingDirectoryLength = workingDirectory.Length;
			foreach(string dir in Directory.GetDirectories(workingDirectory))
			{
				string _dir = dir.Substring(workingDirectoryLength + 1);
				if(Cinema.IsCinemaDB(dir))
					stdout += _dir.PadRight(31) + " <- cinema database\n";
				else
					stdout += _dir + '\n';
			}
			foreach(string file in Directory.GetFiles(workingDirectory))
			{
				string _file = file.Substring(workingDirectoryLength + 1);
				stdout += _file + '\n';
			}
			return stdout;
		}
		private string ChangeWorkingDirectory(string dir)
		{
			if(!Path.IsPathRooted(dir))
				dir = Path.Combine(workingDirectory, dir);
			dir = Path.GetFullPath((new Uri(dir)).LocalPath).TrimEnd(new char[] { '/', '\\' });

			if(Directory.Exists(dir))
				return (workingDirectory = dir) + '\n';
			else
				return "No such directory: " + dir + '\n';
		}

		private bool ConsoleMethodCall(string method, object[] args, out string stdout)
		{
			stdout = "";

			if(method.Equals("ls"))
			{
				stdout += workingDirectory + ":\n";
				int workingDirectoryLength = workingDirectory.Length;
				foreach(string dir in Directory.GetDirectories(workingDirectory))
				{
					string _dir = dir.Substring(workingDirectoryLength);
					if(Cinema.IsCinemaDB(dir))
						stdout += _dir.PadRight(31) + " <- cinema database\n";
					else
						stdout += _dir + '\n';
				}
				foreach(string file in Directory.GetFiles(workingDirectory))
				{
					string _file = file.Substring(workingDirectoryLength);
					stdout += _file + '\n';
				}

				return true;
			}
			if(method.Equals("cd"))
			{
				workingDirectory = Directory.GetParent(workingDirectory.TrimEnd(new char[] { '/', '\\' })).FullName + Path.DirectorySeparatorChar;
				stdout += workingDirectory + ":\n";

				return true;
			}
			return false;
		}

		public static bool Eval(string code, out object[] result, out CompilerErrorCollection errors, string[] usingStatements = null, string[] assemblies = null)  
		{
			var includeUsings = new HashSet<string>(new[] { "System" });
			if (usingStatements != null)
				foreach (var usingStatement in usingStatements)
					includeUsings.Add(usingStatement);

			using (CSharpCodeProvider compiler = new CSharpCodeProvider())
			{
				List<string> includeAssemblies = new List<string>(new[] { "system.dll" });
				if (assemblies != null)
					foreach (var assembly in assemblies)
						includeAssemblies.Add(assembly);

				var parameters = new CompilerParameters(includeAssemblies.ToArray())
				{
					GenerateInMemory = true
				};

				string source = string.Format(@"
{0}
namespace csharp_viewer
{{
    public static class EvalClass
    {{
        public static object[] Eval()
        {{
            return new object[] {{ {1} }};
        }}
    }}
}}", GetUsing(includeUsings), code);

				CompilerResults compilerResult = compiler.CompileAssemblyFromSource(parameters, source);
				if(compilerResult.Errors.Count > 0)
				{
					result = null;
					errors = compilerResult.Errors;
					return false;
				}
				var compiledAssembly = compilerResult.CompiledAssembly;
				var type = compiledAssembly.GetType("csharp_viewer.EvalClass");
				var method = type.GetMethod("Eval");
				result = (object[])method.Invoke(null, new object[] { });
				errors = null;
				return true;
			}  
		}
		private static string GetUsing(HashSet<string> usingStatements)  
		{  
			StringBuilder result = new StringBuilder();  
			foreach (string usingStatement in usingStatements)  
			{  
				result.AppendLine(string.Format("using {0};", usingStatement));  
			}  
			return result.ToString();  
		}
	}
}

