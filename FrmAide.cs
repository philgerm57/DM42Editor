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
        // numéro de version unique : balise <Version> de dm42editor.csproj (lue aussi par build.bat pour Inno Setup)
        static readonly string _Version = Application.ProductVersion.Split('+')[0];
        public FrmAide()
        {
            InitializeComponent();
            Lang.Apply(this);
            // textes sur plusieurs lignes : Lang.Apply ne traduit ni la zone de texte ni les \r\n et espaces de fin
            Text = Lang.T("À propos");          // fenêtre sans bordure : titre vu dans Alt+Tab
            label2.Text = Translate(label2.Text);
            textBox1.Text = Translate(textBox1.Text);
        }

        /// <summary>
        /// Traduit un texte de plusieurs lignes : la clé de lang_en.txt est le texte français, lignes sans espaces de fin,
        /// séparées par \n. Le texte reste tel quel s'il n'a pas de traduction (interface en français).
        /// </summary>
        static string Translate(string text)
        {
            string key = string.Join("\n", text.Replace("\r\n", "\n").Split('\n').Select(l => l.TrimEnd())).Trim('\n');
            string en = Lang.T(key);
            return en == key ? text : en.Replace("\n", "\r\n");
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
