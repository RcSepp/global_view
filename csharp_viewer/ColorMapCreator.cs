using System;

namespace csharp_viewer
{
	public static class ColorMapCreator
	{
		// Reference white-point D65
		private static Vector3 XYZn = new Vector3(95.047f, 100.0f, 108.883f); // from Adobe Cookbook

		// Transfer-matrix for the conversion of RGB to XYZ color space
		private static Matrix3 transM = new Matrix3(0.4124564f, 0.2126729f, 0.0193339f,
													0.3575761f, 0.7151522f, 0.1191920f,
													0.1804375f, 0.0721750f, 0.9503041f);

		private static Vector3 rgblinear(Vector3 rgb)
		{
			// Conversion from the sRGB components to RGB components with physically linear properties.

			// iInitialize the linear RGB array
			Vector3 rgbLinear = new Vector3(0.0f, 0.0f, 0.0f);

			// Calculate the linear RGB values
			for(int i = 0; i < 3; ++i)
			{
				float value = rgb[i];
				value = (float)value / 255.0f;
				if(value > 0.04045f)
					value = (float)Math.Pow((value + 0.055f) / 1.055f, 2.4);
				else
					value = value / 12.92f;
				rgbLinear[i] = value * 100.0f;
			}
			return rgbLinear;
		}

		private static Vector3 srgb(Vector3 rgbLinear)
		{
			// Back conversion from linear RGB to sRGB.

			// Initialize the sRGB array
			Vector3 rgb = new Vector3(0.0f, 0.0f, 0.0f);

			// Calculate the sRGB values
			for(int i = 0; i < 3; ++i)
			{
				float value = rgbLinear[i];
				value = (float)value / 100.0f;

				if(value > 0.00313080495356037152f)
					value = (1.055f * (float)Math.Pow(value, 1.0f / 2.4f)) - 0.055f;
				else
					value = value * 12.92f;

				rgb[i] = (float)Math.Round(value * 255.0f);
			}
			return rgb;
		}

		private static Vector3 rgb2xyz(Vector3 rgb)
		{
			// Conversion of RGB to XYZ using the transfer-matrix
			return rgblinear(rgb) * transM;
		}

		private static Vector3 xyz2rgb(Vector3 xyz)
		{
			// Conversion of RGB to XYZ using the transfer-matrix
			//return np.round(np.dot(XYZ, np.array(np.matrix(transM).I)));
			return srgb(xyz * transM.Inverse());
		}

		delegate float F(float x);
		private static Vector3 rgb2lab(Vector3 rgb)
		{
			// Conversion of RGB to CIELAB

			// Convert RGB to XYZ
			Vector3 xyz = rgb2xyz(rgb);

			// helper function
			F f = delegate(float x) {
				const float limit = 0.008856f;
				return x > limit ? (float)Math.Pow(x, 1.0f / 3.0f) : 7.787f * x + 16.0f / 116.0f;
			};

			// Calculation of L, a and b
			float L = 116.0f * (f(xyz.y / XYZn.y) - (16.0f / 116.0f));
			float a = 500.0f * (f(xyz.x / XYZn.x) - f(xyz.y / XYZn.y));
			float b = 200.0f * (f(xyz.y / XYZn.y) - f(xyz.z / XYZn.z));
			return new Vector3(L, a, b);
		}

		private static Vector3 lab2rgb(Vector3 lab)
		{
			// Conversion of CIELAB to RGB

			// helper function
			F finverse = delegate(float x) {
				float xlim = 0.008856f;
				float a = 7.787f;
				float b = 16.0f / 116.0f;
				float ylim = a * xlim + b;
				return x > ylim ? (float)Math.Pow(x, 3) : ( x - b ) / a;
			};

			// calculation of X, Y and Z
			float X = XYZn.x * finverse((lab.y / 500.0f) + (lab.x + 16.0f) / 116.0f);
			float Y = XYZn.y * finverse((lab.x + 16.0f) / 116.0f);
			float Z = XYZn.z * finverse((lab.x + 16.0f) / 116.0f - (lab.z / 200.0f));

			// Conversion of XYZ to RGB
			return xyz2rgb(new Vector3(X,Y,Z));
		}

		private static Vector3 lab2msh(Vector3 lab)
		{
			// Conversion of CIELAB to Msh

			// calculation of M, s and h
			float M = lab.Length();
			float s = (float)Math.Acos(lab.x / M);
			float h = (float)Math.Atan2(lab.z, lab.y);
			return new Vector3(M, s, h);
		}

		private static Vector3 msh2lab(Vector3 msh)
		{
			// Conversion of Msh to CIELAB

			// calculation of L, a and b
			float L = msh.x * (float)Math.Cos(msh.y);
			float a = msh.x * (float)Math.Sin(msh.y) * (float)Math.Cos(msh.z);
			float b = msh.x * (float)Math.Sin(msh.y) * (float)Math.Sin(msh.z);
			return new Vector3(L, a, b);
		}

		private static Vector3 rgb2msh(Vector3 rgb)
		{
			// Direct conversion of RGB to Msh
			return lab2msh(rgb2lab(rgb));
		}

		private static Vector3 msh2rgb(Vector3 msh)
		{
			// Direct conversion of Msh to RGB
			return lab2rgb(msh2lab(msh));
		}

		private static float adjustHue(Vector3 mshSat, float munSat)
		{
			// Function to provide an adjusted hue when interpolating to an unsaturated color in Msh space

			if(mshSat.x >= munSat)
				return mshSat.z;
			else
			{
				float hSpin = mshSat.y * (float)Math.Sqrt(munSat*munSat - mshSat.x*mshSat.x) / (mshSat.x * (float)Math.Sin(mshSat.y));
				if(mshSat.z > -(float)Math.PI / 3.0f)
					return mshSat.z + hSpin;
				else
					return mshSat.z - hSpin;
			}
		}

		public static Vector3 interpolateColor(Vector3 rgb1, Vector3 rgb2, float interp)
		{
			// Interpolation algorithm to automatically create continuous diverging color maps.

			// Convert RGB to Msh and unpack
			Vector3 msh1 = rgb2msh(rgb1);
			Vector3 msh2 = rgb2msh(rgb2);

			// If points saturated and distinct, place white in middle
			if((msh1.y > 0.05) && (msh2.y > 0.05) && (Math.Abs(msh1.z - msh2.z) > (float)Math.PI / 3.0f))
			{
				float Mmid = Math.Max(Math.Max(msh1.x, msh2.x), 88.0f);
				if(interp < 0.5)
				{
					msh2.x = Mmid;
					msh2.y = 0.0f;
					msh2.z = 0.0f;
					interp = 2 * interp;
				}
				else
				{
					msh1.x = Mmid;
					msh1.y = 0.0f;
					msh1.z = 0.0f;
					interp = 2.0f * interp - 1.0f;
				}
			}

			// Adjust hue of unsaturated colors
			if((msh1.y < 0.05) && (msh2.y > 0.05))
				msh1.z = adjustHue(msh2, msh1.x);
			else if((msh2.y < 0.05) && (msh1.y > 0.05))
				msh2.z = adjustHue(msh1, msh2.x);

			// Linear interpolation on adjusted control points
			return msh2rgb(Vector3.Lerp(msh1, msh2, interp));
		}

		public static Vector3 interpolateLabColor(Vector3 rgb1, Vector3 rgb2, float interp)
		{
			return lab2rgb(Vector3.Lerp(rgb2lab(rgb1), rgb2lab(rgb2), interp));
		}

		public class InterpolatedColorMap
		{
			private System.Collections.Generic.SortedList<float, Vector3> samples = new System.Collections.Generic.SortedList<float, Vector3>();
			private float x_min = float.MaxValue, x_max = float.MinValue;
			public void AddColor(float x, float r, float g, float b)
			{
				if(samples.ContainsKey(x))
					samples.Remove(x);
				else
				{
					x_min = Math.Min(x_min, x);
					x_max = Math.Max(x_max, x);
				}
				samples.Add(x, new Vector3(r, g, b));
			}

			public byte[] Create(int size)
			{
				byte[] colorMap = new byte[size * 3];
				float x_delta = x_max - x_min;

				System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<float, Vector3>> iter = samples.GetEnumerator();
				if(!iter.MoveNext())
					return null; // No samples provided
				System.Collections.Generic.KeyValuePair<float, Vector3> lastSample = iter.Current;
				while(iter.MoveNext())
				{
					System.Collections.Generic.KeyValuePair<float, Vector3> currentSample = iter.Current;

					float lastX = (lastSample.Key - x_min) / x_delta, currentX = (currentSample.Key - x_min) / x_delta;
					int lastIdx = (int)(lastX * (float)size), currentIdx = (int)(currentX * (float)size), deltaIdx = currentIdx - lastIdx;
					for(int i = 0; i < deltaIdx; ++i)
					{
						Vector3 clr = interpolateLabColor(lastSample.Value, currentSample.Value, (float)i / (float)deltaIdx);
						int idx = lastIdx + i;
						colorMap[3 * idx + 0] = (byte)clr.x;
						colorMap[3 * idx + 1] = (byte)clr.y;
						colorMap[3 * idx + 2] = (byte)clr.z;
					}

					lastSample = currentSample;
				}

				return colorMap;
			}
		}

		public class Vector3
		{
			public float x, y, z;
			public Vector3(float x, float y, float z)
			{
				this.x = x;
				this.y = y;
				this.z = z;
			}
			public float this[int c]
			{
				get
				{
					switch(c)
					{
					case 0: return x;
					case 1: return y;
					case 2: return z;
					default: throw new IndexOutOfRangeException();
					}
				}
				set
				{
					switch(c)
					{
					case 0: x = value; break;
					case 1: y = value; break;
					case 2: z = value; break;
					default: throw new IndexOutOfRangeException();
					}
				}
			}
			public static Vector3 operator *(Vector3 v, Matrix3 m)
			{
				return new Vector3(v.x * m._00 + v.y * m._10 + v.z * m._20,
					v.x * m._01 + v.y * m._11 + v.z * m._21,
					v.x * m._02 + v.y * m._12 + v.z * m._22); //TODO: Check this (transpose?)
			}
			public float Length()
			{
				return (float)Math.Sqrt(x*x + y*y + z*z);
			}
			public static Vector3 Lerp(Vector3 v0, Vector3 v1, float f)
			{
				float g = 1.0f - f;
				return new Vector3(v0.x * g + v1.x * f, v0.y * g + v1.y * f, v0.z * g + v1.z * f);
			}
		}

		public class Matrix3
		{
			public float _00, _01, _02;
			public float _10, _11, _12;
			public float _20, _21, _22;
			public Matrix3(float _00, float _01, float _02, float _10, float _11, float _12, float _20, float _21, float _22)
			{
				this._00 = _00; this._01 = _01; this._02 = _02;
				this._10 = _10; this._11 = _11; this._12 = _12;
				this._20 = _20; this._21 = _21; this._22 = _22;
			}
			/*public static Matrix3 operator *(Matrix3 m0, Matrix3 m1)
			{
				return new Matrix3(m0._00 * m1._00 + m0._01 * m1._10 + m0._02 * m1._20, m0._00 * m1._01 + m0._01 * m1._11 + m0._02 * m1._21, m0._00 * m1._02 + m0._01 * m1._12 + m0._02 * m1._22,
								   m0._10 * m1._00 + m0._11 * m1._10 + m0._12 * m1._20, m0._10 * m1._01 + m0._11 * m1._11 + m0._12 * m1._21, m0._10 * m1._02 + m0._11 * m1._12 + m0._12 * m1._22,
								   m0._20 * m1._00 + m0._21 * m1._10 + m0._22 * m1._20, m0._20 * m1._01 + m0._21 * m1._11 + m0._22 * m1._21, m0._20 * m1._02 + m0._21 * m1._12 + m0._22 * m1._22);
			}*/
			public Matrix3 Inverse()
			{
				float a = _00, b = _01, c = _02;
				float d = _10, e = _11, f = _12;
				float g = _20, h = _21, i = _22;

				float A = e * i - f * h, B = f * g - d * i, C = d * h - e * g;
				float D = c * h - b * i, E = a * i - c * g, F = b * g - a * h;
				float G = b * f - c * e, H = c * d - a * f, I = a * e - b * d;

				float invdet = 1.0f / (a * A + b * B + c * C);

				return new Matrix3(A * invdet, D * invdet, G * invdet,
					B * invdet, E * invdet, H * invdet,
					C * invdet, F * invdet, I * invdet);
			}
		}
	}
}

