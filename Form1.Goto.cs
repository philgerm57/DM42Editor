using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Dm42.Editor;

namespace dm42editor
{
    /// <summary>
    /// Menu « Goto... » : aller à une ligne de programme (numéro de pas HP-42S, pas numéro de ligne de texte),
    /// aller à un label, ou à un signet. Raccourci Ctrl+G dans l'éditeur.
    /// </summary>
    public partial class Form1
    {
        static readonly Regex ProgLineNo = new Regex(@"^\s*(\d+)(?:▸|▶|>|\s+)");
        static readonly Regex LblRegex = new Regex(@"\bLBL\s+(""[^""]*""|\d{2}|[A-Ja-e])(?=\s|$)");

        /// <summary>
        /// Lignes de code du programme : (numéro de pas, ligne de texte 0-based).
        /// Lignes vides, commentaires (# @ //) et en-tête { ... } ignorés.
        /// Si une ligne porte son numéro (« 12 RCL "A" »), c'est ce numéro qui compte.
        /// </summary>
        static List<KeyValuePair<int, int>> ProgramLines(CodeEditor tb)
        {
            var result = new List<KeyValuePair<int, int>>();
            int count = 0;
            for (int i = 0; i < tb.LinesCount; i++)
            {
                string line = tb.GetLineText(i);
                string t = line.Trim();
                if (t.Length == 0 || t.StartsWith("#") || t.StartsWith("@") || t.StartsWith("//")) continue;
                var m = ProgLineNo.Match(line);
                string code = m.Success ? line.Substring(m.Length).Trim() : t;
                if (code.StartsWith("{")) { count = 0; continue; } // en-tête : la numérotation repart à 1
                count++;
                int no = m.Success ? int.Parse(m.Groups[1].Value) : count;
                result.Add(new KeyValuePair<int, int>(no, i));
            }
            return result;
        }

        void GotoTextLine(CodeEditor tb, int iLine)
        {
            if (tb == null || iLine < 0 || iLine >= tb.LinesCount) return;
            tb.Navigate(iLine);
            tb.Selection = new TextRange(tb, 0, iLine, tb.GetLineText(iLine).Length, iLine);
            tb.DoSelectionVisible();
            tb.Focus();
            tb.Invalidate();
        }

        /// <summary>Demande un numéro de pas et y place le curseur.</summary>
        void GotoProgramLine()
        {
            var tb = CurrentTB;
            if (tb == null) return;
            var lines = ProgramLines(tb);
            if (lines.Count == 0) { MessageBox.Show(this, "Aucune ligne de programme dans cet onglet.", "Goto"); return; }

            int min = int.MaxValue, max = 0, current = lines[0].Key;
            foreach (var kv in lines)
            {
                min = Math.Min(min, kv.Key);
                max = Math.Max(max, kv.Key);
                if (kv.Value <= tb.Selection.Start.iLine) current = kv.Key;
            }

            using (var dlg = new Form
            {
                Text = "Aller à la ligne de programme",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                ClientSize = new Size(300, 110),
                Font = Font
            })
            {
                var label = new Label { Text = $"Numéro de ligne ({min} à {max}) :", AutoSize = true, Location = new Point(12, 15) };
                var num = new NumericUpDown { Minimum = min, Maximum = max, Value = Math.Max(min, Math.Min(max, current)), Location = new Point(15, 40), Width = 270 };
                var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(129, 75), Width = 75 };
                var cancel = new Button { Text = "Annuler", DialogResult = DialogResult.Cancel, Location = new Point(210, 75), Width = 75 };
                dlg.Controls.AddRange(new Control[] { label, num, ok, cancel });
                dlg.AcceptButton = ok;
                dlg.CancelButton = cancel;
                dlg.Shown += (s, e) => { num.Select(0, num.Text.Length); num.Focus(); };
                if (AppTheme.Dark) AppTheme.Apply(dlg);
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                int wanted = (int)num.Value;
                foreach (var kv in lines)
                {
                    if (kv.Key == wanted) { GotoTextLine(tb, kv.Value); return; }
                }
                MessageBox.Show(this, $"La ligne {wanted} n'existe pas dans ce programme.", "Goto");
            }
        }

        /// <summary>Remplit le menu Goto : ligne de programme, labels de l'onglet courant, puis signets.</summary>
        void FillGotoMenu()
        {
            gotoButton.DropDownItems.Clear();

            var line = new ToolStripMenuItem("Aller à la ligne de programme…", MenuIcon("goto"), (s, e) => GotoProgramLine())
            { ShortcutKeyDisplayString = "Ctrl+G" };
            gotoButton.DropDownItems.Add(line);

            var tb = CurrentTB;
            if (tb == null) return;

            var labels = new List<ToolStripItem>();
            foreach (var kv in ProgramLines(tb))
            {
                var m = LblRegex.Match(tb.GetLineText(kv.Value));
                if (!m.Success) continue;
                int iLine = kv.Value;
                labels.Add(new ToolStripMenuItem($"{kv.Key:00}  LBL {m.Groups[1].Value}", null, (s, e) => GotoTextLine(tb, iLine)));
            }
            if (labels.Count > 0)
            {
                gotoButton.DropDownItems.Add(new ToolStripSeparator());
                gotoButton.DropDownItems.AddRange(labels.ToArray());
            }
        }
    }
}
