using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Dm42.Editor;
using Py = Py42.Core;
using Range = Dm42.Editor.TextRange;

namespace dm42editor
{
    /// <summary>
    /// Éditeur Python → RPN : éditeur coloré, listing RPN synchronisé, messages et simulateur.
    /// Fenêtre construite en code (pas de fichier Designer) pour rester indépendante de Form1.
    /// </summary>
    public partial class FrmPython : Form
    {
        // ------------------------------------------------------------------
        //  Palette : boîtier HP-42S (anthracite, touche shift orange), écran LCD gris-vert
        // ------------------------------------------------------------------
        static readonly Color Body = Color.FromArgb(38, 38, 36);
        static readonly Color Bar = Color.FromArgb(28, 28, 26);
        static readonly Color Ink = Color.FromArgb(236, 234, 228);
        static readonly Color Muted = Color.FromArgb(156, 154, 146);
        static readonly Color Shift = Color.FromArgb(224, 137, 47);
        static readonly Color Paper = Color.FromArgb(251, 250, 246);
        static readonly Color Lcd = Color.FromArgb(198, 205, 176);
        static readonly Color LcdInk = Color.FromArgb(30, 36, 24);

        // Styles Python
        readonly TextStyle pyComment = new TextStyle(new SolidBrush(Color.FromArgb(138, 134, 118)), null, FontStyle.Italic);
        // commentaire multiligne """ … """ : même couleur, enregistré avant les autres styles pour l'emporter sur eux
        readonly TextStyle pyDoc = new TextStyle(new SolidBrush(Color.FromArgb(138, 134, 118)), null, FontStyle.Italic);
        static readonly Regex DocRegex = new Regex(@"(""""""|''')[\s\S]*?(\1|\z)", RegexOptions.Compiled);
        readonly TextStyle pyString = new TextStyle(new SolidBrush(Color.FromArgb(61, 122, 42)), null, FontStyle.Regular);
        readonly TextStyle pyKeyword = new TextStyle(new SolidBrush(Color.FromArgb(184, 86, 27)), null, FontStyle.Bold);
        readonly TextStyle pyBuiltin = new TextStyle(new SolidBrush(Color.FromArgb(45, 94, 158)), null, FontStyle.Regular);
        readonly TextStyle pyHp = new TextStyle(new SolidBrush(Color.FromArgb(120, 40, 140)), null, FontStyle.Bold);
        readonly TextStyle pyNumber = new TextStyle(new SolidBrush(Color.FromArgb(138, 59, 143)), null, FontStyle.Regular);
        readonly TextStyle pyDef = new TextStyle(Brushes.Black, null, FontStyle.Bold);

        // Styles RPN
        readonly TextStyle rpLineNo = new TextStyle(new SolidBrush(Color.FromArgb(115, 123, 98)), null, FontStyle.Regular);
        readonly TextStyle rpLabel = new TextStyle(new SolidBrush(Color.FromArgb(140, 47, 12)), null, FontStyle.Bold);
        readonly TextStyle rpFlow = new TextStyle(new SolidBrush(Color.FromArgb(154, 82, 0)), null, FontStyle.Bold);
        readonly TextStyle rpTest = new TextStyle(new SolidBrush(Color.FromArgb(31, 76, 120)), null, FontStyle.Bold);
        readonly TextStyle rpAlpha = new TextStyle(new SolidBrush(Color.FromArgb(85, 107, 20)), null, FontStyle.Regular);

        // Marques de lignes
        readonly TextStyle errorMark = new TextStyle(null, new SolidBrush(Color.FromArgb(90, 214, 64, 40)), FontStyle.Regular);
        readonly TextStyle syncMark = new TextStyle(null, new SolidBrush(Color.FromArgb(110, 224, 137, 47)), FontStyle.Regular);

        readonly Action<string, string, string> sendToEditor;

        CodeEditor pyBox, rpnBox;
        ListBox messages;
        ToolStripTextBox progName;
        ToolStripButton radButton, upperButton, numButton;
        ToolStripStatusLabel status;
        ComboBox entryBox;
        TextBox stackBox, inputsBox, simOut;
        SplitContainer mainSplit, leftSplit, rightSplit;

        Py.CompileResult last;
        string currentFile;
        string preferredEntry = "MAIN";
        bool syncing;
        const int HeaderLines = 1; // ligne « 00 { Prgm } » en tête du listing

        sealed class Msg
        {
            public string Text;
            public int Line;
            public override string ToString() => Text;
        }

        public FrmPython(Action<string, string, string> sendToEditor)
        {
            this.sendToEditor = sendToEditor;
            BuildUi();
            NewProgram();
        }

        // ==================================================================
        //  Construction de l'interface
        // ==================================================================
        void BuildUi()
        {
            Text = "Python → RPN HP-42S / DM42";
            Size = new Size(1320, 860);
            MinimumSize = new Size(900, 600);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Body;
            ForeColor = Ink;
            KeyPreview = true;
            KeyDown += OnKeyDown;
            Icon = Owner?.Icon;

            // ---- barre d'outils
            var tools = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, BackColor = Bar, ForeColor = Ink, Padding = new Padding(6, 4, 6, 4) };
            tools.Renderer = new HpRenderer();   // survol et menus sombres : le texte blanc reste lisible
            tools.Items.Add(BuildFileMenu(tools));
            tools.Items.Add(BuildViewMenu(tools));
            tools.Items.Add(new ToolStripSeparator());
            tools.Items.Add(Button("Effacer", (s, e) => ClearPage(), "Efface toute la page Python (Ctrl+Z pour annuler)"));
            tools.Items.Add(new ToolStripSeparator());

            tools.Items.Add(new ToolStripLabel("Programme") { ForeColor = Muted });
            progName = new ToolStripTextBox { Text = "MAIN", MaxLength = 7, Width = 70, ToolTipText = "Label global du programme principal (7 caractères max)" };
            progName.TextChanged += (s, e) => Recompile();
            tools.Items.Add(progName);

            radButton = Toggle("RAD auto", true, "Ajoute RAD au début si le programme utilise la trigonométrie (Python calcule en radians)");
            upperButton = Toggle("MAJ", true, "Noms de variables et de labels en majuscules");
            numButton = Toggle("N° ligne", true, "Numéros de ligne dans le listing");
            tools.Items.AddRange(new ToolStripItem[] { radButton, upperButton, numButton, new ToolStripSeparator() });

            var send = new ToolStripButton("→ Éditeur  F6") { BackColor = Shift, ForeColor = Color.FromArgb(28, 27, 25), Font = new Font(Font, FontStyle.Bold), Tag = HpRenderer.ShiftKey, ToolTipText = "Ouvre le RPN dans un onglet de DM42 Editor, prêt pour « Encode and save RAW »" };
            send.Click += (s, e) => SendToEditor();
            tools.Items.Add(send);
            var rawBtn = new ToolStripButton("Générer .raw  F8") { BackColor = Shift, ForeColor = Color.FromArgb(28, 27, 25), Font = new Font(Font, FontStyle.Bold), Tag = HpRenderer.ShiftKey, ToolTipText = "Encode directement le programme en fichier .raw pour la DM42 / Free42" };
            rawBtn.Click += (s, e) => SaveRaw();
            tools.Items.Add(rawBtn);
            tools.Items.Add(Button("Free42  F7", (s, e) => SendToFree42(), "Copie le listing et affiche Free42 / Plus42 (le lance si besoin) : mode PRGM puis Ctrl+V"));
            tools.Items.Add(new ToolStripSeparator());
            tools.Items.Add(Button("Aide", (s, e) => ShowHelp(this), "Python accepté et commandes HP42S"));

            // ---- barre d'état
            var statusStrip = new StatusStrip { BackColor = Bar, SizingGrip = true };
            status = new ToolStripStatusLabel { ForeColor = Muted, Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            statusStrip.Items.Add(status);

            // ---- éditeur Python
            pyBox = NewEditor(Paper, Color.FromArgb(31, 30, 27), false);
            pyBox.ShowLineNumbers = true;
            pyBox.LineNumberColor = Color.FromArgb(163, 159, 148);
            pyBox.IndentBackColor = Color.FromArgb(240, 238, 230);
            pyBox.TabLength = 4;
            pyBox.AutoIndent = false;      // l'auto-indentation de FCTB réindente les lignes existantes : géré à la main
            pyBox.CurrentLineColor = Color.FromArgb(60, 210, 210, 255);
            pyBox.DelayedTextChangedInterval = 400;
            pyBox.DelayedEventsInterval = 150;
            pyBox.TextChanged += (s, e) => HighlightPython(e.ChangedRange);
            pyBox.TextChangedDelayed += (s, e) => Recompile();
            pyBox.SelectionChangedDelayed += (s, e) => SyncFromPython();
            pyBox.KeyPressed += PythonNewLine;
            BuildAutocomplete();

            // ---- listing RPN
            rpnBox = NewEditor(Lcd, LcdInk, true);
            rpnBox.ShowLineNumbers = false;
            rpnBox.IndentBackColor = Lcd;
            rpnBox.LeftPadding = 8;
            rpnBox.DelayedEventsInterval = 150;
            rpnBox.SelectionChangedDelayed += (s, e) => SyncFromRpn();

            // ---- messages
            messages = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(31, 31, 29),
                ForeColor = Ink,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9.5f),
                IntegralHeight = false,
                HorizontalScrollbar = true
            };
            messages.SelectedIndexChanged += (s, e) => GotoMessage();

            // ---- simulation
            var sim = BuildSimulationPanel();

            // ---- assemblage
            leftSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, BackColor = Body, SplitterWidth = 6 };
            leftSplit.Panel1.Controls.Add(Framed("PYTHON", pyBox));
            leftSplit.Panel2.Controls.Add(Framed("MESSAGES", messages));

            rightSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, BackColor = Body, SplitterWidth = 6 };
            rightSplit.Panel1.Controls.Add(Framed("RPN   HP-42S · DM42 · Free42", rpnBox));
            rightSplit.Panel2.Controls.Add(Framed("SIMULATION", sim));

            mainSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, BackColor = Body, SplitterWidth = 6, Padding = new Padding(8) };
            mainSplit.Panel1.Controls.Add(leftSplit);
            mainSplit.Panel2.Controls.Add(rightSplit);

            Controls.Add(mainSplit);
            Controls.Add(tools);
            Controls.Add(statusStrip);

            InitExtras();

            Load += (s, e) =>
            {
                try
                {
                    mainSplit.SplitterDistance = (int)(mainSplit.Width * 0.56);
                    leftSplit.SplitterDistance = (int)(leftSplit.Height * 0.74);
                    rightSplit.SplitterDistance = (int)(rightSplit.Height * 0.60);
                }
                catch (InvalidOperationException) { /* fenêtre trop petite : on garde les valeurs par défaut */ }
            };
        }

        ToolStripButton Button(string text, EventHandler click, string tip)
        {
            var b = new ToolStripButton(text) { ForeColor = Ink, ToolTipText = tip };
            b.Click += click;
            return b;
        }

        ToolStripButton Toggle(string text, bool on, string tip)
        {
            var b = new ToolStripButton(text) { CheckOnClick = true, Checked = on, ForeColor = Ink, ToolTipText = tip };
            b.CheckedChanged += (s, e) => Recompile();
            return b;
        }

        static CodeEditor NewEditor(Color back, Color fore, bool readOnly)
        {
            return new CodeEditor
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 11.5f),
                BackColor = back,
                ForeColor = fore,
                ReadOnly = readOnly,
                BorderStyle = BorderStyle.None,
                CaretColor = Shift,
                SelectionColor = Color.FromArgb(70, 224, 137, 47)
            };
        }

        static Control Framed(string caption, Control content)
        {
            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 22, 0, 0), BackColor = Body };
            var label = new Label
            {
                Text = caption,
                ForeColor = Muted,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                AutoSize = false,
                Height = 20,
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.BottomLeft
            };
            var border = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1), BackColor = Color.FromArgb(69, 68, 63) };
            border.Controls.Add(content);
            panel.Controls.Add(border);
            panel.Controls.Add(label);
            label.Location = new Point(0, 0);
            return panel;
        }

        Control BuildSimulationPanel()
        {
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 4, BackColor = Body, Padding = new Padding(4) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            Label L(string t) => new Label { Text = t, ForeColor = Muted, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 0) };

            entryBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            var run = new Button
            {
                Text = "R/S  (F5)",
                BackColor = Shift,
                ForeColor = Color.FromArgb(28, 27, 25),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(6, 2, 0, 2)
            };
            run.FlatAppearance.BorderColor = Color.FromArgb(181, 102, 28);
            run.Click += (s, e) => RunSimulation();

            stackBox = new TextBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(51, 50, 47), ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 10f) };
            inputsBox = new TextBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(51, 50, 47), ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 10f) };
            var tip = new ToolTip();
            tip.SetToolTip(stackBox, "Valeurs mises sur la pile avant l'appel, la dernière va dans X (ex. : 12 18)");
            tip.SetToolTip(inputsBox, "Réponses dans l'ordre : INPUT, prompt(), VARMENU (une par variable), menu() (n° de touche, 0 = EXIT), GETKEY");

            simOut = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Lcd,
                ForeColor = LcdInk,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 10.5f),
                Margin = new Padding(0, 6, 0, 0)
            };

            grid.Controls.Add(L("Lancer"), 0, 0);
            grid.Controls.Add(entryBox, 1, 0);
            grid.Controls.Add(run, 2, 0);
            grid.Controls.Add(L("Pile"), 0, 1);
            grid.Controls.Add(stackBox, 1, 1);
            grid.SetColumnSpan(stackBox, 2);
            grid.Controls.Add(L("INPUT"), 0, 2);
            grid.Controls.Add(inputsBox, 1, 2);
            grid.SetColumnSpan(inputsBox, 2);
            grid.Controls.Add(simOut, 0, 3);
            grid.SetColumnSpan(simOut, 3);
            return grid;
        }

        void BuildAutocomplete()
        {
            var menu = new AutocompleteMenu(pyBox) { MinFragmentLength = 2, SearchPattern = @"[\w\.]", AppearInterval = 300 };
            var items = new List<AutocompleteItem>();
            foreach (var kw in new[] { "def", "return", "if", "elif", "else", "while", "for", "range", "break", "continue",
                                       "pass", "and", "or", "not", "True", "False", "import math", "import cmath", "import numpy as np", "global", "assert" })
                items.Add(new AutocompleteItem(kw));
            foreach (var f in new[] { "print", "input", "prompt", "menu", "varmenu", "int", "float", "abs", "max", "min", "pow",
                                      "math.sqrt", "math.sin", "math.cos", "math.tan", "math.asin", "math.acos", "math.atan",
                                      "math.atan2", "math.exp", "math.log", "math.log10", "math.hypot", "math.pi", "math.e",
                                      "math.radians", "math.degrees", "math.factorial", "math.gamma",
                                      "np.array", "np.zeros", "np.linalg.solve", "np.linalg.inv", "np.linalg.det", "np.linalg.norm",
                                      "np.dot", "np.cross", "np.transpose", "zeros", "solve", "inv", "det", "transpose", "norm",
                                      "rows", "cols", "len", "dot", "cross", "copy",
                                      "complex", "cmath.sqrt", "cmath.exp", "cmath.log", "cmath.phase", "cmath.rect", "cmath.pi",
                                      "conjugate", "real", "imag" })
                items.Add(new AutocompleteItem(f));
            foreach (var h in Py.Hp42.AllHelp().OrderBy(k => k.Name, StringComparer.OrdinalIgnoreCase))
                items.Add(new AutocompleteItem(h.Name) { ToolTipTitle = h.Name, ToolTipText = h.Help });
            menu.SetItems(items);
        }

        // ==================================================================
        //  Coloration
        // ==================================================================
        static readonly string HpRegex =
            @"\b(" + string.Join("|", Py.Hp42.AllHelp().Select(h => h.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(n => n.Length).Select(Regex.Escape)) + @")\b(?=\s*\()";

        void HighlightPython(Range r)
        {
            // un bloc """ peut couvrir des lignes non modifiées : il est recalculé sur tout le texte
            var all = pyBox.Range;
            all.ClearStyle(pyDoc);
            all.SetStyle(pyDoc, DocRegex);
            r.ClearStyle(pyComment, pyString, pyKeyword, pyBuiltin, pyHp, pyNumber, pyDef);
            r.SetStyle(pyComment, @"#.*$", RegexOptions.Multiline);
            r.SetStyle(pyString, @"""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'");
            r.SetStyle(pyKeyword, @"\b(def|return|if|elif|else|while|for|in|break|continue|pass|and|or|not|True|False|import|from|global|assert)\b");
            r.SetStyle(pyDef, @"(?<=\bdef\s+)\w+");
            r.SetStyle(pyHp, HpRegex, RegexOptions.IgnoreCase);
            r.SetStyle(pyBuiltin, @"\b(print|input|prompt|menu|varmenu|range|int|float|abs|max|min|pow|math|sqrt|sin|cos|tan|asin|acos|atan|atan2|sinh|cosh|tanh|asinh|acosh|atanh|exp|expm1|log|log1p|log10|hypot|pi|e|factorial|gamma|radians|degrees|fabs|trunc|cmath|np|numpy|linalg|array|zeros|solve|inv|det|transpose|norm|rows|cols|len|dot|cross|copy|complex|conjugate|conj|phase|rect|real|imag)\b");
            r.SetStyle(pyNumber, @"\b\d+(\.\d*)?([eE][+-]?\d+)?j\b");
            r.SetStyle(pyNumber, @"\b\d+(\.\d*)?([eE][+-]?\d+)?\b|\.\d+");
        }

        void HighlightRpn()
        {
            var r = rpnBox.Range;
            r.ClearStyle(rpLineNo, rpLabel, rpFlow, rpTest, rpAlpha);
            r.SetStyle(rpLineNo, @"^\d{2,3}(?=\s)", RegexOptions.Multiline);
            r.SetStyle(rpLabel, @"\bLBL\s+(""[^""]*""|\d+)");
            r.SetStyle(rpFlow, @"\b(GTO|XEQ)\s+(""[^""]*""|\d+)|\b(RTN|END|STOP|KEY \d GTO \d+)\b");
            r.SetStyle(rpAlpha, @"⊢?""[^""]*""");
            r.SetStyle(rpTest, @"\b[XF]\S*\?C?\b(\s\d\d)?");
        }

        /// <summary>Entrée : reprend l'indentation, +4 après « : », -4 après return / break / continue / pass.</summary>
        void PythonNewLine(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar != '\r' && e.KeyChar != '\n') return;
            int line = pyBox.Selection.Start.iLine;
            if (line < 1) return;
            string prev = pyBox.GetLineText(line - 1);
            int indent = prev.Length - prev.TrimStart(' ').Length;
            string code = Regex.Replace(prev, @"#.*$", "").TrimEnd();
            if (code.EndsWith(":")) indent += 4;
            else if (Regex.IsMatch(code, @"^\s*(return|break|continue|pass)\b")) indent = Math.Max(0, indent - 4);
            string current = pyBox.GetLineText(line);
            int already = current.Length - current.TrimStart(' ').Length;
            if (indent > already) pyBox.InsertText(new string(' ', indent - already));
        }

        // ==================================================================
        //  Conversion
        // ==================================================================
        void Recompile()
        {
            if (pyBox == null) return;
            var opts = new Py.CompileOptions
            {
                ProgramName = progName.Text,
                Radians = radButton.Checked,
                UpperCase = upperButton.Checked
            };
            messages.BeginUpdate();
            messages.Items.Clear();
            pyBox.Range.ClearStyle(errorMark);
            try
            {
                last = Py.Compiler.Compile(pyBox.Text, opts);
                rpnBox.Text = last.Listing(numButton.Checked, header: true);
                rpnBox.ForeColor = RpnFore;
                HighlightRpn();
                foreach (var w in last.Warnings) messages.Items.Add(new Msg { Text = "⚠  " + w, Line = LineIn(w) });
                if (last.Warnings.Count == 0) messages.Items.Add(new Msg { Text = "✓  Conversion réussie" });
                status.Text = $"{last.Code.Count} pas   ·   {last.LocalLabels}/99 labels locaux   ·   pile max {last.MaxStack}/4   ·   {last.Variables.Count} variable(s) : {string.Join(" ", last.Variables)}";

                string keep = entryBox.SelectedItem as string ?? preferredEntry;
                entryBox.Items.Clear();
                foreach (var e in last.Entries) entryBox.Items.Add(e);
                if (last.Entries.Contains(keep)) entryBox.SelectedItem = keep;
                else if (entryBox.Items.Count > 0) entryBox.SelectedIndex = 0;
            }
            catch (Py.CompileError ex)
            {
                last = null;
                messages.Items.Add(new Msg { Text = ex.Line > 0 ? $"✖  Ligne {ex.Line} : {ex.Message}" : "✖  " + ex.Message, Line = ex.Line });
                if (ex.Line > 0 && ex.Line <= pyBox.LinesCount) new Range(pyBox, ex.Line - 1).SetStyle(errorMark);
                rpnBox.ForeColor = Color.FromArgb(130, RpnFore);
                status.Text = "Erreur : le listing grisé est celui de la dernière conversion réussie.";
            }
            messages.EndUpdate();
            pyBox.Invalidate();
            SyncFromPython();
        }

        static int LineIn(string text)
        {
            var m = Regex.Match(text, @"Ligne (\d+)");
            return m.Success ? int.Parse(m.Groups[1].Value) : 0;
        }

        // ==================================================================
        //  Synchronisation Python ↔ RPN
        // ==================================================================
        void SyncFromPython()
        {
            if (syncing || last == null || rpnBox.Focused) return;
            syncing = true;
            try
            {
                pyBox.Range.ClearStyle(syncMark);
                rpnBox.Range.ClearStyle(syncMark);
                int src = pyBox.Selection.Start.iLine + 1;
                Range first = null;
                for (int i = 0; i < last.Code.Count; i++)
                {
                    if (last.Code[i].Line != src) continue;
                    int line = i + HeaderLines;
                    if (line >= rpnBox.LinesCount) break;
                    var r = new Range(rpnBox, line);
                    r.SetStyle(syncMark);
                    if (first == null) first = r;
                }
                if (first != null) rpnBox.DoRangeVisible(first);
                rpnBox.Invalidate();
                pyBox.Invalidate();
            }
            finally { syncing = false; }
        }

        void SyncFromRpn()
        {
            if (syncing || last == null || !rpnBox.Focused) return;
            syncing = true;
            try
            {
                pyBox.Range.ClearStyle(syncMark);
                rpnBox.Range.ClearStyle(syncMark);
                int i = rpnBox.Selection.Start.iLine - HeaderLines;
                if (i >= 0 && i < last.Code.Count && last.Code[i].Line > 0)
                {
                    int src = last.Code[i].Line;
                    if (src <= pyBox.LinesCount)
                    {
                        var r = new Range(pyBox, src - 1);
                        r.SetStyle(syncMark);
                        pyBox.DoRangeVisible(r);
                    }
                    for (int j = 0; j < last.Code.Count; j++)
                        if (last.Code[j].Line == src && j + HeaderLines < rpnBox.LinesCount)
                            new Range(rpnBox, j + HeaderLines).SetStyle(syncMark);
                }
                rpnBox.Invalidate();
                pyBox.Invalidate();
            }
            finally { syncing = false; }
        }

        void GotoMessage()
        {
            if (!(messages.SelectedItem is Msg m) || m.Line <= 0 || m.Line > pyBox.LinesCount) return;
            pyBox.Selection = new Range(pyBox, 0, m.Line - 1, 0, m.Line - 1);
            pyBox.DoSelectionVisible();
            pyBox.Focus();
        }

        // ==================================================================
        //  Simulation
        // ==================================================================
        static double[] Nums(string s) => s
            .Split(new[] { ' ', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(v => double.Parse(v.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture))
            .ToArray();

        void RunSimulation()
        {
            if (last == null) { simOut.Text = "Corrigez d'abord les erreurs signalées dans les messages."; return; }
            if (!(entryBox.SelectedItem is string entry)) { simOut.Text = "Aucun label à lancer."; return; }
            double[] stack, inputs;
            try { stack = Nums(stackBox.Text); inputs = Nums(inputsBox.Text); }
            catch (FormatException) { simOut.Text = "Valeur invalide : séparez les nombres par des espaces ou des points-virgules."; return; }

            var r = Py.Simulator.Run(last.Code, entry, stack, inputs);
            var sb = new StringBuilder();
            foreach (var d in r.Display) sb.AppendLine(d);
            if (r.Error != null)
            {
                sb.AppendLine().AppendLine($"ERREUR au pas {r.ErrorPc + 1} : {r.Error}");
                if (r.ErrorPc >= 0 && r.ErrorPc < last.Code.Count)
                {
                    syncing = true;
                    pyBox.Range.ClearStyle(syncMark);
                    rpnBox.Range.ClearStyle(syncMark);
                    var rr = new Range(rpnBox, r.ErrorPc + HeaderLines);
                    rr.SetStyle(errorMark);
                    rpnBox.DoRangeVisible(rr);
                    int src = last.Code[r.ErrorPc].Line;
                    if (src > 0 && src <= pyBox.LinesCount)
                    {
                        var pr = new Range(pyBox, src - 1);
                        pr.SetStyle(syncMark);
                        pyBox.DoRangeVisible(pr);
                    }
                    syncing = false;
                    rpnBox.Invalidate();
                    pyBox.Invalidate();
                }
            }
            if (sb.Length > 0) sb.AppendLine();
            sb.AppendLine($"T: {Py.Simulator.Fmt(r.T)}");
            sb.AppendLine($"Z: {Py.Simulator.Fmt(r.Z)}");
            sb.AppendLine($"Y: {Py.Simulator.Fmt(r.Y)}");
            sb.AppendLine($"X: {Py.Simulator.Fmt(r.X)}");
            sb.AppendLine();
            sb.Append($"{r.Steps} pas exécutés");
            if (r.MaxCallDepth > 0) sb.Append($", {r.MaxCallDepth} niveau(x) d'appel XEQ");
            if (r.MaxCallDepth > 8) sb.Append(" (attention : la HP-42S d'origine n'a que 8 niveaux de retour)");
            simOut.Text = sb.ToString().Replace("\n", Environment.NewLine).Replace("\r\r", "\r");
        }

        // ==================================================================
        //  Fichiers, exemples, envoi
        // ==================================================================
        public void NewProgram()
        {
            currentFile = null;
            pyBox.Text = "# Programme Python pour HP-42S / DM42\r\nimport math\r\n\r\ndef carre(x):\r\n    return x * x\r\n\r\nn = float(input(\"N ?\"))\r\nprint(\"CARRE\", carre(n))\r\n";
            pyBox.ClearUndo();
            pyBox.IsChanged = false;
            preferredEntry = "MAIN";
            stackBox.Text = "";
            inputsBox.Text = "5";
            simOut.Text = "";
            UpdateTitle();
            Recompile();
        }

        public void LoadExample(int index)
        {
            var ex = Py.Examples.All[index];
            currentFile = null;
            preferredEntry = ex.Entry;
            entryBox.Items.Clear();
            pyBox.Text = ex.Source.TrimStart('\n').Replace("\r\n", "\n").Replace("\n", "\r\n");
            pyBox.ClearUndo();
            pyBox.IsChanged = false;
            stackBox.Text = ex.Stack;
            inputsBox.Text = ex.Inputs;
            simOut.Text = "";
            UpdateTitle();
            Recompile();
            if (last != null && last.Entries.Contains(ex.Entry)) entryBox.SelectedItem = ex.Entry;
        }

        void OpenFileDialog()
        {
            if (!ConfirmDiscard()) return;
            using (var dlg = new OpenFileDialog { Filter = "Python (*.py)|*.py|Tous les fichiers (*.*)|*.*" })
                if (dlg.ShowDialog(this) == DialogResult.OK) OpenFile(dlg.FileName);
        }

        public void OpenFile(string path)
        {
            try
            {
                pyBox.Text = File.ReadAllText(path);
                pyBox.ClearUndo();
                pyBox.IsChanged = false;
                currentFile = path;
                UpdateTitle();
                Recompile();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Ouverture impossible", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void SavePython(bool saveAs)
        {
            if (currentFile == null || saveAs)
            {
                using (var dlg = new SaveFileDialog { Filter = "Python (*.py)|*.py", FileName = (progName.Text.Trim().Length > 0 ? progName.Text.Trim() : "programme") + ".py" })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    currentFile = dlg.FileName;
                }
            }
            try
            {
                File.WriteAllText(currentFile, pyBox.Text, new UTF8Encoding(false));
                pyBox.IsChanged = false;
                status.Text = "Enregistré : " + currentFile;
                UpdateTitle();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Enregistrement impossible", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void SendToEditor()
        {
            if (last == null) { status.Text = "Rien à envoyer : corrigez d'abord les erreurs."; return; }
            string baseName = currentFile != null ? Path.GetFileNameWithoutExtension(currentFile)
                            : (progName.Text.Trim().Length > 0 ? progName.Text.Trim() : "MAIN");
            string path = currentFile != null ? Path.ChangeExtension(currentFile, ".free42") : null;
            sendToEditor?.Invoke(last.Listing(numButton.Checked, header: true), baseName + ".free42", path);
            status.Text = $"RPN envoyé dans l'onglet « {baseName}.free42 ». Utilisez « Encode and save RAW » pour la DM42.";
        }

        void CopyRpn()
        {
            if (last == null) { status.Text = "Rien à copier : corrigez d'abord les erreurs."; return; }
            try
            {
                Clipboard.SetText(last.Listing(numButton.Checked, header: true));
                status.Text = "Listing copié. Dans Free42 : Edit → Paste Program.";
            }
            catch (Exception ex) { status.Text = "Presse-papiers indisponible : " + ex.Message; }
        }

        void UpdateTitle() =>
            Text = currentFile == null ? "Python → RPN HP-42S / DM42" : "Python → RPN — " + Path.GetFileName(currentFile);

        void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5) { RunSimulation(); e.Handled = true; }
            else if (e.KeyCode == Keys.F6) { SendToEditor(); e.Handled = true; }
            else if (e.KeyCode == Keys.F7) { SendToFree42(); e.Handled = true; }
            else if (e.KeyCode == Keys.F8) { SaveRaw(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.K && pyBox.Focused) { ToggleComment(); e.Handled = true; }
            else if (e.Control && e.Shift && e.KeyCode == Keys.S) { SavePython(true); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.S) { SavePython(false); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.O) { OpenFileDialog(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.N) { if (ConfirmDiscard()) NewProgram(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.P) { pyBox.Print(PrintTitle()); e.Handled = true; }
            if (e.Handled) e.SuppressKeyPress = true;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !ConfirmDiscard()) { e.Cancel = true; return; }
            base.OnFormClosing(e);
        }

        // ==================================================================
        //  Couleurs de la barre d'outils et des menus
        // ==================================================================

        /// <summary>
        /// Rendu sombre : le rendu Windows standard surligne en bleu ciel, sur lequel le texte blanc
        /// de la barre d'outils devient illisible.
        /// </summary>
        sealed class HpRenderer : ToolStripProfessionalRenderer
        {
            public const string ShiftKey = "shift";

            public HpRenderer() : base(new HpColors()) { RoundedEdges = false; }

            protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
            {
                if (e.Item.Tag as string == ShiftKey)
                {
                    // bouton orange « Envoyer » : reste orange, plus clair au survol
                    var c = e.Item.Pressed ? Color.FromArgb(196, 112, 34) : e.Item.Selected ? Color.FromArgb(238, 156, 71) : Shift;
                    using (var b = new SolidBrush(c)) e.Graphics.FillRectangle(b, new Rectangle(Point.Empty, e.Item.Size));
                    return;
                }
                base.OnRenderButtonBackground(e);
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                if (e.Item.Tag as string != ShiftKey && !(e.Item is ToolStripTextBox))
                    e.TextColor = e.Item.Enabled ? Ink : Muted;
                base.OnRenderItemText(e);
            }

            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
            {
                e.ArrowColor = Ink;
                base.OnRenderArrow(e);
            }
        }

        sealed class HpColors : ProfessionalColorTable
        {
            static readonly Color Hover = Color.FromArgb(74, 73, 69);
            static readonly Color Press = Color.FromArgb(96, 94, 88);
            static readonly Color Check = Color.FromArgb(102, 74, 42);
            static readonly Color Edge = Color.FromArgb(110, 108, 100);
            static readonly Color Menu = Color.FromArgb(44, 44, 41);
            static readonly Color MenuSel = Color.FromArgb(150, 86, 26);

            public override Color ToolStripGradientBegin => Bar;
            public override Color ToolStripGradientMiddle => Bar;
            public override Color ToolStripGradientEnd => Bar;
            public override Color ToolStripBorder => Bar;
            public override Color StatusStripGradientBegin => Bar;
            public override Color StatusStripGradientEnd => Bar;
            public override Color SeparatorDark => Edge;
            public override Color SeparatorLight => Bar;

            public override Color ButtonSelectedHighlight => Hover;
            public override Color ButtonSelectedGradientBegin => Hover;
            public override Color ButtonSelectedGradientMiddle => Hover;
            public override Color ButtonSelectedGradientEnd => Hover;
            public override Color ButtonSelectedBorder => Edge;
            public override Color ButtonSelectedHighlightBorder => Edge;
            public override Color ButtonPressedHighlight => Press;
            public override Color ButtonPressedGradientBegin => Press;
            public override Color ButtonPressedGradientMiddle => Press;
            public override Color ButtonPressedGradientEnd => Press;
            public override Color ButtonPressedBorder => Edge;
            public override Color ButtonPressedHighlightBorder => Edge;
            public override Color ButtonCheckedHighlight => Check;
            public override Color ButtonCheckedGradientBegin => Check;
            public override Color ButtonCheckedGradientMiddle => Check;
            public override Color ButtonCheckedGradientEnd => Check;
            public override Color ButtonCheckedHighlightBorder => Shift;
            public override Color CheckBackground => Check;
            public override Color CheckSelectedBackground => MenuSel;
            public override Color CheckPressedBackground => MenuSel;

            public override Color ToolStripDropDownBackground => Menu;
            public override Color ImageMarginGradientBegin => Menu;
            public override Color ImageMarginGradientMiddle => Menu;
            public override Color ImageMarginGradientEnd => Menu;
            public override Color MenuBorder => Edge;
            public override Color MenuItemBorder => MenuSel;
            public override Color MenuItemSelected => MenuSel;
            public override Color MenuItemSelectedGradientBegin => MenuSel;
            public override Color MenuItemSelectedGradientEnd => MenuSel;
            public override Color MenuItemPressedGradientBegin => Press;
            public override Color MenuItemPressedGradientMiddle => Press;
            public override Color MenuItemPressedGradientEnd => Press;
            public override Color OverflowButtonGradientBegin => Bar;
            public override Color OverflowButtonGradientMiddle => Bar;
            public override Color OverflowButtonGradientEnd => Bar;
        }

        // ==================================================================
        //  Aide
        // ==================================================================
        public static void ShowHelp(IWin32Window owner)
        {
            var sb = new StringBuilder();
            sb.AppendLine("PYTHON ACCEPTÉ");
            sb.AppendLine("  Nombres, variables, + - * / ** % //, parenthèses, comparaisons (chaînées), and / or / not");
            sb.AppendLine("  if / elif / else, while (+ else), for i in range(...) (+ else), break, continue, pass");
            sb.AppendLine("  def f(a, b, ...) : 4 paramètres max ; return x ou return a, b (puis a, b = f(...))");
            sb.AppendLine("  global, assert condition, \"message\"");
            sb.AppendLine("  # commentaire  ou bloc \"\"\" … \"\"\" (ou ''') en début de ligne : commentaires ignorés, absents du RPN");
            sb.AppendLine("  x = input(\"...\")  → INPUT \"X\"        x = prompt(\"PRIX ?\") ou PROMPT(\"PRIX ?\")  → texte + PROMPT");
            sb.AppendLine("  print(\"texte\", x)  → ALPHA + AVIEW    print(x) → VIEW \"X\"");
            sb.AppendLine("  import math : sqrt sin cos tan asin acos atan atan2 sinh cosh tanh exp log log10");
            sb.AppendLine("                hypot pow max min abs int factorial gamma radians degrees pi e");
            sb.AppendLine();
            sb.AppendLine("EXPRESSIONS");
            sb.AppendLine("  Parenthèses imbriquées sans limite : ce qui dépasse la pile à 4 niveaux est calculé");
            sb.AppendLine("  à l'avance dans des variables S1, S2... (avertissement affiché)");
            sb.AppendLine();
            sb.AppendLine("MATRICES (import numpy as np, facultatif)");
            sb.AppendLine("  A = [[1, 2], [3, 4]]  ou  np.array([[1, 2], [3, 4]])   NEWMAT + STOEL");
            sb.AppendLine("  v = [1, 2, 3]                 vecteur colonne 3×1");
            sb.AppendLine("  m = np.zeros((l, c))  /  zeros(l, c)        matrice nulle (NEWMAT)");
            sb.AppendLine("  A[i, j]  A[i][j]  v[i]        indices Python à partir de 0 (INDEX, STOIJ, RCLEL / STOEL)");
            sb.AppendLine("  A[0:2, 1:3]  A[1:, :2]        sous-matrice (GETM) ; A[0:2, 0:2] = B (PUTM)");
            sb.AppendLine("  A @ B  A + B  2 * A  A.T      produit (×), somme, scalaire, transposée (TRANS)");
            sb.AppendLine("  inv(A) det(A) solve(A, b) norm(A) dot(u, v) cross(u, v) rows(A) cols(A) len(v)");
            sb.AppendLine("  for x in v:                   parcourt les éléments ligne par ligne");
            sb.AppendLine("  Différence avec numpy : une tranche ou A.T est une COPIE (pas une vue).");
            sb.AppendLine("  Pas de produit élément par élément (A * B) : utilisez A @ B pour le produit matriciel.");
            sb.AppendLine();
            sb.AppendLine("COMPLEXES (import cmath pour sqrt(-1), log(-1)... complexes)");
            sb.AppendLine("  z = 3 + 4j   complex(a, b)   z.real  z.imag  abs(z)  z.conjugate()  cmath.phase(z)  cmath.rect(r, phi)");
            sb.AppendLine("  + - * / ** sqrt exp log sin cos tan sur des complexes ; RECT est ajouté au début du programme");
            sb.AppendLine();
            sb.AppendLine("MENUS DM42");
            sb.AppendLine("  varmenu(a, b, c)            MVAR + VARMENU : tapez une valeur, touche de la variable, puis R/S");
            sb.AppendLine("  choix = menu(\"ADD\", \"SUB\")  menu de touches (6 max) : renvoie 1 à 6, 0 pour EXIT");
            sb.AppendLine();
            sb.AppendLine("DRAPEAUX");
            sb.AppendLine("  SF(n) CF(n)                 lève / baisse le drapeau n");
            sb.AppendLine("  if FS(n): ...  if FC(n):    tests ; FSC(n) / FCC(n) testent puis baissent le drapeau");
            sb.AppendLine();
            sb.AppendLine("COMMANDES HP42S (noms de pyrpn.atug.com/cmds, majuscules ou minuscules)");
            sb.AppendLine("  Les arguments suivent l'ordre de la pile : le dernier va dans X.");
            sb.AppendLine("  SIN(x), COS(x)... utilisent le mode d'angle de la calculatrice (DEG(), RAD()) ;");
            sb.AppendLine("  math.sin(x) travaille toujours en radians.");
            sb.AppendLine("  Deux résultats : a, b = MEAN()  → a reçoit Y, b reçoit X (ordre de la pile).");
            sb.AppendLine("  Date JJ.MMAAAA : j, m, a = dmy(d)  → 12.052024 donne 12, 5, 2024.");
            sb.AppendLine("  Exemple : print(\"sine of\", myvar, \"is\", SIN(myvar))");
            foreach (var h in Py.Hp42.AllHelp().OrderBy(k => k.Name, StringComparer.OrdinalIgnoreCase))
                sb.AppendLine("  " + h.Help);
            sb.AppendLine();
            sb.AppendLine("LIMITES");
            sb.AppendLine("  Pile de 4 niveaux · variables globales (les locales en conflit sont renommées) · pas de récursivité");
            sb.AppendLine("  Noms de 7 caractères · 99 labels locaux · pas de listes, chaînes manipulées, classes, exceptions");
            sb.AppendLine();
            sb.AppendLine("RACCOURCIS : F5 simulation · F6 envoyer vers l'éditeur · F7 envoyer vers Free42 / Plus42 · Ctrl+K commenter · Ctrl+N nouveau · Ctrl+O ouvrir · Ctrl+S enregistrer · Ctrl+P imprimer · Ctrl+F rechercher · Ctrl+H remplacer");
            sb.AppendLine("  Menu Fichier : nouveau, ouvrir, enregistrer (sous), exemples, imprimer le code Python ou le listing RPN, quitter.");
            sb.AppendLine("  Clic droit dans le code : couper, copier, coller, effacer la sélection, effacer la page.");
            sb.AppendLine("  Menu Affichage : thème clair / sombre du Python, couleurs et fond du listing RPN.");
            sb.AppendLine("FICHIER .RAW : F8 (Générer .raw) encode directement le programme : DM42 → copiez-le sur son disque USB puis Load Program ; Free42 → File → Import Programs.");
            sb.AppendLine("FREE42 / PLUS42 : F7 copie le listing et affiche le simulateur ; en mode PRGM (⇧ R/S), Ctrl+V ajoute le programme.");

            var f = new Form
            {
                Text = "Aide Python → RPN",
                Size = new Size(860, 720),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Body,
                ShowInTaskbar = false
            };
            var box = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = new Font("Consolas", 10f),
                BackColor = Paper,
                ForeColor = Color.FromArgb(31, 30, 27),
                BorderStyle = BorderStyle.None,
                Text = sb.ToString()
            };
            f.Controls.Add(box);
            f.Shown += (s, e) => box.Select(0, 0);
            f.Show(owner);
        }
    }
}
