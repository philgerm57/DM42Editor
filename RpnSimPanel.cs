using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace dm42editor
{
    /// <summary>
    /// Simulateur en bas de l'éditeur RPN, comme celui de la fenêtre Python : onglet DM42 → simulateur HP-42S de Py42
    /// (listing relu par Py42.Core.RpnListing) ; onglet C47 → vrai simulateur C47 sans fenêtre, écran compris.
    /// </summary>
    sealed class RpnSimPanel : UserControl
    {
        static readonly Color Lcd = Color.FromArgb(198, 205, 176);
        static readonly Color LcdInk = Color.FromArgb(30, 36, 24);

        readonly Func<string> source;          // texte de l'onglet courant
        readonly Func<bool> isC47;             // onglet C47 plutôt que DM42
        readonly Action<int> gotoLine;         // ligne (1-based) d'une erreur DM42
        readonly ComboBox entryBox;
        readonly TextBox stackBox, inputsBox, output;
        readonly PictureBox lcdBox;
        readonly Label title;

        public RpnSimPanel(Func<string> source, Func<bool> isC47, Action<int> gotoLine)
        {
            this.source = source;
            this.isC47 = isC47;
            this.gotoLine = gotoLine;
            Dock = DockStyle.Fill;

            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 7, RowCount = 2, Padding = new Padding(4) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            Label L(string t) => new Label { Text = t, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(6, 6, 4, 0) };
            title = L("Simulateur DM42");
            title.Font = new Font(Font, FontStyle.Bold);
            entryBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            stackBox = new TextBox { Dock = DockStyle.Fill, Font = new Font("Consolas", 10f) };
            inputsBox = new TextBox { Dock = DockStyle.Fill, Font = new Font("Consolas", 10f) };
            var run = new Button { Text = "R/S  (F5)", AutoSize = true, Margin = new Padding(6, 1, 0, 1) };
            run.Click += (s, e) => Run();
            var tip = new ToolTip();
            tip.SetToolTip(stackBox, Lang.T("Valeurs mises sur la pile avant l'appel, la dernière va dans X (ex. : 12 18)"));
            tip.SetToolTip(inputsBox, Lang.T("Réponses dans l'ordre : INPUT, prompt(), VARMENU (une par variable), menu() (n° de touche, 0 = EXIT), GETKEY"));

            var head = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            head.Controls.Add(title);
            head.Controls.Add(L("Lancer"));
            grid.Controls.Add(head, 0, 0);
            grid.Controls.Add(entryBox, 1, 0);
            grid.Controls.Add(L("Pile"), 2, 0);
            grid.Controls.Add(stackBox, 3, 0);
            grid.Controls.Add(L("INPUT"), 4, 0);
            grid.Controls.Add(inputsBox, 5, 0);
            grid.Controls.Add(run, 6, 0);

            // résultat : écran C47 à gauche (masqué pour la DM42), texte à droite
            output = new TextBox
            {
                Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                BackColor = Lcd, ForeColor = LcdInk, BorderStyle = BorderStyle.None, Font = new Font("Consolas", 10f), Tag = "keep"
            };
            lcdBox = new PictureBox { Dock = DockStyle.Left, SizeMode = PictureBoxSizeMode.Zoom, Visible = false, Cursor = Cursors.Hand };
            tip.SetToolTip(lcdBox, Lang.T("Écran de la C47 à la fin du programme : cliquez pour l'agrandir"));
            lcdBox.Click += (s, e) => ZoomLcd();
            var result = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 0) };
            result.Resize += (s, e) => lcdBox.Width = Math.Min(result.ClientSize.Height * 400 / 240, result.ClientSize.Width / 2);
            result.Controls.Add(output);
            result.Controls.Add(lcdBox);
            grid.Controls.Add(result, 0, 1);
            grid.SetColumnSpan(result, 7);
            Controls.Add(grid);
        }

        /// <summary>Labels globaux de l'onglet, titre DM42 / C47 ; le label choisi est gardé s'il existe encore.</summary>
        public void Refresh(bool keepOutput = true)
        {
            bool c47 = isC47();
            title.Text = Lang.T(c47 ? "Simulateur C47" : "Simulateur DM42");
            string text = source() ?? "";
            var labels = c47
                ? Regex.Matches(text, @"^\s*(?:\d+\s+)?LBL\s+(?:‘([^’]+)’|'([^']+)'|""([^""]+)"")", RegexOptions.Multiline)
                       .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value).ToList()
                : Py42.Core.RpnListing.Parse(text).Entries;
            labels = labels.Distinct().ToList();
            string keep = entryBox.SelectedItem as string;
            if (!labels.SequenceEqual(entryBox.Items.Cast<string>()))
            {
                entryBox.Items.Clear();
                entryBox.Items.AddRange(labels.Cast<object>().ToArray());
            }
            if (entryBox.Items.Count > 0)
                entryBox.SelectedItem = keep != null && entryBox.Items.Contains(keep) ? keep : entryBox.Items[0];
            if (!keepOutput) { output.Text = ""; ShowLcd(null); }
        }

        static double[] Nums(string s) => s
            .Split(new[] { ' ', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(v => double.Parse(v.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture))
            .ToArray();

        public void Run()
        {
            Refresh();
            if (!(entryBox.SelectedItem is string entry)) { output.Text = Lang.T("Aucun label global à lancer (LBL \"NOM\")."); return; }
            double[] stack, inputs;
            try { stack = Nums(stackBox.Text); inputs = Nums(inputsBox.Text); }
            catch (FormatException) { output.Text = Lang.T("Valeur invalide : séparez les nombres par des espaces ou des points-virgules."); return; }
            ShowLcd(null);
            if (isC47()) RunC47(entry, stack, inputs);
            else RunDm42(entry, stack, inputs);
        }

        void RunDm42(string entry, double[] stack, double[] inputs)
        {
            var listing = Py42.Core.RpnListing.Parse(source() ?? "");
            var r = Py42.Core.Simulator.Run(listing.Code, entry, stack, inputs);
            var sb = new StringBuilder();
            sb.AppendLine(Lang.T("Simulateur DM42 (calcul en double précision, 16 chiffres)"));
            foreach (var w in listing.Warnings) sb.AppendLine("⚠ " + w);
            sb.AppendLine();
            foreach (var d in r.Display) sb.AppendLine(d);
            if (r.Error != null)
            {
                int line = r.ErrorPc >= 0 && r.ErrorPc < listing.Code.Count ? listing.Code[r.ErrorPc].Line : 0;
                sb.AppendLine().AppendLine(Lang.F("ERREUR ligne {0} : {1}", line, r.Error));
                if (line > 0) gotoLine(line);
            }
            sb.AppendLine();
            sb.AppendLine($"T: {Py42.Core.Simulator.Fmt(r.T)}");
            sb.AppendLine($"Z: {Py42.Core.Simulator.Fmt(r.Z)}");
            sb.AppendLine($"Y: {Py42.Core.Simulator.Fmt(r.Y)}");
            sb.AppendLine($"X: {Py42.Core.Simulator.Fmt(r.X)}");
            sb.AppendLine(Lang.F("{0} pas exécutés", r.Steps));
            output.Text = sb.ToString().Replace("\n", Environment.NewLine).Replace("\r\r", "\r");
        }

        void RunC47(string entry, double[] stack, double[] inputs)
        {
            string text = source() ?? "";
            var own = C47.Core.Encoder.Encode(text);
            if (!own.Ok)
            {
                var e = own.Errors[0];
                output.Text = Lang.F("ERREUR ligne {0} : {1}", e.Line, e.Message);
                if (e.Line > 0) gotoLine(e.Line);
                return;
            }
            var sim = C47.Core.Encoder.Encode(C47.Core.SimCopy.Build(text, entry));
            if (!sim.Ok) sim = own;   // copie instrumentée refusée : le programme tel quel, sans journal des messages
            output.Text = Lang.T("Simulateur C47 en cours…");
            output.Refresh();
            Emulator.C47Run r;
            Cursor = Cursors.WaitCursor;
            bool logged = sim != own;
            try
            {
                r = Emulator.RunC47(FindForm(), C47.Core.P47File.Write(sim.Program), logged ? C47.Core.SimCopy.Entry : entry, stack, inputs,
                                    logged ? C47.Core.SimCopy.LogVar : null);
            }
            finally { Cursor = Cursors.Default; }
            ShowLcd(r.Screen);
            var sb = new StringBuilder();
            sb.AppendLine(Lang.T("Simulateur C47 (calcul sur 34 chiffres)"));
            var messages = r.Log.Split(C47.Core.SimCopy.LogSep, StringSplitOptions.RemoveEmptyEntries);
            if (messages.Length > 0) { sb.AppendLine(); foreach (var m in messages) sb.AppendLine(Readable(m)); }
            else if (r.Alpha.Length > 0) sb.AppendLine().AppendLine(Lang.T("Dernier message : ") + Readable(r.Alpha));
            if (r.Error.Length > 0) sb.AppendLine().AppendLine(r.Error);
            sb.AppendLine();
            if (r.Stack8) { sb.AppendLine($"D: {r.D}"); sb.AppendLine($"C: {r.C}"); sb.AppendLine($"B: {r.B}"); sb.AppendLine($"A: {r.A}"); }
            sb.AppendLine($"T: {r.T}");
            sb.AppendLine($"Z: {r.Z}");
            sb.AppendLine($"Y: {r.Y}");
            sb.AppendLine($"X: {r.X}");
            output.Text = sb.ToString().Replace("\n", Environment.NewLine).Replace("\r\r", "\r");
        }

        /// <summary>Texte rendu par le simulateur, en codes de la police C47 : exposants lisibles (×⑽ⅫⅣ → ×10⁻³).</summary>
        static string Readable(string s) => C47.Core.C47Text.Display(s).Replace("⑽", "10");

        void ShowLcd(Image screen)
        {
            var old = lcdBox.Image;
            lcdBox.Image = screen;
            lcdBox.Visible = screen != null;
            old?.Dispose();
        }

        void ZoomLcd()
        {
            if (lcdBox.Image == null) return;
            var pic = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, Image = new Bitmap(lcdBox.Image) };
            var f = new Form
            {
                Text = Lang.T("Écran C47"), ClientSize = new Size(800, 480), StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.SizableToolWindow, KeyPreview = true, ShowInTaskbar = false
            };
            f.Controls.Add(pic);
            pic.Click += (s, e) => f.Close();
            f.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) f.Close(); };
            f.FormClosed += (s, e) => pic.Image.Dispose();
            f.Show(FindForm());
        }
    }
}
