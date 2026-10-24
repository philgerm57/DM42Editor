using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace dm42editor
{
    /// <summary>Fichier → « Convertir la syntaxe HP-41 → DM42… » : aperçu des changements, puis application (Ctrl+Z annule).</summary>
    public partial class Form1
    {
        private void InitHp41Menu()
        {
            var item = new ToolStripMenuItem("Convertir la syntaxe HP-41 → DM42…", null, (s, e) => ConvertHp41())
            { ShortcutKeys = Keys.Control | Keys.Shift | Keys.H };
            int at = fileToolStripMenuItem.DropDownItems.IndexOf(toolStripMenuItem2);   // avant « Encoder et enregistrer en .raw »
            fileToolStripMenuItem.DropDownItems.Insert(at < 0 ? fileToolStripMenuItem.DropDownItems.Count : at, item);
            cmMain.Items.Add(new ToolStripMenuItem("Convertir la syntaxe HP-41 → DM42…", null, (s, e) => ConvertHp41()) { ShortcutKeyDisplayString = "Ctrl+Maj+H" });
        }

        private void ConvertHp41()
        {
            var tb = CurrentTB;
            if (tb == null) { MessageBox.Show(this, "Ouvrez d'abord un programme.", "Conversion HP-41"); return; }
            var changes = new List<Hp41Converter.Change>();
            string converted = Hp41Converter.Convert(tb.Text, changes);
            if (changes.Count == 0)
            {
                MessageBox.Show(this, "Aucune écriture HP-41 à convertir dans ce programme.", "Conversion HP-41", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!ShowHp41Preview(changes)) return;
            if (converted == tb.Text) return;   // seulement des avertissements

            int caret = tb.Selection.Start.iLine;
            tb.SelectAll();
            tb.InsertText(converted);           // une seule étape d'annulation
            tb.Selection = new Dm42.Editor.TextRange(tb, 0, caret, 0, caret);
            tb.DoSelectionVisible();
        }

        /// <summary>Liste des changements proposés ; true si l'utilisateur les applique.</summary>
        private bool ShowHp41Preview(List<Hp41Converter.Change> changes)
        {
            int edits = changes.Count(c => !c.Warning), warnings = changes.Count(c => c.Warning);
            using (var f = new Form
            {
                Text = "Conversion HP-41 → DM42",
                Size = new Size(860, 520),
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                ShowInTaskbar = false,
                Font = new Font("Segoe UI", 9f)
            })
            {
                var grid = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    AllowUserToResizeRows = false,
                    RowHeadersVisible = false,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                    BackgroundColor = SystemColors.Window,
                    BorderStyle = BorderStyle.None
                };
                var mono = new Font("Consolas", 10f);
                grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ligne", FillWeight = 10 });
                grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "HP-41", FillWeight = 30, DefaultCellStyle = { Font = mono } });
                grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "DM42", FillWeight = 30, DefaultCellStyle = { Font = mono } });
                grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Remarque", FillWeight = 40 });
                foreach (var c in changes)
                {
                    int r = grid.Rows.Add((c.Line + 1).ToString(), c.Before, c.Warning ? "⚠ inchangé" : c.After, c.Note);
                    if (c.Warning) grid.Rows[r].DefaultCellStyle.ForeColor = Color.FromArgb(190, 60, 20);
                }

                var info = new Label
                {
                    Dock = DockStyle.Top,
                    Height = 36,
                    Padding = new Padding(8, 8, 8, 0),
                    Text = $"{edits} ligne(s) à convertir" + (warnings > 0 ? $", {warnings} instruction(s) sans équivalent à revoir à la main" : "") +
                           ".   Ctrl+Z dans l'éditeur annule la conversion."
                };
                var ok = new Button { Text = edits > 0 ? "Appliquer" : "Fermer", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(100, 0) };
                var cancel = new Button { Text = "Annuler", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(100, 0) };
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(8) };
                buttons.Controls.Add(cancel);
                buttons.Controls.Add(ok);

                // double-clic : montre la ligne dans l'éditeur
                grid.CellDoubleClick += (s, e) =>
                {
                    if (e.RowIndex < 0 || CurrentTB == null) return;
                    GotoTextLine(CurrentTB, changes[e.RowIndex].Line);
                };

                f.Controls.Add(grid);
                f.Controls.Add(info);
                f.Controls.Add(buttons);
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                if (AppTheme.Dark) AppTheme.Apply(f);
                return f.ShowDialog(this) == DialogResult.OK && edits > 0;
            }
        }
    }
}
