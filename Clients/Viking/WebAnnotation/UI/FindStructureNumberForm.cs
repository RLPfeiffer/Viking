﻿using System;
using System.Windows.Forms;
using WebAnnotation.ViewModel;
using WebAnnotationModel;

namespace WebAnnotation.UI
{
    public partial class FindStructureNumberForm : Form
    {
        public FindStructureNumberForm()
        {
            InitializeComponent();
        }

        private void btnGo_Click(object sender, EventArgs e)
        {
            int StructureNumber;
            try
            {
                StructureNumber = this.numStructure.IntValue;
            }
            catch (FormatException)
            {
                return;
            }

            StructureObj structure = Store.Structures.GetObjectByID((long)StructureNumber);
            if (structure == null)
            {
                MessageBox.Show(this, "No structure found with that ID", "Error", MessageBoxButtons.OK);
                return;
            }

            Structure structView = new Structure(structure);

            structView.ShowProperties();

            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
