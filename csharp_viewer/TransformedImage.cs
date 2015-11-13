using System;
using System.Collections.Generic;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace csharp_viewer
{
	public class TransformedImage : Cinema.CinemaImage
	{
		//private GLTextureStream.Texture tex;
		public GLTexture2D tex;
		public int[] key;

		public bool visible = true, selected = false;
		public Vector3 pos, scl; //TODO: Make publicly readonly
		public Quaternion rot; //TODO: Make publicly readonly
		private Vector3 animatedPos = new Vector3(float.NaN);
		public void skipPosAnimation() { animatedPos = pos; }
		private bool locationInvalid = false;
		public void InvalidateLocation() { locationInvalid = true; }

		public delegate void LocationChangedDelegate();
		public event LocationChangedDelegate LocationChanged;

		private List<ImageTransform> transforms = new List<ImageTransform>();

		public int originalWidth = 0, originalHeight = 0;
		public float originalAspectRatio = 1.0f;
		public int renderWidth, renderHeight;
		public float renderPriority = 0;
		public System.Drawing.Bitmap bmp = null;
		private System.Drawing.Bitmap oldbmp = null;
		public System.Threading.Mutex renderMutex = new System.Threading.Mutex();
		public bool texIsStatic = false;
		public bool texFilterLinear = false;

		private float prefetchHoldTime = 0.0f; // To avoid images to be unloaded between prefetching and rendering, prefetchHoldTime gets set to the expected render time inside PrefetchRenderPriority()

		public bool HasDepthInfo { get {return depth_filename != null;} }

		public void LoadTexture(GLTextureStream texstream)
		{
			//if(tex == null && texstream != null)
			//	tex = texstream.CreateTexture(filename, depth_filename);
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

		public bool IsVisible()
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
			return _visible;
		}
		public bool IsVisible(ImageCloud.FreeView freeview, Matrix4 invvieworient, System.Drawing.Size backbuffersize, out Matrix4 transform)
		{
			// Compute dynamic visibility and check if dynamic location updates are required
			bool _visible = visible;
			bool hasDynamicLocationTransform = locationInvalid;
			foreach(ImageTransform t in transforms)
			{
				if(t.SkipImageInterval == ImageTransform.UpdateInterval.Dynamic)
					_visible &= !t.SkipImage(key, this);
				if(t.locationTransformInterval == ImageTransform.UpdateInterval.Dynamic)
					hasDynamicLocationTransform = true;
			}

			if(tex != null && !texIsStatic && (bmp == null || bmp != oldbmp)) // If a texture is loaded and either the image has been unloaded or changed
			{
				// Unload texture
				GL.DeleteTexture(tex.tex);
				tex = null;
			}
			oldbmp = bmp;

			transform = invview; //Matrix4.Identity
			if(_visible)
			{
				if(hasDynamicLocationTransform)
					ComputeLocation();

				//transform *= Matrix4.CreateTranslation(-0.5f, -0.5f, 0.0f);
				transform *= Matrix4.CreateScale(originalAspectRatio, 1.0f, 1.0f);
				//transform *= Matrix4.CreateScale(2.0f, 2.0f, 1.0f);
				if(depth_filename == null) // Do not always face screen when rendering volume images
					transform *= invvieworient;
				transform *= Matrix4.CreateTranslation(animatedPos);

				if(freeview.DoFrustumCulling(transform, Matrix4.Identity, Matrix4.Identity, new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.5f, 0.5f, 0.5f)))
				{
					transform *= freeview.viewprojmatrix;

					// Set render priority and dimensions (thread safety: priority has to be set after width/height)
					Vector3 vsize = Vector3.TransformPerspective(new Vector3(0.5f, 0.5f, 0.0f), transform) - Vector3.TransformPerspective(new Vector3(0.0f, 0.0f, 0.0f), transform); // Size of image in device units
					renderWidth = Math.Max(1, (int)(vsize.X * (float)backbuffersize.Width));
					renderHeight = Math.Max(1, (int)(vsize.Y * (float)backbuffersize.Height));
					//if(renderPriority == 0)
						renderPriority = 1000;

					return true;
				}
				else if(tex != null)
				{
					if(Global.time > prefetchHoldTime)
						renderPriority = 0;
					//tex.Unload();
					return false;
				}
				else
				{
					if(Global.time > prefetchHoldTime)
						renderPriority = 0;
					return false;
				}
			}
			else if(tex != null)
			{
				if(Global.time > prefetchHoldTime)
					renderPriority = 0;
				//tex.Unload();
				return false;
			}
			else
			{
				if(Global.time > prefetchHoldTime)
					renderPriority = 0;
				return false;
			}
		}
		public void PrefetchRenderPriority(ImageCloud.FreeView freeview, Matrix4 invvieworient, System.Drawing.Size backbuffersize)
		{
			// Compute dynamic visibility and check if dynamic location updates are required
			bool _visible = visible;
			bool hasDynamicSkipTransform = false, hasDynamicLocationTransform = false;
			foreach(ImageTransform t in transforms)
			{
				if(t.SkipImageInterval == ImageTransform.UpdateInterval.Dynamic)
				{
					hasDynamicSkipTransform = true;
					_visible &= !t.SkipImage(key, this);
				}
				//if(t.locationTransformInterval == ImageTransform.UpdateInterval.Dynamic) //EDIT: Compute location locally
				//	hasDynamicLocationTransform = true; //EDIT: Compute location locally
			}

			if(!hasDynamicSkipTransform && !hasDynamicLocationTransform) // If neither location nor skipping is time variant
				return; // Don't prefetch

			Matrix4 transform = invview; //Matrix4.Identity
			if(_visible)
			{
				//if(hasDynamicLocationTransform) //EDIT: Compute location locally
				//	ComputeLocation(); //EDIT: Compute location locally

				//transform *= Matrix4.CreateTranslation(-0.5f, -0.5f, 0.0f);
				transform *= Matrix4.CreateScale(originalAspectRatio, 1.0f, 1.0f);
				//transform *= Matrix4.CreateScale(2.0f, 2.0f, 1.0f);
				if(depth_filename == null) // Do not always face screen when rendering volume images
					transform *= invvieworient;
				transform *= Matrix4.CreateTranslation(animatedPos);

				if(freeview.DoFrustumCulling(transform, Matrix4.Identity, Matrix4.Identity, new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.5f, 0.5f, 0.5f)))
				{
					transform *= freeview.viewprojmatrix;

					// Set render priority and dimensions (thread safety: priority has to be set after width/height)
					Vector3 vsize = Vector3.TransformPerspective(new Vector3(0.5f, 0.5f, 0.0f), transform) - Vector3.TransformPerspective(new Vector3(0.0f, 0.0f, 0.0f), transform); // Size of image in device units
					renderWidth = Math.Max(1, (int)(vsize.X * (float)backbuffersize.Width));
					renderHeight = Math.Max(1, (int)(vsize.Y * (float)backbuffersize.Height));
					renderPriority = 1200; // Prefetch renderPriority should be higher than render renderPriority to prefere images being loaded before they are being rendered
					prefetchHoldTime = Global.time;
				}
			}
		}
		public void Render(GLMesh mesh, ImageCloud.RenderShader sdr, float depthscale, ImageCloud.FreeView freeview, Matrix4 transform, int fragmentcounter)
		{
			bool texloaded;
			if(texIsStatic)
				texloaded = true;
			else if(renderMutex.WaitOne(0))
			{
				if(bmp != null)
				{
					if(tex == null)
						tex = new GLTexture2D(bmp, false, texFilterLinear);
					renderMutex.ReleaseMutex();
					texloaded = true;
				}
				else
				{
					renderMutex.ReleaseMutex();
					texloaded = false;
				}
			}
			else
				texloaded = false;

			Color4 clr = new Color4(1.0f, 1.0f, 1.0f, 1.0f);
			/*if(selected)
			{
				clr.R = 1.0f;//Color4.Azure.R / 2.0f;
				clr.G = 0.5f;//Color4.Azure.G / 2.0f;
				clr.B = 0.5f;//Color4.Azure.B / 2.0f;
			}
			//clr.A = selected ? 1.0f : 0.3f;
			clr.A = 1.0f;*/

			sdr.Bind(transform, clr, texloaded, depthscale);
			/*GL.Uniform4(sdr_colorParam, clr);
			//if(sdr_imageViewInv != -1)
			//	GL.UniformMatrix4(sdr_imageViewInv, false, ref invview);
			if(sdr_DepthScale != -1)
				GL.Uniform1(sdr_DepthScale, depthscale);*/

			mesh.Bind(sdr, tex);//mesh.Bind(sdr, tex.tex, tex.depth_tex);
			//GL.BeginQuery(QueryTarget.SamplesPassed, fragmentcounter);
			mesh.Draw();
			//GL.EndQuery(QueryTarget.SamplesPassed);

			foreach(ImageTransform t in transforms)
				t.RenderImage(key, this, freeview);

			/*int numfragments;
			GL.GetQueryObject(fragmentcounter, GetQueryObjectParam.QueryResult, out numfragments);
			renderPriority = numfragments;*/
		}

		public Matrix4 GetWorldMatrix(Matrix4 invvieworient)
		{
			Matrix4 worldmatrix = invview; //Matrix4.Identity
			//worldmatrix *= Matrix4.CreateTranslation(-0.5f, -0.5f, 0.0f);
			//worldmatrix *= Matrix4.CreateScale((float)img.Width / (float)img.Height, 1.0f, 1.0f);
			//worldmatrix *= Matrix4.CreateScale(2.0f, 2.0f, 1.0f);
			if(depth_filename == null) // Do not always face screen when rendering volume images
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
			//if(tex == null)
			//	return float.MaxValue;

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

			float halfwidth = 0.5f * originalAspectRatio, halfheight = 0.5f;
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
			locationInvalid = false;
			pos = Vector3.Zero;
			rot = Quaternion.Identity;
			scl = Vector3.One;

			Vector3 transformpos;
			Matrix4 transformmatrix = Matrix4.Identity;
			for(int t = 0; t < transforms.Count - 1; ++t)
			{
				transforms[t].LocationTransform(key, this, out transformpos, ref rot, ref scl);
				pos += Vector3.TransformPosition(transformpos, transformmatrix);
				//transformmatrix *= transforms[t].GetTransformBounds(this).GetTransform();
			}
			if(transforms.Count > 0)
			{
				transforms[transforms.Count - 1].LocationTransform(key, this, out transformpos, ref rot, ref scl);
				pos += Vector3.TransformPosition(transformpos, transformmatrix);
			}

			// Do not animate towards initial position
			if(float.IsNaN(animatedPos.X))
				skipPosAnimation();

			if(LocationChanged != null)
				LocationChanged();
		}

		public AABB GetBounds()
		{
			AABB aabb = new AABB();

			//foreach(ImageTransform t in transforms)
			//	aabb.Include(t.ImageBounds(key, this));

			Matrix4 worldmatrix = invview; //Matrix4.Identity
			//worldmatrix *= Matrix4.CreateScale(2.0f, 2.0f, 1.0f);
			worldmatrix *= Matrix4.CreateTranslation(pos);

			aabb.Include(Vector3.TransformPosition(new Vector3(-0.5f * originalAspectRatio, -0.5f, -0.5f * originalAspectRatio), worldmatrix));
			aabb.Include(Vector3.TransformPosition(new Vector3( 0.5f * originalAspectRatio,  0.5f,  0.5f * originalAspectRatio), worldmatrix));

			return aabb;
		}
	}

	/*public class TransformedImageCollection : Dictionary<int[], TransformedImage>
	{

	}*/
	public class TransformedImageCollection : List<TransformedImage>
	{
		public List<TransformedImage> Values { get { return this; } }
		public IEnumerable<TransformedImage> ReverseValues { get
		{
			for(int i = this.Count - 1; i >= 0; --i)
			{
				yield return this[i];
			}
		}}
	}
}

