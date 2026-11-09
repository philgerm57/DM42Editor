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
        /// <summary>Conversion HP-41 → DM42 du menu Fichier et du menu contextuel : grisée sur un onglet C47 (SyncWithTabType).</summary>
        ToolStripMenuItem hp41Menu, hp41Popup;

        private void InitHp41Menu()
        {
            var item = hp41Menu = new ToolStripMenuItem(Lang.T("Convertir la syntaxe HP-41 → DM42…"), null, (s, e) => ConvertHp41())
            { ShortcutKeys = Keys.Control | Keys.Shift | Keys.H };
            int at = fileToolStripMenuItem.DropDownItems.IndexOf(toolStripMenuItem2);   // avant « Encoder et enregistrer en .raw »
            fileToolStripMenuItem.DropDownItems.Insert(at < 0 ? fileToolStripMenuItem.DropDownItems.Count : at, item);
            cmMain.Items.Add(hp41Popup = new ToolStripMenuItem(Lang.T("Convertir la syntaxe HP-41 → DM42…"), null, (s, e) => ConvertHp41()) { ShortcutKeyDisplayString = "Ctrl+Maj+H" });

            // envoi vers l'émulateur réglé dans Paramètres (Free42, Plus42…)
            var send = new ToolStripMenuItem(Lang.T("Envoyer vers l'émulateur"), null, (s, e) => SendToEmulator()) { ShortcutKeys = Keys.F7 };
            int p = fileToolStripMenuItem.DropDownItems.IndexOf(toolStripMenuItem2);
            fileToolStripMenuItem.DropDownItems.Insert(p < 0 ? fileToolStripMenuItem.DropDownItems.Count : p + 1, send);
            cmMain.Items.Add(new ToolStripMenuItem(Lang.T("Envoyer vers l'émulateur"), null, (s, e) => SendToEmulator()) { ShortcutKeyDisplayString = "F7" });

            // envoi du .p47 vers l'émulateur R47 / C47 d'un téléphone Android branché en USB (AndroidR47.cs)
            androidMenu = new ToolStripMenuItem(Lang.T("Envoyer vers la R47 Android (USB)"), null, (s, e) => SendToAndroid()) { ShortcutKeys = Keys.Shift | Keys.F7 };
            fileToolStripMenuItem.DropDownItems.Insert(fileToolStripMenuItem.DropDownItems.IndexOf(send) + 1, androidMenu);
            cmMain.Items.Add(androidPopup = new ToolStripMenuItem(Lang.T("Envoyer vers la R47 Android (USB)"), null, (s, e) => SendToAndroid()) { ShortcutKeyDisplayString = "Maj+F7" });
        }

        /// <summary>Envoi vers la R47 Android : grisé sur un onglet DM42 (SyncWithTabType).</summary>
        ToolStripMenuItem androidMenu, androidPopup;

        /// <summary>Maj+F7 sur un onglet C47 : encode et copie le .p47 dans l'application R47 / C47 du téléphone.</summary>
        private void SendToAndroid()
        {
            var tb = CurrentSourceTB;
            if (!IsC47(tb)) { lbWordUnderMouse.Text = Lang.T("Ouvrez d'abord un onglet C47."); return; }
            CheckC47(tb);
            var r = ((TbInfo)tb.Tag).C47Result;
            if (!r.Ok)
            {
                MessageBox.Show(this, Lang.F("Le programme contient {0} erreur(s) : corrigez-les avant de l'envoyer.", r.Errors.Count) + "\n\n"
                    + string.Join("\n", r.Errors.Take(10).Select(e => Lang.F("Ligne {0} : {1}", e.Line, C47.Core.Tr.T(e.Message)))), Lang.T("R47 Android"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (r.Program.Length == 0) { lbWordUnderMouse.Text = Lang.T("Rien à envoyer : l'onglet ne contient pas d'instruction."); return; }
            lbWordUnderMouse.Text = Lang.T("Envoi vers le téléphone…");
            lbWordUnderMouse.Owner?.Refresh();
            lbWordUnderMouse.Text = AndroidR47.Send(this, C47.Core.P47File.Write(r.Program), C47ProgramName(tb));
        }

        /// <summary>Copie le programme de l'onglet (sans commentaires) et affiche l'émulateur : mode PRGM puis Ctrl+V.</summary>
        private void SendToEmulator()
        {
            var tb = CurrentTB;
            if (tb == null) { MessageBox.Show(this, Lang.T("Ouvrez d'abord un programme."), Lang.T("Émulateur")); return; }
            if (IsC47(tb)) { SendC47ToSimulator(tb); return; }   // Form1.C47.cs
            string listing = Emulator.CleanListing(tb.Text, out int steps);
            if (steps == 0) { lbWordUnderMouse.Text = Lang.T("Rien à envoyer : l'onglet ne contient pas d'instruction."); return; }
            lbWordUnderMouse.Text = Emulator.Send(this, listing, steps);
        }

        private void ConvertHp41()
        {
            var tb = CurrentTB;
            if (tb == null) { MessageBox.Show(this, Lang.T("Ouvrez d'abord un programme."), Lang.T("Conversion HP-41")); return; }
            if (IsC47(tb)) return;   // syntaxe HP-42S seulement : la C47 a ses propres noms
            var changes = new List<Hp41Converter.Change>();
            string converted = Hp41Converter.Convert(tb.Text, changes);
            if (changes.Count == 0)
            {
                MessageBox.Show(this, Lang.T("Aucune écriture HP-41 à convertir dans ce programme."), Lang.T("Conversion HP-41"), MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                Text = Lang.T("Conversion HP-41 → DM42"),
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
                grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Lang.T("Ligne"), FillWeight = 10 });
                grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "HP-41", FillWeight = 30, DefaultCellStyle = { Font = mono } });
                grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "DM42", FillWeight = 30, DefaultCellStyle = { Font = mono } });
                grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Lang.T("Remarque"), FillWeight = 40 });
                foreach (var c in changes)
                {
                    int r = grid.Rows.Add((c.Line + 1).ToString(), c.Before, c.Warning ? Lang.T("⚠ inchangé") : c.After, c.Note);
                    if (c.Warning) grid.Rows[r].DefaultCellStyle.ForeColor = Color.FromArgb(190, 60, 20);
                }

                var info = new Label
                {
                    Dock = DockStyle.Top,
                    Height = 36,
                    Padding = new Padding(8, 8, 8, 0),
                    Text = Lang.F("{0} ligne(s) à convertir", edits) + (warnings > 0 ? Lang.F(", {0} instruction(s) sans équivalent à revoir à la main", warnings) : "") +
                           Lang.T(".   Ctrl+Z dans l'éditeur annule la conversion.")
                };
                var ok = new Button { Text = edits > 0 ? Lang.T("Appliquer") : Lang.T("Fermer"), DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(100, 0) };
                var cancel = new Button { Text = Lang.T("Annuler"), DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(100, 0) };
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
                Lang.Apply(f);
                return f.ShowDialog(this) == DialogResult.OK && edits > 0;
            }
        }
    }
}
