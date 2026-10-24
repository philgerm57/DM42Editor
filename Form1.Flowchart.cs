using System;
using System.Windows.Forms;
using dm42editor.Flowchart;

namespace dm42editor
{
    /// <summary>Menu « Logigramme du programme » (Fichier, menu contextuel, Ctrl+L).</summary>
    public partial class Form1
    {
        private void InitFlowchartMenu()
        {
            var item = new ToolStripMenuItem("Logigramme du programme…", null, (s, e) => ShowFlowchart())
            { ShortcutKeys = Keys.Control | Keys.L };
            int at = fileToolStripMenuItem.DropDownItems.IndexOf(printToolStripMenuItem);
            fileToolStripMenuItem.DropDownItems.Insert(at < 0 ? fileToolStripMenuItem.DropDownItems.Count : at + 1, item);

            cmMain.Items.Add(new ToolStripSeparator());
            cmMain.Items.Add(new ToolStripMenuItem("Logigramme du programme…", null, (s, e) => ShowFlowchart()) { ShortcutKeyDisplayString = "Ctrl+L" });
            cmMain.Items.Add(new ToolStripMenuItem("Aligner les commentaires", null, (s, e) => AlignComments(CurrentTB))
            { ShortcutKeys = Keys.Control | Keys.Shift | Keys.A });
        }

        /// <summary>
        /// Range les commentaires de fin de ligne (// # @) dans une même colonne, deux espaces après
        /// l'instruction la plus longue. Les lignes de commentaire seules ne bougent pas. Ctrl+Z annule.
        /// </summary>
        internal static bool AlignComments(Dm42.Editor.CodeEditor tb)
        {
            if (tb == null || tb.ReadOnly) return false;
            var lines = tb.Lines;
            var parts = new (string Code, string Marker, string Comment)[lines.Count];
            int col = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                FlowGraph.SplitComment(lines[i], out string code, out string marker, out string comment);
                parts[i] = (code.TrimEnd(), marker, comment);
                if (marker != null && code.Trim().Length > 0) col = Math.Max(col, code.TrimEnd().Length);
            }
            if (col == 0) return false;   // aucun commentaire de fin de ligne
            col += 2;

            var result = new string[lines.Count];
            for (int i = 0; i < lines.Count; i++)
            {
                var p = parts[i];
                result[i] = p.Marker != null && p.Code.Trim().Length > 0
                    ? p.Code.PadRight(col) + p.Marker + (p.Comment.Length > 0 ? " " + p.Comment : "")
                    : lines[i];
            }
            string text = string.Join("\r\n", result);
            if (text == tb.Text) return false;

            int caret = tb.Selection.Start.iLine;
            tb.SelectAll();
            tb.InsertText(text);      // une seule étape d'annulation
            tb.Selection = new Dm42.Editor.TextRange(tb, 0, caret, 0, caret);
            tb.DoSelectionVisible();
            return true;
        }

        /// <summary>Ouvre le logigramme de l'onglet courant ; double-clic sur une forme : retour à la ligne.</summary>
        private void ShowFlowchart()
        {
            var tb = CurrentTB;
            if (tb == null) { MessageBox.Show(this, "Ouvrez d'abord un programme RPN.", "Logigramme"); return; }
            string title = tsFiles.SelectedItem?.Title ?? "Programme";
            var f = new FrmFlowchart(() => tb.IsDisposed ? "" : tb.Text, title, line =>
            {
                if (tb.IsDisposed) return;
                CurrentTB = tb;
                GotoTextLine(tb, line);
                Activate();
            });
            // le logigramme suit les modifications du programme (commentaires, instructions…)
            EventHandler<Dm42.Editor.TextChangedEventArgs> follow = (s, e) => { if (!f.IsDisposed) f.Reload(); };
            tb.TextChangedDelayed += follow;
            f.FormClosed += (s, e) => tb.TextChangedDelayed -= follow;
            f.Show(this);
        }
    }
}
