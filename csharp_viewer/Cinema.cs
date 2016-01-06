﻿using System;
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

			public void AddValue(float value, string strValue)
			{
				Array.Resize(ref values, values.Length + 1);
				Array.Resize(ref strValues, strValues.Length + 1);

				int idx;
				//for(idx = values.Length - 1; idx >= 0 && values[idx] >= value; --idx) {}
				for(idx = 0; idx < values.Length - 1 && values[idx] <= value; ++idx) {}

				for(int i = values.Length - 2; i >= idx; --i)
				{
					values[i + 1] = values[i];
					strValues[i + 1] = strValues[i];
				}
				values[idx] = value;
				strValues[idx] = strValue;
			}

			public float defaultValue;
			public string defaultStrValue;
			public string name, label;

			public static CinemaArgument Find(CinemaArgument[] arguments, string label)
			{
				foreach(CinemaArgument arg in arguments)
					if(label.Equals(arg.label))
						return arg;
				return null;
			}
			public static int FindIndex(CinemaArgument[] arguments, string label)
			{
				for(int argidx = 0; argidx < arguments.Length; ++argidx)
					if(label.Equals(arguments[argidx].label))
						return argidx;
				return -1;
			}
		}

		public class CinemaImage
		{
			public float[] values;
			public string[] strValues;
			public CinemaArgument[] args;
			public int[] globalargindices;

			public Dictionary<string, object> meta;

			public OpenTK.Matrix4 invview; // Required to recreate 3D pixel locations from depth image
		}

		public static bool IsCinemaDB(string path)
		{
			return File.Exists(path + "/image/info.json");
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
			JObject associationsMeta = null;
			try {
				associationsMeta = meta.associations;
			}
			catch {}
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

				Dictionary<string, int> strValueIndices = null;
				if(defaultValue.GetType() == typeof(string))
				{
					if(!float.TryParse((string)defaultValue, out carg.defaultValue))
					{
						strValueIndices = new Dictionary<string, int>();
						strValueIndices[(string)defaultValue] = 0;
						carg.defaultValue = 0.0f;
					}
				}
				else if(defaultValue.GetType() == typeof(long))
					carg.defaultValue = (float)(long)defaultValue;

				carg.values = new float[values.Length];
				for(int i = 0; i < values.Length; ++i)
				{
					if(strValueIndices != null)
					{
						// Values are indices of unique strings (mapping through strValueIndices)
						int index;
						if(!strValueIndices.TryGetValue((string)values[i], out index))
							strValueIndices[(string)values[i]] = index = strValueIndices.Count;
						carg.values[i] = (float)index;
						continue;
					}

					if(values[i].GetType() == typeof(string))
						float.TryParse((string)values[i], out carg.values[i]);
					else if(values[i].GetType() == typeof(long))
						carg.values[i] = (float)(long)values[i];
				}
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

			if(json["lookat"] != null)
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
			}
		}



		public class CinemaStore
		{
			private string namePattern, depthNamePattern;
			private Dictionary<string, CinemaArgument> argumentMap = new Dictionary<string, CinemaArgument>();
			public CinemaArgument[] arguments;
			public class Parameter : CinemaArgument
			{
				//public string name;
				//public string defaultValue;
				public bool isField, isLayer;
				//public string label;
				//public string[] values;
				public string type;
				public string[] types;

				public string depthValue, lumValue;

				public override string ToString()
				{
					return string.Format("Parameter {0}", name);
				}
			}
			private Dictionary<string, Parameter> parameterMap = new Dictionary<string, Parameter>();
			public Parameter[] parameters;
			public class DependencyMap : Dictionary<CinemaArgument, string[]> {}
			public struct Association
			{
				public Parameter parameter;
				public DependencyMap dependencyMap;
				public Association(Parameter parameter, DependencyMap dependencyMap)
				{
					this.parameter = parameter;
					this.dependencyMap = dependencyMap;
				}
			}
			private List<Association> associations = new List<Association>();

			public static CinemaStore Load(string filename)
			{
				CinemaStore store = new CinemaStore();

				StreamReader sr = new StreamReader(new FileStream(filename, FileMode.Open, FileAccess.Read));
				dynamic meta = JsonConvert.DeserializeObject(sr.ReadToEnd());
				sr.Close();

				try {
					store.namePattern = meta.name_pattern;
				}
				catch {
					throw new Exception("Missing entry in info.json: name_pattern");
				}
				try { store.depthNamePattern = meta.depth_name_pattern; } catch {}
				JObject argumentsMeta = null;
				try {
					argumentsMeta = meta.arguments;
				}
				catch {
					throw new Exception("Missing entry in info.json: arguments");
				}
					
				foreach(KeyValuePair<string, JToken> argumentMeta in argumentsMeta)
				{
					CinemaArgument carg;
					Parameter parameter;

					string type = (string)argumentMeta.Value["type"];
					if(type == "range")
					{
						// argumentMeta describes image dimension

						// Create CinemaArgument from JToken
						parameter = null;
						carg = new CinemaArgument();
						store.argumentMap.Add(argumentMeta.Key, carg);
						carg.name = argumentMeta.Key;
						carg.label = argumentMeta.Value["label"].ToObject<string>();
						carg.strValues = argumentMeta.Value["values"].ToObject<string[]>();
					} else if(type == "option" || type == "hidden")
					{
						// argumentMeta describes option
						carg = parameter = new Parameter();
						parameter.name = argumentMeta.Key;
						parameter.defaultStrValue = (string)argumentMeta.Value["default"];
						parameter.isField = (string)argumentMeta.Value["isfield"] == "yes" ? true : false;
						parameter.isLayer = (string)argumentMeta.Value["islayer"] == "yes" ? true : false;
						parameter.label = (string)argumentMeta.Value["label"];
						parameter.strValues = (string[])argumentMeta.Value["values"].ToObject<string[]>();
						parameter.type = (string)argumentMeta.Value["type"];
						try
						{
							parameter.types = (string[])argumentMeta.Value["types"].ToObject<string[]>();
						} catch
						{
						}

						store.parameterMap.Add(argumentMeta.Key, parameter);
					} else
						throw new Exception("Invalid value for argument type: " + type);

					object[] values = argumentMeta.Value["values"].ToObject<object[]>();
					if(parameter != null && parameter.types != null)
					{
						int depthIdx = Array.IndexOf<string>(parameter.types, "depth");
						if(depthIdx != -1)
						{
							parameter.depthValue = parameter.strValues[depthIdx];
							values = values.RemoveAt(depthIdx);
							parameter.strValues = parameter.strValues.RemoveAt(depthIdx);
							parameter.types = parameter.types.RemoveAt(depthIdx);
						}

						int lumIdx = Array.IndexOf<string>(parameter.types, "luminance");
						if(lumIdx != -1)
						{
							parameter.lumValue = parameter.strValues[lumIdx];
							values = values.RemoveAt(lumIdx);
							parameter.strValues = parameter.strValues.RemoveAt(lumIdx);
							parameter.types = parameter.types.RemoveAt(lumIdx);
						}
					}
					object defaultValue = argumentMeta.Value["default"].ToObject<object>();

					Dictionary<string, int> strValueIndices = null;
					if(defaultValue.GetType() == typeof(string))
					{
						if(!float.TryParse((string)defaultValue, out carg.defaultValue))
						{
							strValueIndices = new Dictionary<string, int>();
							strValueIndices[(string)defaultValue] = 0;
							carg.defaultValue = 0.0f;
						}
					}
					else if(defaultValue.GetType() == typeof(long))
						carg.defaultValue = (float)(long)defaultValue;

					carg.values = new float[values.Length];
					for(int i = 0; i < values.Length; ++i)
					{
						if(strValueIndices != null)
						{
							// Values are indices of unique strings (mapping through strValueIndices)
							int index;
							if(!strValueIndices.TryGetValue((string)values[i], out index))
								strValueIndices[(string)values[i]] = index = strValueIndices.Count;
							carg.values[i] = (float)index;
							continue;
						}

						if(values[i].GetType() == typeof(string))
							float.TryParse((string)values[i], out carg.values[i]);
						else if(values[i].GetType() == typeof(long))
							carg.values[i] = (float)(long)values[i];
					}
				}
				store.arguments = new CinemaArgument[store.argumentMap.Count];
				store.argumentMap.Values.CopyTo(store.arguments, 0);
				store.parameters = new Parameter[store.parameterMap.Count];
				store.parameterMap.Values.CopyTo(store.parameters, 0);
				Array.Sort(store.parameters, new ParameterComparer());

				JObject associationsMeta = null;
				try {
					associationsMeta = meta.associations;
				}
				catch {}

				if(associationsMeta != null)
					foreach(KeyValuePair<string, JToken> associationMeta in associationsMeta)
					{
						Parameter parameter;
						if(!store.parameterMap.TryGetValue(associationMeta.Key, out parameter))
							throw new Exception(string.Format("Association for inexistent parameter '{0}'", associationMeta.Key));

						DependencyMap dependencyMap = new DependencyMap();
						store.associations.Add(new Association(parameter, dependencyMap));

						foreach(KeyValuePair<string, JToken> dependencyMeta in (JObject)associationMeta.Value)
						{
							string[] validValues;
							if(dependencyMeta.Value.HasValues)
								validValues = (string[])dependencyMeta.Value.ToObject<string[]>();
							else
							{
								validValues = new string[1];
								validValues[0] = (string)dependencyMeta.Value;
							}

							Parameter dependentParameter;
							CinemaArgument dependentArgument;
							if(store.parameterMap.TryGetValue(dependencyMeta.Key, out dependentParameter))
								dependencyMap.Add(dependentParameter, validValues);
							else if(store.argumentMap.TryGetValue(dependencyMeta.Key, out dependentArgument))
								dependencyMap.Add(dependentArgument, validValues);
							else
								throw new Exception(string.Format("Association depends on inexistent parameter or argument '{0}'", dependencyMeta.Key));
						}
					}

				return store;
			}

			public class KeyCollection : IEnumerable<int[]>
			{
				private CinemaStore store;

				public KeyCollection(CinemaStore store)
				{
					this.store = store;
				}

				public IEnumerator<int[]> GetEnumerator()
				{
					int[] argidx = new int[store.arguments.Length];
					bool done;
					do {
						int[] copy = new int[argidx.Length];
						Array.Copy(argidx, copy, argidx.Length);
						yield return copy;

						// Get next argument combination -> argidx[]
						done = true;
						for(int i = 0; i < store.arguments.Length; ++i) {
							if(++argidx[i] == store.arguments[i].values.Length)
								argidx[i] = 0;
							else {
								done = false;
								break;
							}
						}
					} while(!done);
				}
				System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
				{
					return this.GetEnumerator();
				}
			}
			public KeyCollection iterateKeys()
			{
				return new KeyCollection(this);
			}
				
			private class ParameterComparer : IComparer<Parameter>
			{
				public int Compare(Parameter x, Parameter y)
				{
					return string.Compare(x.name, y.name);
				}
			}
			public Association[] GetDependentAssociations(int[] argidx)
			{
				SortedDictionary<Parameter, DependencyMap> dependentAssociations = new SortedDictionary<Parameter, DependencyMap>(new ParameterComparer());

				for(int i = 0; i < associations.Count; ++i)
				{
					Association association = associations[i];

					if(dependentAssociations.ContainsKey(association.parameter))
						continue; // association is already part of dependentAssociations

					bool dependenciesSatisfied = true;
					foreach(KeyValuePair<CinemaArgument, string[]> dependency in association.dependencyMap)
					{
						Parameter dependentParameter = dependency.Key as Parameter;
						if(dependentParameter != null) // If dependency.Key is a parameter
						{
							if(!dependentAssociations.ContainsKey(dependentParameter))
							{
								// Dependency not satisfied (dependent parameter is not part of dependentAssociations)
								dependenciesSatisfied = false;
								break;
							}

							// EDIT: Validate value of dependentParameter
						}
						else // If dependency.Key is an argument
						{
							CinemaArgument dependentArgument = dependency.Key;

							// Validate value of dependentArgument
							int dependentArgumentIdx = Array.IndexOf<CinemaArgument>(arguments, dependentArgument);
							if(Array.IndexOf(dependency.Value, dependentArgument.strValues[argidx[dependentArgumentIdx]]) == -1)
							{
								// Dependency not satisfied (strValue wasn't part of valid string-values of dependency)
								dependenciesSatisfied = false;
								break;
							}
						}
					}

					if(dependenciesSatisfied)
					{
						// Add association and restart for loop ()
						dependentAssociations.Add(association.parameter, association.dependencyMap);
						i = 0;
					}
				}

				/*// Sort dependent associations alphabetically (because this is the order in which image paths are assembled)
				dependentAssociations.Sort(delegate(Parameter x, Parameter y) {
					return strcmp(x.name, y.name);
				});

				return dependentAssociations.ToArray();*/

				/*Parameter[] dependentAssociationsArray = new Parameter[dependentAssociations.Count];
				dependentAssociations.CopyTo(dependentAssociationsArray);
				return dependentAssociationsArray;*/

				Association[] dependentAssociationsArray = new Association[dependentAssociations.Count];
				int j = 0;
				foreach(KeyValuePair<Parameter, DependencyMap> dependentAssociation in dependentAssociations)
					dependentAssociationsArray[j++] = new Association(dependentAssociation.Key, dependentAssociation.Value);
				return dependentAssociationsArray;
			}

			/*public void GetImageFilePath(int[] argidx, Association[] dependentAssociations, out string imagepath, out string imageDepthPath, out string imageLumPath, out bool isFloatImage)
			{
				Dictionary<string, string> imageParameters = new Dictionary<string, string>();
				for(int i = 0; i < arguments.Length; ++i)
					imageParameters.Add(arguments[i].name, arguments[i].strValues[argidx[i]]);

				// Start with name pattern
				imagepath = namePattern;

				// Split path and extension
				string ext = Path.GetExtension(imagepath);
				imagepath = imagepath.Substring(0, imagepath.Length - ext.Length);

				// Insert argument names
				for(int i = 0; i < arguments.Length; ++i)
					imagepath = imagepath.Replace("{" + arguments[i].name + "}", arguments[i].strValues[argidx[i]]);

				// Up to this point depth-path == luminance-path == image-path
				imageDepthPath = imageLumPath = imagepath;

				// Append dependent parameters
				bool hasDepth = false, hasLum = false;
				isFloatImage = false;
				foreach(Association association in dependentAssociations)
				{
					imageParameters.Add(association.parameter.name, association.parameter.defaultStrValue);

					imagepath += Path.DirectorySeparatorChar + association.parameter.name + "=" + association.parameter.defaultStrValue;
					int defaultIndex;
					if(association.parameter.types != null && (defaultIndex = Array.IndexOf<string>(association.parameter.strValues, association.parameter.defaultStrValue)) != -1 && association.parameter.types[defaultIndex] == "value")
						isFloatImage = true;

					if(association.parameter.depthValue != null)
						hasDepth = true;
					imageDepthPath += Path.DirectorySeparatorChar + association.parameter.name + "=" + (association.parameter.depthValue != null ? association.parameter.depthValue : association.parameter.defaultStrValue);

					if(association.parameter.lumValue != null)
						hasLum = true;
					imageLumPath += Path.DirectorySeparatorChar + association.parameter.name + "=" + (association.parameter.lumValue != null ? association.parameter.lumValue : association.parameter.defaultStrValue);
				}

				// Assemble final paths (relative to Cinema database directory)
				imagepath = "image/" + imagepath + ext;
				imageDepthPath = hasDepth ? "image/" + imageDepthPath + ".im" : null;
				imageLumPath = hasLum ? "image/" + imageLumPath + ext : null;
			}*/

			public struct LayerDescription
			{
				public string imagepath, imageDepthPath, imageLumPath;
				public bool isFloatImage;

				public LayerDescription(LayerDescription layer)
				{
					this.imagepath = layer.imagepath;
					this.imageDepthPath = layer.imageDepthPath;
					this.imageLumPath = layer.imageLumPath;
					this.isFloatImage = layer.isFloatImage;
				}
			}
			public class LayerCollection : IEnumerable<LayerDescription>
			{
				private CinemaStore store;
				private int[] argidx;
				//private Association[] dependentAssociations;

				public LayerCollection(CinemaStore store, int[] argidx, Association[] dependentAssociations)
				{
					this.store = store;
					this.argidx = argidx;
					//this.dependentAssociations = dependentAssociations;
				}

				private bool ValidateAssociation(Association association, int[] argidx, int[] paramidx, bool[] paramvalid)
				{
					foreach(KeyValuePair<CinemaArgument, string[]> dependency in association.dependencyMap)
					{
						Parameter dependentParameter = dependency.Key as Parameter;
						if(dependentParameter != null) // If dependency.Key is a parameter
						{
							if(paramvalid[Array.IndexOf(store.parameters, dependentParameter)] == false)
								return false;
							
							int dependentParameterIndex = Array.IndexOf(store.parameters, dependentParameter);
							string dependentParameterStrValue = dependentParameter.strValues[paramidx[dependentParameterIndex]];
							if(Array.IndexOf(dependency.Value, dependentParameterStrValue) == -1)
								return false;
						}
						else // If dependency.Key is an argument
						{
							CinemaArgument dependentArgument = dependency.Key;

							int dependentArgumentIndex = Array.IndexOf(store.arguments, dependentArgument);
							string dependentArgumentStrValue = dependentArgument.strValues[argidx[dependentArgumentIndex]];
							if(Array.IndexOf(dependency.Value, dependentArgumentStrValue) == -1)
								return false;
						}
					}
					return true;
				}

				public IEnumerator<LayerDescription> GetEnumerator()
				{
					LayerDescription layer = new LayerDescription();

					/*Dictionary<string, string> imageParameters = new Dictionary<string, string>();
					for(int i = 0; i < store.arguments.Length; ++i)
						imageParameters.Add(store.arguments[i].name, store.arguments[i].strValues[argidx[i]]);*/

					// Start with name pattern
					layer.imagepath = store.namePattern;

					// Split path and extension
					string ext = Path.GetExtension(layer.imagepath);
					layer.imagepath = layer.imagepath.Substring(0, layer.imagepath.Length - ext.Length);

					// Insert argument names
					for(int i = 0; i < store.arguments.Length; ++i)
						layer.imagepath = layer.imagepath.Replace("{" + store.arguments[i].name + "}", store.arguments[i].strValues[argidx[i]]);

					// Up to this point depth-path == luminance-path == image-path
					layer.imageDepthPath = layer.imageLumPath = layer.imagepath;

					// Iterate dependent parameter cobinations
					int[] paramidx = new int[store.parameters.Length];
					bool[] paramvalid = new bool[store.parameters.Length];
					bool done;
					do {
						LayerDescription _layer = new LayerDescription(layer);

						for(int i = 0; i < store.parameters.Length; ++i)
							paramvalid[i] = true;
						for(int i = 0; i < store.parameters.Length; ++i)
						{
							if(paramvalid[i] == true)
								foreach(Association association in store.associations)
								{
									if(association.parameter == store.parameters[i])
									{
										if(!ValidateAssociation(association, argidx, paramidx, paramvalid))
										{
											paramvalid[i] = false;
											i = 0;
										}
										break;
									}
								}
						}

						// Append dependent parameters
						bool hasDepth = false, hasLum = false;
						_layer.isFloatImage = false;
						int p = 0;
						foreach(Parameter parameter in store.parameters)
						{
							string strValue = parameter.strValues[paramidx[p]];

							/*// Check if any associations are violated when association.parameter is set to strValue
							string[] validValues;
							bool isValid = true;
							foreach(Association _association in store.associations)
								if(_association.dependencyMap.TryGetValue(parameter, out validValues) && Array.IndexOf(validValues, strValue) == -1)
								{
									isValid = false;
									break;
								}
							if(!isValid)
							{
								++p;
								continue;
							}*/

							bool dependenciesSatisfied = true;
							foreach(Association association in store.associations)
							{
								if(association.parameter == parameter)
								{
									if(!ValidateAssociation(association, argidx, paramidx, paramvalid))
										dependenciesSatisfied = false;
									break;
								}
							}
							if(!dependenciesSatisfied)
							{
								++p;
								continue;
							}

							//imageParameters.Add(parameter.name, strValue);

							if(_layer.imagepath.Contains("{" + parameter.name + "}"))
							{
								_layer.imagepath = _layer.imagepath.Replace("{" + parameter.name + "}", strValue);
								_layer.imageDepthPath = _layer.imageDepthPath.Replace("{" + parameter.name + "}", strValue);
								_layer.imageLumPath = _layer.imageLumPath.Replace("{" + parameter.name + "}", strValue);
							}
							else
							{
								_layer.imagepath += Path.DirectorySeparatorChar + parameter.name + "=" + parameter.strValues[paramidx[p]];
								int defaultIndex;
								if(parameter.types != null && (defaultIndex = Array.IndexOf<string>(parameter.strValues, strValue)) != -1 && parameter.types[defaultIndex] == "value")
									_layer.isFloatImage = true;

								if(parameter.depthValue != null)
									hasDepth = true;
								_layer.imageDepthPath += Path.DirectorySeparatorChar + parameter.name + "=" + (parameter.depthValue != null ? parameter.depthValue : strValue);

								if(parameter.lumValue != null)
									hasLum = true;
								_layer.imageLumPath += Path.DirectorySeparatorChar + parameter.name + "=" + (parameter.lumValue != null ? parameter.lumValue : strValue);
							}
							++p;
						}

						// Assemble final paths (relative to Cinema database directory)
						_layer.imagepath = "image/" + _layer.imagepath + ext;
						_layer.imageDepthPath = hasDepth ? "image/" + _layer.imageDepthPath + ".im" : null;
						_layer.imageLumPath = hasLum ? "image/" + _layer.imageLumPath + ext : null;

						yield return _layer;

						// Get next parameter combination -> paramidx[]
						done = true;
						for(int i = 0; i < store.parameters.Length; ++i) {
							if(++paramidx[i] == store.parameters[i].values.Length)
								paramidx[i] = 0;
							else {
								done = false;
								break;
							}
						}
					} while(!done);
				}
				System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
				{
					return this.GetEnumerator();
				}
			}
			public LayerCollection iterateLayers(int[] argidx, Association[] dependentAssociations)
			{
				return new LayerCollection(this, argidx, dependentAssociations);
			}

			public float[] GetImageValues(int[] argidx)
			{
				float[] imagevalues = new float[arguments.Length];
				for(int i = 0; i < arguments.Length; ++i)
					imagevalues[i] = arguments[i].values[argidx[i]];
				return imagevalues;
			}
			public string[] GetImageStrValues(int[] argidx)
			{
				string[] imagestrvalues = new string[arguments.Length];
				for(int i = 0; i < arguments.Length; ++i)
					imagestrvalues[i] = arguments[i].strValues[argidx[i]];
				return imagestrvalues;
			}
		}
	}

	public static class Extensions
	{
		public static T[] RemoveAt<T>(this T[] source, int index)
		{
			T[] dest = new T[source.Length - 1];
			if( index > 0 )
				Array.Copy(source, 0, dest, 0, index);

			if( index < source.Length - 1 )
				Array.Copy(source, index + 1, dest, index, source.Length - index - 1);

			return dest;
		}
	}
}

