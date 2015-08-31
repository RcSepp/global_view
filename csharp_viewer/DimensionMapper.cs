using System;
using System.Windows.Forms;
using System.Drawing;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace csharp_viewer
{
	public class DimensionMapper : Form
	{
		private Cinema.CinemaArgument[] arguments;

		private GroupBox gbOptions;
		private FlowLayoutPanel flpInput;
		private ListBox lbTransforms;
		private Button cmdAddTransform;
		private ComboBox cbAddTransform;

		public delegate void TransformDelegate(ImageTransform transform);
		public event TransformDelegate TransformAdded, TransformRemoved;

		public DimensionMapper()
		{
			// >>> Initialize Components: Form

			this.Text = "csharp_viewer - dimension mapper";
			this.ClientSize = new Size(800, 600);
			this.FormClosing += Form_Closing;

			// >>> Initialize Components: Options

			gbOptions = new GroupBox();
			gbOptions.Text = "Transformation options";
			gbOptions.Dock = DockStyle.Fill;
			gbOptions.Enabled = false;
			this.Controls.Add(gbOptions);

			GroupBox gbInput = new GroupBox();
			gbInput.Text = "Input";
			gbInput.Dock = DockStyle.Top;
			gbOptions.Controls.Add(gbInput);

			flpInput = new FlowLayoutPanel();
			flpInput.Dock = DockStyle.Fill;
			gbInput.Controls.Add(flpInput);

			Button cmdApply = new Button();
			cmdApply.Text = "Apply";
			cmdApply.Dock = DockStyle.Bottom;
			cmdApply.Click += cmdApply_Click;
			gbOptions.Controls.Add(cmdApply);

			Button cmdRemove= new Button();
			cmdRemove.Text = "Remove";
			cmdRemove.Dock = DockStyle.Bottom;
			cmdRemove.Click += cmdRemove_Click;
			gbOptions.Controls.Add(cmdRemove);

			// >>> Initialize Components: Transformation list

			Panel pnlLeft = new Panel();
			pnlLeft.Dock = DockStyle.Left;
			pnlLeft.Width = 200;
			this.Controls.Add(pnlLeft);

			lbTransforms = new ListBox();
			lbTransforms.Dock = DockStyle.Fill;
			lbTransforms.IntegralHeight = false;
			lbTransforms.SelectedIndexChanged += lbTransforms_SelectedIndexChanged;
			pnlLeft.Controls.Add(lbTransforms);

			cmdAddTransform = new Button();
			cmdAddTransform.Text = "Add Transformation";
			cmdAddTransform.Dock = DockStyle.Top;
			cmdAddTransform.Click += cmdAdd_Click;
			pnlLeft.Controls.Add(cmdAddTransform);

			cbAddTransform = new ComboBox();
			cbAddTransform.Dock = DockStyle.Top;
			cbAddTransform.DropDownStyle = ComboBoxStyle.DropDownList;
			cbAddTransform.Visible = false;
			cbAddTransform.SelectedIndexChanged += cbAddTransform_SelectedIndexChanged;
			foreach(Type transformtype in GetImplementingClasses(typeof(ImageTransform)))
				cbAddTransform.Items.Add(transformtype);
			pnlLeft.Controls.Add(cbAddTransform);


			Deserialize();
		}

		public void Load(Cinema.CinemaArgument[] arguments)
		{
			this.arguments = arguments;
		}

		private void Form_Closing(object sender, FormClosingEventArgs e)
		{
			Serialize();

			e.Cancel = true;
			this.Hide();
		}

		private static IEnumerable<Type> GetImplementingClasses(Type interfacetype)
		{
			foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
				foreach(Type type in assembly.GetTypes())
					if(type.BaseType == interfacetype)//if(interfacetype.IsAssignableFrom(type) && !type.IsInterface)
						yield return type;
		}

		private void cmdAdd_Click(object sender, EventArgs e)
		{
			cmdAddTransform.Visible = false;
			cbAddTransform.Visible = true;
			cbAddTransform.DroppedDown = true;
		}
		private void cbAddTransform_SelectedIndexChanged(object sender, EventArgs e)
		{
			cbAddTransform.Visible = false;
			cmdAddTransform.Visible = true;

			try{
			ImageTransform transform = (ImageTransform)Activator.CreateInstance((Type)cbAddTransform.SelectedItem);
			transform.SetArguments(arguments);
			lbTransforms.SelectedIndex = lbTransforms.Items.Add(transform);
			}catch(Exception ex){
				MessageBox.Show(ex.ToString());
			}
		}

		private void cmdApply_Click(object sender, EventArgs e)
		{
			if(((Button)sender).Enabled == false)
				return;

			if(TransformAdded != null)
			{
				ImageTransform transform = (ImageTransform)lbTransforms.SelectedItem;//ImageTransform instance = (ImageTransform)Activator.CreateInstance((Type)cbAddTransform.SelectedItem, new object[] {0});
				TransformAdded(transform);
			}
		}
		private void cmdRemove_Click(object sender, EventArgs e)
		{
			if(((Button)sender).Enabled == false)
				return;

			if(TransformRemoved != null)
			{
				ImageTransform transform = (ImageTransform)lbTransforms.SelectedItem;
				TransformRemoved(transform);
			}

			lbTransforms.Items.RemoveAt(lbTransforms.SelectedIndex);
		}

		private void lbTransforms_SelectedIndexChanged(object sender, EventArgs e)
		{
			flpInput.Controls.Clear();

			if(lbTransforms.SelectedIndices.Count != 1)
				gbOptions.Enabled = false;
			else
			{
				gbOptions.Enabled = true;

				ImageTransform transform = (ImageTransform)lbTransforms.SelectedItem;

				for(int i = 0, index; (index = transform.GetIndex(i)) != -1; ++i)
				{
					ComboBox cbInput = new ComboBox();
					cbInput.Dock = DockStyle.Top;
					cbInput.DropDownStyle = ComboBoxStyle.DropDownList;
					cbInput.Tag = i;
					cbInput.SelectedIndexChanged += cbInput_SelectedIndexChanged;
					foreach(Cinema.CinemaArgument argument in arguments)
						cbInput.Items.Add(argument.name);
					cbInput.SelectedIndex = index;
					flpInput.Controls.Add(cbInput);
				}
			}
		}

		private void cbInput_SelectedIndexChanged(object sender, EventArgs e)
		{
			ComboBox cbInput = (ComboBox)sender;
			ImageTransform transform = (ImageTransform)lbTransforms.SelectedItem;
			transform.SetIndex((int)cbInput.Tag, cbInput.SelectedIndex);
		}

		private void Deserialize()
		{
			/*lbTransforms.Items.Clear();
			if(File.Exists(Application.StartupPath + "/environment"))
			{
				try
				{
					Stream file = File.Open(Application.StartupPath + "/environment", FileMode.Open);
					BinaryFormatter formatter = new BinaryFormatter();
					ImageTransform[] transformList = (ImageTransform[])formatter.Deserialize(file);
					file.Close();

					lbTransforms.Items.AddRange(transformList);
				}
				catch(Exception ex)
				{
					Console.WriteLine(ex.ToString());
				}
			}*/
		}
		private void Serialize()
		{
			/*ImageTransform[] transformList = new ImageTransform[lbTransforms.Items.Count];

			int i = 0;
			foreach(object lvi in lbTransforms.Items)
				transformList[i++] = (ImageTransform)lvi;

			Stream file = File.Open(Application.StartupPath + "/environment", FileMode.Create);
			BinaryFormatter formatter = new BinaryFormatter();
			try{
			formatter.Serialize(file, transformList);
			}catch(Exception ex){
				MessageBox.Show(ex.ToString());
			}
			file.Close();*/
		}
	}
}

