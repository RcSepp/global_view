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
			public Dictionary<string, object> meta;
		}

		// Parse meta data from info.json
		public static void ParseCinemaDescriptor(string databasePath, out CinemaArgument[] arguments, out string name_pattern, out string pixel_format)
		{
			arguments = null;
			name_pattern = null;
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
		public static Dictionary<string, object> ParseImageDescriptor(string imagemetapath)
		{
			if(File.Exists(imagemetapath))
			{
				StreamReader sr = new StreamReader(new FileStream(imagemetapath, FileMode.Open, FileAccess.Read));
				dynamic meta = JsonConvert.DeserializeObject(sr.ReadToEnd());
				sr.Close();

				return ((JObject)meta.variables).ToObject<Dictionary<string, object>>();
			}
			else
				return null;
		}
	}
}

