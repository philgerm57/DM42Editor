using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using FarsiLibrary.Win;
using Dm42.Editor;

namespace dm42editor
{
    /// <summary>
    /// Menu « Python » : conversion d'un Python simplifié en RPN HP-42S / DM42 (bibliothèque Py42.Core).
    /// Le RPN généré arrive dans un onglet normal de l'éditeur, prêt pour « Encode and save RAW ».
    /// </summary>
    public partial class Form1
    {
        FrmPython pythonWindow;

        private void InitPythonMenu()
        {
            var menu = new ToolStripMenuItem("Python");

            var nouveau = new ToolStripMenuItem("Nouveau programme Python", null, (s, e) => ShowPython(null))
            { ShortcutKeys = Keys.Control | Keys.Shift | Keys.N };
            var ouvrir = new ToolStripMenuItem("Ouvrir un fichier Python…", null, (s, e) => OpenPythonFile())
            { ShortcutKeys = Keys.Control | Keys.Shift | Keys.O };
            var fenetre = new ToolStripMenuItem("Afficher l'éditeur Python", null, (s, e) => ShowPython(null, keepText: true))
            { ShortcutKeys = Keys.F12 };

            var exemples = new ToolStripMenuItem("Exemples");
            for (int i = 0; i < Py42.Core.Examples.All.Count; i++)
            {
                int idx = i;
                exemples.DropDownItems.Add(Py42.Core.Examples.All[i].Title, null, (s, e) => ShowPython(null).LoadExample(idx));
            }

            var aide = new ToolStripMenuItem("Aide Python → RPN", null, (s, e) => FrmPython.ShowHelp(this));

            menu.DropDownItems.AddRange(new ToolStripItem[]
            {
                nouveau, ouvrir, fenetre, new ToolStripSeparator(), exemples, new ToolStripSeparator(), aide
            });

            // avant le menu Help
            int at = menuStrip1.Items.IndexOf(helpToolStripMenuItem);
            menuStrip1.Items.Insert(at < 0 ? menuStrip1.Items.Count : at, menu);
        }

        private FrmPython ShowPython(string file, bool keepText = false)
        {
            if (pythonWindow == null || pythonWindow.IsDisposed)
            {
                pythonWindow = new FrmPython(SendRpnToTab);
                pythonWindow.Show(this);
            }
            else if (!keepText)
            {
                pythonWindow.NewProgram();
            }
            if (file != null) pythonWindow.OpenFile(file);
            pythonWindow.WindowState = pythonWindow.WindowState == FormWindowState.Minimized ? FormWindowState.Normal : pythonWindow.WindowState;
            pythonWindow.Activate();
            return pythonWindow;
        }

        private void OpenPythonFile()
        {
            using (var dlg = new OpenFileDialog { Filter = "Python (*.py)|*.py|Tous les fichiers (*.*)|*.*", Title = "Ouvrir un programme Python" })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK) ShowPython(dlg.FileName, keepText: true);
            }
        }

        /// <summary>
        /// Reçoit le RPN de la fenêtre Python et l'ouvre dans un onglet (réutilise l'onglet du même nom).
        /// path = chemin du .free42 à côté du .py, ou null si le .py n'est pas enregistré.
        /// </summary>
        private void SendRpnToTab(string rpn, string title, string path)
        {
            FATabStripItem tab = null;
            foreach (FATabStripItem t in tsFiles.Items)
            {
                if (t.Title == title && t.Controls.Count > 0 && t.Controls[0] is CodeEditor)
                {
                    tab = t;
                    break;
                }
            }

            if (tab == null)
            {
                var tb = InitNewTab(Color.Black);
                if (tb == null) return;
                tab = new FATabStripItem(title, tb);
                tab.Tag = path;               // null : « Save » demandera un nom
                tab.Title = title;
                tsFiles.AddTab(tab);
            }

            var box = (CodeEditor)tab.Controls[0];
            box.Text = rpn;
            box.IsChanged = true;
            tsFiles.SelectedItem = tab;
            Activate();
        }
    }
}
