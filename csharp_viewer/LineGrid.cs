using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace csharp_viewer
{
	public class LineGrid
	{
		private const int LABEL_CAM_DISTANCE = 200;
		private const double LABEL_CAM_DISTANCE_FADE = 4.0;
		private const double LABEL_AXIS_DISTANCE_FADE = 4.0;

		private Color4 POSITIVE_AXIS_COLOR = new Color4(100, 120, 140, 255);
		private Color4 NEGATIVE_AXIS_COLOR = new Color4(140, 100, 120, 255);

		private GLMesh axismesh, tickmesh, linemesh;

		public LineGrid()
		{
			Vector3[] positions;

			positions = new Vector3[] {
				new Vector3(0.0f, 0.0f, 0.0f), new Vector3(1000.0f, 0.0f, 0.0f),
				new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 1000.0f, 0.0f),
				new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 1000.0f)
			};
			axismesh = new GLMesh(positions, null, null, null, null, null, PrimitiveType.Lines);

			positions = new Vector3[] {
				new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.03f, 0.0f, 0.0f),
				new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f),
				new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.03f, 0.0f),
				new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f)
			};
			tickmesh = new GLMesh(positions, null, null, null, null, null, PrimitiveType.Lines);

			positions = new Vector3[] {
				new Vector3(0.0f, 0.0f, -1.0f), new Vector3(0.0f, 0.0f, 1.0f)
			};
			linemesh = new GLMesh(positions, null, null, null, null, null, PrimitiveType.Lines);
		}

		public void Draw(ImageCloud.FreeView freeview, AABB selectionAabb, Color4 selectionAabbColor, Size backbuffersize)
		{
			Vector3 __axis_dist = new Vector3((new Vector2(freeview.viewpos.Y, freeview.viewpos.Z)).Length,
											(new Vector2(freeview.viewpos.X, freeview.viewpos.Z)).Length,
											(new Vector2(freeview.viewpos.X, freeview.viewpos.Y)).Length);
			Vector3 axis_dist = new Vector3(), _axis_dist = new Vector3();
			for(int i = 0; i < 3; ++i)
			{
				_axis_dist[i] = (float)Math.Log10(Math.Max(ImageCloud.Z_NEAR, __axis_dist[i]));
				axis_dist[i] = (float)Math.Pow(10.0, Math.Floor(_axis_dist[i]));
			}

			int[] floor_viewpos = new int[3];
			float[] floor_viewpos10 = new float[3], axis_dist_fract = new float[3];
			Matrix4[] scalematrix = new Matrix4[3];
			for(int d = 0; d < 3; ++d)
			{
				floor_viewpos[d] = Math.Abs((int)(Math.Floor(freeview.viewpos[d] / axis_dist[d] * 10.0f) / 10.0f));
				floor_viewpos10[d] = Math.Abs((float)Math.Floor(freeview.viewpos[d] / axis_dist[d] / 10.0f) * axis_dist[d] * 10.0f);
				axis_dist_fract[d] = (_axis_dist[d] + 100.0f) % 1.0f; //EDIT: 100.0f ... constant to keep log positive
				scalematrix[d] = Matrix4.CreateScale(axis_dist[d]);
			}

			Vector3[] unitvector = {
				new Vector3(1.0f, 0.0f, 0.0f),
				new Vector3(0.0f, 1.0f, 0.0f),
				new Vector3(0.0f, 0.0f, 1.0f)
			};

			Matrix4[] tickrotmatrix = {
				Matrix4.CreateRotationY(MathHelper.PiOver2) * Matrix4.CreateRotationX(MathHelper.PiOver2),
				Matrix4.CreateRotationX(-MathHelper.PiOver2) * Matrix4.CreateRotationY(-MathHelper.PiOver2),
				Matrix4.Identity
			};

			// >>> Draw axes

			GL.LineWidth(4.0f);

			axismesh.Bind(Common.sdrSolidColor);

			// Draw axes in positive directions
			Common.sdrSolidColor.Bind(freeview.viewprojmatrix);
			GL.Uniform4(Common.sdrSolidColor_colorUniform, POSITIVE_AXIS_COLOR);
			axismesh.Draw();

			// Draw axes in negative directions
			Common.sdrSolidColor.Bind(Matrix4.CreateRotationY(MathHelper.PiOver2) * Matrix4.CreateRotationZ(MathHelper.Pi) * freeview.viewprojmatrix);
			GL.Uniform4(Common.sdrSolidColor_colorUniform, NEGATIVE_AXIS_COLOR);
			axismesh.Draw();

			// >>> Draw selection AABB

			if(selectionAabb != null)
			{
				linemesh.Bind(Common.sdrSolidColor);

				Matrix4 selectionAabbTransform = selectionAabb.GetTransform();
				Matrix4 offsetTransform = Matrix4.CreateTranslation(0.0f, 0.0f, -0.00001f);

				Common.sdrSolidColor.Bind(tickrotmatrix[0] * selectionAabbTransform * Matrix4.CreateScale(unitvector[0]) * freeview.viewprojmatrix * offsetTransform);
				GL.Uniform4(Common.sdrSolidColor_colorUniform, selectionAabbColor);
				linemesh.Draw();
				Common.sdrSolidColor.Bind(tickrotmatrix[1] * selectionAabbTransform * Matrix4.CreateScale(unitvector[1]) * freeview.viewprojmatrix * offsetTransform);
				linemesh.Draw();
				Common.sdrSolidColor.Bind(tickrotmatrix[2] * selectionAabbTransform * Matrix4.CreateScale(unitvector[2]) * freeview.viewprojmatrix * offsetTransform);
				linemesh.Draw();
			}

			// >>> Draw ticks

			GL.LineWidth(2.0f);

			tickmesh.Bind(Common.sdrSolidColor);

			for(int d = 0; d < 3; ++d)
			{
				if(__axis_dist[d] < ImageCloud.Z_NEAR)
					continue;

				for(int t = floor_viewpos[d] + LABEL_CAM_DISTANCE; t >= floor_viewpos[d]; --t)
				{
					if(t == 0)
						continue;
					
					Vector3 lblpos = unitvector[d] * (float)t * axis_dist[d];
					float lbldist = (freeview.viewpos - lblpos).Length;

					float opacity = (float)Math.Pow(axis_dist_fract[d], LABEL_AXIS_DISTANCE_FADE);
					if(t % 10 == 0)
						opacity *= 0.1f;
					opacity += (float)Math.Pow(lbldist / LABEL_CAM_DISTANCE, LABEL_CAM_DISTANCE_FADE);

					float scale = axis_dist_fract[d];
					if(t % 10 == 0)
						scale *= 0.01f;

					Color4 clr = t > 0 ? POSITIVE_AXIS_COLOR : NEGATIVE_AXIS_COLOR;
					clr.A = 1.0f - opacity;
					Common.sdrSolidColor.Bind(Matrix4.CreateScale(__axis_dist[d] * (1.0f - 0.5f * scale)) * tickrotmatrix[d] * Matrix4.CreateTranslation(lblpos) * freeview.viewprojmatrix);
					GL.Uniform4(Common.sdrSolidColor_colorUniform, clr);
					tickmesh.Draw();
				}
				for(int t = floor_viewpos[d] - LABEL_CAM_DISTANCE; t < floor_viewpos[d]; ++t)
				{
					if(t == 0)
						continue;

					Vector3 lblpos = unitvector[d] * (float)t * axis_dist[d];
					float lbldist = (freeview.viewpos - lblpos).Length;

					float opacity = (float)Math.Pow(axis_dist_fract[d], LABEL_AXIS_DISTANCE_FADE);
					if(t % 10 == 0)
						opacity *= 0.1f;
					opacity += (float)Math.Pow(lbldist / LABEL_CAM_DISTANCE, LABEL_CAM_DISTANCE_FADE);

					float scale = axis_dist_fract[d];
					if(t % 10 == 0)
						scale *= 0.01f;

					Color4 clr = t > 0 ? POSITIVE_AXIS_COLOR : NEGATIVE_AXIS_COLOR;
					clr.A = 1.0f - opacity;
					Common.sdrSolidColor.Bind(Matrix4.CreateScale(__axis_dist[d] * (1.0f - 0.5f * scale)) * tickrotmatrix[d] * Matrix4.CreateTranslation(lblpos) * freeview.viewprojmatrix);
					GL.Uniform4(Common.sdrSolidColor_colorUniform, clr);
					tickmesh.Draw();
				}
			}

			GL.LineWidth(1.0f);

			// >>> Draw labels

			Matrix4 vieworient = freeview.viewmatrix, invvieworient;
			vieworient.M41 = vieworient.M42 = vieworient.M43 = 0.0f;
			invvieworient = vieworient;
			invvieworient.Transpose();

			for(int d = 0; d < 3; ++d)
			{
				if(__axis_dist[d] < ImageCloud.Z_NEAR)
					continue;

				for(int t = floor_viewpos[d] + LABEL_CAM_DISTANCE; t >= floor_viewpos[d]; --t)
				{
					if(t == 0)
						continue;

					Vector3 lblpos = unitvector[d] * (float)t * axis_dist[d];
					float lbldist = (freeview.viewpos - lblpos).Length;

					float opacity = (float)Math.Pow(axis_dist_fract[d], LABEL_AXIS_DISTANCE_FADE);
					if(t % 10 == 0)
						opacity *= 0.1f;
					opacity += (float)Math.Pow(lbldist / LABEL_CAM_DISTANCE, LABEL_CAM_DISTANCE_FADE);

					Color4 clr = new Color4(1.0f, 1.0f, 1.0f, 1.0f - opacity);
					Vector3 offset = (freeview.viewpos - lblpos).Normalized() * 1.0f;
					Common.fontText2.DrawStringAt(lblpos + offset, freeview.viewprojmatrix, ((decimal)t * (decimal)axis_dist[d]).ToString(), backbuffersize, clr);
				}
				for(int t = floor_viewpos[d] - LABEL_CAM_DISTANCE; t < floor_viewpos[d]; ++t)
				{
					if(t == 0)
						continue;
					
					Vector3 lblpos = unitvector[d] * (float)t * axis_dist[d];
					float lbldist = (freeview.viewpos - lblpos).Length;

					float opacity = (float)Math.Pow(axis_dist_fract[d], LABEL_AXIS_DISTANCE_FADE);
					if(t % 10 == 0)
						opacity *= 0.1f;
					opacity += (float)Math.Pow(lbldist / LABEL_CAM_DISTANCE, LABEL_CAM_DISTANCE_FADE);

					Color4 clr = new Color4(1.0f, 1.0f, 1.0f, 1.0f - opacity);
					Vector3 offset = (freeview.viewpos - lblpos).Normalized() * 1.0f;
					Common.fontText2.DrawStringAt(lblpos + offset, freeview.viewprojmatrix, ((decimal)t * (decimal)axis_dist[d]).ToString(), backbuffersize, clr);
				}
			}

			// >>> Draw selection AABB labels

			if(selectionAabb != null)
			{
				for(int d = 0; d < 3; ++d)
				{
					if(Math.Abs(selectionAabb.max[d] - selectionAabb.min[d]) < 0.1f)
						continue;

					float value = (selectionAabb.max[d] + selectionAabb.min[d]) / 2.0f;

					Vector3 lblpos = unitvector[d] * value;
					float lbldist = (freeview.viewpos - lblpos).Length;

					float opacity = 0.0f;
					opacity += (float)Math.Pow(lbldist / LABEL_CAM_DISTANCE, LABEL_CAM_DISTANCE_FADE);

					Color4 clr = selectionAabbColor;
					clr.A = 1.0f - opacity;
					Vector3 offset = (freeview.viewpos - lblpos).Normalized() * 1.0f;
					Common.fontText2.DrawStringAt(lblpos + offset, freeview.viewprojmatrix, ((decimal)value).ToString(), backbuffersize, clr);
				}
			}
		}
	}
}

