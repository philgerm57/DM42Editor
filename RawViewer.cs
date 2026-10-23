using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Dm42.Editor;

namespace dm42editor
{
    public partial class RawViewer : UserControl
    {


        FarsiLibrary.Win.FATabStrip  _FL; 
        public RawViewer()
        {
            InitializeComponent();
        }


        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public FarsiLibrary.Win.FATabStrip SetTabStrib
        {
            set { _FL = value; }
        }

        public void Clear()
        {
            this.listView1.Items.Clear(); 
        }

        public void Add(string rpn, string raw, string ErrMsg="")
        {
            ListViewItem lvi = new ListViewItem();

            listView1.Items.Add(new ListViewItem(new string[] { rpn, raw, ErrMsg })); 
          
        }

        private void listView1_DoubleClick(object sender, EventArgs e)
        {
            // recupere le numero de ligne 
            int lPosition=0 ;
            FarsiLibrary.Win.FATabStripItem  TB;
            TB = (FarsiLibrary.Win.FATabStripItem)Tag;

            lPosition = ((ListView)sender).SelectedItems[0].Index+1; 
            // selectionne la page du code source lié

            if (_FL != null)
            {
                
                _FL.SelectedItem = TB;

                CodeEditor FCTB = (CodeEditor)TB.Controls[0];

                FCTB.Selection.Start = new Place(0, lPosition);
                FCTB.DoSelectionVisible();
                FCTB.Focus();
            }

            // place le curseur sur la même ligne 
        }
    }
}
