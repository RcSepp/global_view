using System;
using System.IO;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace csharp_viewer
{
	public class GLShader
	{
		private int shaderprogram, vertexshader, fragmentshader, geometryshader;
		public struct DefaultParameters
		{
			public int world, worldarray, view, proj, viewproj, worldviewproj, worldviewprojarray, worldinvtrans, worldinvtransarray, textransform; // Matrices
			public int viewpos, ambient, diffuse, specular; // Vectors
			public int numlights, lightparams, power; // Light parameters
			public int tex, hastex; // Textures
			public int time; // Misc
		}
		public DefaultParameters defparams;
		public struct DefaultAttributes
		{
			public int pos, nml, texcoord, blendidx;
		}
		public DefaultAttributes defattrs;

		public GLShader(string filename)
		{
			if(!File.Exists(filename))
				throw new FileNotFoundException();

			DateTime writetime = File.GetLastWriteTime(filename);

			// >>> Create effect

			// If a precompiled effect file is found, use the precompiled effect
			bool compileshader = true;
#if DEBUG
			string compiledfilename = filename + "c_d";
#else
			string compiledfilename = filename + "c";
#endif
			if(File.Exists(compiledfilename))
			{
				BinaryReader compiledfile = new BinaryReader(new FileStream(compiledfilename, FileMode.Open, FileAccess.Read));
				DateTime compiledwritetime = DateTime.FromBinary((long)compiledfile.ReadInt64());
				if(compiledwritetime.Equals(writetime))
				{
					BinaryFormat compiledshaderformat = (BinaryFormat)compiledfile.ReadUInt64();
					byte[] compiledshaderbytes = compiledfile.ReadBytes((int)(compiledfile.BaseStream.Length - compiledfile.BaseStream.Position));
					compiledfile.Close();

					shaderprogram = GL.CreateProgram();
					GL.ProgramBinary<byte>(shaderprogram, compiledshaderformat, compiledshaderbytes, compiledshaderbytes.Length);
					int isLinked;
					GL.GetProgram(shaderprogram, GetProgramParameterName.LinkStatus, out isLinked);
					if(isLinked != 0)
						compileshader = false;
				}
				else
					compiledfile.Close();
			}

			// If a precompiled effect file wasn't found, compile the effect
			if(compileshader)
			{
				StreamReader shaderfile = new StreamReader(new FileStream(filename, FileMode.Open, FileAccess.Read));
				string shaderstr = shaderfile.ReadToEnd();
				shaderfile.Close();

				// Get #vs sections from shaderstr
				int vsstart = -1;
				do
				{
					vsstart = shaderstr.IndexOf("#vs");
					if(vsstart == -1)
						throw new IOException("No vertex shader section (#vs) defined in shader file");
					vsstart += "#vs".Length;
				} while(shaderstr[vsstart] != '\n' && shaderstr[vsstart] != '\r');

				// Get #fs sections from shaderstr
				int fsstart = -1;
				do
				{
					fsstart = shaderstr.IndexOf("#fs");
					if(fsstart == -1)
						throw new IOException("No fragment shader section (#fs) defined in shader file");
					fsstart += "#vs".Length;
				} while(shaderstr[fsstart] != '\n' && shaderstr[fsstart] != '\r');

				string vsstr = vsstart > fsstart ? shaderstr.Substring(vsstart) : shaderstr.Substring(vsstart, fsstart - "#fs".Length - vsstart);
				string fsstr = fsstart > vsstart ? shaderstr.Substring(fsstart) : shaderstr.Substring(fsstart, vsstart - "#vs".Length - fsstart);

				// Compile vertex shader
				int isCompiled;
				vertexshader = GL.CreateShader(ShaderType.VertexShader);
				GL.ShaderSource(vertexshader, vsstr);
				GL.CompileShader(vertexshader);
				GL.GetShader(vertexshader, ShaderParameter.CompileStatus, out isCompiled);
				if(isCompiled == 0)
					throw new Exception("Error compiling vertex shader:\n" + GL.GetShaderInfoLog(vertexshader));

				// Compile fragment shader
				fragmentshader = GL.CreateShader(ShaderType.FragmentShader);
//				GL.ShaderSource(fragmentshader, fsstr);
StreamReader foo = new StreamReader(new FileStream("test.gx", FileMode.Open, FileAccess.Read));
string[] bar = new string[] { fsstr, foo.ReadToEnd() };
foo.Close();
				GL.ShaderSource(fragmentshader, 2, bar, (int[])null);
				GL.CompileShader(fragmentshader);
				GL.GetShader(fragmentshader, ShaderParameter.CompileStatus, out isCompiled);
				if(isCompiled == 0)
					throw new Exception("Error compiling fragment shader:\n" + GL.GetShaderInfoLog(fragmentshader));

				int isLinked;
				shaderprogram = GL.CreateProgram();
				GL.AttachShader(shaderprogram, vertexshader);
				GL.AttachShader(shaderprogram, fragmentshader);
				GL.ProgramParameter(shaderprogram, ProgramParameterName.ProgramBinaryRetrievableHint, 1);
				GL.LinkProgram(shaderprogram);
				GL.GetProgram(shaderprogram, GetProgramParameterName.LinkStatus, out isLinked);
				if(isLinked == 0)
					throw new Exception("Error linking shader program:\n" + GL.GetProgramInfoLog(shaderprogram));

				// Get compiled effect binary
				int binarylength;
				BinaryFormat binaryformat;
				byte[] binary = new byte[binarylength = 1024 * 1024];
				//GL.GetProgram(shaderprogram, GetProgramParameterName.
				GL.GetProgramBinary<byte>(shaderprogram, binarylength, out binarylength, out binaryformat, binary);
				bool storecompiledeffect = (binarylength != 0);

				if(storecompiledeffect)
				{
					// Store compiled effect in a compiled effect file
					BinaryWriter compiledfile = new BinaryWriter(new FileStream(compiledfilename, FileMode.Create, FileAccess.Write));
					compiledfile.Write(writetime.ToBinary());
					compiledfile.Write((UInt64)binaryformat);
					compiledfile.Write(binary, 0, binarylength);
				}
			}

			GetDefaults();
		}

		public GLShader(string[] vs, string[] fs, string[] gs = null)
		{
			// Compile vertex shader
			int isCompiled;
			vertexshader = GL.CreateShader(ShaderType.VertexShader);
			GL.ShaderSource(vertexshader, vs.Length, vs, (int[])null);
			GL.CompileShader(vertexshader);
			GL.GetShader(vertexshader, ShaderParameter.CompileStatus, out isCompiled);
			if(isCompiled == 0)
				throw new Exception("Error compiling vertex shader:\n" + GL.GetShaderInfoLog(vertexshader));

			// Compile fragment shader
			fragmentshader = GL.CreateShader(ShaderType.FragmentShader);
			GL.ShaderSource(fragmentshader, fs.Length, fs, (int[])null);
			GL.CompileShader(fragmentshader);
			GL.GetShader(fragmentshader, ShaderParameter.CompileStatus, out isCompiled);
			if(isCompiled == 0)
				throw new Exception("Error compiling fragment shader:\n" + GL.GetShaderInfoLog(fragmentshader));

			if(gs != null)
			{
				// Compile geometry shader
				geometryshader = GL.CreateShader(ShaderType.GeometryShader);
				GL.ShaderSource(geometryshader, gs.Length, gs, (int[])null);
				GL.CompileShader(geometryshader);
				GL.GetShader(geometryshader, ShaderParameter.CompileStatus, out isCompiled);
				if(isCompiled == 0)
					throw new Exception("Error compiling geometry shader:\n" + GL.GetShaderInfoLog(geometryshader));
			}

			int isLinked;
			shaderprogram = GL.CreateProgram();
			GL.AttachShader(shaderprogram, vertexshader);
			GL.AttachShader(shaderprogram, fragmentshader);
			if(gs != null)
			{
				GL.ProgramParameter(shaderprogram, (Version32)0x8DDA, 4);
				GL.ProgramParameter(shaderprogram, (Version32)0x8DDB, 0);
				GL.ProgramParameter(shaderprogram, (Version32)0x8DDC, 7);
				GL.AttachShader(shaderprogram, geometryshader);
			}
			GL.ProgramParameter(shaderprogram, ProgramParameterName.ProgramBinaryRetrievableHint, 1);
			GL.LinkProgram(shaderprogram);
			GL.GetProgram(shaderprogram, GetProgramParameterName.LinkStatus, out isLinked);
			if(isLinked == 0)
				throw new Exception("Error linking shader program:\n" + GL.GetProgramInfoLog(shaderprogram));

			GetDefaults();
		}

		private void GetDefaults()
		{
			// >>> Obtain default parameters

			defparams.world = GL.GetUniformLocation(shaderprogram, "World");
			defparams.worldarray = GL.GetUniformLocation(shaderprogram, "WorldArray");
			defparams.view = GL.GetUniformLocation(shaderprogram, "View");
			defparams.proj = GL.GetUniformLocation(shaderprogram, "Proj");
			defparams.viewproj = GL.GetUniformLocation(shaderprogram, "ViewProj");
			defparams.worldviewproj = GL.GetUniformLocation(shaderprogram, "WorldViewProj");
			defparams.worldviewprojarray = GL.GetUniformLocation(shaderprogram, "WorldViewProjArray");
			defparams.worldinvtrans = GL.GetUniformLocation(shaderprogram, "WorldInvTrans");
			defparams.worldinvtransarray = GL.GetUniformLocation(shaderprogram, "WorldInvTransArray");
			defparams.textransform = GL.GetUniformLocation(shaderprogram, "TextureTransform");

			defparams.viewpos = GL.GetUniformLocation(shaderprogram, "ViewPos");
			defparams.ambient = GL.GetUniformLocation(shaderprogram, "Ambient");
			defparams.diffuse = GL.GetUniformLocation(shaderprogram, "Diffuse");
			defparams.specular = GL.GetUniformLocation(shaderprogram, "Specular");

			defparams.numlights = GL.GetUniformLocation(shaderprogram, "NumLights");
			defparams.lightparams = GL.GetUniformLocation(shaderprogram, "LightParams");
			defparams.power = GL.GetUniformLocation(shaderprogram, "Power");

			defparams.tex = GL.GetUniformLocation(shaderprogram, "Texture");
			defparams.hastex = GL.GetUniformLocation(shaderprogram, "HasTexture");

			defparams.time = GL.GetUniformLocation(shaderprogram, "Time");

			// >>> Obtain default vertex attributes

			defattrs.pos = GL.GetAttribLocation(shaderprogram, "vpos");
			defattrs.nml = GL.GetAttribLocation(shaderprogram, "vnml");
			defattrs.texcoord = GL.GetAttribLocation(shaderprogram, "vtexcoord");
			defattrs.blendidx = GL.GetAttribLocation(shaderprogram, "vblendidx");
		}

		public void Bind()
		{
			GL.UseProgram(shaderprogram);
		}
		public void Bind(Matrix4 worldmatrix)
		{
			GL.UseProgram(shaderprogram);

			if(defparams.world != -1)
				GL.UniformMatrix4(defparams.world, false, ref worldmatrix);
			if(defparams.worldinvtrans != -1)
			{
				Matrix4 worldinvtrans = Matrix4.Invert(worldmatrix);
				GL.UniformMatrix4(defparams.worldinvtrans, true, ref worldinvtrans);
			}
			/*if(defparams.viewpos)
				GL.Uniform3(defparams.viewpos, cam.pos);*/
		}

		public void SetTexture()
		{
			if(defparams.tex != -1)
				GL.Uniform1(defparams.tex, 0);
		}

		public int GetUniformLocation(string name)
		{
			return GL.GetUniformLocation(shaderprogram, name);
		}
	}
}

