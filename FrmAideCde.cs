using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace dm42editor
{
    public partial class FrmAideCde : Form
    {
        public FrmAideCde()
        {
            InitializeComponent();

            ReadCsvFct("liste_fonctions.csv", listView1); 
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            Close(); 
        }


        private void ReadCsvFct(string fileName, ListView lst)
        {

            List<ListViewItem> items = new List<ListViewItem>();

            if (!Path.IsPathRooted(fileName)) fileName = Path.Combine(Application.StartupPath, fileName);   // indépendant du dossier courant
            using (var reader = new StreamReader(fileName,Encoding.UTF8))
            {
              
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = line.Split(';');

                    ListViewItem dateItem = new ListViewItem(values[0]);
                    if (values.Length > 1)  dateItem.SubItems.Add(values[1]);
                    if (values.Length > 2)  dateItem.SubItems.Add(values[2]);
                    if (values.Length > 3)  dateItem.SubItems.Add(values[3]);
                    if (values.Length > 4)  dateItem.SubItems.Add(values[4]);
                    items.Add(dateItem);


                }
            }

            lst.Items.AddRange(items.ToArray());

        }
    }
}
