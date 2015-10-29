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
	public class ScriptingConsole : Console
	{
		private FastColoredTextBox txt;
		private Range lastselection;
		private TextStyle OUTPUT_STYLE = new TextStyle(Brushes.Brown, null, FontStyle.Regular);

		public Control Create()
		{
			txt = new FastColoredTextBox();
			txt.ReadOnly = false;
			//txt.MouseWheel += MouseWheel;
			txt.KeyDown += KeyDown;
			//txt.KeyPress += KeyPress;
			//txt.TextChanged += TextChanged;
			txt.SelectionChanged += SelectionChanged;

			//txt.Language = Language.CSharp;
			//txt.Language = Language.SQL;
			txt.Language = Language.Custom;
			txt.DescriptionFile = "isql_syntax_desc.xml";
			txt.Font = new Font("Courier New", 10);
			txt.WordWrap = true;
			txt.WordWrapMode = WordWrapMode.CharWrapControlWidth;

			txt.Text = "> ";

			return txt;
		}

		void SelectionChanged (object sender, EventArgs e)
		{
			if(txt.Selection.Start.iLine == txt.LinesCount - 1)
			{
				if(txt.Selection.Start.iChar < 2)
					txt.Selection = new Range(txt, Math.Max(2, txt.Selection.Start.iChar), txt.Selection.Start.iLine, Math.Max(2, txt.Selection.End.iChar), txt.Selection.Start.iLine);
				lastselection = txt.Selection.Clone();
			}
		}

		private void KeyDown(object sender, KeyEventArgs e)
		{
			if((txt.Selection.Start.iLine < txt.LinesCount - 1 || txt.Selection.Start.iChar < 2) &&
				((char)e.KeyData == 13 || e.KeyData == (Keys.Alt | Keys.V) || e.KeyData == (Keys.Alt | Keys.X) ||
					((char)e.KeyData != ' ' && (char)e.KeyData != '\t' && !char.IsControl((char)e.KeyData) && (Control.ModifierKeys & ~Keys.Shift) == 0)))
			{
				if(e.KeyData == (Keys.Alt | Keys.X))
				{
					txt.Copy();
					e.Handled = true;
				}
				else
					txt.Selection = lastselection;
			}

			switch(e.KeyCode)
			{
			/*case Keys.LButton: // Mac bugfix
				if((e.Modifiers & Keys.Shift) != 0)
					txt.Selection = new Range(txt, 0, txt.Selection.Start.iLine, txt.Selection.End.iChar, txt.Selection.End.iLine);
				else
					txt.Selection = new Range(txt, 0, txt.Selection.Start.iLine, 0, txt.Selection.Start.iLine);
				e.Handled = true;
				break;
			case Keys.MButton: // Mac bugfix
				if((e.Modifiers & Keys.Shift) != 0)
					txt.Selection = new Range(txt, txt.GetLineLength(txt.Selection.End.iLine), txt.Selection.End.iLine, txt.Selection.End.iChar, txt.Selection.End.iLine);
				else
					txt.Selection = new Range(txt, txt.GetLineLength(txt.Selection.End.iLine), txt.Selection.End.iLine, txt.GetLineLength(txt.Selection.End.iLine), txt.Selection.End.iLine);
				e.Handled = true;
				break;*/

			case Keys.Up:
				{
					string current = txt.Lines[txt.LinesCount - 1].Substring(2);
					if(HistoryUp(ref current))
					{
						txt.Selection.BeginUpdate();
						txt.Selection.Start = new Place(0, txt.LinesCount - 1);
						txt.Selection.End = new Place(txt.Lines[txt.LinesCount - 1].Length, txt.LinesCount - 1);
						txt.Selection.EndUpdate();
						txt.InsertText(current);
					}
				}
				e.Handled = true;
				break;
			case Keys.Down:
				{
					string current = txt.Lines[txt.LinesCount - 1].Substring(2);
					if(HistoryDown(ref current))
					{
						txt.Selection.BeginUpdate();
						txt.Selection.Start = new Place(0, txt.LinesCount - 1);
						txt.Selection.End = new Place(txt.Lines[txt.LinesCount - 1].Length, txt.LinesCount - 1);
						txt.Selection.EndUpdate();
						txt.InsertText(current);
					}
				}
				e.Handled = true;
				break;
			case Keys.Left: case Keys.Back:
				if(txt.Selection.Start.iChar < 2 || (txt.Selection.Start.iChar == 2 && txt.SelectionLength == 0)) e.Handled = true;
				break;
			case Keys.Enter:
			case Keys.Cancel: // Mac num-block return key
				txt.GoEnd();
				string method = txt.Lines[txt.LinesCount - 1].Substring(2);

				string output = Execute(method);
				if(method.Equals("clear"))
				{
					txt.Clear();
					txt.AppendText("> ");
					txt.SelectionStart += 2;
					e.Handled = true;
					break;
				}

				if(output != null && output != "")
				{
					output = output;

					/*Place startpos = txt.Selection.Start;
					txt.AppendText('\n' + output);
					txt.GoEnd();//txt.SelectionStart += output.Length + 1;
					new Range(txt, startpos, txt.Selection.Start).SetStyle(OUTPUT_STYLE);*/

					//txt.AppendText('\n' + output);
					string[] lines = output.Split('\n');
					foreach(string line in lines)
						txt.AppendText('\n' + line);
					txt.GoEnd();
				}

				txt.AppendText("\n");
				txt.AppendText("> ");
				txt.SelectionStart += 3;
				//new Range(txt, 0, txt.Selection.Start.iLine, txt.Selection.Start.iChar, txt.Selection.Start.iLine).ReadOnly = true;
				e.Handled = true;
				break;
			}
		}
	}
}

