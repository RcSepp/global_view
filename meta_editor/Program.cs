using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace csharp_viewer
{
	class MainClass
	{
		public static string DATABASE_PATH = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "/work/mpas_060km/";
		//public static string DATABASE_PATH = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "/work/mpas_test/";
		//public static string DATABASE_PATH = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "/work/cinema/";

		public static void Main(string[] args)
		{
			Console.WriteLine("Parsing " + DATABASE_PATH + "image/info.json ...");

			// Parse meta data from info.json
			string name_pattern, pixel_format;
			Cinema.CinemaArgument[] arguments;
			Cinema.ParseCinemaDescriptor(DATABASE_PATH, out arguments, out name_pattern, out pixel_format);

			Console.WriteLine("Altering meta data files ...");

			//Font imgfont = new Font("Verdana", 50.0f);

			// Load images and image meta data by iterating all argument combinations
			int[] argidx = new int[arguments.Length];
			bool done;
			do
			{
				// Construct image file path && image meta file path from argidx[]
				object[] imagekey = new object[arguments.Length];
				String imagepath = name_pattern;
				String imagemetapath = name_pattern;
				for(int i = 0; i < arguments.Length; ++i)
				{
					imagekey[i] = arguments[i].values[argidx[i]];
					imagepath = imagepath.Replace("{" + arguments[i].name + "}", imagekey[i].ToString());
					imagemetapath = imagemetapath.Replace("{" + arguments[i].name + "}", imagekey[i].ToString());
				}
				imagepath = DATABASE_PATH + "image/" + imagepath;
				imagemetapath = DATABASE_PATH + "image/" + imagemetapath.Substring(0, imagemetapath.Length - "png".Length) + "json";

				// Deserialize or create empty Json object
				dynamic meta;
				if(File.Exists(imagemetapath))
				{
					StreamReader sr = new StreamReader(new FileStream(imagemetapath, FileMode.Open, FileAccess.Read));
					meta = JsonConvert.DeserializeObject(sr.ReadToEnd());
					sr.Close();
				}
				else
					meta = new JObject();

				// Load image
				Bitmap bmp = (Bitmap)Bitmap.FromFile(imagepath);


				// >>> Perform meta data manipulations

				if(meta.variables == null)
					meta.variables = new JObject();

				meta.variables.histogram = JToken.FromObject(new System.Collections.Generic.List<double>(CreateHistogram(bmp)));

				/*long theta = (long)arguments[2].values[argidx[2]];
				long phi = (long)arguments[1].values[argidx[1]];

				switch(theta)
				{
				case -180: meta.variables.madagascar_visible = phi >= -120 && phi <= 0; break;
				case -150: meta.variables.madagascar_visible = phi >= -120 && phi <= 30; break;
				case -120: meta.variables.madagascar_visible = phi >= -120 && phi <= 30; break;
				case  -90: meta.variables.madagascar_visible = true; break;
				case  -60: meta.variables.madagascar_visible = phi <= -120 || phi >= 30; break;
				case  -30: meta.variables.madagascar_visible = phi <= -150 || phi >= 60; break;
				case    0: meta.variables.madagascar_visible = phi <= -150 || phi >= 60; break;
				case   30: meta.variables.madagascar_visible = phi <= -180 || phi >= 90; break;
				case   60: meta.variables.madagascar_visible = false; break;
				case   90: meta.variables.madagascar_visible = false; break;
				case  120: meta.variables.madagascar_visible = false; break;
				case  150: meta.variables.madagascar_visible = phi >= -90 && phi <= 0; break;
				}*/

				/*// >>> Perform image manipulations

				Bitmap bmp = new Bitmap(256, 256);
				Graphics gfx = Graphics.FromImage(bmp);
				gfx.Clear(Color.Black);
				gfx.DrawString(((long)imagekey[2]).ToString(), imgfont, Brushes.White, 48.0f, 16.0f);
				gfx.DrawString(((long)imagekey[1]).ToString(), imgfont, Brushes.Aqua, 48.0f, 144.0f);
				gfx.Flush();
				bmp.Save(imagepath);*/

				// Serialize manipulated Json object
				StreamWriter sw = new StreamWriter(new FileStream(imagemetapath, FileMode.Create, FileAccess.Write));
				sw.Write(JsonConvert.SerializeObject(meta));
				sw.Close();

				// >>> Get next argument combination -> argidx[]

				done = true;
				for(int i = 0; i < arguments.Length; ++i)
				{
					if(++argidx[i] == arguments[i].values.Length)
						argidx[i] = 0;
					else
					{
						done = false;
						break;
					}
				}
			} while(!done);
		}

		private static double[] CreateHistogram(Bitmap bmp, int size = 256)
		{
			double[] histogram = new double[size];

			BitmapData bmpdata = bmp.LockBits(new Rectangle(Point.Empty, bmp.Size), System.Drawing.Imaging.ImageLockMode.ReadOnly, bmp.PixelFormat);
			byte[] bytes = new byte[bmpdata.Stride * bmpdata.Height];
			Marshal.Copy(bmpdata.Scan0, bytes, 0, bytes.Length);

			for(int i = 0; i < bytes.Length; i += 4)
			{
				double value = ColorToValue(bytes, i);
				if(!double.IsNaN(value))
				{
					int idx = (int)((double)size * value);
					++histogram[idx < size ? idx : size - 1];
				}
			}

			bmp.UnlockBits(bmpdata);

			// Normalize by pixel count
			double numpixels = (double)(bmp.Width * bmp.Height);
			for(int i = 0; i < size; ++i)
				histogram[i] /= numpixels;

			return histogram;
		}

		private static double ColorToValue(byte[] bytes, int idx)
		{
			int valueI = ((int)bytes[idx + 2])<<16 | ((int)bytes[idx + 1])<<8 | ((int)bytes[idx + 0]);
			if(valueI == 0)
				return double.NaN;
			return (double)(valueI-0x1)/(double)0xfffffe; // 0 is reserved as "nothing"
		}
	}
}
