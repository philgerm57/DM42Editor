using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace dm42editor
{
    public partial class FrmAide : Form
    {
        string _Version = "2.2";
        public FrmAide()
        {
            InitializeComponent();

            // Centre la fenetre

        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void FrmAide_Load(object sender, EventArgs e)
        {
            this.CenterToParent();
            label4.Text = "Version:" + _Version;
        }

        private void label5_Click(object sender, EventArgs e)
        {

        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void label6_Click(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
