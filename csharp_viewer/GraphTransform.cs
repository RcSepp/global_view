using System;

using OpenTK;

namespace csharp_viewer
{
	//[Serializable]
	public class GraphTransform : ImageTransform
	{
		private int[] idx = new int[2];
		private Cinema.CinemaArgument[] arguments;
		private float[] y;
		private GLMesh meshValueLine = null;

		public GraphTransform()
		{
			y = new float[1024];

			Random rand = new Random();
			for(int i = 0; i < y.Length; ++i)
				y[i] = (float)rand.NextDouble() * 10.0f;
		}

		ImageCloud.FreeView freeview;
		public override void OnRender(float dt, ImageCloud.FreeView freeview)
		{
			//Common.sdrSolidColor.Bind(freeview.viewprojmatrix);
			//Common.meshLineCube.Bind(Common.sdrSolidColor, null);
			//Common.meshLineCube.Draw();

			if(meshValueLine == null)
			{
				Vector3[] positions = new Vector3[] {
					new Vector3(1.0f, 0.0f, 1.0f), new Vector3(1.0f, 1.0f, 1.0f),
					new Vector3(1.0f, 0.0f, 1.0f), new Vector3(1.0f, 0.0f, 0.0f),
					new Vector3(1.0f, 0.0f, 1.0f), new Vector3(0.0f, 0.0f, 1.0f)
				};
				meshValueLine = new GLMesh(positions, null, null, null, null, null, OpenTK.Graphics.OpenGL.PrimitiveType.Lines);
			}

			//meshValueLine.Bind(Common.sdrSolidColor, null);
			//meshValueLine.Draw();

			this.freeview = freeview;
		}

		public override void LocationTransform(int[] imagekey, TransformedImage image, out Vector3 pos, ref Quaternion rot, ref Vector3 scl)
		{
			float x = (float)imagekey[idx[0]];
			float z = (float)imagekey[idx[1]];

			pos = new Vector3(x * 2.1f, y[(imagekey[idx[0]] + arguments[idx[0]].values.Length * imagekey[idx[1]]) % 1024], z * 2.1f);
		}
		public override void RenderImage(int[] imagekey, TransformedImage image, ImageCloud.FreeView freeview)
		{
			float x = (float)imagekey[idx[0]];
			float z = (float)imagekey[idx[1]];

			Matrix4 worldmatrix = Matrix4.CreateScale(x * 2.1f, y[(imagekey[idx[0]] + arguments[idx[0]].values.Length * imagekey[idx[1]]) % 1024], z * 2.1f);

			Common.sdrSolidColor.Bind(worldmatrix * freeview.viewprojmatrix);
			meshValueLine.Bind(Common.sdrSolidColor, null);
			meshValueLine.Draw();
		}
	}
}

