using System;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace csharp_viewer
{
	public static class Cinema
	{
		public class CinemaArgument
		{
			public float[] values;
			public string[] strValues;
			public SortedDictionary<float, List<TransformedImage>> images = new SortedDictionary<float, List<TransformedImage>>();

			public float defaultValue;
			public string name, label;
		}

		public class CinemaImage
		{
			public string filename;
			public string depth_filename;

			public float[] values;
			public CinemaArgument[] args;

			public Dictionary<string, object> meta;

			public OpenTK.Matrix4 invview; // Required to recreate 3D pixel locations from depth image
		}

		// Parse meta data from info.json
		public static void ParseCinemaDescriptor(string databasePath, out CinemaArgument[] arguments, out string name_pattern, out string depth_name_pattern, out string pixel_format)
		{
			arguments = null;
			name_pattern = null;
			depth_name_pattern = null;
			pixel_format = null;

			StreamReader sr = new StreamReader(new FileStream(databasePath + "image/info.json", FileMode.Open, FileAccess.Read));
			dynamic meta = JsonConvert.DeserializeObject(sr.ReadToEnd());
			sr.Close();

			try {
				name_pattern = meta.name_pattern;
			}
			catch {
				MessageBox.Show("Missing entry in info.json: name_pattern");
				return;
			}
			try {
				depth_name_pattern = meta.depth_name_pattern;
			} catch {}
			JObject argumentsMeta = null;
			try {
				argumentsMeta = meta.arguments;
			}
			catch {
				MessageBox.Show("Missing entry in info.json: arguments");
				return;
			}
			try {
				pixel_format = meta.metadata.pixel_format;
			}
			catch {}

			MatchCollection matches = Regex.Matches(name_pattern, "{\\w*}");
			arguments = new CinemaArgument[matches.Count];
			int matchidx = 0;
			foreach(Match match in matches)
			{
				string argumentStr = match.Value.Substring(1, match.Value.Length - 2);

				JToken argumentMeta = null;
				foreach(JProperty prop in argumentsMeta.Children())
				{
					if(argumentStr.Equals(prop.Name))
					{
						argumentMeta = prop.Value;
						break;
					}
				}
				if(argumentMeta == null)
				{
					MessageBox.Show("Missing argument: " + argumentStr);
					return;
				}

				// Create CinemaArgument from JToken
				CinemaArgument carg = arguments[matchidx++] = new CinemaArgument();
				carg.name = argumentStr;
				carg.label = argumentMeta["label"].ToObject<string>();
				carg.strValues = argumentMeta["values"].ToObject<string[]>();

				object[] values = argumentMeta["values"].ToObject<object[]>();
				object defaultValue = argumentMeta["default"].ToObject<object>();

				carg.values = new float[values.Length];
				for(int i = 0; i < values.Length; ++i)
				{
					if(values[i].GetType() == typeof(string))
						float.TryParse((string)values[i], out carg.values[i]);
					else if(values[i].GetType() == typeof(long))
						carg.values[i] = (float)(long)values[i];
				}

				if(defaultValue.GetType() == typeof(string))
					float.TryParse((string)defaultValue, out carg.defaultValue);
				else if(defaultValue.GetType() == typeof(long))
					carg.defaultValue = (float)(long)defaultValue;

//if(matchidx == 1)
//	Array.Resize<object>(ref carg.values, 1);
			}
		}

		// Parse meta data from image Json
		public static void ParseImageDescriptor(string imagemetapath, out Dictionary<string, object> meta, out OpenTK.Matrix4 invview)
		{
			meta = null;
			invview = OpenTK.Matrix4.Identity;

			if(!File.Exists(imagemetapath))
				return;

			StreamReader sr = new StreamReader(new FileStream(imagemetapath, FileMode.Open, FileAccess.Read));
			dynamic json = JsonConvert.DeserializeObject(sr.ReadToEnd());
			sr.Close();

			try {
				meta = ((JObject)json.variables).ToObject<Dictionary<string, object>>();
			}
			catch {}
			/*if(json["lookat"] != null)
			{
				float[] lookat = json["lookat"].ToObject<float[]>();
				if(lookat.Length == 9)
					invview = OpenTK.Matrix4.LookAt(lookat[0], lookat[1], lookat[2], -lookat[3], lookat[4], lookat[5], lookat[6], lookat[7], lookat[8]).Inverted(); //EDIT: -lookat[3] or lookat[3] ???
			} else if(json["theta-phi-xyz"] != null)
			{
				float[] theta_phi_xyz = json["theta-phi-xyz"].ToObject<float[]>();
				if(theta_phi_xyz.Length == 5)
				{
					invview = OpenTK.Matrix4.CreateTranslation(-theta_phi_xyz[2], theta_phi_xyz[3], -theta_phi_xyz[4]);
					invview *= OpenTK.Matrix4.CreateRotationY(theta_phi_xyz[0]);
					invview *= OpenTK.Matrix4.CreateRotationX(-theta_phi_xyz[1]);
					invview.Invert();
				}
			}*/

		}
	}
}

