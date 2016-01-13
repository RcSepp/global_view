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

		private static void ReadExifRational(ExifReader reader, ExifTags tag, ref List<GLTextureStream.ImageMetaData> meta)
		{
			int[] rational;
			if (reader.GetTagValue(tag, out rational))
			{
				float value = (float)rational[0] / (float)rational[1];
				meta.Add(new GLTextureStream.ImageMetaData(tag.ToString(), value, value.ToString()));
			}
		}
		private static void ReadExifShort(ExifReader reader, ExifTags tag, ref List<GLTextureStream.ImageMetaData> meta)
		{
			UInt16 num;
			if (reader.GetTagValue(tag, out num))
				meta.Add(new GLTextureStream.ImageMetaData(tag.ToString(), (float)num, num.ToString()));
		}
		private static void ReadExifLong(ExifReader reader, ExifTags tag, ref List<GLTextureStream.ImageMetaData> meta)
		{
			UInt32 num;
			if (reader.GetTagValue(tag, out num))
				meta.Add(new GLTextureStream.ImageMetaData(tag.ToString(), (float)num, num.ToString()));
		}
		private static void ReadExifString(ExifReader reader, ExifTags tag, ref List<GLTextureStream.ImageMetaData> meta)
		{
			string str;
			if (reader.GetTagValue(tag, out str))
				meta.Add(new GLTextureStream.ImageMetaData(tag.ToString(), 0.0f, str));
		}
		private static void ReadExifDate(ExifReader reader, ExifTags tag, ref List<GLTextureStream.ImageMetaData> meta)
		{
			string str;
			if (reader.GetTagValue(tag, out str))
			{
				DateTime date = DateTime.ParseExact(str, "yyyy:MM:dd HH:mm:ss", System.Globalization.CultureInfo.CurrentCulture);
				meta.Add(new GLTextureStream.ImageMetaData(tag.ToString(), (float)date.Ticks, date.ToShortDateString()));
			}
		}
		public static Bitmap Load(string filename, List<GLTextureStream.ImageMetaData> meta)
		{
			if(filename.EndsWith(".im", StringComparison.OrdinalIgnoreCase))
				return ImImageLoader.Load(filename);

			if(filename.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
				try
				{
					using(ExifReader reader = new ExifReader(filename))
					{
						//foreach (ExifTags tag in Enum.GetValues(typeof(ExifTags)))
						//	ReadExifValue(reader, tag, ref meta);

						ReadExifRational(reader, ExifTags.ExposureTime, ref meta);
						ReadExifRational(reader, ExifTags.FNumber, ref meta);
						ReadExifShort(reader, ExifTags.ExposureProgram, ref meta);
						ReadExifShort(reader, ExifTags.PhotographicSensitivity, ref meta);
						ReadExifShort(reader, ExifTags.SensitivityType, ref meta);
						ReadExifLong(reader, ExifTags.RecommendedExposureIndex, ref meta);
						//ReadExifValue(reader, ExifTags.ExifVersion, ref meta); //undefined
						ReadExifDate(reader, ExifTags.DateTimeOriginal, ref meta);
						ReadExifDate(reader, ExifTags.DateTimeDigitized, ref meta);
						//ReadExifValue(reader, ExifTags.ComponentsConfiguration, ref meta); //undefined
						ReadExifRational(reader, ExifTags.CompressedBitsPerPixel, ref meta);
						ReadExifRational(reader, ExifTags.BrightnessValue, ref meta);
						ReadExifRational(reader, ExifTags.ExposureBiasValue, ref meta);
						ReadExifRational(reader, ExifTags.MaxApertureValue, ref meta);
						ReadExifShort(reader, ExifTags.MeteringMode, ref meta);
						ReadExifShort(reader, ExifTags.LightSource, ref meta);
						ReadExifShort(reader, ExifTags.Flash, ref meta);
						ReadExifRational(reader, ExifTags.FocalLength, ref meta);
						//ReadExifValue(reader, ExifTags.MakerNote, ref meta); //undefined
						//ReadExifValue(reader, ExifTags.UserComment, ref meta); //comment
						//ReadExifValue(reader, ExifTags.FlashpixVersion, ref meta); //undefined
						ReadExifShort(reader, ExifTags.ColorSpace, ref meta);
						ReadExifLong(reader, ExifTags.PixelXDimension, ref meta);
						ReadExifLong(reader, ExifTags.PixelYDimension, ref meta);
						ReadExifShort(reader, ExifTags.CustomRendered, ref meta);
						ReadExifShort(reader, ExifTags.ExposureMode, ref meta);
						ReadExifShort(reader, ExifTags.WhiteBalance, ref meta);
						ReadExifRational(reader, ExifTags.DigitalZoomRatio, ref meta);
						ReadExifShort(reader, ExifTags.FocalLengthIn35mmFilm, ref meta);
						ReadExifShort(reader, ExifTags.SceneCaptureType, ref meta);
						ReadExifShort(reader, ExifTags.Contrast, ref meta);
						ReadExifShort(reader, ExifTags.Saturation, ref meta);
						ReadExifShort(reader, ExifTags.Sharpness, ref meta);
						ReadExifRational(reader, ExifTags.LensSpecification, ref meta);
						ReadExifString(reader, ExifTags.LensModel, ref meta);

						/*int[] rational;
						UInt16 word;
						string str;
						if(reader.GetTagValue(ExifTags.ApertureValue, out rational))
						{
							float value = (float)rational[0] / (float)rational[1];
							meta.Add(new GLTextureStream.ImageMetaData("Aperture", value, value.ToString()));
						}
						if (reader.GetTagValue(ExifTags.BrightnessValue, out rational))
						{
							float value = (float)rational[0] / (float)rational[1];
							meta.Add(new GLTextureStream.ImageMetaData("BrightnessValue", value, value.ToString()));
						}
						if (reader.GetTagValue(ExifTags.ExposureBiasValue, out rational))
						{
							float value = (float)rational[0] / (float)rational[1];
							meta.Add(new GLTextureStream.ImageMetaData("ExposureBiasValue", value, value.ToString()));
						}
						if(reader.GetTagValue(ExifTags.PhotographicSensitivity, out word))
						{
							meta.Add(new GLTextureStream.ImageMetaData("ISO", (float)word, word.ToString()));
						}
						if(reader.GetTagValue(ExifTags.DateTime, out str))
						{
							DateTime date = DateTime.ParseExact(str, "yyyy:MM:dd HH:mm:ss", System.Globalization.CultureInfo.CurrentCulture);
							meta.Add(new GLTextureStream.ImageMetaData("Date", (float)date.Ticks, date.ToShortDateString()));
						}*/

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
			float[] values = new float[numpixels], valuesFlippedY = new float[numpixels];
			float vmin = float.MaxValue, vmax = float.MinValue;
			for(int i = 0; i < numpixels; ++i)
			{
				values[i] = br.ReadSingle();
				vmin = Math.Min(vmin, values[i]);
				vmax = Math.Max(vmax, values[i]);
			}

			/*float vscale = 255.0f / (vmax - vmin);
			for(int y = 0; y < height; ++y)
				for(int x = 0; x < width; ++x)
					valuesFlippedY[y * width + x] = (values[(height - y - 1) * width + x] - vmin) * vscale;*/

			for(int y = 0; y < height; ++y)
				for(int x = 0; x < width; ++x)
					valuesFlippedY[y * width + x] = values[(height - y - 1) * width + x];

			fs.Close();

			Bitmap bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

			System.Drawing.Imaging.BitmapData bmpdata = bmp.LockBits(new Rectangle(Point.Empty, bmp.Size), System.Drawing.Imaging.ImageLockMode.WriteOnly, bmp.PixelFormat);
			System.Runtime.InteropServices.Marshal.Copy(valuesFlippedY, 0, bmpdata.Scan0, valuesFlippedY.Length);
			bmp.UnlockBits(bmpdata);

			values = null;
			valuesFlippedY = null;

			return bmp;
		}
	}
}

