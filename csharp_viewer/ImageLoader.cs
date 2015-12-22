using System;
using System.Drawing;
using System.Collections.Generic;
using System.IO;

using ExifLib;

namespace csharp_viewer
{
	public static class ImageLoader
	{
		public static Bitmap Load(string filename)
		{
			if(filename.EndsWith(".im", StringComparison.OrdinalIgnoreCase))
				return ImImageLoader.Load(filename);

			try {
				return (Bitmap)Bitmap.FromFile(filename);
			}
			catch
			{
				int foo = filename.LastIndexOf('/');
				string newfilename = filename.Substring(0, foo + 1) + "damaged_" + filename.Substring(foo + 1);
				System.IO.File.Move(filename, newfilename);
				return null;
			}
			//return (Bitmap)Bitmap.FromFile(filename);
		}

		public static Bitmap Load(string filename, List<GLTextureStream.ImageMetaData> meta)
		{
			if(filename.EndsWith(".im", StringComparison.OrdinalIgnoreCase))
				return ImImageLoader.Load(filename);

			if(filename.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
				try
				{
					using(var reader = new ExifReader(filename))
					{
						int[] rational;
						UInt16 word;
						string str;
						if(reader.GetTagValue(ExifTags.ApertureValue, out rational))
						{
							float value = (float)rational[0] / (float)rational[1];
							meta.Add(new GLTextureStream.ImageMetaData("Aperture", value, value.ToString()));
						}
						if(reader.GetTagValue(ExifTags.PhotographicSensitivity, out word))
						{
							/*Type type = obj.GetType();
							while (type != null)
							{
								System.Console.WriteLine(type.Name);
								type = type.BaseType;
							}*/
							meta.Add(new GLTextureStream.ImageMetaData("ISO", (float)word, word.ToString()));
						}
						if(reader.GetTagValue(ExifTags.DateTime, out str))
						{
							DateTime date = DateTime.ParseExact(str, "yyyy:MM:dd HH:mm:ss", System.Globalization.CultureInfo.CurrentCulture);
							meta.Add(new GLTextureStream.ImageMetaData("Date", (float)date.Ticks, date.ToShortDateString()));
						}

						/*object obj;
						if(reader.GetTagValue(ExifTags.PhotographicSensitivity, out obj))
						{
							Type type = obj.GetType();
							while (type != null)
							{
								System.Console.WriteLine(type.Name);
								type = type.BaseType;
							}
							int abc = 0;
						}*/
					}
				}
				catch
				{
				}

			try {
				return (Bitmap)Bitmap.FromFile(filename);
			}
			catch
			{
				int foo = filename.LastIndexOf('/');
				string newfilename = filename.Substring(0, foo + 1) + "damaged_" + filename.Substring(foo + 1);
				System.IO.File.Move(filename, newfilename);
				return null;
			}
			//return (Bitmap)Bitmap.FromFile(filename);
		}
	}

	public static class ImImageLoader
	{
		public static Bitmap Load(string filename)
		{
			FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);

			StreamReader sr = new StreamReader(fs);

			string readline = sr.ReadLine();
			if(!readline.StartsWith("Image type: "))
			{
				fs.Close();
				return null;
			}
			string imageType = readline.Substring("Image type: ".Length);

			readline = sr.ReadLine();
			if(!readline.StartsWith("Name: "))
			{
				fs.Close();
				return null;
			}
			string name = readline.Substring("Name: ".Length);

			readline = sr.ReadLine();
			if(!readline.StartsWith("Image size (x*y): "))
			{
				fs.Close();
				return null;
			}
			string[] imageSize = readline.Substring("Image size (x*y): ".Length).Split('*');
			int width, height;
			if(imageSize.Length != 2 || !int.TryParse(imageSize[0], out width) || width <= 0 || !int.TryParse(imageSize[1], out height) || height <= 0)
			{
				// Error: Illegal value for 'Image size'
				fs.Close();
				return null;
			}

			readline = sr.ReadLine();
			if(!readline.StartsWith("File size (no of images): "))
			{
				fs.Close();
				return null;
			}
			string numImages = readline.Substring("File size (no of images): ".Length);

			fs.Position = 0x200;

			BinaryReader br = new BinaryReader(fs);
			int numpixels = width * height;
			float[] values = new float[numpixels];
			byte[] data = new byte[4 * numpixels];
			float vmin = float.MaxValue, vmax = float.MinValue;
			for(int i = 0; i < numpixels; ++i)
			{
				values[i] = br.ReadSingle();
				vmin = Math.Min(vmin, values[i]);
				vmax = Math.Max(vmax, values[i]);
			}

			vmin = 0.0f;
			vmax = 1.0f;//256.0f;

			float vscale = 255.0f / (vmax - vmin);
			for(int y = 0; y < height; ++y)
				for(int x = 0; x < width; ++x)
				{
					int i = y * width + x;
					data[4 * i + 0] = data[4 * i + 1] = data[4 * i + 2] = (byte)((values[(height - y - 1) * width + x] - vmin) * vscale);
					data[4 * i + 3] = 255;
				}

			fs.Close();

			Bitmap bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

			System.Drawing.Imaging.BitmapData bmpdata = bmp.LockBits(new Rectangle(Point.Empty, bmp.Size), System.Drawing.Imaging.ImageLockMode.WriteOnly, bmp.PixelFormat);
			System.Runtime.InteropServices.Marshal.Copy(data, 0, bmpdata.Scan0, data.Length);
			bmp.UnlockBits(bmpdata);

			return bmp;
		}
	}
}

