using System;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace csharp_viewer
{
	public class GLMesh
	{
		private int posbuffer = -1, nmlbuffer = -1, tgtbuffer = -1, bnmbuffer = -1, texcoordbuffer = -1, idxbuffer = -1;
		int numvertices, numindices;
		PrimitiveType primitivetype;

		public GLMesh(Vector3[] positions, Vector3[] normals = null, Vector3[] tangents = null, Vector3[] binormals = null, Vector2[] texcoords = null, int[] indices = null, PrimitiveType? _primitivetype = null)
		{
			if(positions != null) // Mesh vertex positions array can't be null
				Reset(positions, normals, tangents, binormals, texcoords, indices, _primitivetype);
		}

		public void Free()
		{
			if(posbuffer != -1)
			{
				GL.DeleteBuffer(posbuffer);
				posbuffer = -1;
			}
			if(nmlbuffer != -1)
			{
				GL.DeleteBuffer(nmlbuffer);
				nmlbuffer = -1;
			}
			if(tgtbuffer != -1)
			{
				GL.DeleteBuffer(tgtbuffer);
				tgtbuffer = -1;
			}
			if(bnmbuffer != -1)
			{
				GL.DeleteBuffer(bnmbuffer);
				bnmbuffer = -1;
			}
			if(texcoordbuffer != -1)
			{
				GL.DeleteBuffer(texcoordbuffer);
				texcoordbuffer = -1;
			}
			if(idxbuffer != -1)
			{
				GL.DeleteBuffer(idxbuffer);
				idxbuffer = -1;
			}
		}

		public void Reset(Vector3[] positions, Vector3[] normals = null, Vector3[] tangents = null, Vector3[] binormals = null, Vector2[] texcoords = null, int[] indices = null, PrimitiveType? _primitivetype = null)
		{
			numvertices = positions.Length;
			numindices = 0;

			if(posbuffer == -1)
				posbuffer = GL.GenBuffer();
			GL.BindBuffer(BufferTarget.ArrayBuffer, posbuffer);
			GL.BufferData<Vector3>(BufferTarget.ArrayBuffer, (IntPtr)(positions.Length * 3 * sizeof(float)), positions, BufferUsageHint.StaticDraw);
			if(normals != null)
			{
				if(nmlbuffer == -1)
					nmlbuffer = GL.GenBuffer();
				GL.BindBuffer(BufferTarget.ArrayBuffer, nmlbuffer);
				GL.BufferData<Vector3>(BufferTarget.ArrayBuffer, (IntPtr)(normals.Length * 3 * sizeof(float)), normals, BufferUsageHint.StaticDraw);
			}
			else if(nmlbuffer != -1)
				GL.DeleteBuffer(nmlbuffer);
			if(tangents != null)
			{
				if(tgtbuffer == -1)
					tgtbuffer = GL.GenBuffer();
				GL.BindBuffer(BufferTarget.ArrayBuffer, tgtbuffer);
				GL.BufferData<Vector3>(BufferTarget.ArrayBuffer, (IntPtr)(tangents.Length * 3 * sizeof(float)), tangents, BufferUsageHint.StaticDraw);
			}
			else if(tgtbuffer != -1)
				GL.DeleteBuffer(tgtbuffer);
			if(binormals != null)
			{
				if(bnmbuffer == -1)
					bnmbuffer = GL.GenBuffer();
				GL.BindBuffer(BufferTarget.ArrayBuffer, bnmbuffer);
				GL.BufferData<Vector3>(BufferTarget.ArrayBuffer, (IntPtr)(binormals.Length * 3 * sizeof(float)), binormals, BufferUsageHint.StaticDraw);
			}
			else if(bnmbuffer != -1)
				GL.DeleteBuffer(bnmbuffer);
			if(texcoords != null)
			{
				if(texcoordbuffer == -1)
					texcoordbuffer = GL.GenBuffer();
				GL.BindBuffer(BufferTarget.ArrayBuffer, texcoordbuffer);
				GL.BufferData<Vector2>(BufferTarget.ArrayBuffer, (IntPtr)(texcoords.Length * 2 * sizeof(float)), texcoords, BufferUsageHint.StaticDraw);
			}
			else if(texcoordbuffer != -1)
				GL.DeleteBuffer(texcoordbuffer);
			if(indices != null)
			{
				if(idxbuffer == -1)
					idxbuffer = GL.GenBuffer();
				GL.BindBuffer(BufferTarget.ElementArrayBuffer, idxbuffer);
				GL.BufferData<int>(BufferTarget.ElementArrayBuffer, (IntPtr)(indices.Length * sizeof(int)), indices, BufferUsageHint.StaticDraw);
				numindices = indices.Length;
				if(_primitivetype == null)
					_primitivetype = PrimitiveType.Triangles; // Default primitive type for indexed geometry is TRIANGLES
			}
			else
			{
				if(idxbuffer != -1)
					GL.DeleteBuffer(idxbuffer);
				if(_primitivetype == null)
					_primitivetype = PrimitiveType.TriangleStrip; // Default primitive type for non-indexed geometry is TRIANGLE_STRIP
			}
			primitivetype = _primitivetype.Value;
		}

		public void Bind(GLShader shader, GLTexture texture = null, GLTexture texture2 = null)
		{
			if(posbuffer == -1) // Mesh without vertex positions can't be rendered
				return;

			/*for(var i = 0; i < 16; i++)
				GL.DisableVertexAttribArray(i);

			GL.EnableVertexAttribArray(sdr.vertexPositionAttribute);*/
			GL.BindBuffer(BufferTarget.ArrayBuffer, posbuffer);
			GL.EnableClientState(ArrayCap.VertexArray);
			GL.VertexPointer(3, VertexPointerType.Float, 0, IntPtr.Zero);
			/*GL.VertexAttribPointer(sdr.vertexPositionAttribute, 3, VertexAttribType.Float, false, 0, 0);*/
			if(nmlbuffer != -1 /*&& sdr.vertexNormalAttribute != -1*/)
			{
				/*GL.EnableVertexAttribArray(sdr.vertexNormalAttribute);*/
				GL.BindBuffer(BufferTarget.ArrayBuffer, nmlbuffer);
				/*GL.VertexAttribPointer(sdr.vertexNormalAttribute, 3, gl.FLOAT, false, 0, 0);*/
			}
			/*if(tgtbuffer != -1 && sdr.vertexTangentAttribute != -1)
			{
				GL.EnableVertexAttribArray(sdr.vertexTangentAttribute);
				GL.BindBuffer(BufferTarget.ArrayBuffer, tgtbuffer);
				GL.VertexAttribPointer(sdr.vertexTangentAttribute, 3, gl.FLOAT, false, 0, 0);
			}
			if(bnmbuffer != -1 && sdr.vertexBinormalAttribute != -1)
			{
				GL.EnableVertexAttribArray(sdr.vertexBinormalAttribute);
				GL.BindBuffer(BufferTarget.ArrayBuffer, bnmbuffer);
				GL.VertexAttribPointer(sdr.vertexBinormalAttribute, 3, gl.FLOAT, false, 0, 0);
			}*/
			if(texcoordbuffer != -1 && shader.defattrs.texcoord != -1)
			{
				GL.EnableVertexAttribArray(shader.defattrs.texcoord);
				GL.BindBuffer(BufferTarget.ArrayBuffer, texcoordbuffer);
				GL.VertexAttribPointer(shader.defattrs.texcoord, 2, VertexAttribPointerType.Float, false, 0, 0);
			}
			if(texture != null)
			{
				GL.ActiveTexture(TextureUnit.Texture0);
				texture.Bind();
				if(shader != null)
					shader.SetTexture(0);
			}
			if(texture2 != null)
			{
				GL.ActiveTexture(TextureUnit.Texture1);
				texture2.Bind();
				if(shader != null)
					shader.SetTexture(1);
			}
			if(idxbuffer != -1)
				GL.BindBuffer(BufferTarget.ElementArrayBuffer, idxbuffer);
		}

		public void Draw()
		{
			if(posbuffer == -1) // Mesh without vertex positions can't be rendered
				return;

			if(idxbuffer != -1)
				GL.DrawElements(primitivetype, numindices, DrawElementsType.UnsignedInt, 0);
			else
				GL.DrawArrays(primitivetype, 0, numvertices);
		}

		public void Draw(int start, int count)
		{
			if(posbuffer == -1) // Mesh without vertex positions can't be rendered
				return;

			if(idxbuffer != -1)
				GL.DrawElements(primitivetype, count, DrawElementsType.UnsignedInt, start);
			else
				GL.DrawArrays(primitivetype, start, count);
		}
	}

	public class GLDynamicMesh
	{
		private int numvertices, posbuffer;
		private PrimitiveType primitivetype;

		public GLDynamicMesh(int numvertices)
		{
			this.numvertices = numvertices;
			posbuffer = GL.GenBuffer();
		}
		public void Bind(Vector3[] vertices, PrimitiveType primitivetype)
		{
			if(vertices.Length != numvertices)
				throw new InvalidOperationException("vertices.Length != numvertices");

			GL.BindBuffer(BufferTarget.ArrayBuffer, posbuffer);
			GL.EnableClientState(ArrayCap.VertexArray);
			GL.VertexPointer(3, VertexPointerType.Float, 0, IntPtr.Zero);
			GL.BufferData<Vector3>(BufferTarget.ArrayBuffer, (IntPtr)(vertices.Length * 3 * sizeof(float)), vertices, BufferUsageHint.DynamicDraw);
			this.primitivetype = primitivetype;
		}
		public void Draw()
		{
			GL.DrawArrays(primitivetype, 0, numvertices);
		}
	}
}

