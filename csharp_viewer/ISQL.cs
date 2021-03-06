﻿using System;
using System.Collections.Generic;

using System.CodeDom.Compiler;
using Microsoft.CSharp;

using csharp_viewer;

namespace ISQL
{
	public class Compiler
	{
		// A simple interpreter for the Image-SQL

		public delegate string MethodCallDelegate(string method, object[] args, string isqlString);
		public delegate string TransformCompiledDelegate(ImageTransform transform, IEnumerable<TransformedImage> images, string isqlString);

		private static Dictionary<string, int> transformCreators = new Dictionary<string, int>() {
			{"x", 0}
		};

		public static void Execute(string code, MethodCallDelegate MethodCall, TransformCompiledDelegate TransformCompiled, out string output, out string warnings)
		{
			output = "";
			warnings = "";

			// Tokenize code
			Tokenizer.Fragment[] fragments = Tokenizer.Tokenize(code);

			if(fragments.Length == 0)
				return;


			/*string strtokens = "";
			foreach(Tokenizer.Fragment fragment in fragments)
				strtokens += fragment.ToString() + " ";
			//strtokens += code.Substring(fragment.startidx, fragment.endidx - fragment.startidx) + " ";

			System.Windows.Forms.MessageBox.Show(code + "\n\n" + strtokens);*/


			// Interpret first fragment as statement
			string statement = fragments[0].GetString(code);
			if(fragments[0].token != Tokenizer.Token.Var)
				throw new Exception("Expected statement instead of " + statement);

			int creator;
			if(transformCreators.TryGetValue(statement, out creator))
			{
				// Treat code as transform creator statement

				//EDIT
			}
			else
			{
				// Treat code as method call

				//EDIT
			}

			// Interpret clauses
			IEnumerable<csharp_viewer.TransformedImage> scope = null;
			string[] byExpr = null;
			HashSet<int> byExpr_usedArgumentIndices = null;
			bool byExpr_isTemporal = false;
			int lastfragment = fragments.Length - 1;
			for(int i = fragments.Length - 1; i > 0; --i)
			{
				if(!Tokenizer.IsClause(fragments[i].token))
					continue;

				if(i == lastfragment)
					throw new Exception(string.Format("Expected expression after '{0}' clause", fragments[i].GetString(code)));

				if(fragments[i].token == Tokenizer.Token.Where)
				{
					// Compile 'where' block into imageEnumerator

					scope = CompileScopeCondition(code, fragments, i + 1, lastfragment, ref warnings);
				} else if(fragments[i].token == Tokenizer.Token.By)
				{
					// Parse 'by' block into byExpr

					byExpr_usedArgumentIndices = new HashSet<int>();
					byExpr = ParseExpression(code, fragments, i + 1, lastfragment, byExpr_usedArgumentIndices, out byExpr_isTemporal);
				}

				lastfragment = i - 1;
			}
				
			if(scope == null && fragments[lastfragment].token == Tokenizer.Token.Var && csharp_viewer.Viewer.groups.TryGetValue((string)fragments[lastfragment].value, out scope))
				--lastfragment;

			List<object> args = new List<object>();
			for(int i = 1; i <= lastfragment; ++i)
			{
				if(fragments[i].token == Tokenizer.Token.Str)
					args.Add(((string)fragments[i].value).Replace("\\\"", "\""));
				if(fragments[i].token == Tokenizer.Token.TfId)
					args.Add(fragments[i].value);
			}
			if(args.Count == 0 && lastfragment >= 1)
			{
				CompilerErrorCollection errors;
				object[] newargs;
				if(csharp_viewer.Console.Eval(code.Substring(fragments[1].startidx, fragments[lastfragment].endidx - fragments[1].startidx), out newargs, out errors, null, new string[] { System.Reflection.Assembly.GetEntryAssembly().Location }))
					args.AddRange(newargs);
				else
				{
					foreach(CompilerError error in errors)
						output += error.ErrorText + '\n';
					output.TrimEnd(new char[] { '\n' });
				}
			}
			if(byExpr != null)
			{
				args.Add(byExpr);
				args.Add(byExpr_usedArgumentIndices);
				args.Add(byExpr_isTemporal);
			}
			if(scope != null)
				args.Add(scope);
			
			output += MethodCall(statement, args.ToArray(), code);
		}

		private static string[] ParseExpression(string code, Tokenizer.Fragment[] fragments, int firstfragment, int lastfragment, HashSet<int> usedArgumentIndices, out bool isTemporal)
		{
			int exprOffset = fragments[firstfragment].startidx;
			string expr = code.Substring(exprOffset, fragments[lastfragment].endidx - exprOffset);
			List<string> exprList = new List<string>();

			isTemporal = false;

			// Replace variables in expr with compileable expressions
			int bracketLevel = 0;
			for(int j = lastfragment; j >= firstfragment;--j)
			{
				if(fragments[j].token == Tokenizer.Token.OBr)
					++bracketLevel;
				else if(fragments[j].token == Tokenizer.Token.CBr)
					--bracketLevel;
				else if(bracketLevel == 0 && fragments[j].token == Tokenizer.Token.Cma)
				{
					exprList.Add(expr.Substring(fragments[j].endidx - exprOffset).Trim());
					expr = expr.Substring(0, fragments[j].startidx - exprOffset);
				}
				else if(fragments[j].token == Tokenizer.Token.Var)
				{
					string varExpr = fragments[j].GetString(code);

					if(j + 3 <= lastfragment && fragments[j + 1].token == Tokenizer.Token.OBr && fragments[j + 2].token == Tokenizer.Token.Var && fragments[j + 3].token == Tokenizer.Token.CBr)
					{
						// Fragments of format Var '(' Var ')' found

						// Find argidx for argument with label == fragments[j + 2].value
						string argname = (string)fragments[j + 2].value;
						int argidx = csharp_viewer.Cinema.CinemaArgument.FindIndex(csharp_viewer.Global.arguments, argname);
						if(argidx == -1)
							continue; // Function argument isn't argument label

						if(varExpr.Equals("len"))
							varExpr = "Global.arguments[" + argidx.ToString() + "].values.Length";
						else
							continue;

						expr = expr.Substring(0, fragments[j].startidx - exprOffset) + varExpr + expr.Substring(fragments[j + 3].endidx - exprOffset);
					}
					else
					{
						// Fragment of format Var found

						if(varExpr.Equals("time"))
						{
							varExpr = "Global.time";
							isTemporal = true;
						} else if(varExpr.Equals("sin"))
							varExpr = "(float)global::System.Math.Sin";
						else if(varExpr.Equals("cos"))
							varExpr = "(float)global::System.Math.Cos";
						else if(varExpr.Equals("tan"))
							varExpr = "(float)global::System.Math.Tan";
						else if(varExpr.Equals("pi"))
							varExpr = "(float)global::System.Math.PI";
						else
							continue;

						expr = expr.Substring(0, fragments[j].startidx - exprOffset) + varExpr + expr.Substring(fragments[j].endidx - exprOffset);
					}
				}
				else if(fragments[j].token >= Tokenizer.Token.ArgVal && fragments[j].token <= Tokenizer.Token.ArgIdx)
				{
					// Find argidx for argument with label == fragments[j].value
					string argname = (string)fragments[j].value;
					int argidx = csharp_viewer.Cinema.CinemaArgument.FindIndex(csharp_viewer.Global.arguments, argname);
					if(argidx == -1)
						throw new Exception("Unknown argument name " + argname);

					if(j + 1 < fragments.Length && fragments[j + 1].token == Tokenizer.Token.Dot)
					{
						if(j + 1 >= fragments.Length || fragments[j + 2].token != Tokenizer.Token.Var)
							throw new Exception("Expected attribute after \"" + fragments[j].GetString(code) + ".\"");

						string attribute = fragments[j + 2].GetString(code);
						string varExpr = null;
						if(attribute == "min")
						{
							switch(fragments[j].token)
							{
							case Tokenizer.Token.ArgVal:
								varExpr = "Global.arguments[" + argidx.ToString() + "].values[0]";
								break;
							case Tokenizer.Token.ArgStr:
								// Replace @ARG with "image.strValues[" + argidx.ToString() + "]"
								varExpr = "Global.arguments[" + argidx.ToString() + "].strValues[0]";
								break;
							case Tokenizer.Token.ArgIdx:
								varExpr = "0";
								break;
							}
						}
						else if(attribute == "max")
						{
							switch(fragments[j].token)
							{
							case Tokenizer.Token.ArgVal:
								varExpr = "Global.arguments[" + argidx.ToString() + "].values[Global.arguments[" + argidx.ToString() + "].values.Length - 1]";
								break;
							case Tokenizer.Token.ArgStr:
								// Replace @ARG with "image.strValues[" + argidx.ToString() + "]"
								varExpr = "Global.arguments[" + argidx.ToString() + "].strValues[Global.arguments[" + argidx.ToString() + "].values.Length - 1]";
								break;
							case Tokenizer.Token.ArgIdx:
								varExpr = "(Global.arguments[" + argidx.ToString() + "].values.Length - 1)";
								break;
							}
						}
						else
							throw new Exception("Argument " + argname + " doesn't have attribute " + attribute);
						
						expr = expr.Substring(0, fragments[j].startidx - exprOffset) + varExpr + expr.Substring(fragments[j + 2].endidx - exprOffset);
					}
					else
					{
						string varExpr = null;
						switch(fragments[j].token)
						{
						case Tokenizer.Token.ArgVal:
						// Replace $ARG with "image.values[" + argidx.ToString() + "]"
							varExpr = "image.values[image.globalargindices[" + argidx.ToString() + "]]";
							break;
						case Tokenizer.Token.ArgStr:
						// Replace @ARG with "image.strValues[" + argidx.ToString() + "]"
							varExpr = "image.strValues[image.globalargindices[" + argidx.ToString() + "]]";
							break;
						case Tokenizer.Token.ArgIdx:
						// Replace #ARG with "Array.IndexOf(image.args[" + argidx.ToString() + "].values, image.values[" + argidx.ToString() + "])"
							varExpr = "Array.IndexOf(Global.arguments[" + argidx.ToString() + "].values, image.values[image.globalargindices[" + argidx.ToString() + "]])";
							break;
						}
						expr = expr.Substring(0, fragments[j].startidx - exprOffset) + varExpr + expr.Substring(fragments[j].endidx - exprOffset);
					}

					if(usedArgumentIndices != null)
						usedArgumentIndices.Add(argidx);
				}
			}
			exprList.Add(expr.Trim());
			exprList.Reverse();
			return exprList.ToArray();
		}
		private static IEnumerable<csharp_viewer.TransformedImage> CompileScopeCondition(string code, Tokenizer.Fragment[] fragments, int firstfragment, int lastfragment, ref string warnings)
		{
			HashSet<int> byExpr_usedArgumentIndices = new HashSet<int>();
			bool isTemporal;
			string scopeExpr = ParseExpression(code, fragments, firstfragment, lastfragment, byExpr_usedArgumentIndices, out isTemporal)[0];

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
				if(!({0}) && {1})
					yield return image;
		}}
		IEnumerator IEnumerable.GetEnumerator() {{ return this.GetEnumerator(); }}
	}}
}}", csharp_viewer.Viewer.GetSkipImageExpr(byExpr_usedArgumentIndices), scopeExpr);

			return (IEnumerable<csharp_viewer.TransformedImage>)CompileCSharpClass(source, "csharp_viewer", "ImageEnumerator", ref warnings, csharp_viewer.Viewer.images);
		}

		public static object CompileCSharpClass(string code, string namespace_name, string class_name, ref string warnings, params object[] constructor_params)
		{
			// Compile source code for image enumerator class
			CSharpCodeProvider csharp_compiler = new CSharpCodeProvider();
			CompilerParameters csharp_compilerparams = new CompilerParameters(new[] { "System.Core.dll", csharp_viewer.Global.EXE_DIR + "OpenTK.dll", System.Reflection.Assembly.GetEntryAssembly().Location }) {
				GenerateInMemory = true,
				GenerateExecutable = false
			};
			CompilerResults compilerResult = csharp_compiler.CompileAssemblyFromSource(csharp_compilerparams, code);
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
					throw new Exception("Error compiling scope condition:" + code/*.Replace("\n", "\n> ")*/ + errstr);
			}

			// Return instance of image enumerator class
			System.Reflection.Assembly compiledAssembly = compilerResult.CompiledAssembly;
			Type imageEnumeratorType = compiledAssembly.GetType(namespace_name + "." + class_name);
			return Activator.CreateInstance(imageEnumeratorType, constructor_params);
		}

		private class Tokenizer
		{
			public enum Token
			{
				Invalid, Var, Num, Str, // Basic types
				ArgVal, ArgStr, ArgIdx, TfId, // Specialized types
				Add, Sub, Mul, Div, Mod, Eq, NEq, Gr, Sm, GrEq, SmEq, Asn, OBr, CBr, Cma, Dot, //Opcodes
				Where, From, By // Clauses
			}
			public static bool IsClause(Token token)
			{
				return token >= Token.Where && token <= Token.By;
			}
			public static bool IsArgumentType(Token token)
			{
				return token >= Token.ArgVal && token <= Token.ArgIdx;
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
				Asn = Token.Asn, // =

				OBr = Token.OBr, // (
				CBr = Token.CBr, // )

				Cma = Token.Cma, // ,
				Dot = Token.Dot // .
			}

			public class Fragment
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
					case Token.TfId:
						return string.Format("TfId({0})", (ImageTransform.Id)value);
					default:
						return token.ToString();
					}
				}
				public string GetString(string code, int codeOffset = 0)
				{
					return code.Substring(startidx + codeOffset, endidx - startidx);
				}
			}

			public static Fragment[] Tokenize(string code)
			{
				List<Fragment> fragments = new List<Fragment>();

				int tokenstart = 0, tokenend = 0;
				string strtoken;
				while((strtoken = nextToken(code, ref tokenstart, ref tokenend)) != null)
				{
					Fragment fragment = new Fragment();
					fragment.startidx = tokenstart;
					fragment.endidx = tokenend;
					fragments.Add(fragment);

					if(strtoken.Length == 0)
						continue;

					fragment.token = Token.Invalid; // Default to invalid token

					// Check if strtoken represents an opcode
					switch(strtoken.Length)
					{
					case 1:
						switch(strtoken[0])
						{
						case '+':
							fragment.token = Token.Add;
							break;
						case '-':
							fragment.token = Token.Sub;
							break;
						case '*':
							fragment.token = Token.Mul;
							break;
						case '/':
							fragment.token = Token.Div;
							break;
						case '%':
							fragment.token = Token.Mod;
							break;
						case '>':
							fragment.token = Token.Gr;
							break;
						case '<':
							fragment.token = Token.Sm;
							break;
						case '=':
							fragment.token = Token.Asn;
							break;
						case '(':
							fragment.token = Token.OBr;
							break;
						case ')':
							fragment.token = Token.CBr;
							break;
						case ',':
							fragment.token = Token.Cma;
							break;
						case '.':
							fragment.token = Token.Dot;
							break;
						}
						break;

					case 2:

						if(strtoken[1] == '=')
							switch(strtoken[0])
							{
							case '=':
								fragment.token = Token.Eq;
								break;
							case '!':
								fragment.token = Token.NEq;
								break;
							case '>':
								fragment.token = Token.GrEq;
								break;
							case '<':
								fragment.token = Token.SmEq;
								break;
							}
						break;
					}
					if(fragment.token != Token.Invalid)
						continue;

					// Check if strtoken represents a string or an argument type
					switch(strtoken[0])
					{
					case '\"':
						if(strtoken[strtoken.Length - 1] == '\"')
						{
							fragment.token = Token.Str;
							fragment.value = strtoken.Substring(1, strtoken.Length - 2);
						}
						break;
					case '$':
						if(IsVar(strtoken, 1))
						{
							fragment.token = Token.ArgVal;
							fragment.value = strtoken.Substring(1);
						}
						break;
					case '@':
						if(IsVar(strtoken, 1))
						{
							fragment.token = Token.ArgStr;
							fragment.value = strtoken.Substring(1);
						}
						break;
					case '#':
						if(IsVar(strtoken, 1))
						{
							fragment.token = Token.ArgIdx;
							fragment.value = strtoken.Substring(1);
						}
						break;
					}
					ImageTransform.Id tfId;
					if(ImageTransform.Id.TryParse(strtoken, out tfId))
					{
						fragment.token = Token.TfId;
						fragment.value = tfId;
					}
					if(fragment.token != Token.Invalid)
						continue;

					// Check if strtoken represents a variable or clause
					if(IsVar(strtoken))
					{
						if(strtoken.Equals("where", StringComparison.OrdinalIgnoreCase))
							fragment.token = Token.Where;
						else if(strtoken.Equals("from", StringComparison.OrdinalIgnoreCase))
							fragment.token = Token.From;
						else if(strtoken.Equals("by", StringComparison.OrdinalIgnoreCase))
							fragment.token = Token.By;
						else
						{
							fragment.token = Token.Var;
							fragment.value = strtoken;
						}
						continue;
					}

					// Check if strtoken represents a Token.Num
					float value;
					if(float.TryParse(strtoken, out value))
					{
						fragment.token = Token.Num;
						fragment.value = value;
						continue;
					}

					// Respond to invalid token
					if(fragment.token == Token.Invalid)
					{
						//EDIT: Report invalid token or hrow exception
					}
				}

				return fragments.ToArray();
			}

			private static bool IsVar(string str, int idx = 0, int len = -1)
			{
				if(str.Length <= idx || (str[idx] != '_' && !char.IsLetter(str[idx])))
					return false;

				int end = len < 0 ? str.Length : idx + len;
				for(++idx; idx < end; ++idx)
					if(str[idx] != '_' && !char.IsLetterOrDigit(str[idx]))
						return false;

				return true;
			}

			/*private static string[] split(string code)
			{
				List<string> strtokens = new List<string>();

				int strstart = 0, strend = 0;
				bool insideString = false, escapedChar = false;
				char last_c = unchecked((char)-1);
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
						last_c = c;
						continue;
					}

					if(c == '\"')
					{
						insideString = true;
						++strend;
						last_c = c;
						continue;
					}

					if(char.IsWhiteSpace(c))
					{
						if(strend != strstart)
							strtokens.Add(code.Substring(strstart, strend - strstart));
						strstart = ++strend;
						last_c = c;
						continue;
					}

					if(char.IsLetterOrDigit(c))
					{
						if(char.IsPunctuation(last_c))
						{
							strtokens.Add(code.Substring(strstart, strend - strstart));
							strstart = strend++;
							last_c = c;
							continue;
						}
					}
					else if(char.IsPunctuation(c))
					{
						if(char.IsLetterOrDigit(last_c))
						{
							strtokens.Add(code.Substring(strstart, strend - strstart));
							strstart = strend++;
							last_c = c;
							continue;
						}
					}
							
					++strend;
					last_c = c;
				}
				if(strend != strstart)
					strtokens.Add(code.Substring(strstart, strend - strstart));

				return strtokens.ToArray();
			}*/

			private static string nextToken(string code, ref int strstart, ref int strend)
			{
				bool insideString = false, escapedChar = false;
				char last_c = unchecked((char)-1);
				for(strstart = strend; strend < code.Length; ++strend)
				{
					char c = code[strend];

					if(insideString)
					{
						if(c == '\"' && !escapedChar)
						{
							++strend;
							break;
						}
						else
						{
							escapedChar = (c == '\\');
						}
						last_c = c;
						continue;
					}

					if(c == '\"')
					{
						insideString = true;
						last_c = c;
						continue;
					}

					if(char.IsWhiteSpace(c))
					{
						if(strend != strstart)
							break;
						++strstart;
						last_c = c;
						continue;
					}

					if(char.IsLetterOrDigit(c))
					{
						if(char.IsPunctuation(last_c) && last_c != '$' && last_c != '@' && last_c != '#')
							break;
					}
					else if(char.IsPunctuation(c))
					{
						if(char.IsLetterOrDigit(last_c))
							break;
					}

					last_c = c;
				}
				if(strend == code.Length && strstart == strend)
					return null;

				return code.Substring(strstart, strend - strstart);
			}
		}
	}
}
