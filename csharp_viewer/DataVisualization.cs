using System;
using System.Windows.Forms;
using System.Drawing;
using System.Collections.Generic;

namespace csharp_viewer
{
	public class DataVisualization
	{
		private readonly Dictionary<int[], TransformedImage> images;
		private readonly Cinema.CinemaArgument[] arguments;
		public readonly HashSet<string> valueset;

		private readonly Panel rendertarget;
		private PictureBox picPCView;
		private ComboBox cbValue, cbValueOperator;
		private HashSet<string> cbValue_valueset = new HashSet<string>();

		private struct ImportanceCriteria
		{
			public string value;
			public Operator op;
			public bool ApplyOperator(Dictionary<string, object> meta)
			{
				return meta.ContainsKey(value) && op.op(meta[value]);
			}
		}
		private ImportanceCriteria importanceCriteria;

		private class Operator
		{
			public Func<object, bool> op;
			public string name;
			public override string ToString()
			{
				return name;
			}
			public Operator(string name, Func<object, bool> op)
			{
				this.name = name;
				this.op = op;
			}
		}

		private delegate Point PointFromArgumentIndexDelegate(int a, int b);

		public DataVisualization(Dictionary<int[], TransformedImage> images, Cinema.CinemaArgument[] arguments, HashSet<string> valueset, Panel rendertarget)
		{
			this.images = images;
			this.arguments = arguments;
			this.valueset = valueset;
			this.rendertarget = rendertarget;

			picPCView = new PictureBox();
			picPCView.BackColor = Color.Black;
			//picPCView.Dock = DockStyle.Fill;
			picPCView.SetBounds(4, 100, rendertarget.Width - 8, rendertarget.Height - 100);
			picPCView.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			picPCView.SizeMode = PictureBoxSizeMode.Zoom;
			rendertarget.Controls.Add(picPCView);

			// >>> Create controls for selecting importance criteria

			cbValue = new ComboBox();
			cbValue.SetBounds(16, 16, 140, 21);
			cbValue.DropDownStyle = ComboBoxStyle.DropDownList;
			cbValue.SelectedIndexChanged += SetImportanceCriteria;
			rendertarget.Controls.Add(cbValue);

			cbValueOperator = new ComboBox();
			cbValueOperator.SetBounds(cbValue.Right, 16, 140, 21);
			cbValueOperator.DropDownStyle = ComboBoxStyle.DropDownList;
			cbValueOperator.Items.Add(new Operator("== true", delegate(object input) {return (bool)input == true;}));
			cbValueOperator.Items.Add(new Operator("== false", delegate(object input) {return (bool)input == false;}));
			cbValueOperator.SelectedIndex = 0;
			cbValueOperator.SelectedIndexChanged += SetImportanceCriteria;
			rendertarget.Controls.Add(cbValueOperator);
		}

		private void SetImportanceCriteria(object sender, EventArgs e)
		{
			if(cbValue.Items.Count > 0 && cbValueOperator.Items.Count > 0)
			{
				importanceCriteria.value = cbValue.SelectedItem.ToString();
				importanceCriteria.op = (Operator)cbValueOperator.SelectedItem;
			}

			if(arguments.Length > 1) // Cannot draw parllel coordiantes graph on 1D data
				DrawParallelCoordinatesGraph(null); //TODO: Draw with selection (probably by storing selection inside SelectImage())
		}

		public void SelectImage(int[] selectionidx, Dictionary<int[], TransformedImage> images)
		{
			if(arguments.Length > 1) // Cannot draw parllel coordiantes graph on 1D data
				DrawParallelCoordinatesGraph(selectionidx);
		}
		public void ImagesAdded()
		{
			if(cbValue.Items.Count < valueset.Count)
			{
				bool firstValueAdded = (cbValue.Items.Count == 0);
				foreach(string value in valueset)
					if(!cbValue_valueset.Contains(value))
					{
						cbValue_valueset.Add(value);
						cbValue.Items.Add(value);
					}
				if(firstValueAdded)
					cbValue.SelectedIndex = 0;
			}

			if(arguments.Length > 1) // Cannot draw parllel coordiantes graph on 1D data
				DrawParallelCoordinatesGraph(null); //TODO: Draw with selection (probably by storing selection inside SelectImage())
		}

		private void DrawParallelCoordinatesGraph(int[] selectionidx = null)
		{
			const int PC_GRAPH_WIDTH = 1600;
			const int PC_GRAPH_HEIGHT = 1200;
			Pen PC_GRAPH_AXIS_PEN = new Pen(Color.FromArgb(255, 197, 47), 2.0f);
			Pen PC_GRAPH_LINE_PEN = Pens.White;
			Pen PC_GRAPH_SELECTION_PEN = new Pen(Color.FromArgb(80, 255, 0, 0), 6.0f);
			Font PC_GRAPH_AXIS_FONT = new Font(rendertarget.Font.FontFamily, 24.0f);

			Bitmap bmpPCGraph = new Bitmap(PC_GRAPH_WIDTH, PC_GRAPH_HEIGHT);
			Graphics gfx = Graphics.FromImage(bmpPCGraph);
			gfx.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
			gfx.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
			gfx.Clear(picPCView.BackColor);

			PointFromArgumentIndexDelegate PointFromArgumentIndex = delegate(int argumentidx, int valueidx) {
				int numvalues = arguments[argumentidx].values.Length;
				return new Point(50 + argumentidx * (bmpPCGraph.Width - 2 * 50) / (arguments.Length - 1), numvalues == 1 ? 50 + (bmpPCGraph.Height - 2 * 50) / 2 : 50 + valueidx * (bmpPCGraph.Height - 2 * 50) / (numvalues - 1));
			};

			// Draw axes
			for(int i = 0; i < arguments.Length; ++i)
			{
				// Draw axis
				Point axisEnd = PointFromArgumentIndex(i, arguments[i].values.Length - 1);
				gfx.DrawLine(PC_GRAPH_AXIS_PEN, PointFromArgumentIndex(i, 0), axisEnd);

				// Draw axis label
				SizeF labelsize = gfx.MeasureString(arguments[i].label, PC_GRAPH_AXIS_FONT);
				gfx.DrawString(arguments[i].label, PC_GRAPH_AXIS_FONT, Brushes.White, (float)axisEnd.X - labelsize.Width / 2.0f, (float)axisEnd.Y + labelsize.Height - 30.0f); // - 30.0f ... bugfix
			}

			// Draw a line for each image in images that conforms to the importance criteria
			if(importanceCriteria.value != null)
				foreach(KeyValuePair<int[], TransformedImage> iter in images)
					if(importanceCriteria.ApplyOperator(iter.Value.meta))
						for(int i = 0; i < arguments.Length - 1; ++i)
							gfx.DrawLine(PC_GRAPH_LINE_PEN, PointFromArgumentIndex(i, iter.Key[i]), PointFromArgumentIndex(i + 1, iter.Key[i + 1]));

			// Draw selection
			if(selectionidx != null)
				for(int i = 0; i < arguments.Length - 1; ++i)
					gfx.DrawLine(PC_GRAPH_SELECTION_PEN, PointFromArgumentIndex(i, selectionidx[i]), PointFromArgumentIndex(i + 1, selectionidx[i + 1]));

			picPCView.Image = bmpPCGraph;
		}
	}
}

