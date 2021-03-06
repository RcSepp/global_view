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
			public string[] types;
			public string depthValue, lumValue;

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

			public override string ToString()
			{
				return string.Format("Argument {0}", name);
			}
		}

		public class CinemaImage
		{
			public float[] values;
			public string[] strValues;
			public CinemaArgument[] args;
			public int[] globalargindices;

			public string metaFilename;
			public Dictionary<string, object> meta;

			public OpenTK.Matrix4 invview; // Required to recreate 3D pixel locations from depth image
		}

		public static bool IsCinemaDB(string path)
		{
			return (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && File.Exists(path)) || File.Exists(path + "/image/info.json");
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
			public string namePattern, depthNamePattern;
			public string pixel_format;
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
				public bool[] isChecked;

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
				public CinemaArgument parameter;
				public DependencyMap dependencyMap;
				public Association(CinemaArgument parameter, DependencyMap dependencyMap)
				{
					this.parameter = parameter;
					this.dependencyMap = dependencyMap;
				}
			}
			//private List<Association> associations = new List<Association>();
			private Dictionary<CinemaArgument, DependencyMap> associations = new Dictionary<CinemaArgument, DependencyMap>();

			public static CinemaStore Load(string filename)
			{
				CinemaStore store = new CinemaStore();

				StreamReader sr = new StreamReader(new FileStream(filename, FileMode.Open, FileAccess.Read));
				dynamic meta;
				try {
					meta = JsonConvert.DeserializeObject(sr.ReadToEnd());
				}
				catch(Exception ex) {
					Global.cle.PrintOutput("Error parsing info.json: " + ex.Message);
					sr.Close();
					return null;
				}
				sr.Close();

				try {
					store.namePattern = meta.name_pattern;
				}
				catch {}
				if(store.namePattern == null)
				{
					Global.cle.PrintOutput("Error parsing info.json: Missing entry in info.json: name_pattern");
					return null;
				}
				try { store.depthNamePattern = meta.depth_name_pattern; } catch {}
				JObject argumentsMeta = null;
				try {
					argumentsMeta = meta.arguments;
				}
				catch {}
				if(argumentsMeta == null)
				{
					Global.cle.PrintOutput("Error parsing info.json: Missing entry in info.json: arguments");
					return null;
				}
				store.pixel_format = "R8G8B8";
				JObject metadata = null;
				try {
					metadata = meta.metadata;
				}
				catch {}
				if(metadata != null)
				{
					JToken t;
					if(metadata.TryGetValue("pixel_format", out t))
					{
						store.pixel_format = t.Value<string>();
						if(Array.IndexOf(new string[] {"R8G8B8", "I24"}, store.pixel_format) == -1)
						{
							Global.cle.PrintOutput(string.Format("Warning parsing info.json: pixel_format is '{0}' (expected 'R8G8B8' or 'I24')", store.pixel_format));
							store.pixel_format = "R8G8B8";
						}
					}
					else
						Global.cle.PrintOutput("Warning parsing info.json: Missing entry in info.json metadata section: pixel_format");
				}
				else
					Global.cle.PrintOutput("Warning parsing info.json: Missing entry in info.json: metadata");
					
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

						carg.name = argumentMeta.Key;
						carg.label = argumentMeta.Value["label"].ToObject<string>();
						carg.strValues = argumentMeta.Value["values"].ToObject<string[]>();
						carg.defaultStrValue = argumentMeta.Value["default"].ToObject<string>();
						if(Array.IndexOf(carg.strValues, carg.defaultStrValue) == -1)
						{
							Global.cle.PrintOutput(string.Format("Error parsing info.json: Values of argument '{0}' don't contain default ('{1}')", carg.name, carg.defaultStrValue));
							return null;
						}

						store.argumentMap.Add(argumentMeta.Key, carg);
					} else if(type == "option" || type == "hidden")
					{
						// argumentMeta describes option
						carg = parameter = new Parameter();

						parameter.name = argumentMeta.Key;
						parameter.label = argumentMeta.Value["label"].ToObject<string>();
						parameter.strValues = argumentMeta.Value["values"].ToObject<string[]>();
						parameter.defaultStrValue = argumentMeta.Value["default"].ToObject<string>();
						if(Array.IndexOf(carg.strValues, carg.defaultStrValue) == -1)
						{
							Global.cle.PrintOutput(string.Format("Error parsing info.json: Values of argument '{0}' don't contain default ('{1}')", carg.name, carg.defaultStrValue));
							return null;
						}

						parameter.isField = (string)argumentMeta.Value["isfield"] == "yes" ? true : false;
						parameter.isLayer = (string)argumentMeta.Value["islayer"] == "yes" ? true : false;
						parameter.type = (string)argumentMeta.Value["type"];
						parameter.isChecked = new bool[parameter.strValues.Length];

						if(type == "option")
							for(int i = 0; i < parameter.strValues.Length; ++i)
								parameter.isChecked[i] = true;
						else
						{
							for(int i = 0; i < parameter.strValues.Length; ++i)
								parameter.isChecked[i] = false;
							parameter.isChecked[Array.IndexOf(parameter.strValues, parameter.defaultStrValue)] = true;
						}

						store.parameterMap.Add(argumentMeta.Key, parameter);
					}
					else
					{
						Global.cle.PrintOutput(string.Format("Error parsing info.json: Type field of argument '{0}' is '{1}' (expected 'range', 'option' or 'hidden')", argumentMeta.Key, type));
						return null;
					}

					try { carg.types = (string[])argumentMeta.Value["types"].ToObject<string[]>(); } catch {}
					if(carg.types != null && carg.types.Length != carg.strValues.Length)
					{
						Global.cle.PrintOutput(string.Format("Error parsing info.json: Types field of argument '{0}' contains a different number of elements than values field (# values = {1}, # types = {2})", argumentMeta.Key, carg.strValues.Length, carg.types.Length));
						return null;
					}

					object[] values = argumentMeta.Value["values"].ToObject<object[]>();
					if(carg.types != null)
					{
						int depthIdx = Array.IndexOf<string>(carg.types, "depth");
						if(depthIdx != -1)
						{
							carg.depthValue = carg.strValues[depthIdx];
							values = values.RemoveAt(depthIdx);
							carg.strValues = carg.strValues.RemoveAt(depthIdx);
							carg.types = carg.types.RemoveAt(depthIdx);
							if(parameter != null)
								parameter.isChecked = parameter.isChecked.RemoveAt(depthIdx);
						}

						int lumIdx = Array.IndexOf<string>(carg.types, "luminance");
						if(lumIdx != -1)
						{
							carg.lumValue = carg.strValues[lumIdx];
							values = values.RemoveAt(lumIdx);
							carg.strValues = carg.strValues.RemoveAt(lumIdx);
							carg.types = carg.types.RemoveAt(lumIdx);
							if(parameter != null)
								parameter.isChecked = parameter.isChecked.RemoveAt(lumIdx);
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
						CinemaArgument argument;
						DependencyMap dependencyMap;
						if(store.parameterMap.TryGetValue(associationMeta.Key, out parameter))
							store.associations.Add(parameter, dependencyMap = new DependencyMap());
						else if(store.argumentMap.TryGetValue(associationMeta.Key, out argument))
							store.associations.Add(argument, dependencyMap = new DependencyMap());
						else
							throw new Exception(string.Format("Association for inexistent parameter '{0}'", associationMeta.Key));

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
				
			private class ParameterComparer : IComparer<CinemaArgument>
			{
				public int Compare(CinemaArgument x, CinemaArgument y)
				{
					return string.Compare(x.name, y.name);
				}
			}
			/*public Association[] GetDependentAssociations(int[] argidx)
			{
				SortedDictionary<CinemaArgument, DependencyMap> dependentAssociations = new SortedDictionary<CinemaArgument, DependencyMap>(new ParameterComparer());

				Dictionary<CinemaArgument, DependencyMap>.Enumerator associationEnum = associations.GetEnumerator();
				while(associationEnum.MoveNext())
				{
					if(dependentAssociations.ContainsKey(associationEnum.Current.Key))
						continue; // association is already part of dependentAssociations

					bool dependenciesSatisfied = true;
					foreach(KeyValuePair<CinemaArgument, string[]> dependency in associationEnum.Current.Value)
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
						dependentAssociations.Add(associationEnum.Current.Key, associationEnum.Current.Value);
						associationEnum = associations.GetEnumerator(); // Restart loop
					}
				}

				Association[] dependentAssociationsArray = new Association[dependentAssociations.Count];
				int j = 0;
				foreach(KeyValuePair<CinemaArgument, DependencyMap> dependentAssociation in dependentAssociations)
					dependentAssociationsArray[j++] = new Association(dependentAssociation.Key, dependentAssociation.Value);
				return dependentAssociationsArray;
			}*/

			public struct LayerDescription
			{
				public string imagepath, imageDepthPath, imageLumPath;
				public bool isFloatImage;
				public int[] paramidx;
				public bool[] paramvalid;

				public LayerDescription(LayerDescription layer)
				{
					this.imagepath = layer.imagepath;
					this.imageDepthPath = layer.imageDepthPath;
					this.imageLumPath = layer.imageLumPath;
					this.isFloatImage = layer.isFloatImage;
					this.paramidx = layer.paramidx;
					this.paramvalid = layer.paramvalid;
				}
			}
			public class LayerCollection : IEnumerable<LayerDescription>
			{
				private CinemaStore store;
				private int[] argidx;

				public LayerCollection(CinemaStore store, int[] argidx)
				{
					this.store = store;
					this.argidx = argidx;
				}

				private bool ValidateAssociation(DependencyMap dependencyMap, int[] argidx, int[] paramidx, bool[] paramvalid, out bool depthValid, out bool lumValid)
				{
					depthValid = lumValid = true;

					// Validate the following:
					// <association>: {
					//     <dependency1>,
					//     <dependency2>
					// }

					/* // Variant 1:
					// Return satisfied(<dependency1>) && satisfied(<dependency2>)
					foreach(KeyValuePair<CinemaArgument, string[]> dependency in dependencyMap)
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

							if(dependentParameter.depthValue != null && Array.IndexOf(dependency.Value, dependentParameter.depthValue) == -1)
								depthValid = false;
							if(dependentParameter.lumValue != null && Array.IndexOf(dependency.Value, dependentParameter.lumValue) == -1)
								lumValid = false;
						}
						else // If dependency.Key is an argument
						{
							CinemaArgument dependentArgument = dependency.Key;

							int dependentArgumentIndex = Array.IndexOf(store.arguments, dependentArgument);
							string dependentArgumentStrValue = dependentArgument.strValues[argidx[dependentArgumentIndex]];
							if(Array.IndexOf(dependency.Value, dependentArgumentStrValue) == -1)
								return false;

							if(dependentArgument.depthValue != null && Array.IndexOf(dependency.Value, dependentArgument.depthValue) == -1)
								depthValid = false;
							if(dependentArgument.lumValue != null && Array.IndexOf(dependency.Value, dependentArgument.lumValue) == -1)
								lumValid = false;
						}
					}
					return true;*/

					// Variant 2:
					// Return satisfied(<dependency1>) || satisfied(<dependency2>)
					foreach(KeyValuePair<CinemaArgument, string[]> dependency in dependencyMap)
					{
						Parameter dependentParameter = dependency.Key as Parameter;
						if(dependentParameter != null) // If dependency.Key is a parameter
						{
							if(paramvalid[Array.IndexOf(store.parameters, dependentParameter)] == false)
								continue;

							int dependentParameterIndex = Array.IndexOf(store.parameters, dependentParameter);
							string dependentParameterStrValue = dependentParameter.strValues[paramidx[dependentParameterIndex]];
							if(Array.IndexOf(dependency.Value, dependentParameterStrValue) == -1)
								continue;

							if(dependentParameter.depthValue != null && Array.IndexOf(dependency.Value, dependentParameter.depthValue) == -1)
								depthValid = false;
							if(dependentParameter.lumValue != null && Array.IndexOf(dependency.Value, dependentParameter.lumValue) == -1)
								lumValid = false;
						}
						else // If dependency.Key is an argument
						{
							CinemaArgument dependentArgument = dependency.Key;

							int dependentArgumentIndex = Array.IndexOf(store.arguments, dependentArgument);
							string dependentArgumentStrValue = dependentArgument.strValues[argidx[dependentArgumentIndex]];
							if(Array.IndexOf(dependency.Value, dependentArgumentStrValue) == -1)
								continue;

							if(dependentArgument.depthValue != null && Array.IndexOf(dependency.Value, dependentArgument.depthValue) == -1)
								depthValid = false;
							if(dependentArgument.lumValue != null && Array.IndexOf(dependency.Value, dependentArgument.lumValue) == -1)
								lumValid = false;
						}

						return true;
					}
					return false;
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

					// Up to this point depth-path == luminance-path == image-path
					layer.imageDepthPath = layer.imageLumPath = layer.imagepath;

					string depthExt = ".im";
					if(store.depthNamePattern != null)
					{
						layer.imageDepthPath = store.depthNamePattern;
						depthExt = Path.GetExtension(layer.imageDepthPath);
						layer.imageDepthPath = layer.imageDepthPath.Substring(0, layer.imageDepthPath.Length - ext.Length);
					}

					// Iterate dependent parameter combinations
					int[] paramidx = new int[store.parameters.Length];
					bool[] paramvalid = new bool[store.parameters.Length];
					bool done;
					do {
						LayerDescription _layer = new LayerDescription(layer);
						List<string> pathAdditions = new List<string>();
						List<string> depthPathAdditions = new List<string>();
						List<string> lumPathAdditions = new List<string>();

						for(int i = 0; i < store.parameters.Length; ++i)
							paramvalid[i] = true;
						for(int i = 0; i < store.parameters.Length; ++i)
						{
							DependencyMap dependencyMap;
							bool depthValid, lumValid;
							if(paramvalid[i] == true && store.associations.TryGetValue(store.parameters[i], out dependencyMap) && !ValidateAssociation(dependencyMap, argidx, paramidx, paramvalid, out depthValid, out lumValid))
							{
								// Dependencies not satisfied for store.parameters[i]
								paramvalid[i] = false;
								i = 0; // Restart loop
							}
						}

						// Append dependent parameters
						bool hasDepth = store.depthNamePattern != null, hasLum = false;
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

							DependencyMap dependencyMap;
							bool depthValid = true, lumValid = true;
							if(store.associations.TryGetValue(parameter, out dependencyMap) && !ValidateAssociation(dependencyMap, argidx, paramidx, paramvalid, out depthValid, out lumValid))
							{
								// Dependencies not satisfied for parameter
								++p;
								continue;
							}

							if(_layer.imagepath.Contains("{" + parameter.name + "}"))
							{
								_layer.imagepath = _layer.imagepath.Replace("{" + parameter.name + "}", strValue);
								_layer.imageDepthPath = _layer.imageDepthPath.Replace("{" + parameter.name + "}", depthValid ? (parameter.depthValue != null ? parameter.depthValue : strValue) : parameter.defaultStrValue);
								_layer.imageLumPath = _layer.imageLumPath.Replace("{" + parameter.name + "}", lumValid ? (parameter.lumValue != null ? parameter.lumValue : strValue) : parameter.defaultStrValue);
							}
							else
							{
								if(parameter.types != null && parameter.types[paramidx[p]] == "value")
									_layer.isFloatImage = true;
								pathAdditions.Add(parameter.name + "=" + parameter.strValues[paramidx[p]]);

								if(parameter.depthValue != null)
									hasDepth = true;
								if(depthValid)
									depthPathAdditions.Add(parameter.name + "=" + (parameter.depthValue != null ? parameter.depthValue : strValue));

								if(parameter.lumValue != null)
									hasLum = true;
								if(lumValid)
									lumPathAdditions.Add(parameter.name + "=" + (parameter.lumValue != null ? parameter.lumValue : strValue));
							}
							++p;
						}

						// Insert argument names
						for(int i = 0; i < store.arguments.Length; ++i)
						{
							CinemaArgument argument = store.arguments[i];
							string argStr = "{" + argument.name + "}";

							DependencyMap dependencyMap;
							bool depthValid = true, lumValid = true;
							if(store.associations.TryGetValue(argument, out dependencyMap) && !ValidateAssociation(dependencyMap, argidx, paramidx, paramvalid, out depthValid, out lumValid))
							{
								// Dependencies not satisfied for argument
								if(_layer.imagepath.Contains(argStr))
								{
									_layer.imagepath = _layer.imagepath.Replace(argStr, argument.defaultStrValue);
									_layer.imageDepthPath = _layer.imageDepthPath.Replace(argStr, argument.defaultStrValue);
									_layer.imageLumPath = _layer.imageLumPath.Replace(argStr, argument.defaultStrValue);
								}
								continue;
							}

							if(_layer.imagepath.Contains(argStr))
							{
								_layer.imagepath = _layer.imagepath.Replace(argStr, argument.strValues[argidx[i]]);
								_layer.imageDepthPath = _layer.imageDepthPath.Replace(argStr, depthValid ? (argument.depthValue != null ? argument.depthValue : argument.strValues[argidx[i]]) : argument.defaultStrValue);
								_layer.imageLumPath = _layer.imageLumPath.Replace(argStr, lumValid ? (argument.lumValue != null ? argument.lumValue : argument.strValues[argidx[i]]) : argument.defaultStrValue);
							}
							else
							{
								if(argument.types != null && argument.types[argidx[i]] == "value")
									_layer.isFloatImage = true;
								pathAdditions.Add(argument.name + "=" + argument.strValues[argidx[i]]);

								if(argument.depthValue != null)
									hasDepth = true;
								if(depthValid)
									depthPathAdditions.Add(argument.name + "=" + (argument.depthValue != null ? argument.depthValue : argument.strValues[argidx[i]]));

								if(argument.lumValue != null)
									hasLum = true;
								if(lumValid)
									lumPathAdditions.Add(argument.name + "=" + (argument.lumValue != null ? argument.lumValue : argument.strValues[argidx[i]]));
							}
						}

						pathAdditions.Sort();
						depthPathAdditions.Sort();
						lumPathAdditions.Sort();

						// Assemble final paths (relative to Cinema database directory)
						_layer.imagepath = _layer.imagepath + (pathAdditions.Count != 0 ? Path.DirectorySeparatorChar + string.Join(new string(Path.DirectorySeparatorChar, 1), pathAdditions) : "") + ext;
						_layer.imageDepthPath = hasDepth ? _layer.imageDepthPath + (depthPathAdditions.Count != 0 ? Path.DirectorySeparatorChar + string.Join(new string(Path.DirectorySeparatorChar, 1), depthPathAdditions) : "") + depthExt : null;
						_layer.imageLumPath = hasLum ? _layer.imageLumPath + (lumPathAdditions.Count != 0 ? Path.DirectorySeparatorChar + string.Join(new string(Path.DirectorySeparatorChar, 1), lumPathAdditions) : "") + ext : null;
						_layer.paramidx = new int[paramidx.Length];
						Array.Copy(paramidx, _layer.paramidx, paramidx.Length);
						_layer.paramvalid = new bool[paramvalid.Length];
						Array.Copy(paramvalid, _layer.paramvalid, paramvalid.Length);

						yield return _layer;

						// Get next parameter combination -> paramidx[]
						done = true;
						for(int i = 0; i < store.parameters.Length; ++i) {
							if(paramvalid[i] == false ||  ++paramidx[i] == store.parameters[i].values.Length)
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
			public LayerCollection iterateLayers(int[] argidx)
			{
				return new LayerCollection(this, argidx);
			}

			public string GetMetaPath(int[] argidx)
			{
				// >>> Compute meta path

				// Start with name pattern
				string metapath = namePattern;

				// Split path and extension
				string ext = Path.GetExtension(metapath);
				metapath = metapath.Substring(0, metapath.Length - ext.Length);

				List<string> metaPathAdditions = new List<string>();

				// Insert default values for parameters
				int p = 0;
				foreach(Parameter parameter in parameters)
				{
					if(metapath.Contains("{" + parameter.name + "}"))
						metapath = metapath.Replace("{" + parameter.name + "}", parameter.defaultStrValue);
					++p;
				}

				// Insert argument names
				for(int i = 0; i < arguments.Length; ++i)
				{
					CinemaArgument argument = arguments[i];
					string argStr = "{" + argument.name + "}";

					/*DependencyMap dependencyMap;
					if(associations.TryGetValue(argument, out dependencyMap) && !ValidateAssociation(dependencyMap, argidx, paramidx, paramvalid, out depthValid, out lumValid))
					{
						// Dependencies not satisfied for argument
						if(metapath.Contains(argStr))
							metapath = metapath.Replace(argStr, argument.defaultStrValue);
						continue;
					}*/

					if(metapath.Contains(argStr))
						metapath = metapath.Replace(argStr, argument.strValues[argidx[i]]);
					else
						metaPathAdditions.Add(argument.name + "=" + argument.strValues[argidx[i]]);
				}

				metaPathAdditions.Sort();

				// Assemble final path (relative to Cinema database directory)
				return metapath + (metaPathAdditions.Count != 0 ? Path.DirectorySeparatorChar + string.Join(new string(Path.DirectorySeparatorChar, 1), metaPathAdditions) : "") + ".json";
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

