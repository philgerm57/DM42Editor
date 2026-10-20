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
using Microsoft.Web.WebView2.Core;

namespace dm42editor
{
    // Visionneuse PDF basée sur WebView2 (lecteur PDF intégré d'Edge)
    public partial class FrmPDF : Form
    {
        string pdfPath;

        public FrmPDF() : this(DocPath("dm42_user_manual.pdf"))
        {
        }

        /// <summary>Document cherché à côté de l'exe, puis dans le sous-dossier doc (installation).</summary>
        public static string DocPath(string fileName)
        {
            string p = Path.Combine(Application.StartupPath, fileName);
            if (File.Exists(p)) return p;
            string d = Path.Combine(Application.StartupPath, "doc", fileName);
            return File.Exists(d) ? d : p;
        }

        public FrmPDF(string aPdfPath)
        {
            InitializeComponent();
            pdfPath = aPdfPath;
            Load += FrmPDF_Load;
        }

        private async void FrmPDF_Load(object sender, EventArgs e)
        {
            if (!File.Exists(pdfPath))
            {
                MessageBox.Show("Fichier PDF introuvable :\n" + pdfPath, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // dossier de données WebView2 hors du répertoire de l'exe (qui peut être en lecture seule)
                string userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dm42editor", "WebView2");
                CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await pdfViewer.EnsureCoreWebView2Async(env);
                Text = Path.GetFileName(pdfPath);
                pdfViewer.CoreWebView2.Navigate(new Uri(pdfPath).AbsoluteUri);
            }
            catch (WebView2RuntimeNotFoundException)
            {
                MessageBox.Show("Le runtime Microsoft Edge WebView2 n'est pas installé.\nTéléchargez-le sur https://developer.microsoft.com/microsoft-edge/webview2/", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
