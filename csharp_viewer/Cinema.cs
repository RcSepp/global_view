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
			public object[] values;
			public object defaultValue;
			public string name, label;
		}

		public class CinemaImage
		{
			public object[] values;
			public string filename;
			public string depth_filename;
			public Dictionary<string, object> meta;
			public OpenTK.Matrix4 view; // Required to recreate 3D pixel locations from depth image
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
				carg.values = argumentMeta["values"].ToObject<object[]>();
				carg.defaultValue = argumentMeta["default"].ToObject<object>();
				carg.label = argumentMeta["label"].ToObject<string>();

//if(matchidx == 1)
//	Array.Resize<object>(ref carg.values, 1);
			}
		}

		// Parse meta data from image Json
		public static void ParseImageDescriptor(string imagemetapath, out Dictionary<string, object> meta, out OpenTK.Matrix4 view)
		{
			meta = null;
			view = new OpenTK.Matrix4();

			if(!File.Exists(imagemetapath))
				return;

			StreamReader sr = new StreamReader(new FileStream(imagemetapath, FileMode.Open, FileAccess.Read));
			dynamic json = JsonConvert.DeserializeObject(sr.ReadToEnd());
			try {
				meta = ((JObject)json.variables).ToObject<Dictionary<string, object>>();
			}
			catch {}
			if(json["lookat"] != null)
			{
				float[] lookat = json["lookat"].ToObject<float[]>();
				if(lookat.Length == 9)
					view = OpenTK.Matrix4.LookAt(lookat[0], lookat[1], lookat[2], lookat[3], lookat[4], lookat[5], lookat[6], lookat[7], lookat[8]);
			}
			sr.Close();
		}
	}
}

