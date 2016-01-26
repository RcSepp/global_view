using System;
using System.Drawing;
using System.Collections.Generic;
using System.IO;

using ExifLib;

namespace csharp_viewer
{
	public static class ImageLoader
	{
		public static IBitmap Load(string filename)
		{
		reattempt_load:
			try {
				if(filename.EndsWith(".im", StringComparison.OrdinalIgnoreCase))
					return ImImageLoader.Load(filename);
			}
			catch(OutOfMemoryException) {
				GC.WaitForPendingFinalizers();
				goto reattempt_load;
			}

			try {
				return new GdiBitmap((Bitmap)Bitmap.FromFile(filename));
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
		public static IBitmap Load(string filename, List<GLTextureStream.ImageMetaData> meta)
		{
		reattempt_load:
			try {
				if(filename.EndsWith(".im", StringComparison.OrdinalIgnoreCase))
					return ImImageLoader.Load(filename);
			}
			catch(OutOfMemoryException) {
				GC.WaitForPendingFinalizers();
				goto reattempt_load;
			}

			Dictionary<ExifTags, GLTextureStream.ImageMetaData> exifdict = null;
			if(filename.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
				try
				{
					using(ExifReader reader = new ExifReader(filename))
					{
						//foreach (ExifTags tag in Enum.GetValues(typeof(ExifTags)))
						//	ReadExifValue(reader, tag, ref meta);

						/*ReadExifRational(reader, ExifTags.ExposureTime, ref meta);
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
						ReadExifString(reader, ExifTags.LensModel, ref meta);*/


						exifdict = new Dictionary<ExifTags,GLTextureStream.ImageMetaData>();
						foreach (ExifTags tag in Enum.GetValues(typeof(ExifTags)))
						{
							if (exifdict.ContainsKey(tag))
								continue;

							object val;
							if (reader.GetTagValue(tag, out val))
							{
								GLTextureStream.ImageMetaData m;
								if (val is string)
									m = new GLTextureStream.ImageMetaData(tag.ToString(), 0.0f, (string)val);
								else if (val is Array)
									continue; // Skip arrays
								else
									m = new GLTextureStream.ImageMetaData(tag.ToString(), (float)(dynamic)val, val.ToString());

								exifdict.Add(tag, m);
								meta.Add(m);
							}
						}

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

		reattempt_load2:
			try {
				Bitmap bmp = (Bitmap)Bitmap.FromFile(filename);
				GLTextureStream.ImageMetaData orientation;
				if (exifdict != null && exifdict.TryGetValue(ExifTags.Orientation, out orientation))
				{
					switch((byte)orientation.value)
					{
						case 1: break;
						case 2: bmp.RotateFlip(RotateFlipType.RotateNoneFlipX); break;
						case 3: bmp.RotateFlip(RotateFlipType.Rotate180FlipNone); break;
						case 4: bmp.RotateFlip(RotateFlipType.RotateNoneFlipY); break;
						case 5: bmp.RotateFlip(RotateFlipType.Rotate90FlipX); break;
						case 6: bmp.RotateFlip(RotateFlipType.Rotate90FlipNone); break;
						case 7: bmp.RotateFlip(RotateFlipType.Rotate90FlipY); break;
						case 8: bmp.RotateFlip(RotateFlipType.Rotate270FlipNone); break;
					}
				}
				return new GdiBitmap(bmp);
			}
			catch(OutOfMemoryException) {
				GC.WaitForPendingFinalizers();
				goto reattempt_load2;
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
		public static IBitmap Load(string filename)
		{
			FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);

			StreamReader sr = new StreamReader(fs);

			string readline = sr.ReadLine();
			if(!readline.StartsWith("Image type: "))
			{
				fs.Close();
				return null;
			}
			//string imageType = readline.Substring("Image type: ".Length);

			readline = sr.ReadLine();
			if(!readline.StartsWith("Name: "))
			{
				fs.Close();
				return null;
			}
			//string name = readline.Substring("Name: ".Length);

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
			//string numImages = readline.Substring("File size (no of images): ".Length);

			fs.Position = 0x200;

			BinaryReader br = new BinaryReader(fs);
			int numpixels = width * height;
			float[] valuesFlippedY = new float[numpixels];
			for(int y = 0; y < height; ++y)
				for(int x = 0; x < width; ++x)
					valuesFlippedY[(height - y - 1) * width + x] = br.ReadSingle();

			fs.Close();

			/*Bitmap bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

			System.Drawing.Imaging.BitmapData bmpdata = bmp.LockBits(new Rectangle(Point.Empty, bmp.Size), System.Drawing.Imaging.ImageLockMode.WriteOnly, bmp.PixelFormat);
			System.Runtime.InteropServices.Marshal.Copy(valuesFlippedY, 0, bmpdata.Scan0, valuesFlippedY.Length);
			bmp.UnlockBits(bmpdata);

			valuesFlippedY = null;

			return bmp;*/

			return new F32Bitmap(width, height, valuesFlippedY);
		}
	}

	public interface IBitmap
	{
		OpenTK.Graphics.OpenGL.PixelFormat pixelFormat { get; }
		int Width { get; }
		int Height { get; }
		float maxDepth { get; }
		void TexImage2D(OpenTK.Graphics.OpenGL.PixelInternalFormat destformat);
		void Downscale(int newwidth, int newheight, System.Drawing.Drawing2D.InterpolationMode mode);
		void Dispose();
	}
	public class GdiBitmap : IBitmap
	{
		private Bitmap bmp;

		public GdiBitmap(Bitmap bmp)
		{
			this.bmp = bmp;
		}
		public GdiBitmap(string filename)
		{
			this.bmp = new Bitmap(filename);
		}

		#region IBitmap implementation
		public void TexImage2D(OpenTK.Graphics.OpenGL.PixelInternalFormat destformat)
		{
			System.Drawing.Imaging.BitmapData bmpdata = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, bmp.PixelFormat);
			OpenTK.Graphics.OpenGL.GL.TexImage2D(OpenTK.Graphics.OpenGL.TextureTarget.Texture2D, 0, destformat, bmpdata.Width, bmpdata.Height, 0, pixelFormat, OpenTK.Graphics.OpenGL.PixelType.UnsignedByte, bmpdata.Scan0);
			bmp.UnlockBits(bmpdata);
		}
		public void Downscale(int newwidth, int newheight, System.Drawing.Drawing2D.InterpolationMode mode)
		{
			Bitmap originalBmp = bmp;
			bmp = new Bitmap(newwidth, newheight, originalBmp.PixelFormat);
			Graphics gfx = Graphics.FromImage(bmp);
			gfx.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
			gfx.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;
			gfx.InterpolationMode = mode;
			gfx.DrawImage(originalBmp, new Rectangle(0, 0, newwidth, newheight));
			gfx.Flush();
			originalBmp.Dispose();
			originalBmp = null;
		}

		public OpenTK.Graphics.OpenGL.PixelFormat pixelFormat {
			get {
				switch(bmp.PixelFormat)
				{
				case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
					return OpenTK.Graphics.OpenGL.PixelFormat.Bgr;
				case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
					return OpenTK.Graphics.OpenGL.PixelFormat.Bgra;
				default:
					return (OpenTK.Graphics.OpenGL.PixelFormat)(-1);
				}
			}
		}

		public float maxDepth { get { return 0.0f; } }
		public int Width { get { return bmp.Width; } }
		public int Height { get { return bmp.Height; } }

		public void Dispose()
		{
			bmp.Dispose();
			bmp = null;
		}
		#endregion
	}
	public class F32Bitmap : IBitmap
	{
		private int width, height;
		float[] values;
		float maxdepth;

		public F32Bitmap(int width, int height, float[] values)
		{
			this.width = width;
			this.height = height;
			this.values = values;

			maxdepth = float.MinValue;
			for(int i = 0, numpixels = width * height; i < numpixels; ++i)
				maxdepth = Math.Max(maxdepth, values[i]);
		}

		#region IBitmap implementation
		public void TexImage2D(OpenTK.Graphics.OpenGL.PixelInternalFormat destformat)
		{
			OpenTK.Graphics.OpenGL.GL.TexImage2D<float>(OpenTK.Graphics.OpenGL.TextureTarget.Texture2D, 0, OpenTK.Graphics.OpenGL.PixelInternalFormat.R32f, width, height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Red, OpenTK.Graphics.OpenGL.PixelType.Float, values);
		}
		public void Downscale(int newwidth, int newheight, System.Drawing.Drawing2D.InterpolationMode mode)
		{
			float[] newvalues = new float[newwidth * newheight];

			// Nearest neighbour sampling
			for(int y = 0; y < newheight; ++y)
				for(int x = 0; x < newwidth; ++x)
				{
					float src_xf = (float)x * (float)width / (float)newwidth;
					int src_xi = (int)src_xf;
					if(src_xf - (float)src_xi >= 0.5f)
						++src_xi;

					float src_yf = (float)y * (float)height / (float)newheight;
					int src_yi = (int)src_yf;
					if(src_yf - (float)src_yi >= 0.5f)
						++src_yi;

					newvalues[y * newwidth + x] = values[src_yi * width + src_xi];
				}

			/*// Linear sampling
			for(int y = 0; y < newheight; ++y)
				for(int x = 0; x < newwidth; ++x)
				{
					float src_xf = (float)x * (float)width / (float)newwidth;
					int src_xi = (int)src_xf;
					src_xf = src_xf - (float)src_xi;

					float src_yf = (float)y * (float)height / (float)newheight;
					int src_yi = (int)src_yf;
					src_yf = src_yf - (float)src_yi;

					newvalues[y * newwidth + x] = values[src_yi * width + src_xi] * src_xf * src_yf;
					newvalues[y * newwidth + x] += values[src_yi * width + src_xi + 1] * (1.0f - src_xf) * src_yf;
					newvalues[y * newwidth + x] += values[++src_yi * width + src_xi] * src_xf * (1.0f - src_yf);
					newvalues[y * newwidth + x] += values[src_yi * width + src_xi + 1] * (1.0f - src_xf) * (1.0f - src_yf);
				}*/

			width = newwidth;
			height = newheight;
			values = null;
			values = newvalues;
		}

		public OpenTK.Graphics.OpenGL.PixelFormat pixelFormat {
			get {
				throw new NotImplementedException();
			}
		}

		public float maxDepth { get { return maxdepth; } }
		public int Width { get { return width; } }
		public int Height { get { return height; } }

		public void Dispose()
		{
			values = null;
		}
		#endregion
	}
}

