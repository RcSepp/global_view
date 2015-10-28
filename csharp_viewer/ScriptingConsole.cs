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
		private TextStyle OUTPUT_STYLE = new TextStyle(Brushes.Brown, null, FontStyle.Regular);

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
				break;

			case Keys.Up:
				{
					string current = txt.Lines[txt.LinesCount - 1];
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
					string current = txt.Lines[txt.LinesCount - 1];
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
				if(txt.Selection.Start.iChar == 0 && txt.SelectionLength == 0) e.Handled = true;
				break;
			case Keys.Enter:
			case Keys.Cancel: // Mac num-block return key
				txt.GoEnd();
				string method = txt.Lines[txt.LinesCount - 1];

				string output = Execute(method);

				if(output != null && output != "")
				{
					Place startpos = txt.Selection.Start;
					txt.AppendText('\n' + output);
					txt.GoEnd();//txt.SelectionStart += output.Length + 1;
					new Range(txt, startpos, txt.Selection.Start).SetStyle(OUTPUT_STYLE);
				}

				if(e.KeyCode == Keys.Cancel)
				{
					txt.AppendText("\n");
					++txt.SelectionStart;
				}
				e.Handled = true;
				break;
			}

			if((e.Modifiers & Keys.Alt) != 0)
				switch(e.KeyCode)
				{
				case Keys.A:
					// Select whole line
					txt.Selection = new Range(txt, 0, txt.Selection.Start.iLine, txt.GetLineLength(txt.Selection.Start.iLine), txt.Selection.Start.iLine);
					e.Handled = true;
					break;
				case Keys.C: // Mac bugfix
					Clipboard.SetText(txt.SelectedText);
					e.Handled = true;
					break;
				case Keys.V: // Mac bugfix
					txt.Paste();
					e.Handled = true;
					break;
				case Keys.X: // Mac bugfix
					Clipboard.SetText(txt.SelectedText);
					txt.ClearSelected();
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
			if(e.KeyChar == 13 || (e.KeyChar != ' ' && e.KeyChar != '\t' && !char.IsControl(e.KeyChar) && (Control.ModifierKeys & ~Keys.Shift) == 0)) txt.InsertText(e.KeyChar.ToString()); // Bugfix
		}

		/*private void TextChanged(object sender, TextChangedEventArgs e)
		{
			new Range(txt, 0, 0, 0, txt.LinesCount - 1).ReadOnly = true;
		}*/
	}
}

