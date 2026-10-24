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

        static readonly System.Text.RegularExpressions.Regex StepRx =
            new System.Text.RegularExpressions.Regex(@"^\s*(\d+)(?:▸|▶|>|\s+)(?=\S)");
        int step;   // numéro de pas quand la ligne source n'en porte pas

        public void Clear()
        {
            this.listView1.Items.Clear();
            step = 0;
        }

        /// <summary>
        /// Ajoute une ligne encodée : numéro de pas, code, commentaire (séparé du code), octets, erreur.
        /// textLine : ligne de l'éditeur (0-based) pour le double-clic, -1 si inconnue.
        /// </summary>
        public void Add(string rpn, string raw, string ErrMsg = "", int textLine = -1)
        {
            string body = rpn ?? "";
            var m = StepRx.Match(body);
            step++;
            string no = step.ToString("00");
            if (m.Success) { no = m.Groups[1].Value; body = body.Substring(m.Length); }
            Flowchart.FlowGraph.SplitComment(body, out string code, out _, out string comment);
            code = code.Trim();
            if (code.Equals("END", StringComparison.OrdinalIgnoreCase)) step = 0;   // programme suivant : on repart à 01

            var item = new ListViewItem(new[] { no, code, comment ?? "", raw ?? "", ErrMsg ?? "" }) { Tag = textLine };
            listView1.Items.Add(item);
        }

        private void listView1_DoubleClick(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 0) return;
            var item = listView1.SelectedItems[0];
            // ligne de l'éditeur ; à défaut, l'ancienne correspondance (une ligne par entrée, après l'en-tête)
            int lPosition = item.Tag is int t && t >= 0 ? t : item.Index + 1;

            FarsiLibrary.Win.FATabStripItem TB = (FarsiLibrary.Win.FATabStripItem)Tag;

            if (_FL != null && TB != null && TB.Controls.Count > 0 && TB.Controls[0] is CodeEditor FCTB)
            {
                // selectionne la page du code source lié et place le curseur sur la ligne
                _FL.SelectedItem = TB;
                FCTB.Selection = new TextRange(FCTB, 0, lPosition, FCTB.GetLineLength(lPosition), lPosition);
                FCTB.DoSelectionVisible();
                FCTB.Focus();
            }
        }
    }
}
