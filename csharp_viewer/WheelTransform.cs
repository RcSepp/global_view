using System;

using OpenTK;

namespace csharp_viewer
{
	[Serializable]
	public class WheelTransform : ImageTransform
	{
		private static float HALF_SQRT_2 = (float)(Math.Sqrt(2.0) / 2.0);
		private static Vector3[][] WEIGHTS = {
			/* 0 dimensions */ new Vector3[] {
			},
			/* 1 dimension */ new Vector3[] {
				new Vector3(1.0f, 0.0f, 0.0f)
			},
			/* 2 dimensions */ new Vector3[] {
				new Vector3(1.0f, 0.0f, 0.0f),
				new Vector3(0.0f, 1.0f, 0.0f)
			},
			/* 3 dimensions */ new Vector3[] {
				new Vector3(1.0f, 0.0f, 0.0f),
				new Vector3(0.0f, 1.0f, 0.0f),
				new Vector3(0.0f, 0.0f, 1.0f)
			},
			/* 4 dimensions */ new Vector3[] {
				new Vector3( 0.75f, -0.5f * (float)Math.Sqrt(0.75), -1.0f / 3.0f),
				new Vector3(  0.0f,  1.0f * (float)Math.Sqrt(0.75), -1.0f / 3.0f),
				new Vector3(-0.75f, -0.5f * (float)Math.Sqrt(0.75), -1.0f / 3.0f),
				new Vector3(  0.0f,  0.0f,                           1.0f)
			},
			/* 5 dimensions */ new Vector3[] {
				new Vector3( (float)Math.Sqrt(0.75), -0.5f, 0.0f),
				new Vector3( 0.0f,                    1.0f, 0.0f),
				new Vector3(-(float)Math.Sqrt(0.75), -0.5f, 0.0f),
				new Vector3(  0.0f,  0.0f,  (float)Math.Sqrt(0.75)),
				new Vector3( 0.0f,   0.0f, -(float)Math.Sqrt(0.75))
			},
			/* 6 dimensions new Vector3[] {
				new Vector3( (float)Math.Sqrt(0.75),  0.5f,                  -0.5f),
				new Vector3( 0.0f,                    (float)Math.Sqrt(0.5),  1.0f),
				new Vector3(-(float)Math.Sqrt(0.75),  0.5f,                  -0.5f),
				new Vector3(-0.5f,                   -0.5f,                   (float)Math.Sqrt(0.75)),
				new Vector3( 1.0f,                   -(float)Math.Sqrt(0.5),  0.0f),
				new Vector3(-0.5f,                   -0.5f,                  -(float)Math.Sqrt(0.75))
			}*/
			/* 6 dimensions */ new Vector3[] { // <- EDIT: works except for signs!
				new Vector3(-(float)Math.Cos(  54.0 * Math.PI / 180.0) * HALF_SQRT_2, -(float)Math.Sin(  54.0 * Math.PI / 180.0) * HALF_SQRT_2, HALF_SQRT_2),
				new Vector3((float)Math.Cos( 126.0 * Math.PI / 180.0) * HALF_SQRT_2, (float)Math.Sin( 126.0 * Math.PI / 180.0) * HALF_SQRT_2, -HALF_SQRT_2),
				new Vector3(-(float)Math.Cos(-162.0 * Math.PI / 180.0) * HALF_SQRT_2, -(float)Math.Sin(-162.0 * Math.PI / 180.0) * HALF_SQRT_2, HALF_SQRT_2),
				new Vector3((float)Math.Cos( -90.0 * Math.PI / 180.0) * HALF_SQRT_2, (float)Math.Sin( -90.0 * Math.PI / 180.0) * HALF_SQRT_2, -HALF_SQRT_2),
				new Vector3(-(float)Math.Cos( -18.0 * Math.PI / 180.0) * HALF_SQRT_2, -(float)Math.Sin( -18.0 * Math.PI / 180.0) * HALF_SQRT_2, HALF_SQRT_2),
				new Vector3(0.0f, 0.0f, -1.0f)
			}
		};

		/* 3 dimensions 2D: new Vector3[] {
			new Vector3( (float)Math.Sqrt(0.75), -0.5f, 0.0f),
			new Vector3( 0.0f,         1.0f,        0.0f),
			new Vector3(-(float)Math.Sqrt(0.75), -0.5f, 0.0f)
		}*/

		public override Description GetDescription() {return new Description(2);}
		public override int GetIndex(int i) { return i < arguments.Length ? idx[i] : -1; }
		public override int SetIndex(int i, int index) { return i < arguments.Length ? idx[i] = index : -1; }

		private int[] idx;
		private Cinema.CinemaArgument[] arguments;

		public WheelTransform()
		{
		}

		public override void SetArguments(Cinema.CinemaArgument[] arguments)
		{
			this.arguments = arguments;
			idx = new int[arguments.Length];
		}

		public override void LocationTransform(int[] imagekey, TransformedImage image, out Vector3 pos, ref Quaternion rot, ref Vector3 scl)
		{
			pos = new Vector3(0.0f, 0.0f, 0.0f);

			int numdims = idx.Length;
			if(numdims < WEIGHTS.Length)
			{
				Vector3[] weights = WEIGHTS[numdims];
				for(int d = 0; d < numdims; ++d)
				{
					float factor = 30.0f * (float)imagekey[idx[d]] / (float)arguments[idx[d]].values.Length;
				pos += weights[d] /** new Vector3(2.0f, 1.5f, 1.0f)*/ * factor; //TODO: delete "new Vector3(2.0f, 1.0f, 1.0f)"
				}
			}
			else
			{
				// 2D version
				for(int d = 0; d < numdims; ++d)
				{
					float angle = (float)d * MathHelper.TwoPi / (float)numdims;
					float factor = (float)imagekey[idx[d]] / (float)arguments[idx[d]].values.Length;

					pos.X += 50.0f * (float)Math.Cos(angle) * factor;
					pos.Y += 50.0f * (float)Math.Sin(angle) * factor;
				}
			}
		}
	}
}

