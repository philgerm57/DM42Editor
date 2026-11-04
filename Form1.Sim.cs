using System;
using System.Windows.Forms;

namespace dm42editor
{
    /// <summary>Simulateur DM42 / C47 en bas de l'éditeur RPN (RpnSimPanel) : Ctrl+F5 l'affiche ou le masque, F5 lance.</summary>
    public partial class Form1
    {
        SplitContainer editorSplit;
        RpnSimPanel simPanel;
        ToolStripMenuItem simMenuItem;

        private void InitSimPanel()
        {
            // l'éditeur et le simulateur se partagent la zone des onglets, séparés par une barre déplaçable
            var host = tsFiles.Parent;
            host.SuspendLayout();
            host.Controls.Remove(tsFiles);
            editorSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, Panel2Collapsed = true, SplitterWidth = 5 };
            editorSplit.Panel1.Controls.Add(tsFiles);
            tsFiles.Dock = DockStyle.Fill;
            simPanel = new RpnSimPanel(
                () => CurrentSourceTB?.Text ?? "",
                () => IsC47(CurrentSourceTB),
                line => { var tb = CurrentSourceTB; if (tb != null) GotoTextLine(tb, line - 1); });
            editorSplit.Panel2.Controls.Add(simPanel);
            host.Controls.Add(editorSplit);
            editorSplit.BringToFront();
            host.ResumeLayout();

            simMenuItem = new ToolStripMenuItem("Simulateur du programme", null, (s, e) => ShowSimPanel(editorSplit.Panel2Collapsed))
            { ShortcutKeys = Keys.Control | Keys.F5 };
            var runItem = new ToolStripMenuItem("Lancer la simulation", null, (s, e) => RunSimPanel()) { ShortcutKeys = Keys.F5 };
            int at = fileToolStripMenuItem.DropDownItems.IndexOf(printToolStripMenuItem);
            fileToolStripMenuItem.DropDownItems.Insert(at < 0 ? fileToolStripMenuItem.DropDownItems.Count : at + 1, runItem);
            fileToolStripMenuItem.DropDownItems.Insert(at < 0 ? fileToolStripMenuItem.DropDownItems.Count : at + 1, simMenuItem);
            cmMain.Items.Add(new ToolStripMenuItem("Simulateur du programme", null, (s, e) => ShowSimPanel(editorSplit.Panel2Collapsed)) { ShortcutKeyDisplayString = "Ctrl+F5" });
        }

        void ShowSimPanel(bool show)
        {
            if (show == !editorSplit.Panel2Collapsed) return;
            editorSplit.Panel2Collapsed = !show;
            simMenuItem.Checked = show;
            if (show)
            {
                editorSplit.SplitterDistance = Math.Max(120, editorSplit.Height - 260);
                simPanel.Refresh(keepOutput: false);
            }
        }

        void RunSimPanel()
        {
            if (CurrentSourceTB == null) { MessageBox.Show(this, Lang.T("Ouvrez d'abord un programme RPN."), Lang.T("Simulateur")); return; }
            ShowSimPanel(true);
            simPanel.Run();
        }

        /// <summary>Onglet changé : le simulateur reprend ses labels et son type (DM42 / C47).</summary>
        void SyncSimPanel()
        {
            if (simPanel != null && !editorSplit.Panel2Collapsed) simPanel.Refresh(keepOutput: false);
        }
    }
}
