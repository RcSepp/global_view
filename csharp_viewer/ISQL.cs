using System;
using System.Collections.Generic;

using System.CodeDom.Compiler;
using Microsoft.CSharp;

namespace ISQL
{
	public class Compiler
	{
		// A simple interpreter for the Image-SQL

		public delegate string MethodCallDelegate(string method, object[] args);

		public static void Execute(string code, MethodCallDelegate MethodCall, out string output, out string warnings)
		{
			output = "";
			warnings = "";
			string methodStr;

			// Tokenize code
			Tokenizer.Fragment[] fragments = Tokenizer.Tokenize(code);

			if(fragments.Length == 0)
				return;

			for(int i = 0; i < fragments.Length; ++i)
			{
				if(fragments[i].token == Tokenizer.Token.Where)
				{
					if(i == 0)
						throw new Exception("Expected command before 'where'");
					if(i + 1 == fragments.Length)
						throw new Exception("Expected scope condition after 'where'");
					System.Collections.Generic.IEnumerable<csharp_viewer.TransformedImage> imageEnumerator = CompileScopeCondition(code, fragments, i + 1, ref warnings);

					methodStr = code.Substring(fragments[0].startidx, fragments[i - 1].endidx - fragments[0].startidx);
					output += MethodCall(methodStr, new object[] { imageEnumerator });
					return;
				} else if(fragments[i].token == Tokenizer.Token.All)
				{
					if(i == 0)
						throw new Exception("Expected command before 'all'");
					if(i + 1 != fragments.Length)
						throw new Exception("Unexpected symbol after 'all': " + code.Substring(fragments[i + 1].startidx, fragments[i + 1].endidx - fragments[i + 1].startidx));

					methodStr = code.Substring(fragments[0].startidx, fragments[i - 1].endidx - fragments[0].startidx);
					output += MethodCall(methodStr, new object[] { csharp_viewer.Viewer.images });
					return;
				} else if(fragments[i].token == Tokenizer.Token.Selection)
				{
					if(i == 0)
						throw new Exception("Expected command before 'selection'");
					if(i + 1 != fragments.Length)
						throw new Exception("Unexpected symbol after 'selection': " + code.Substring(fragments[i + 1].startidx, fragments[i + 1].endidx - fragments[i + 1].startidx));

					methodStr = code.Substring(fragments[0].startidx, fragments[i - 1].endidx - fragments[0].startidx);
					output += MethodCall(methodStr, new object[] { csharp_viewer.Viewer.selection });
					return;
				}
			}

			// >>> DELETE
			if(fragments.Length >= 2 && fragments[1].token == Tokenizer.Token.Str)
			{
				methodStr = code.Substring(fragments[0].startidx, fragments[0].endidx - fragments[0].startidx);
				output += MethodCall(methodStr, new object[] {fragments[1].value});
			}
			// <<< DELETE


			methodStr = code.Substring(fragments[0].startidx, fragments[fragments.Length - 1].endidx - fragments[0].startidx);
			output += MethodCall(methodStr, new object[] {});
			return;


			string strtokens = "";
			foreach(Tokenizer.Fragment fragment in fragments)
				strtokens += fragment.ToString() + " ";
				//strtokens += code.Substring(fragment.startidx, fragment.endidx - fragment.startidx) + " ";
			
			System.Windows.Forms.MessageBox.Show(code + "\n\n" + strtokens);
		}

		private static System.Collections.Generic.IEnumerable<csharp_viewer.TransformedImage> CompileScopeCondition(string code, Tokenizer.Fragment[] fragments, int startFragmentIdx, ref string warnings)
		{
			int scopeConditionIdx = fragments[startFragmentIdx].startidx;
			string scopeConditionStr = code.Substring(scopeConditionIdx);

			// Replace variables in scopeConditionStr with compileable expressions
			for(int j = fragments.Length - 1; j >= startFragmentIdx;--j)
			{
				if(fragments[j].token == Tokenizer.Token.ArgVal || fragments[j].token == Tokenizer.Token.ArgIdx)
				{
					// Find argidx for argument with label == fragments[j].value
					string argname = (string)fragments[j].value;
					int argidx = csharp_viewer.Cinema.CinemaArgument.FindIndex(csharp_viewer.Viewer.arguments, argname);
					if(argidx == -1)
						throw new Exception("Unknown argument name " + argname);

					string expr = null;
					if(fragments[j].token == Tokenizer.Token.ArgVal)
						// Replace $ARG with expr
						expr = "image.values[" + argidx.ToString() + "]";
					else if(fragments[j].token == Tokenizer.Token.ArgIdx)
						// Replace #ARG with "Array.IndexOf(image.args[" + argidx.ToString() + "].values, image.values[" + argidx.ToString() + "])"
						expr = "Array.IndexOf(image.args[" + argidx.ToString() + "].values, image.values[" + argidx.ToString() + "])";
					scopeConditionStr = scopeConditionStr.Substring(0, fragments[j].startidx - scopeConditionIdx) + expr + scopeConditionStr.Substring(fragments[j].endidx - scopeConditionIdx);
				}
			}
			//System.Windows.Forms.MessageBox.Show("Scope condition:\n" + scopeConditionStr);

			// Define source code for image enumerator class
			string source = string.Format(@"
using System;
using System.Collections;
using System.Collections.Generic;

namespace csharp_viewer
{{
	public class ImageEnumerator : IEnumerable<TransformedImage>
	{{
		private readonly TransformedImageCollection images;
		public ImageEnumerator(TransformedImageCollection images) {{ this.images = images; }}
		
	    public IEnumerator<TransformedImage> GetEnumerator()
		{{
			foreach(TransformedImage image in images)
				if({0})
					yield return image;
		}}
		IEnumerator IEnumerable.GetEnumerator() {{ return this.GetEnumerator(); }}
	}}
}}", scopeConditionStr);

			// Compile source code for image enumerator class
			CSharpCodeProvider csharp_compiler = new CSharpCodeProvider();
			CompilerParameters csharp_compilerparams = new CompilerParameters(new[] { "system.dll", System.Reflection.Assembly.GetEntryAssembly().Location }) {
				GenerateInMemory = true,
				GenerateExecutable = false
			};
			CompilerResults compilerResult = csharp_compiler.CompileAssemblyFromSource(csharp_compilerparams, source);
			if(compilerResult.Errors.Count > 0)
			{
				string errstr = "";
				foreach(CompilerError error in compilerResult.Errors)
				{
					if(error.IsWarning)
						warnings += "Warning compiling scope condition: " + error.ErrorText + '\n';
					else
						errstr += "\n" + error.Line.ToString() + ": " + error.ErrorText;
				}
				if(errstr != "")
					throw new Exception("Error compiling scope condition:" + source.Replace("\n", "\n> ") + errstr);
			}

			// Return instance of image enumerator class
			System.Reflection.Assembly compiledAssembly = compilerResult.CompiledAssembly;
			Type imageEnumeratorType = compiledAssembly.GetType("csharp_viewer.ImageEnumerator");
			return (System.Collections.Generic.IEnumerable<csharp_viewer.TransformedImage>)Activator.CreateInstance(imageEnumeratorType, csharp_viewer.Viewer.images);
		}

		private class Tokenizer
		{
			public enum Token
			{
				Invalid, Var, Num, Str, // Basic types
				ArgVal, ArgIdx, // Specific types
				Add, Sub, Mul, Div, Mod, Eq, NEq, Gr, Sm, GrEq, SmEq, Asn, //Opcodes
				Where, All, Selection // Keywords
			}
			public enum Opcodes
			{
				Add = Token.Add, // +
				Sub = Token.Sub, // -
				Mul = Token.Mul, // *
				Div = Token.Add, // /
				Mod = Token.Mod, // %

				Eq = Token.Eq, // ==
				NEq = Token.NEq, // !=
				Gr = Token.Gr, // >
				Sm = Token.Sm, // <
				GrEq = Token.GrEq, // >=
				SmEq = Token.SmEq, // <=
				Asn = Token.Asn // =
			}

			public struct Fragment
			{
				public Token token;
				public object value;
				public int startidx, endidx;

				public override string ToString()
				{
					switch(token)
					{
					case Token.Var:
						return string.Format("Var({0})", (string)value);
					case Token.Num:
						return string.Format("Num({0})", (float)value);
					case Token.Str:
						return string.Format("Str({0})", (string)value);
					case Token.ArgVal:
						return string.Format("ArgVal({0})", (string)value);
					case Token.ArgIdx:
						return string.Format("ArgIdx({0})", (string)value);
					default:
						return token.ToString();
					}
				}
			}

			public static Fragment[] Tokenize(string code)
			{
				string[] strtokens = split(code);
				Fragment[] fragments = new Fragment[strtokens.Length];

				int t = 0, idx = 0;
				foreach(string strtoken in strtokens)
				{
					fragments[t].startidx = idx;
					idx += strtoken.Length;
					fragments[t].endidx = idx;
					++idx; // Skip ' '

					if(strtoken.Length == 0)
						continue;

					fragments[t].token = Token.Invalid; // Default to invalid token

					// Check if strtoken represents an opcode
					switch(strtoken.Length)
					{
					case 1:
						switch(strtoken[0])
						{
						case '+':
							fragments[t].token = Token.Add;
							break;
						case '-':
							fragments[t].token = Token.Sub;
							break;
						case '*':
							fragments[t].token = Token.Mul;
							break;
						case '/':
							fragments[t].token = Token.Div;
							break;
						case '%':
							fragments[t].token = Token.Mod;
							break;
						case '>':
							fragments[t].token = Token.Gr;
							break;
						case '<':
							fragments[t].token = Token.Sm;
							break;
						case '=':
							fragments[t].token = Token.Asn;
							break;
						}
						break;

					case 2:

						if(strtoken[1] == '=')
							switch(strtoken[0])
							{
							case '=':
								fragments[t].token = Token.Eq;
								break;
							case '!':
								fragments[t].token = Token.NEq;
								break;
							case '>':
								fragments[t].token = Token.GrEq;
								break;
							case '<':
								fragments[t].token = Token.SmEq;
								break;
							}
						break;
					}
					if(fragments[t].token != Token.Invalid)
					{
						++t;
						continue;
					}

					// Check if strtoken represents a Token.Str, Token.ArgVal or Token.ArgIdx
					switch(strtoken[0])
					{
					case '\"':
						if(strtoken[strtoken.Length - 1] == '\"')
						{
							fragments[t].token = Token.Str;
							fragments[t].value = strtoken.Substring(1, strtoken.Length - 2);
						}
						break;
					case '$':
						if(IsVar(strtoken, 1))
						{
							fragments[t].token = Token.ArgVal;
							fragments[t].value = strtoken.Substring(1);
						}
						break;
					case '#':
						if(IsVar(strtoken, 1))
						{
							fragments[t].token = Token.ArgIdx;
							fragments[t].value = strtoken.Substring(1);
						}
						break;
					}
					if(fragments[t].token != Token.Invalid)
					{
						++t;
						continue;
					}

					// Check if strtoken represents a Token.Var or keyword
					if(IsVar(strtoken))
					{
						if(strtoken.Equals("where", StringComparison.OrdinalIgnoreCase))
							fragments[t++].token = Token.Where;
						else if(strtoken.Equals("all", StringComparison.OrdinalIgnoreCase))
							fragments[t++].token = Token.All;
						else if(strtoken.Equals("selection", StringComparison.OrdinalIgnoreCase))
							fragments[t++].token = Token.Selection;
						else
						{
							fragments[t].token = Token.Var;
							fragments[t++].value = strtoken;
						}
						continue;
					}

					// Check if strtoken represents a Token.Num
					float value;
					if(float.TryParse(strtoken, out value))
					{
						fragments[t].token = Token.Num;
						fragments[t++].value = value;
						continue;
					}

					// Respond to invalid token
					if(fragments[t].token == Token.Invalid)
					{
						//EDIT: Report invalid token or hrow exception
					}
					++t;
				}

				if(t < fragments.Length)
					Array.Resize(ref fragments, t);
				return fragments;
			}

			private static bool IsVar(string str, int idx = 0, int len = -1)
			{
				if(str[idx] != '_' && !char.IsLetter(str[idx]))
					return false;

				int end = len < 0 ? str.Length : idx + len;
				for(++idx; idx < end; ++idx)
					if(str[idx] != '_' && !char.IsLetterOrDigit(str[idx]))
						return false;

				return true;
			}

			private static string[] split(string code)
			{
				List<string> strtokens = new List<string>();

				int strstart = 0, strend = 0;
				bool insideString = false, escapedChar = false;
				foreach(char c in code)
				{
					if(insideString)
					{
						if(c == '\"' && !escapedChar)
						{
							insideString = false;
							strtokens.Add(code.Substring(strstart, ++strend - strstart));
							strstart = strend;
						}
						else
						{
							escapedChar = (c == '\\');
							++strend;
						}
						continue;
					}

					if(c == '\"')
					{
						insideString = true;
						++strend;
						continue;
					}

					if(char.IsWhiteSpace(c))
					{
						if(strend != strstart)
							strtokens.Add(code.Substring(strstart, strend - strstart));
						strstart = ++strend;
						continue;
					}

					++strend;
				}
				if(strend != strstart)
					strtokens.Add(code.Substring(strstart, strend - strstart));

				return strtokens.ToArray();
			}
		}
	}
}
