using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Dm42.Editor
{
    /// <summary>Fenêtre Rechercher / Remplacer d'un CodeEditor (Ctrl+F, Ctrl+H, F3 / Maj+F3).</summary>
    sealed class FindReplaceForm : Form
    {
        readonly CodeEditor tb;
        readonly TextBox findBox = new TextBox { Width = 260 };
        readonly TextBox replaceBox = new TextBox { Width = 260 };
        readonly CheckBox matchCase = new CheckBox { Text = "Respecter la casse", AutoSize = true };
        readonly CheckBox wholeWord = new CheckBox { Text = "Mot entier", AutoSize = true };
        readonly CheckBox useRegex = new CheckBox { Text = "Expression régulière", AutoSize = true };
        readonly Label replaceLabel = new Label { Text = "Remplacer par", AutoSize = true, Anchor = AnchorStyles.Left };
        readonly Button replaceButton = new Button { Text = "Remplacer", AutoSize = true };
        readonly Button replaceAllButton = new Button { Text = "Tout remplacer", AutoSize = true };
        readonly Label info = new Label { AutoSize = true, ForeColor = Color.Gray };

        public FindReplaceForm(CodeEditor tb)
        {
            this.tb = tb;
            Text = "Rechercher";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(10);
            KeyPreview = true;
            Font = new Font("Segoe UI", 9f);

            var grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
            var next = new Button { Text = "Suivant", AutoSize = true };
            var prev = new Button { Text = "Précédent", AutoSize = true };
            var close = new Button { Text = "Fermer", AutoSize = true, DialogResult = DialogResult.Cancel };
            next.Click += (s, e) => FindNext(false);
            prev.Click += (s, e) => FindNext(true);
            close.Click += (s, e) => Hide();
            replaceButton.Click += (s, e) => ReplaceOne();
            replaceAllButton.Click += (s, e) => ReplaceAll();

            grid.Controls.Add(new Label { Text = "Rechercher", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            grid.Controls.Add(findBox, 1, 0);
            grid.Controls.Add(replaceLabel, 0, 1);
            grid.Controls.Add(replaceBox, 1, 1);
            var options = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0, 6, 0, 0) };
            options.Controls.AddRange(new Control[] { matchCase, wholeWord, useRegex });
            grid.Controls.Add(options, 1, 2);
            var buttons = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
            buttons.Controls.AddRange(new Control[] { next, prev, replaceButton, replaceAllButton, close });
            grid.Controls.Add(buttons, 0, 3);
            grid.SetColumnSpan(buttons, 2);
            grid.Controls.Add(info, 0, 4);
            grid.SetColumnSpan(info, 2);
            Controls.Add(grid);

            AcceptButton = next;
            CancelButton = close;
            findBox.TextChanged += (s, e) => info.Text = "";
            FormClosing += (s, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            };
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.F3) { FindNext(e.Shift); e.Handled = true; }
            };
        }

        public void ShowFor(bool replace, string initial)
        {
            Text = replace ? "Remplacer" : "Rechercher";
            replaceLabel.Visible = replaceBox.Visible = replaceButton.Visible = replaceAllButton.Visible = replace;
            if (!string.IsNullOrEmpty(initial)) findBox.Text = initial;
            info.Text = "";
            var owner = tb.FindForm();
            if (!Visible)
            {
                var p = tb.PointToScreen(new Point(Math.Max(0, tb.ClientSize.Width - 420), 10));
                Location = p;
                Show(owner);
            }
            Activate();
            findBox.Focus();
            findBox.SelectAll();
        }

        Regex BuildRegex()
        {
            string pattern = useRegex.Checked ? findBox.Text : Regex.Escape(findBox.Text);
            if (wholeWord.Checked) pattern = @"\b" + pattern + @"\b";
            var opt = matchCase.Checked ? RegexOptions.None : RegexOptions.IgnoreCase;
            try { return new Regex(pattern, opt); }
            catch (ArgumentException ex) { info.Text = "Expression invalide : " + ex.Message; return null; }
        }

        /// <summary>Cherche après (ou avant) le curseur, en reprenant au début du texte si besoin.</summary>
        public bool FindNext(bool backward)
        {
            if (findBox.Text.Length == 0) { ShowFor(replaceBox.Visible, null); return false; }
            var re = BuildRegex();
            if (re == null) return false;
            var all = new System.Collections.Generic.List<TextRange>(tb.Range.GetRanges(re));
            if (all.Count == 0) { info.Text = "Introuvable."; return false; }
            TextRange found = null;
            if (!backward)
            {
                var from = tb.Selection.To;
                found = all.Find(r => r.From >= from && !(r.From == tb.Selection.From && r.To == tb.Selection.To)) ?? all[0];
            }
            else
            {
                var from = tb.Selection.From;
                found = all.FindLast(r => r.From < from) ?? all[all.Count - 1];
            }
            tb.Selection = new TextRange(tb, found.To, found.From);
            tb.DoRangeVisible(found);
            info.Text = $"{all.Count} occurrence(s)";
            return true;
        }

        void ReplaceOne()
        {
            if (tb.ReadOnly) return;
            var re = BuildRegex();
            if (re == null) return;
            string sel = tb.Selection.Text;
            var m = re.Match(sel);
            if (!tb.Selection.IsEmpty && m.Success && m.Index == 0 && m.Length == sel.Length)
                tb.InsertText(useRegex.Checked ? m.Result(replaceBox.Text) : replaceBox.Text);
            FindNext(false);
        }

        void ReplaceAll()
        {
            if (tb.ReadOnly) return;
            var re = BuildRegex();
            if (re == null) return;
            string text = tb.Text;
            int count = re.Matches(text).Count;
            if (count == 0) { info.Text = "Introuvable."; return; }
            string result = useRegex.Checked ? re.Replace(text, replaceBox.Text) : re.Replace(text, _ => replaceBox.Text);
            tb.SelectAll();
            tb.InsertText(result);
            info.Text = $"{count} remplacement(s)";
        }
    }
}
