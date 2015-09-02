using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using System.CodeDom.Compiler;
using Microsoft.CSharp;
using System.Text;

using FastColoredTextBoxNS;

namespace csharp_viewer
{
	public class ScriptingConsole
	{
		private static string HISTORY_PATH;

		private FastColoredTextBox txt;
		private TextStyle OUTPUT_STYLE = new TextStyle(Brushes.Brown, null, FontStyle.Regular);
		private List<string> history = new List<string>();
		private int history_idx = 0;
		private string history_current;

		public delegate void MethodCallDelegate(string method, object[] args, ref string stdout);
		public event MethodCallDelegate MethodCall;

		public ScriptingConsole()
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

		~ScriptingConsole()
		{
			StreamWriter sw = new StreamWriter(HISTORY_PATH);
			foreach(string h in history)
				sw.WriteLine(h);
			sw.Close();
		}

		public Control Create()
		{
			txt = new FastColoredTextBox();
			txt.ReadOnly = false;
			//txt.MouseWheel += MouseWheel;
			txt.KeyDown += KeyDown;
			txt.KeyPress += KeyPress;
			//txt.TextChanged += TextChanged;

			txt.Language = Language.CSharp;
			txt.Font = new Font("Courier New", 10);

			return txt;
		}


		/*private void MouseWheel(object sender, MouseEventArgs e)
		{
		}*/

		private void KeyDown(object sender, KeyEventArgs e)
		{
			switch(e.KeyCode)
			{
			case Keys.LButton: // Mac bugfix
				txt.GoHome();
				e.Handled = true;
				break;
			case Keys.MButton: // Mac bugfix
				txt.GoEnd();
				e.Handled = true;
				break;

			case Keys.Up:
				if(history_idx > 0)
				{
					txt.Selection.BeginUpdate();
					txt.Selection.Start = new Place(0, txt.LinesCount - 1);
					txt.Selection.End = new Place(txt.Lines[txt.LinesCount - 1].Length, txt.LinesCount - 1);
					txt.Selection.EndUpdate();
					if(history_idx == history.Count)
						history_current = txt.Selection.Text;
					txt.InsertText(history[--history_idx]);
				}
				e.Handled = true;
				break;
			case Keys.Down:
				if(history_idx == history.Count - 1)
				{
					txt.Selection.BeginUpdate();
					txt.Selection.Start = new Place(0, txt.LinesCount - 1);
					txt.Selection.End = new Place(txt.Lines[txt.LinesCount - 1].Length, txt.LinesCount - 1);
					txt.Selection.EndUpdate();
					++history_idx;
					txt.InsertText(history_current);
				}
				else if(history_idx < history.Count - 1)
				{
					txt.Selection.BeginUpdate();
					txt.Selection.Start = new Place(0, txt.LinesCount - 1);
					txt.Selection.End = new Place(txt.Lines[txt.LinesCount - 1].Length, txt.LinesCount - 1);
					txt.Selection.EndUpdate();
					txt.InsertText(history[++history_idx]);
				}
				e.Handled = true;
				break;
			case Keys.Left: case Keys.Back:
				if(txt.Selection.Start.iChar == 0) e.Handled = true;
				break;
			case Keys.Enter:
			case Keys.Cancel: // Mac num-block return key
				txt.GoEnd();
				string method = txt.Lines[txt.LinesCount - 1];

				history.Add(method);
				history_idx = history.Count;

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
						string stdout = "";
						MethodCall(method, args, ref stdout);
						stdout.TrimEnd(new char[] { ' ', '\t', '\n' });
						if(stdout.Length > 0)
						{
							Place startpos = txt.Selection.Start;
							txt.AppendText('\n' + stdout);
							txt.SelectionStart += stdout.Length + 1;
							new Range(txt, startpos, txt.Selection.Start).SetStyle(OUTPUT_STYLE);
						}
					}
				} else
				{
					Place startpos = txt.Selection.Start;
					int i = 0;
					foreach(CompilerError error in errors)
					{
						txt.AppendText('\n' + error.ErrorText);
						i += 1 + error.ErrorText.Length;
					}
					txt.SelectionStart += i;
					new Range(txt, startpos, txt.Selection.Start).SetStyle(OUTPUT_STYLE);
				}

				if(e.KeyCode == Keys.Cancel)
				{
					txt.AppendText("\n");
					++txt.SelectionStart;
				}
				break;
			}

			if((e.Modifiers & Keys.Alt) != 0)
				switch(e.KeyCode)
				{
				case Keys.C: // Mac bugfix
					txt.Copy();
					e.Handled = true;
					break;
				case Keys.V: // Mac bugfix
					txt.Paste();
					e.Handled = true;
					break;
				case Keys.X: // Mac bugfix
					txt.Cut();
					e.Handled = true;
					break;
				case Keys.Z: // Mac bugfix
					txt.Undo();
					e.Handled = true;
					break;
				}
		}
		private void KeyPress(object sender, KeyPressEventArgs e)
		{
			if(e.KeyChar == 13 || (e.KeyChar != ' ' && e.KeyChar != '\t' && !char.IsControl(e.KeyChar))) txt.InsertText(e.KeyChar.ToString()); // Bugfix
		}

		/*private void TextChanged(object sender, TextChangedEventArgs e)
		{
			new Range(txt, 0, 0, 0, txt.LinesCount - 1).ReadOnly = true;
		}*/

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

