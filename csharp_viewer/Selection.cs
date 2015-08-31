using System;
using System.Collections.Generic;

namespace csharp_viewer
{
	public abstract class Selection : IEnumerable<KeyValuePair<int[], TransformedImage>>
	{
		public delegate void ChangedDelegate(Selection selection);

		public abstract Selection Clone();
		public abstract bool Contains(int[] key);
		public abstract int Count { get;}
		public virtual bool IsEmpty { get { return Count == 0; } }

		public abstract IEnumerator<KeyValuePair<int[], TransformedImage>> GetEnumerator();
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}
	}

	public class IndexProductSelection : Selection
	{
		private readonly Dictionary<int[], TransformedImage> images;

		private HashSet<int>[] indices;
		private KeyValuePair<string, HashSet<object>>[] constraints;

		public HashSet<int> this[int index]  
		{  
			get { return index < indices.Length ? indices[index] : null; }  
		} 

		public IndexProductSelection(int numdimensions, int numconstraints, Dictionary<int[], TransformedImage> images)
		{
			this.images = images;

			indices = new HashSet<int>[numdimensions];
			for(int i = 0; i < numdimensions; ++i)
				indices[i] = new HashSet<int>();

			constraints = new KeyValuePair<string, HashSet<object>>[numconstraints];
			for(int i = 0; i < numconstraints; ++i)
				constraints[i] = new KeyValuePair<string, HashSet<object>>();
		}
		public override Selection Clone()
		{
			IndexProductSelection clone = new IndexProductSelection(indices.Length, constraints.Length, images);
			//clone.indices = (HashSet<int>[])indices.Clone();
			for(int i = 0; i < indices.Length; ++i)
				foreach(int entry in indices[i])
					clone.indices[i].Add(entry);
			/*for(int i = 0; i < constraints.Length; ++i)
			{
				clone.constraints[i].Key = constraints[i].Key;
				foreach(object entry in constraints[i].Value)
					clone.constraints[i].Value.Add(entry);
			}*/ //TODO: Deep copy constraints
			return clone;
		}

		public override IEnumerator<KeyValuePair<int[], TransformedImage>> GetEnumerator()
		{
			HashSet<int>.Enumerator[] selectioniter = new HashSet<int>.Enumerator[indices.Length];
			int[] selectionkey = new int[indices.Length];
			for(int i = 0; i < indices.Length; ++i)
			{
				selectioniter[i] = indices[i].GetEnumerator();
				selectioniter[i].MoveNext();
				selectionkey[i] = selectioniter[i].Current;
			}

			bool done;
			TransformedImage selectionimg;
			do {
				if(images.TryGetValue(selectionkey, out selectionimg))
				{
					object value;
					bool satisfiesConstraints = true;
					/*foreach(KeyValuePair<string, HashSet<object>> constraint in constraints)
						if(selectionimg.meta.TryGetValue(constraint.Key, out value) && !constraint.Value.Contains(value))
						{
							satisfiesConstraints = false;
							break;
						}*/
						
					if(satisfiesConstraints)
						yield return new KeyValuePair<int[], TransformedImage>(selectionkey, selectionimg);
				}

				// Get next argument combination -> selectioniter[]
				done = true;
				for(int i = 0; i < indices.Length; ++i) {
					if(selectioniter[i].MoveNext())
					{
						done = false;
						selectionkey[i] = selectioniter[i].Current;
						break;
					}
					else
					{
						selectioniter[i] = indices[i].GetEnumerator();
						selectioniter[i].MoveNext();
						selectionkey[i] = selectioniter[i].Current;
					}
				}
			} while(!done);
		}

		public override bool Contains(int[] key)
		{
			int i = 0;
			foreach(HashSet<int> indices in this.indices)
				if(!indices.Contains(key[i++]))
					return false;
			return true;
		}

		public override int Count
		{
			get {
				int c = 1;
				foreach(HashSet<int> indices in this.indices)
					c *= indices.Count;
				return c;
			}
		}
		public override bool IsEmpty
		{
			get {
				foreach(HashSet<int> indices in this.indices)
					if(indices.Count == 0)
						return true;
				return false;
			}
		}
	}

	public class ArraySelection : Selection
	{
		private readonly Dictionary<int[], TransformedImage> images;
		private Dictionary<int[], TransformedImage> selection;

		public ArraySelection(Dictionary<int[], TransformedImage> images)
		{
			this.images = images;
			this.selection = new Dictionary<int[], TransformedImage>();
		}
		public override Selection Clone()
		{
			ArraySelection clone = new ArraySelection(images);
			foreach(KeyValuePair<int[], TransformedImage> entry in selection)
				clone.selection.Add(entry.Key, entry.Value);
			return clone;
		}

		public void Add(int[] key, TransformedImage value)
		{
			if(!selection.ContainsKey(key))
				selection.Add(key, value);
		}
		public void Add(int[] key)
		{
			if(!selection.ContainsKey(key))
			{
				TransformedImage value;
				images.TryGetValue(key, out value);
				selection.Add(key, value);
			}
		}
		public void Clear()
		{
			selection.Clear();
		}

		public override bool Contains(int[] key)
		{
			return selection.ContainsKey(key);
		}

		public override int Count
		{
			get { return selection.Count; }
		}

		public override IEnumerator<KeyValuePair<int[], TransformedImage>> GetEnumerator()
		{
			return selection.GetEnumerator();
		}
	}
}

