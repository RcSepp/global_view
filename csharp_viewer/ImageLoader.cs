using System;
using System.Drawing;
using System.Collections.Generic;

using ExifLib;

namespace csharp_viewer
{
	public static class ImageLoader
	{
		public static Bitmap Load(string filename)
		{
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
}

