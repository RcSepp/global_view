using System;
using System.Collections.Generic;

namespace csharp_viewer
{
	public abstract class _Selection : IEnumerable<TransformedImage>
	{
		public delegate void ChangedDelegate(Selection selection);

		public abstract _Selection Clone();
		public abstract bool Contains(TransformedImage image);
		public abstract int Count { get;}
		public virtual bool IsEmpty { get { return Count == 0; } }

		public abstract IEnumerator<TransformedImage> GetEnumerator();
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public _Selection Values()
		{
			return this;
		}
		/*public ValueCollection Values()
		{
			return new ValueCollection(this);
		}

		public class ValueCollection : IEnumerable<TransformedImage>
		{
			private Selection s;

			public ValueCollection(Selection s)
			{
				this.s = s;
			}
			
			public IEnumerator<TransformedImage> GetEnumerator()
			{
				foreach(KeyValuePair<int[], TransformedImage> image in s)
					yield return image.Value;
			}
			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return this.GetEnumerator();
			}
		}*/
	}

	/*public class IndexProductSelection : Selection
	{
		private readonly TransformedImageCollection images;

		private HashSet<int>[] indices;
		private KeyValuePair<string, HashSet<object>>[] constraints;

		public HashSet<int> this[int index]  
		{  
			get { return index < indices.Length ? indices[index] : null; }  
		} 

		public IndexProductSelection(int numdimensions, int numconstraints, TransformedImageCollection images)
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
			//for(int i = 0; i < constraints.Length; ++i)
			//{
			//	clone.constraints[i].Key = constraints[i].Key;
			//	foreach(object entry in constraints[i].Value)
			//		clone.constraints[i].Value.Add(entry);
			//} //TODO: Deep copy constraints
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
					//foreach(KeyValuePair<string, HashSet<object>> constraint in constraints)
					//	if(selectionimg.meta.TryGetValue(constraint.Key, out value) && !constraint.Value.Contains(value))
					//	{
					//		satisfiesConstraints = false;
					//		break;
					//	}
						
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
	}*/

	public class _ArraySelection : _Selection
	{
		private readonly TransformedImageCollection images;
		private HashSet<TransformedImage> selection;

		public _ArraySelection(TransformedImageCollection images)
		{
			this.images = images;
			this.selection = new HashSet<TransformedImage>();
		}
		public override _Selection Clone()
		{
			_ArraySelection clone = new _ArraySelection(images);
			foreach(TransformedImage entry in selection)
				clone.selection.Add(entry);
			return clone;
		}

		public void Add(TransformedImage image)
		{
			if(!selection.Contains(image))
				selection.Add(image);
		}
		/*public void Add(int[] key)
		{
			if(!selection.ContainsKey(key))
			{
				TransformedImage value;
				images.TryGetValue(key, out value);
				selection.Add(key, value);
			}
		}*/
		public void Clear()
		{
			selection.Clear();
		}

		public override bool Contains(TransformedImage image)
		{
			return selection.Contains(image);
		}

		public override int Count
		{
			get { return selection.Count; }
		}

		public override IEnumerator<TransformedImage> GetEnumerator()
		{
			return selection.GetEnumerator();
		}
	}






	public class Selection : IEnumerable<TransformedImage>
	{
		public delegate void ChangedDelegate(Selection selection);

		protected readonly TransformedImageCollection images;
		protected HashSet<TransformedImage> selection;

		public Selection(TransformedImageCollection images)
		{
			this.images = images;
			this.selection = new HashSet<TransformedImage>();
		}
		public Selection Clone()
		{
			Selection clone = new Selection(images);
			foreach(TransformedImage entry in selection)
				clone.selection.Add(entry);
			return clone;
		}

		public void Add(TransformedImage image)
		{
			if(!selection.Contains(image))
				selection.Add(image);
		}
		public void Clear()
		{
			selection.Clear();
		}

		public bool Contains(TransformedImage image)
		{
			return selection.Contains(image);
		}

		public int Count
		{
			get { return selection.Count; }
		}

		public bool IsEmpty { get { return selection.Count == 0; } }

		public IEnumerator<TransformedImage> GetEnumerator()
		{
			return selection.GetEnumerator();
		}
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return selection.GetEnumerator();
		}
	}

	/*public class ConstraintSelection : Selection
	{
		private readonly TransformedImageCollection images;
		private HashSet<TransformedImage> selection;
		public List<Constraint> constraints; // read-only!

		public ConstraintSelection(TransformedImageCollection images) : base(images)
		{
			this.images = images;
			this.selection = new HashSet<TransformedImage>();
			//constraintGroups = new List<ConstraintGroup>();
		}
		public Selection Clone()
		{
			Selection clone = new Selection(images);
			foreach(TransformedImage entry in selection)
				clone.selection.Add(entry);
			return clone;
		}

		public void Add(TransformedImage image)
		{
			if(!selection.Contains(image))
				selection.Add(image);
		}
		public void Clear()
		{
			selection.Clear();
			constraints.Clear();
		}

		public bool Contains(TransformedImage image)
		{
			return selection.Contains(image);
		}

		public int Count
		{
			get { return selection.Count; }
		}

		public bool IsEmpty { get { return selection.Count == 0; } }

		public IEnumerator<TransformedImage> GetEnumerator()
		{
			return selection.GetEnumerator();
		}


		public struct Constraint
		{
			public int argidx;
			public float min, max;

			public bool Satisfies(TransformedImage image)
			{
				float value = image.values[argidx];
				return value >= min && value <= max;
			}
			public bool Fails(TransformedImage image)
			{
				float value = image.values[argidx];
				return value < min || value > max;
			}
		}

		public void AddGroup(List<Constraint> constraints)
		{
			foreach(TransformedImage image in images)
			{
				bool satisfiesConstraints = true;
				foreach(Constraint constraint in constraints)
					if(constraint.Fails(image))
					{
						satisfiesConstraints = false;
						break;
					}

				if(satisfiesConstraints)
					Add(image);
			}

			this.constraints = constraints;
		}
	}*/
}

