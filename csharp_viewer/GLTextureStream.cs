//#define DEBUG_GLTEXTURESTREAM
//#define ENABLE_TRADEOFF // Tradeoff scales image size by relative remaining memory // Warning: Tradeoff doesn't work with prefetching!

using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Collections.Generic;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace csharp_viewer
{
	public class GLTextureStream
	{
		public struct ImageMetaData
		{
			public string name, strValue;
			public float value;
			public ImageMetaData(string name, float value, string strValue)
			{
				this.name = name;
				this.value = value;
				this.strValue = strValue;
			}
		}
		public delegate bool ReadImageMetaDataDelegate(TransformedImage image, ImageMetaData[] meta);

		private Bitmap bmpFileNotFound;
		private GLTexture2D texFileNotFound;

		private int currentmemorysize, memorysizelimit;

		private class AsyncImageLoader
		{
			public class Image : IComparable<Image> //EDIT: Make private
			{
				public TransformedImage.ImageLayer image; //EDIT: Make private
				private int width, height;
				public float sizePriority; //EDIT: Make private
				public int memory; //EDIT: Make private
				private bool metadataLoaded;

				public Image(TransformedImage.ImageLayer image)
				{
					this.image = image;
					sizePriority = 1.0f;
					memory = 0;
					metadataLoaded = false;
				}

				public int RequiredMemory(float tradeoffRenderSizeFactor, bool forceOriginalSize)
				{
					// Returns (guessed) memory required for Load()

					if(image.renderPriority <= 0 || image.texIsStatic) // If image doesn't need to be loaded
					{
						return -memory; // Return memory gain achieved by unloading this image
					}

					int factor = 1 + (image.depth_filename != null ? 1 : 0) + (image.lum_filename != null ? 1 : 0);

					if(memory > 0) // If image already loaded
					{
						// Recompute image size
						int newwidth, newheight;
						if (forceOriginalSize || image.renderWidth >= image.originalWidth || image.renderHeight >= image.originalHeight)
						{
							newwidth = image.originalWidth;
							newheight = image.originalHeight;
						}
						else
						{
							float fw = (float)image.renderWidth / (float)image.originalWidth;
							float fh = (float)image.renderHeight / (float)image.originalHeight;

							if(fw > fh)
							{
								newwidth = image.renderWidth;
								newheight = (int)((float)image.originalHeight * fw);
							}
							else
							{
								newheight = image.renderHeight;
								newwidth = (int)((float)image.originalWidth * fh);
							}
						}

						// Compare magnitude of change
						float size_diff_factor = (float)newwidth / (float)this.width;
						if(size_diff_factor > 2.0f || size_diff_factor < 0.5f)
							return newwidth * newheight * factor - memory; // Return memory gained or lost by resolution change

						return 0; // Image considered unchanged. Return no extra memory required
					}

					int renderWidth = (int)((float)image.renderWidth * tradeoffRenderSizeFactor);
					int renderHeight = (int)((float)image.renderHeight * tradeoffRenderSizeFactor);

					if(image.originalWidth == 0) // If original size is unknown (because image hasn't been loaded so far)
						return renderWidth * renderHeight * factor;

					// Compute image size
					int width, height; // width and height are only computed locally
					if (forceOriginalSize || renderWidth >= image.originalWidth || renderHeight >= image.originalHeight)
					{
						width = image.originalWidth;
						height = image.originalHeight;
					}
					else
					{
						float fw = (float)renderWidth / (float)image.originalWidth;
						float fh = (float)renderHeight / (float)image.originalHeight;

						if(fw > fh)
						{
							width = renderWidth;
							height = (int)((float)image.originalHeight * fw);
						}
						else
						{
							height = renderHeight;
							width = (int)((float)image.originalWidth * fh);
						}
					}

					return width * height * factor;
				}
				public bool MemoryDisposable()
				{
					return memory > 0 && image.renderPriority <= 0;
				}
				public bool MemoryShrinkable(bool forceOriginalSize)
				{
					if(memory > 0)
					{
						int newwidth;
						if (forceOriginalSize || image.renderWidth >= image.originalWidth || image.renderHeight >= image.originalHeight)
							newwidth = image.originalWidth;
						else
						{
							float fw = (float)image.renderWidth / (float)image.originalWidth;
							float fh = (float)image.renderHeight / (float)image.originalHeight;

							if(fw > fh)
								newwidth = image.renderWidth;
							else
								newwidth = (int)((float)image.originalWidth * fh);
						}

						float size_diff_factor = (float)newwidth / (float)this.width;
						return size_diff_factor < 0.5f;
					}

					return false;
				}

				/*private object DeserializePropertyItem(PropertyItem prop)
				{
					int[] values;
					switch(prop.Type)
					{
					case 2:
						return System.Text.Encoding.UTF8.GetString(prop.Value);
					case 3:
						values = new int[prop.Value.Length / 2];
						for(int i = 0; i < values.Length; ++i)
							values[i] = (int)BitConverter.ToUInt16(prop.Value, 2 * i);
						return values;
					case 4:
						values = new int[prop.Value.Length / 4];
						for(int i = 0; i < values.Length; ++i)
							values[i] = (int)BitConverter.ToUInt32(prop.Value, 4 * i);
						return values;
					default:
						return "<" + prop.Type + ">";
					}
				}

				public int Load(float tradeoffRenderSizeFactor, GLTexture2D texFileNotFound, ReadImageMetaDataDelegate ReadImageMetaData)
				{
					if(image.texIsStatic || !File.Exists(image.filename))
					{
						image.tex = texFileNotFound;
						image.texIsStatic = true;
						return memory = 0;
					}

					//try {
					//	Bitmap.FromFile(image.filename);
					//}
					//catch(Exception ex)
					//{
					//	int foo = image.filename.LastIndexOf('/');
					//	string newfilename = image.filename.Substring(0, foo + 1) + "damaged_" + image.filename.Substring(foo + 1);
					//	File.Move(image.filename, newfilename);
					//
					//	image.tex = texFileNotFound;
					//	image.texIsStatic = true;
					//	return memory = 0;
					//}

					// Load image from disk
					Bitmap newbmp = (Bitmap)Bitmap.FromFile(image.filename);
					++GLTextureStream.foo;

					// Read metadata
					if(!metadataLoaded && ReadImageMetaData != null)
					{
						metadataLoaded = true;
						List<ImageMetaData> meta = new List<ImageMetaData>();

						foreach(PropertyItem prop in newbmp.PropertyItems)
						{
							string name = null;
							switch(prop.Id)
							{
							case 0x010F: name = "EquipmentVendor"; break;
							case 0x0110: name = "EquipmentModel"; break;
							case 0x0112: name = "Orientation"; break;
							case 0x011A:
								{
									uint numerator = BitConverter.ToUInt32(prop.Value, 0);
									uint denominator = BitConverter.ToUInt32(prop.Value, 4);
									float value = (float)numerator / (float)denominator;
									meta.Add(new ImageMetaData("ResX", value, value.ToString() + " DPI"));
								}
								break;
							case 0x011B:
								{
									uint numerator = BitConverter.ToUInt32(prop.Value, 0);
									uint denominator = BitConverter.ToUInt32(prop.Value, 4);
									float value = (float)numerator / (float)denominator;
									meta.Add(new ImageMetaData("ResY", value, value.ToString() + " DPI"));
								}
								break;
							case 0x0132:
								string datestr = System.Text.Encoding.UTF8.GetString(prop.Value, 0, prop.Value.Length - 1);
								DateTime date = DateTime.ParseExact(datestr, "yyyy:MM:dd HH:mm:ss", System.Globalization.CultureInfo.CurrentCulture);
								meta.Add(new ImageMetaData("Date", (float)date.Ticks, date.ToString()));
								break;
							case 0x829A:
								{
									uint numerator = BitConverter.ToUInt32(prop.Value, 0);
									uint denominator = BitConverter.ToUInt32(prop.Value, 4);
									float value = (float)numerator / (float)denominator;
									meta.Add(new ImageMetaData("ExposureTime", value, denominator == 1 ? numerator.ToString() : numerator.ToString() + " / " + denominator.ToString()));
								}
								break;
							case 0x829D:
								{
									uint numerator = BitConverter.ToUInt32(prop.Value, 0);
									uint denominator = BitConverter.ToUInt32(prop.Value, 4);
									float value = (float)numerator / (float)denominator;
									meta.Add(new ImageMetaData("FNumber", value, value.ToString()));
								}
								break;

							case 0x0100: // PropertyTagImageWidth
							case 0x0101: // PropertyTagImageHeight
							case 0x013E: // PropertyTagWhitePoint
							case 0x013F: // PropertyTagPrimaryChromaticities
							case 0x0128: // PropertyTagResolutionUnit
							case 0x0301: // PropertyTagGamma
							case 0x5110: // PropertyTagPixelUnit
							case 0x5111: // PropertyTagPixelPerUnitX
							case 0x5112: // PropertyTagPixelPerUnitY
								break;

							case 0x9286: //PropertyTagExifUserComment
								if(prop.Type == 2)
								{
									string value = System.Text.Encoding.UTF8.GetString(prop.Value);
									System.Console.WriteLine(string.Format(value));
								}
								break;
							
							default:
								System.Console.WriteLine(string.Format("{0:X}: {1}", prop.Id, DeserializePropertyItem(prop)));
								break;
							}
							if(name == null)
								continue;

							//int[] values;
							switch(prop.Type)
							{
							case 2:
								string strValue = System.Text.Encoding.UTF8.GetString(prop.Value);
								meta.Add(new ImageMetaData(name, (float)strValue.GetHashCode(), strValue));
								break;
							case 3:
								if(prop.Value.Length == 2)
								{
									ushort value = BitConverter.ToUInt16(prop.Value, 0);
									meta.Add(new ImageMetaData(name, (float)value, value.ToString()));
								}
								break;
							}
						}

						if(meta.Count != 0)
							ReadImageMetaData(image, meta.ToArray());
					}

					// Compute original dimensions
					image.originalWidth = newbmp.Width;
					image.originalHeight = newbmp.Height;
					image.originalAspectRatio = (float)image.originalWidth / (float)image.originalHeight;

					int renderWidth, renderHeight;
					if(memory > 0)
					{
						renderWidth = image.renderWidth;
						renderHeight = image.renderHeight;
					}
					else
					{
						renderWidth = (int)((float)image.renderWidth * tradeoffRenderSizeFactor);
						renderHeight = (int)((float)image.renderHeight * tradeoffRenderSizeFactor);
					}

					// Compute width & height
					if(forceOriginalSize || renderWidth >= image.originalWidth || renderHeight >= image.originalHeight)
					{
						width = image.originalWidth;
						height = image.originalHeight;
					}
					else
					{
						float fw = (float)renderWidth / (float)image.originalWidth;
						float fh = (float)renderHeight / (float)image.originalHeight;

						if(fw > fh)
						{
							width = renderWidth;
							height = (int)((float)image.originalHeight * fw);
						}
						else
						{
							height = renderHeight;
							width = (int)((float)image.originalWidth * fh);
						}
					}

					// Downscale image from originalWidth/originalHeight to width/height
					if(width != image.originalWidth || height != image.originalHeight)
					{
						Bitmap originalBmp = newbmp;
						newbmp = new Bitmap(width, height, originalBmp.PixelFormat);
						Graphics gfx = Graphics.FromImage(newbmp);
						gfx.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
						gfx.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;
						gfx.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
						gfx.DrawImage(originalBmp, new Rectangle(0, 0, width, height));
						gfx.Flush();
						originalBmp.Dispose();
						originalBmp = null;
					}

					#if DEBUG_GLTEXTURESTREAM
					Graphics gfxDebug = Graphics.FromImage(newbmp);
					gfxDebug.Clear(Color.WhiteSmoke);
					gfxDebug.DrawString(((float)image.originalWidth / (float)width).ToString(), new Font("Consolas", height / 3.0f), Brushes.Black, 0.0f, 0.0f);
					gfxDebug.Flush();
					#endif

					image.renderMutex.WaitOne();

					if(image.bmp != null) // If image already loaded (resize required)
					{
						// Unload old image
						image.bmp.Dispose();
						image.bmp = null;
					}

					image.bmp = newbmp;

					image.renderMutex.ReleaseMutex();

					sizePriority = (float)image.originalWidth / (float)width;

					int memorydiff = width * height - memory;
					memory = width * height;
					return memorydiff;
				}*/

				public int Load(float tradeoffRenderSizeFactor, bool forceOriginalSize, GLTexture2D texFileNotFound, ReadImageMetaDataDelegate ReadImageMetaData)
				{
					if(image.texIsStatic || !File.Exists(image.filename))
					{
						Global.cle.PrintOutput("File not found: " + image.filename);
						image.tex = texFileNotFound;
						image.texIsStatic = true;
						return memory = 0;
					}

					// Load image from disk
					Bitmap newbmp;
					if(!metadataLoaded && ReadImageMetaData != null)
					{
						List<ImageMetaData> meta = new List<ImageMetaData>();

						newbmp = ImageLoader.Load(image.filename, meta);
						if(newbmp == null)
						{
							Global.cle.PrintOutput("File not readable: " + image.filename);
							image.tex = texFileNotFound;
							image.texIsStatic = true;
							return memory = 0;
						}

						if(meta.Count == 0 || ReadImageMetaData(image.image, meta.ToArray()))
							metadataLoaded = true;
					}
					else
					{
						newbmp = ImageLoader.Load(image.filename);
						if(newbmp == null)
						{
							Global.cle.PrintOutput("File not readable: " + image.filename);
							image.tex = texFileNotFound;
							image.texIsStatic = true;
							return memory = 0;
						}
					}
					++GLTextureStream.foo;

					// Load depth image from disk
					Bitmap newbmp_depth = null;
					if(image.depth_filename != null)
					{
						if(!File.Exists(image.depth_filename) || (newbmp_depth = ImageLoader.Load(image.depth_filename)) == null)
						{
							if(newbmp != null)
							{
								newbmp.Dispose();
								newbmp = null;
							}

							Global.cle.PrintOutput((File.Exists(image.depth_filename) ? "File not readable: " : "File not found: ") + image.depth_filename);
							image.tex = texFileNotFound;
							image.texIsStatic = true;
							return memory = 0;
						}
						++GLTextureStream.foo;
					}

					// Load luminance image from disk
					Bitmap newbmp_lum = null;
					if(image.lum_filename != null)
					{
						if(!File.Exists(image.lum_filename) || (newbmp_lum = ImageLoader.Load(image.lum_filename)) == null)
						{
							if(newbmp != null)
							{
								newbmp.Dispose();
								newbmp = null;
							}
							if(newbmp_depth != null)
							{
								newbmp_depth.Dispose();
								newbmp_depth = null;
							}

							Global.cle.PrintOutput((File.Exists(image.lum_filename) ? "File not readable: " : "File not found: ") + image.lum_filename);
							image.tex = texFileNotFound;
							image.texIsStatic = true;
							return memory = 0;
						}
						++GLTextureStream.foo;
					}

					// Compute original dimensions
					image.originalWidth = newbmp.Width;
					image.originalHeight = newbmp.Height;
					image.originalAspectRatio = (float)image.originalWidth / (float)image.originalHeight;

					int renderWidth, renderHeight;
					if(memory > 0)
					{
						renderWidth = image.renderWidth;
						renderHeight = image.renderHeight;
					}
					else
					{
						renderWidth = (int)((float)image.renderWidth * tradeoffRenderSizeFactor);
						renderHeight = (int)((float)image.renderHeight * tradeoffRenderSizeFactor);
					}

					// Compute width & height
					if (forceOriginalSize || renderWidth >= image.originalWidth || renderHeight >= image.originalHeight)
					{
						width = image.originalWidth;
						height = image.originalHeight;
					}
					else
					{
						float fw = (float)renderWidth / (float)image.originalWidth;
						float fh = (float)renderHeight / (float)image.originalHeight;

						if(fw > fh)
						{
							width = renderWidth;
							height = (int)((float)image.originalHeight * fw);
						}
						else
						{
							height = renderHeight;
							width = (int)((float)image.originalWidth * fh);
						}
					}

					// Downscale image from originalWidth/originalHeight to width/height
					if(width != image.originalWidth || height != image.originalHeight)
					{
						Bitmap originalBmp = newbmp;
						newbmp = new Bitmap(width, height, originalBmp.PixelFormat);
						Graphics gfx = Graphics.FromImage(newbmp);
						gfx.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
						gfx.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;
						gfx.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
						gfx.DrawImage(originalBmp, new Rectangle(0, 0, width, height));
						gfx.Flush();
						originalBmp.Dispose();
						originalBmp = null;

						if(image.depth_filename != null)
						{
							originalBmp = newbmp_depth;
							newbmp_depth = new Bitmap(width, height, originalBmp.PixelFormat);
							gfx = Graphics.FromImage(newbmp_depth);
							gfx.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
							gfx.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;
							gfx.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
							gfx.DrawImage(originalBmp, new Rectangle(0, 0, width, height));
							gfx.Flush();
							originalBmp.Dispose();
							originalBmp = null;
						}

						if(image.lum_filename != null)
						{
							originalBmp = newbmp_lum;
							newbmp_lum = new Bitmap(width, height, originalBmp.PixelFormat);
							gfx = Graphics.FromImage(newbmp_lum);
							gfx.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
							gfx.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;
							gfx.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
							gfx.DrawImage(originalBmp, new Rectangle(0, 0, width, height));
							gfx.Flush();
							originalBmp.Dispose();
							originalBmp = null;
						}
					}

					#if DEBUG_GLTEXTURESTREAM
					Graphics gfxDebug = Graphics.FromImage(newbmp);
					gfxDebug.Clear(Color.WhiteSmoke);
					gfxDebug.DrawString(((float)image.originalWidth / (float)width).ToString(), new Font("Consolas", height / 3.0f), Brushes.Black, 0.0f, 0.0f);
					gfxDebug.Flush();
					#endif

					image.renderMutex.WaitOne();

					if(image.bmp != null) // If image already loaded (resize required)
					{
						// Unload old image
						image.bmp.Dispose();
						image.bmp = null;
						--GLTextureStream.foo;
					}
					image.bmp = newbmp;

					if(image.depth_filename != null)
					{
						if(image.bmp_depth != null)
						{
							// Unload old image
							image.bmp_depth.Dispose();
							image.bmp_depth = null;
							--GLTextureStream.foo;
						}
						image.bmp_depth = newbmp_depth;
					}

					if(image.lum_filename != null)
					{
						if(image.bmp_lum != null)
						{
							// Unload old image
							image.bmp_lum.Dispose();
							image.bmp_lum = null;
							--GLTextureStream.foo;
						}
						image.bmp_lum = newbmp_lum;
					}

					image.renderMutex.ReleaseMutex();

					sizePriority = (float)image.originalWidth / (float)width;

					int factor = 1 + (image.depth_filename != null ? 1 : 0) + (image.lum_filename != null ? 1 : 0);

					int memorydiff = width * height * factor - memory;
					memory = width * height * factor;
					return memorydiff;
				}

				public int Unload()
				{
					if(image.texIsStatic)
						return 0;

					if(image.bmp == null)
						throw new Exception("assert(bmp != null) triggered inside Unload()");

					image.renderMutex.WaitOne();

					// Unload image
					image.bmp.Dispose();
					image.bmp = null;
					--GLTextureStream.foo;

					if(image.bmp_depth != null)
					{
						// Unload old image
						image.bmp_depth.Dispose();
						image.bmp_depth = null;
						--GLTextureStream.foo;
					}
						
					if(image.bmp_lum != null)
					{
						// Unload old image
						image.bmp_lum.Dispose();
						image.bmp_lum = null;
						--GLTextureStream.foo;
					}

					image.renderMutex.ReleaseMutex();

					sizePriority = 1.0f;
					width = height = 0;

					int m = memory;
					memory = 0;
					return m;
				}

				public int CompareTo(Image other)
				{
					return image.renderPriority == other.image.renderPriority ? sizePriority.CompareTo(other.sizePriority) : image.renderPriority.CompareTo(other.image.renderPriority);
				}
			}

			private readonly int memorysize;
			private int availablememory;

			private readonly GLTexture2D texFileNotFound;

			private Thread loaderThread;
			private bool closeLoaderThread, loaderThreadClosed;

			public List<Image> prioritySortedImages; //EDIT: Make private
			private Mutex addImageMutex;

			private float tradeoffRenderSizeFactor = 1.0f;
			public bool forceOriginalSize = false;

			private ReadImageMetaDataDelegate ReadImageMetaData;

			public AsyncImageLoader(/*TransformedImageCollection images,*/ int memorysize, GLTexture2D texFileNotFound, ReadImageMetaDataDelegate ReadImageMetaData)
			{
				this.memorysize = memorysize;
				this.availablememory = memorysize;
				this.texFileNotFound = texFileNotFound;
				this.ReadImageMetaData = ReadImageMetaData;

				foo = 0;

				prioritySortedImages = new List<Image>();
				addImageMutex = new Mutex();
				/*prioritySortedImages = new List<Image>(images.Count);
				foreach(TransformedImage image in images)
					prioritySortedImages.Add(new Image(image));*/

				closeLoaderThread = loaderThreadClosed = false;
				loaderThread = new Thread(LoaderThread);
				loaderThread.Start();
			}

			public void CloseThread()
			{
				closeLoaderThread = true;
			}
			public void WaitForThreadClose()
			{
				while(!loaderThreadClosed)
					Thread.Sleep(1);
			}

			public void AddImage(TransformedImage image)
			{
				addImageMutex.WaitOne();
				foreach(TransformedImage.ImageLayer layer in image.activelayers)
					prioritySortedImages.Add(new Image(layer));
				foreach(TransformedImage.ImageLayer layer in image.inactivelayers)
					prioritySortedImages.Add(new Image(layer));
				addImageMutex.ReleaseMutex();
			}
			public void AddImages(IEnumerable<TransformedImage> images)
			{
				addImageMutex.WaitOne();
				foreach(TransformedImage image in images)
				{
					foreach(TransformedImage.ImageLayer layer in image.activelayers)
						prioritySortedImages.Add(new Image(layer));
					foreach(TransformedImage.ImageLayer layer in image.inactivelayers)
						prioritySortedImages.Add(new Image(layer));
				}
				addImageMutex.ReleaseMutex();
			}
			public void RemoveImage(TransformedImage image) // O(n)
			{
				addImageMutex.WaitOne();
				prioritySortedImages.RemoveAll(delegate(Image img) {
					if(image.activelayers.Contains(img.image) || image.inactivelayers.Contains(img.image))
					{
						if(img.image.bmp != null)
							availablememory += img.Unload();
						return true;
					}
					else
						return false;
				});
				addImageMutex.ReleaseMutex();
			}
			public void RemoveImages(IEnumerable<TransformedImage> images) // O(n^2)
			{
				addImageMutex.WaitOne();
				prioritySortedImages.RemoveAll(delegate(Image img) {
					foreach(TransformedImage image in images)
						if(image.activelayers.Contains(img.image) || image.inactivelayers.Contains(img.image))
						{
							if(img.image.bmp != null)
								availablememory += img.Unload();
							return true;
						}
					return false;
				});
				addImageMutex.ReleaseMutex();
			}
			public void ClearImages()
			{
				addImageMutex.WaitOne();
				foreach(Image image in prioritySortedImages)
					if(image.image.bmp != null)
						availablememory += image.Unload();
				prioritySortedImages.Clear();
				addImageMutex.ReleaseMutex();
			}

			private void LoaderThread()
			{
				while(!closeLoaderThread)
				{
					//Thread.Sleep(10);
					Thread.Sleep(1);

					addImageMutex.WaitOne();

					prioritySortedImages.Sort();

					// Load highest priority image
					int unloadidx = 0, len = prioritySortedImages.Count, loadidx = len - 1;
					//bool outofmemory = false;
					for(; loadidx >= 0; --loadidx)
					{
						Image img = prioritySortedImages[loadidx];
						int requiredmemory = img.RequiredMemory(tradeoffRenderSizeFactor, forceOriginalSize);

						if(requiredmemory <= 0) // If image is already loaded
							continue; // continue checking next-highest priority image
						if(requiredmemory <= availablememory) // If we have enough memory to load img
						{
							availablememory -= img.Load(tradeoffRenderSizeFactor, forceOriginalSize, texFileNotFound, ReadImageMetaData); // Load image and update available memory
							break; // Done
						}
						else // If we don't have enough memory to load img
						{
							// Unload lower priority images to regain memory and retry loading img
							bool imageLoaded = false;
							for(; unloadidx < len; ++unloadidx)
							{
								Image u_img = prioritySortedImages[unloadidx];
								if(u_img.MemoryDisposable()) // If image is loaded, but doesn't need to be (renderPriority <= 0)
								{
									availablememory += u_img.Unload(); // Unload image and update available memory
									if(requiredmemory <= availablememory) // If we now have enough memory to load img
									{
										availablememory -= img.Load(tradeoffRenderSizeFactor, forceOriginalSize, texFileNotFound, ReadImageMetaData); // Load image and update available memory
										imageLoaded = true;
										break; // Done
									}
								}
								else if(u_img.MemoryShrinkable(forceOriginalSize)) // If image is loaded, but can be shrinked
								{
									availablememory -= u_img.Load(tradeoffRenderSizeFactor, forceOriginalSize, texFileNotFound, ReadImageMetaData); // Reload image and update available memory
									if(requiredmemory <= availablememory) // If we now have enough memory to load img
									{
										availablememory -= img.Load(tradeoffRenderSizeFactor, forceOriginalSize, texFileNotFound, ReadImageMetaData); // Load image and update available memory
										imageLoaded = true;
										break; // Done
									}
								}
							}

							if(imageLoaded)
								break;

							//outofmemory = true;
						}
						//Thread.Sleep(1);
					}

					#if ENABLE_TRADEOFF
					int theoreticalusedmemory = 0;
					foreach(Image img in prioritySortedImages)
						if(img.image.renderPriority > 0)
							theoreticalusedmemory += img.memory;

					tradeoffRenderSizeFactor = Math.Max(0.1f, (float)(memorysize - theoreticalusedmemory) / (float)memorysize);
					//tradeoffRenderSizeFactor = (float)availablememory / (float)memorysize;
					GLTextureStream.foo2 = tradeoffRenderSizeFactor.ToString();
					#else
					GLTextureStream.foo2 = (availablememory / 262144).ToString() + " MB";
					#endif

					addImageMutex.ReleaseMutex();
				}

				// Free memory
				foreach(Image image in prioritySortedImages)
				{
					if(image.image.bmp != null)
					{
						image.image.bmp.Dispose();
						image.image.bmp = null;
					}

					/*if(image.image.tex != null  && !image.image.texIsStatic) //EDIT: Do this in main threat
					{
						GL.DeleteTexture(image.image.tex.tex);
						image.image.tex = null;
					}*/
				}

				loaderThreadClosed = true;
			}
		}
		private AsyncImageLoader loader;

		public bool forceOriginalSize { get { return loader.forceOriginalSize; } set { loader.forceOriginalSize = value; } }

		private static int ceilBin(int v)
		{
			int b;
			for(b = 1; v > b; b <<= 1) {}
			return b;
		}

		public GLTextureStream(int memorysize, ReadImageMetaDataDelegate ReadImageMetaData)
		{
			#if DEBUG_GLTEXTURESTREAM
			memorysize = 8 * 79 * 79;
			#endif

			this.currentmemorysize = 0;
			this.memorysizelimit = memorysize;

			// >>> Create file-not-found bitmap and texture

			int texwidth = 512, texheight = 512;
			// Find font size to fit "File not found" into Size(texwidth, texheight)
			Bitmap bmpTemp = new Bitmap(1, 1);
			Graphics gfx = Graphics.FromImage(bmpTemp);
			float fontsize = 16.0f;
			Size fileNotFoundSize;
			Font fntFileNotFound;
			do
			{
				fontsize *= 2.0f;
				fntFileNotFound = new Font("Impact", fontsize);
				fileNotFoundSize = gfx.MeasureString("File not found", fntFileNotFound).ToSize();
			} while(fileNotFoundSize.Width < texwidth && fileNotFoundSize.Height < texheight);
			fontsize /= 2.0f;
			fntFileNotFound = new Font("Impact", fontsize);
			fileNotFoundSize = gfx.MeasureString("File not found", fntFileNotFound).ToSize();
			bmpTemp.Dispose();

			// Draw bitmap
			bmpFileNotFound = new Bitmap(texwidth, texheight);
			gfx = Graphics.FromImage(bmpFileNotFound);
			gfx.Clear(Color.IndianRed);
			gfx.DrawString("File not found", fntFileNotFound, Brushes.LightSlateGray, (float)((texwidth - fileNotFoundSize.Width) / 2), (float)((texheight - fileNotFoundSize.Height) / 2));
			gfx.Flush();

			// Load texture
			texFileNotFound = new GLTexture2D("texFileNotFound", bmpFileNotFound, true);

			loader = new AsyncImageLoader(memorysize, texFileNotFound, ReadImageMetaData);
		}
		public void Free()
		{
			loader.CloseThread();

			// Free local resources ...

			loader.WaitForThreadClose();
		}

		public void AddImage(TransformedImage image)
		{
			loader.AddImage(image);
		}
		public void AddImages(IEnumerable<TransformedImage> images)
		{
			loader.AddImages(images);
		}
		public void RemoveImage(TransformedImage image)
		{
			loader.RemoveImage(image);
		}
		public void RemoveImages(IEnumerable<TransformedImage> images)
		{
			loader.RemoveImages(images);
		}
		public void ClearImages()
		{
			loader.ClearImages();
		}

		#if DEBUG_GLTEXTURESTREAM
		GLFont fntDebug = null;
		GLLabel[] lblDebug;
		public void DrawDebugInfo(Size backbufferSize)
		{
			if(fntDebug == null)
			{
				fntDebug = Common.fontText2;

				lblDebug = new GLLabel[loader.prioritySortedImages.Count];
				for(int i = 0; i < loader.prioritySortedImages.Count; ++i)
				{
					lblDebug[i] = new GLLabel();
					lblDebug[i].Font = fntDebug;
					lblDebug[i].Bounds = new Rectangle(100, 32 * i, 64, (int)Math.Ceiling(fntDebug.MeasureString(" ").Y));
					lblDebug[i].OnParentSizeChanged(backbufferSize, backbufferSize);
				}
			}
				
			for(int i = 0; i < loader.prioritySortedImages.Count; ++i)
			{
				//lblDebug[i].Text = loader.prioritySortedImages[i].memory.ToString();
				lblDebug[i].Text = loader.prioritySortedImages[i].image.strValues[0] + ": " + loader.prioritySortedImages[i].image.renderPriority.ToString();
				lblDebug[i].Draw(0.0f);
			}
		}
		#else
		public void DrawDebugInfo(Size backbufferSize) {}
		#endif

public static int foo = 0;
public static string foo2 = "";
	}
}

