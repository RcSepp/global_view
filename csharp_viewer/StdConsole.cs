using System;
using System.Threading;

namespace csharp_viewer
{
	public class StdConsole : Console
	{
		private string current = "";
		private bool closing = false;

		public void Run(bool async = true)
		{
			if(async)
			{
				Thread consoleLoopThread = new Thread(ConsoleLoop);
				consoleLoopThread.Start();
			}
			else
				ConsoleLoop();
		}

		public void Close()
		{
			closing = true;
			System.Console.Write('\0');
		}

		public void ConsoleLoop()
		{
			while(!closing)
			{
				ConsoleKeyInfo key = System.Console.ReadKey();
				if(char.IsControl(key.KeyChar))
				{
					switch(key.Key)
					{
					case ConsoleKey.UpArrow:
						if(HistoryUp(ref current))
						{
							int top = System.Console.CursorTop;
							System.Console.CursorLeft = 0;
							for(int i = 0; i < System.Console.BufferWidth; ++i)
								System.Console.Write(" ");
							System.Console.SetCursorPosition(0, top);
							System.Console.Write(current);
						}
						break;
					case ConsoleKey.DownArrow:
						if(HistoryDown(ref current))
						{
							int top = System.Console.CursorTop;
							for(int i = 0; i < System.Console.BufferWidth; ++i)
								System.Console.Write(" ");
							System.Console.SetCursorPosition(0, top);
							System.Console.Write(current);
						}
						break;

					case ConsoleKey.LeftArrow:
						System.Console.SetCursorPosition(System.Console.CursorLeft - 1, System.Console.CursorTop);
						break;
					case ConsoleKey.RightArrow:
						System.Console.SetCursorPosition(System.Console.CursorLeft + 1, System.Console.CursorTop);
						break;

					case ConsoleKey.Enter:
						string output = Execute(current);

						if(output != null && output != "")
						{
							System.Console.ForegroundColor = ConsoleColor.DarkMagenta;
							System.Console.WriteLine(output);
							System.Console.ResetColor();
						}

						current = "";
						break;
					}
				}
				else if(System.Console.CursorLeft < current.Length)
					current.Insert(System.Console.CursorLeft, "" + key.KeyChar);
				else
					current += key.KeyChar;
				System.Windows.Forms.Application.DoEvents();
			}
		}
	}
}

