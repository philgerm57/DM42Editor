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
        ToolStripStatusLabel modeLabel;
        string modeShown;

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
                line => { var tb = CurrentSourceTB; if (tb != null) GotoTextLine(tb, line - 1); },
                () => CurrentSourceTB);
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
            // pas à pas (DM42) : touches des débogueurs de Visual Studio
            var debug = new ToolStripMenuItem("Pas à pas");
            ToolStripMenuItem D(string text, Keys keys, Action act) =>
                new ToolStripMenuItem(text, null, (s, e) => act()) { ShortcutKeys = keys };
            debug.DropDownItems.AddRange(new ToolStripItem[]
            {
                D("Pas détaillé", Keys.F11, () => StepSimPanel(RpnSimPanel.StepKind.Into)),
                D("Pas principal", Keys.F10, () => StepSimPanel(RpnSimPanel.StepKind.Over)),
                D("Sortir du sous-programme", Keys.Shift | Keys.F11, () => StepSimPanel(RpnSimPanel.StepKind.Out)),
                D("Pas précédent", Keys.Control | Keys.F10, () => StepSimPanel(RpnSimPanel.StepKind.Back)),
                D("Arrêter le pas à pas", Keys.Shift | Keys.F5, () => simPanel.StopDebug()),
                new ToolStripSeparator(),
                D("Point d'arrêt", Keys.F9, () => simPanel.ToggleBreakpoint()),
                D("Effacer les points d'arrêt", Keys.Control | Keys.Shift | Keys.F9, () => CurrentSourceTB?.ClearBreakpoints()),
            });
            fileToolStripMenuItem.DropDownItems.Insert(fileToolStripMenuItem.DropDownItems.IndexOf(runItem) + 1, debug);
            cmMain.Items.Add(new ToolStripMenuItem("Simulateur du programme", null, (s, e) => ShowSimPanel(editorSplit.Panel2Collapsed)) { ShortcutKeyDisplayString = "Ctrl+F5" });

            // barre d'état, à droite : type de l'onglet courant et calculatrice par défaut (double-clic : Paramètres)
            modeLabel = new ToolStripStatusLabel
            {
                BorderSides = ToolStripStatusLabelBorderSides.Left, BorderStyle = Border3DStyle.Etched,
                Padding = new Padding(6, 0, 6, 0), DoubleClickEnabled = true,
                ToolTipText = Lang.T("Type de l'onglet courant et calculatrice par défaut (double-clic : Fichier → Paramètres)")
            };
            modeLabel.DoubleClick += (s, e) => parameterToolStripMenuItem.PerformClick();
            statusStrip1.ShowItemToolTips = true;
            // le libellé de gauche (Spring) prend la place restante : sans taille fixe, le nouveau tient sur la même ligne
            lbWordUnderMouse.AutoSize = true;
            statusStrip1.Items.Add(modeLabel);
            UpdateModeLabel(null, EventArgs.Empty);
            Application.Idle += UpdateModeLabel;
            FormClosed += (s, e) => Application.Idle -= UpdateModeLabel;
        }

        /// <summary>« Onglet C47 · Par défaut : DM42 » : suit l'onglet choisi et le réglage des Paramètres.</summary>
        void UpdateModeLabel(object sender, EventArgs e)
        {
            string def = Emulator.DefaultC47 ? "C47" : "DM42";
            var tb = CurrentSourceTB;
            string text = tb == null
                ? Lang.F("Par défaut : {0}", def)
                : Lang.F("Onglet {0}  ·  Par défaut : {1}", IsC47(tb) ? "C47" : "DM42", def);
            if (text == modeShown) return;
            modeShown = text;
            modeLabel.Text = text;
        }

        void ShowSimPanel(bool show)
        {
            if (show == !editorSplit.Panel2Collapsed) return;
            editorSplit.Panel2Collapsed = !show;
            simMenuItem.Checked = show;
            if (!show) simPanel.StopDebug();
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

        void StepSimPanel(RpnSimPanel.StepKind kind)
        {
            if (CurrentSourceTB == null) { MessageBox.Show(this, Lang.T("Ouvrez d'abord un programme RPN."), Lang.T("Simulateur")); return; }
            ShowSimPanel(true);
            simPanel.DoStep(kind);
        }

        /// <summary>Onglet changé : le simulateur reprend ses labels et son type (DM42 / C47).</summary>
        void SyncSimPanel()
        {
            if (simPanel != null && !editorSplit.Panel2Collapsed) simPanel.Refresh(keepOutput: false);
            if (modeLabel != null) UpdateModeLabel(null, EventArgs.Empty);
        }
    }
}
