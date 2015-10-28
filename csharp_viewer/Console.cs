using System;
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

		// Execution delegate
		public delegate string MethodCallDelegate(string method, object[] args); //EDIT: deprecated
		public event MethodCallDelegate MethodCall; //EDIT: deprecated
		public System.Windows.Forms.Control MethodCallInvoker = null; //EDIT: deprecated

		// Working directory
		public string workingDirectory = Directory.GetCurrentDirectory() + '/';

		public Console()
		{
			HISTORY_PATH = System.Reflection.Assembly.GetEntryAssembly().Location;
			HISTORY_PATH = HISTORY_PATH.Substring(0, Math.Max(HISTORY_PATH.LastIndexOf('/'), HISTORY_PATH.LastIndexOf('\\')) + 1);
			HISTORY_PATH += ".history";

			if(File.Exists(HISTORY_PATH))
			{
				StreamReader sr = new StreamReader(HISTORY_PATH);
				while(sr.Peek() != -1)
					history.Add(sr.ReadLine());
				sr.Close();
				history_idx = history.Count;
			}
		}
		~Console()
		{
			StreamWriter sw = new StreamWriter(HISTORY_PATH);
			foreach(string h in history)
				sw.WriteLine(h);
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
				ISQL.Compiler.Execute(command, compiler_MethodCall, out output, out warnings);
				return warnings + output;
			}
			catch(Exception ex) {
				return ex.Message;
			}
		}
		private string compiler_MethodCall(string method, object[] args)
		{
			if(MethodCall == null)
				return null;

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
					stdout += _dir + '\n';
				}
				foreach(string file in Directory.GetFiles(workingDirectory))
				{
					string _file = file.Substring(workingDirectoryLength);
					stdout += _file + '\n';
				}

				return true;
			}
			return false;
		}

		private static bool Eval(string code, out object[] result, out CompilerErrorCollection errors, string[] usingStatements = null, string[] assemblies = null)  
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
namespace NS
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
				var type = compiledAssembly.GetType("NS.EvalClass");
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

