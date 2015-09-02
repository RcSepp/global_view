using System;
using System.Collections.Generic;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace csharp_viewer
{
	public class TransformedImage : Cinema.CinemaImage
	{
		private GLTextureStream.Texture tex;
		public int[] key;

		public bool visible = true, selected = false;
		public Vector3 pos, scl; //TODO: Make publicly readonly
		public Quaternion rot; //TODO: Make publicly readonly
		private Vector3 animatedPos = new Vector3(float.NaN);
		public void skipPosAnimation() { animatedPos = pos; }

		private List<ImageTransform> transforms = new List<ImageTransform>();

		public void LoadTexture(GLTextureStream texstream)
		{
			if(tex == null && texstream != null)
				tex = texstream.CreateTexture(filename);
			/*{
				System.Drawing.Bitmap bmp = (System.Drawing.Bitmap)System.Drawing.Image.FromFile(filename);
				tex = texstream.CreateTexture(bmp);
			}*/
		}

		public void Update(float dt)
		{
			Common.AnimateTransition(ref animatedPos, pos, dt);
		}

		public void PrepareRender()
		{
			foreach(ImageTransform t in transforms)
				t.PrepareImage(key, this);
		}
		/*public void Render(GLMesh mesh, GLShader sdr, int sdr_colorParam, ImageCloud.FreeView freeview, Matrix4 invvieworient)
		{
			// Compute dynamic visibility and check if dynamic location updates are required
			bool _visible = visible;
			bool hasDynamicLocationTransform = false;
			foreach(ImageTransform t in transforms)
			{
				if(t.SkipImageInterval == ImageTransform.UpdateInterval.Dynamic)
					_visible &= !t.SkipImage(key, this);
				if(t.locationTransformInterval == ImageTransform.UpdateInterval.Dynamic)
					hasDynamicLocationTransform = true;
			}

			if(_visible)
			{
				if(hasDynamicLocationTransform)
					ComputeLocation();

				Matrix4 worldmatrix = Matrix4.Identity;
				worldmatrix *= Matrix4.CreateTranslation(-0.5f, -0.5f, 0.0f);
				worldmatrix *= Matrix4.CreateScale((float)img.Width / (float)img.Height, 1.0f, 1.0f);
				//worldmatrix *= Matrix4.CreateScale(2.0f, 2.0f, 1.0f);
				worldmatrix *= invvieworient;//Matrix4_CreateBillboardRotation(animatedPos, viewpos);
				worldmatrix *= Matrix4.CreateTranslation(animatedPos);

				if(freeview.DoFrustumCulling(worldmatrix, Matrix4.Identity, Matrix4.Identity, new Vector3(0.5f, 0.5f, 0.0f), new Vector3(0.5f, 0.5f, 0.5f)))
				{
					tex.Load();

					Color4 clr = new Color4(1.0f, 1.0f, 1.0f, 1.0f);
					if(selected)
					{
						clr.R += Color4.Azure.R / 2.0f;
						clr.G += Color4.Azure.G / 2.0f;
						clr.B += Color4.Azure.B / 2.0f;
					}

					sdr.Bind(worldmatrix * freeview.viewprojmatrix);
					GL.Uniform4(sdr_colorParam, clr);
					mesh.Bind(sdr, tex);
					mesh.Draw();
				}
				else
					tex.Unload();
			}
			else
				tex.Unload();
		}*/

		public bool IsVisible(ImageCloud.FreeView freeview, Matrix4 invvieworient, out Matrix4 transform)
		{
			// Compute dynamic visibility and check if dynamic location updates are required
			bool _visible = visible;
			bool hasDynamicLocationTransform = false;
			foreach(ImageTransform t in transforms)
			{
				if(t.SkipImageInterval == ImageTransform.UpdateInterval.Dynamic)
					_visible &= !t.SkipImage(key, this);
				if(t.locationTransformInterval == ImageTransform.UpdateInterval.Dynamic)
					hasDynamicLocationTransform = true;
			}

			transform = Matrix4.Identity;
			if(_visible)
			{
				if(hasDynamicLocationTransform)
					ComputeLocation();

				transform *= Matrix4.CreateTranslation(-0.5f, -0.5f, 0.0f);
				if(tex != null)
					transform *= Matrix4.CreateScale((float)tex.width / (float)tex.height, 1.0f, 1.0f);
				//transform *= Matrix4.CreateScale(2.0f, 2.0f, 1.0f);
				transform *= invvieworient;//Matrix4_CreateBillboardRotation(animatedPos, viewpos);
				transform *= Matrix4.CreateTranslation(animatedPos);

				if(freeview.DoFrustumCulling(transform, Matrix4.Identity, Matrix4.Identity, new Vector3(0.5f, 0.5f, 0.0f), new Vector3(0.5f, 0.5f, 0.5f)))
				{
					transform *= freeview.viewprojmatrix;
					return true;
				}
				else if(tex != null)
				{
					tex.Unload();
					return false;
				}
				else
					return false;
			}
			else if(tex != null)
			{
				tex.Unload();
				return false;
			}
			else
				return false;
		}
		public void Render(GLMesh mesh, GLShader sdr, int sdr_colorParam, ImageCloud.FreeView freeview, Matrix4 transform)
		{
			tex.Load();

			Color4 clr = new Color4(1.0f, 1.0f, 1.0f, 1.0f);
			/*if(selected)
			{
				clr.R += Color4.Azure.R / 2.0f;
				clr.G += Color4.Azure.G / 2.0f;
				clr.B += Color4.Azure.B / 2.0f;
			}*/
			//clr.A = selected ? 1.0f : 0.3f;
			clr.A = 1.0f;

			sdr.Bind(transform);
			GL.Uniform4(sdr_colorParam, clr);
			mesh.Bind(sdr, tex);
			mesh.Draw();

			foreach(ImageTransform t in transforms)
				t.RenderImage(key, this, freeview);
		}

		public Matrix4 GetWorldMatrix(Matrix4 invvieworient)
		{
			Matrix4 worldmatrix = Matrix4.Identity;
			//worldmatrix *= Matrix4.CreateTranslation(-0.5f, -0.5f, 0.0f);
			//worldmatrix *= Matrix4.CreateScale((float)img.Width / (float)img.Height, 1.0f, 1.0f);
			//worldmatrix *= Matrix4.CreateScale(2.0f, 2.0f, 1.0f);
			worldmatrix *= invvieworient;//Matrix4_CreateBillboardRotation(animatedPos, viewpos);
			worldmatrix *= Matrix4.CreateTranslation(animatedPos);

			return worldmatrix;
		}

		private static Matrix4 Matrix4_CreateBillboardRotation(Vector3 targetpos, Vector3 viewpos)
		{
			Vector3 view = targetpos - viewpos; view.Normalize();
			Vector3 up = new Vector3(0.0f, 1.0f, 0.0f);
			Vector3 right = Vector3.Cross(view, up); right.Normalize();
			up = Vector3.Cross(right, view); up.Normalize();

			return new Matrix4(right.X, right.Y, right.Z, 0.0f,
							   up.X, up.Y, up.Z, 0.0f,
							   view.X, view.Y, view.Z, 0.0f,
							   0.0f, 0.0f, 0.0f, 1.0f);
		}

		public float CastRay(Vector3 from, Vector3 dir, Matrix4 invvieworient)
		{
			if(tex == null)
				return float.MaxValue;

			// Compute dynamic visibility
			bool _visible = visible;
			foreach(ImageTransform t in transforms)
				if(t.SkipImageInterval == ImageTransform.UpdateInterval.Dynamic)
					_visible &= !t.SkipImage(key, this);
			if(!_visible)
				return float.MaxValue;

			Matrix4 invworldmatrix = GetWorldMatrix(invvieworient);
			invworldmatrix.Invert();
			from = Vector3.TransformPerspective(from, invworldmatrix); //TransformPosition
			if(from.Z < 0.0f)
				return float.MaxValue;
			dir = Vector3.TransformNormal(dir, invworldmatrix);

			Vector3 dest = from - dir * (from.Z / dir.Z);

			//System.Windows.Forms.MessageBox.Show(dest.ToString());

			float halfwidth = 0.5f * (float)tex.width / (float)tex.height, halfheight = 0.5f;
			return -halfwidth < dest.X && dest.X < halfwidth && -halfheight < dest.Y && dest.Y < halfheight ? from.Z : float.MaxValue;
		}

		public void AddTransform(ImageTransform transform)
		{
			transforms.Add(transform);

			// Evaluate transform visibility
			visible = true;
			foreach(ImageTransform t in transforms)
				if(t.SkipImageInterval == ImageTransform.UpdateInterval.Static)
					visible &= !t.SkipImage(key, this);

			ComputeLocation();

			// Update transform bounds
			transform.OnAddTransform(key, this);
		}
		public void RemoveTransform(ImageTransform transform)
		{
			if(!transforms.Remove(transform))
				return;

			// Evaluate transform visibility
			visible = true;
			foreach(ImageTransform t in transforms)
				if(t.SkipImageInterval == ImageTransform.UpdateInterval.Static)
					visible &= !t.SkipImage(key, this);

			ComputeLocation();
		}
		public void ClearTransforms()
		{
			transforms.Clear();

			visible = true;

			ComputeLocation();
		}

		public void ComputeLocation()
		{
			pos = Vector3.Zero;
			rot = Quaternion.Identity;
			scl = Vector3.One;

			Vector3 transformpos;
			Matrix4 transformmatrix = Matrix4.Identity;
			for(int t = 0; t < transforms.Count - 1; ++t)
			{
				transforms[t].LocationTransform(key, this, out transformpos, ref rot, ref scl);
				pos += Vector3.TransformPosition(transformpos, transformmatrix);
				transformmatrix *= transforms[t].GetTransformBounds(this).GetTransform();
			}
			if(transforms.Count > 0)
			{
				transforms[transforms.Count - 1].LocationTransform(key, this, out transformpos, ref rot, ref scl);
				pos += Vector3.TransformPosition(transformpos, transformmatrix);
			}

			// Do not animate towards initial position
			if(float.IsNaN(animatedPos.X))
				skipPosAnimation();
		}

		public AABB GetBounds()
		{
			if(tex == null)
				return new AABB();

			AABB aabb = new AABB();

			//foreach(ImageTransform t in transforms)
			//	aabb.Include(t.ImageBounds(key, this));

			Matrix4 worldmatrix = Matrix4.Identity;
			//worldmatrix *= Matrix4.CreateScale(2.0f, 2.0f, 1.0f);
			worldmatrix *= Matrix4.CreateTranslation(pos);

			float aspectRatio = (float)tex.width / (float)tex.height;
			aabb.Include(Vector3.TransformPosition(new Vector3(-0.5f * aspectRatio, -0.5f, -0.5f * aspectRatio), worldmatrix));
			aabb.Include(Vector3.TransformPosition(new Vector3( 0.5f * aspectRatio,  0.5f,  0.5f * aspectRatio), worldmatrix));

			return aabb;
		}
	}
}

