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

        /// <summary>
        /// Tests HP-42S (X&lt;Y?, X=0?, X≤Y?, FS? 01, FC?C 12, BIT?…) et leur argument : partagé avec l'éditeur RPN.
        /// </summary>
        internal const string RpnTestPattern =
            @"(?<!\S)(X(<>|<=|>=|!=|=|≠|<|>|≤|≥|#)(0|Y)\?|F[SC]\?C?|BIT\?|MAT\?|CPX\?|REAL\?|STR\?)(?!\S)([ \t]+(IND[ \t]+)?(ST[ \t]+[XYZTL]|\d+|""[^""]*""))?";   // argument sur la même ligne seulement
        static readonly Regex RpnTestRegex = new Regex(RpnTestPattern, RegexOptions.Compiled);
        readonly TextStyle rpAlpha = new TextStyle(new SolidBrush(Color.FromArgb(85, 107, 20)), null, FontStyle.Regular);

        // Marques de lignes
        readonly TextStyle errorMark = new TextStyle(null, new SolidBrush(Color.FromArgb(90, 214, 64, 40)), FontStyle.Regular);
        readonly TextStyle syncMark = new TextStyle(null, new SolidBrush(Color.FromArgb(110, 224, 137, 47)), FontStyle.Regular);

        readonly Action<string, string, string> sendToEditor;

        CodeEditor pyBox, rpnBox;
        ListBox messages;
        ToolStripTextBox progName;
        ToolStripButton radButton, upperButton, numButton, sendButton, rawButton;
        /// <summary>Cible du listing : HP-42S / DM42, ou C47 / R47 (conversion par C47.Core.Hp42Converter).</summary>
        ToolStripComboBox targetBox;
        C47.Core.Hp42Converter.Result lastC47;
        C47.Core.EncodeResult lastC47Code;
        bool TargetC47 => targetBox != null && targetBox.SelectedIndex == 1;
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
            Lang.Apply(this);
            UpdateTarget();   // libellés .raw / .p47 selon la cible de départ
            NewProgram();
        }

        // ==================================================================
        //  Construction de l'interface
        // ==================================================================
        void BuildUi()
        {
            Text = "Python → RPN DM42";
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

            tools.Items.Add(new ToolStripLabel("Cible") { ForeColor = Muted });
            targetBox = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110, ToolTipText = "Calculatrice visée : DM42 (HP-42S, Free42, Plus42), ou C47 / R47" };
            targetBox.Items.AddRange(new object[] { "DM42", "C47 / R47" });
            targetBox.SelectedIndex = Emulator.DefaultC47 ? 1 : 0;   // calculatrice par défaut (Paramètres)
            targetBox.SelectedIndexChanged += (s, e) => { UpdateTarget(); Recompile(); };
            tools.Items.Add(targetBox);
            tools.Items.Add(new ToolStripSeparator());

            var send = sendButton = new ToolStripButton("→ Éditeur  F6") { BackColor = Shift, ForeColor = Color.FromArgb(28, 27, 25), Font = new Font(Font, FontStyle.Bold), Tag = HpRenderer.ShiftKey, ToolTipText = "Ouvre le RPN dans un onglet de DM42 Editor, prêt pour « Encoder et enregistrer en .raw »" };
            send.Click += (s, e) => SendToEditor();
            tools.Items.Add(send);
            var rawBtn = rawButton = new ToolStripButton("Générer .raw  F8") { BackColor = Shift, ForeColor = Color.FromArgb(28, 27, 25), Font = new Font(Font, FontStyle.Bold), Tag = HpRenderer.ShiftKey, ToolTipText = "Encode directement le programme en fichier .raw pour la DM42 / Free42" };
            rawBtn.Click += (s, e) => SaveRaw();
            tools.Items.Add(rawBtn);
            tools.Items.Add(Button("Émulateur  F7", (s, e) => SendToFree42(), "Copie le listing et affiche l'émulateur réglé dans Paramètres (Free42, Plus42…), lancé si besoin : mode PRGM puis Ctrl+V"));
            tools.Items.Add(new ToolStripSeparator());
            tools.Items.Add(Button("Aide", (s, e) => ShowHelp(this, TargetC47), "Aide de la cible choisie (DM42 ou C47) : Python accepté et commandes"));

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
            rightSplit.Panel1.Controls.Add(Framed("RPN   DM42 · HP-42S · Free42", rpnBox));
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
            tip.SetToolTip(stackBox, Lang.T("Valeurs mises sur la pile avant l'appel, la dernière va dans X (ex. : 12 18)"));
            tip.SetToolTip(inputsBox, Lang.T("Réponses dans l'ordre : INPUT, prompt(), VARMENU (une par variable), menu() (n° de touche, 0 = EXIT), GETKEY"));

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
                                      "math.radians", "math.degrees", "math.factorial", "math.gamma", "math.floor", "math.ceil", "round",
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

        /// <summary>Chaîne "…" / '…' (fermée ou non, sur une seule ligne) ou commentaire # jusqu'en fin de ligne.</summary>
        static readonly Regex StringOrComment = new Regex(@"""(?:\\.|[^""\\\n])*""?|'(?:\\.|[^'\\\n])*'?|#[^\n]*", RegexOptions.Compiled);

        void HighlightPython(Range r)
        {
            // un bloc """ peut couvrir des lignes non modifiées : il est recalculé sur tout le texte
            var all = pyBox.Range;
            all.ClearStyle(pyDoc);
            all.SetStyle(pyDoc, DocRegex);
            r.ClearStyle(pyComment, pyString, pyKeyword, pyBuiltin, pyHp, pyNumber, pyDef);
            // chaînes et commentaires lus ensemble, de gauche à droite et sans passer à la ligne :
            // l'apostrophe de « # l'angle » ne commence pas une chaîne, le # de "N#1" n'est pas un commentaire
            var none = new Range(pyBox, 0, 0, 0, 0);
            none.SetStyle(pyComment);    // ordre des styles : commentaire et chaîne l'emportent sur les mots-clés
            none.SetStyle(pyString);
            foreach (var m in r.GetRanges(StringOrComment))
                m.SetStyle(m.Text.StartsWith("#") ? pyComment : pyString);
            r.SetStyle(pyKeyword, @"\b(def|return|if|elif|else|while|for|in|break|continue|pass|and|or|not|True|False|import|from|global|assert)\b");
            r.SetStyle(pyDef, @"(?<=\bdef\s+)\w+");
            r.SetStyle(pyHp, HpRegex, RegexOptions.IgnoreCase);
            r.SetStyle(pyBuiltin, @"\b(print|input|prompt|menu|varmenu|range|int|float|abs|max|min|pow|math|sqrt|sin|cos|tan|asin|acos|atan|atan2|sinh|cosh|tanh|asinh|acosh|atanh|exp|expm1|log|log1p|log10|hypot|pi|e|factorial|gamma|radians|degrees|fabs|trunc|floor|ceil|round|cmath|np|numpy|linalg|array|zeros|solve|inv|det|transpose|norm|rows|cols|len|dot|cross|copy|complex|conjugate|conj|phase|rect|real|imag)\b");
            r.SetStyle(pyNumber, @"\b\d+(\.\d*)?([eE][+-]?\d+)?j\b");
            r.SetStyle(pyNumber, @"\b\d+(\.\d*)?([eE][+-]?\d+)?\b|\.\d+");
        }

        void HighlightRpn()
        {
            var r = rpnBox.Range;
            r.ClearStyle(rpLineNo, rpLabel, rpFlow, rpTest, rpAlpha);
            r.SetStyle(rpLineNo, @"^\d{2,3}(?=\s)", RegexOptions.Multiline);
            // guillemets HP-42S "…" ou C47 ‘…’ (cible C47)
            r.SetStyle(rpLabel, @"\bLBL\s+(""[^""]*""|‘[^’]*’|\d+)");
            r.SetStyle(rpFlow, @"\b(GTO|XEQ)\s+(""[^""]*""|‘[^’]*’|\d+)|\b(RTN|END|STOP|(42)?KEY \d GTO \d+)\b|^\s*(IF|ELSE|ENDIF|DO|WHILE|ENDDO|FORᵀᴼᴾ|NEXT)\b.*$", RegexOptions.Multiline);
            r.SetStyle(rpAlpha, @"⊢?""[^""\n]*""|(42α|42⊢) ‘[^’\n]*’");
            r.SetStyle(rpTest, RpnTestRegex);
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
                UpperCase = upperButton.Checked,
                // C47 : structures IF / DO / FOR, registres locaux ; les noms HP-42S restants passent ensuite par Hp42Converter
                Target = TargetC47 ? Py.Target.C47 : Py.Target.Hp42
            };
            messages.BeginUpdate();
            messages.Items.Clear();
            pyBox.Range.ClearStyle(errorMark);
            try
            {
                last = Py.Compiler.Compile(pyBox.Text, opts);
                rpnBox.Text = last.Listing(numButton.Checked, header: true);
                if (TargetC47)
                {
                    // même listing, en source C47 : une ligne par ligne, la synchronisation avec le Python est gardée
                    lastC47 = C47.Core.Hp42Converter.Convert(rpnBox.Text);
                    lastC47Code = C47.Core.Encoder.Encode(lastC47.Source);
                    rpnBox.Text = lastC47.Source;
                }
                rpnBox.ForeColor = RpnFore;
                HighlightRpn();
                foreach (var w in last.Warnings) messages.Items.Add(new Msg { Text = "⚠  " + w, Line = LineIn(w) });
                if (TargetC47)
                {
                    foreach (var w in lastC47.Warnings) messages.Items.Add(new Msg { Text = "⚠  C47 : " + C47.Core.Tr.T(w) });
                    foreach (var er in lastC47Code.Errors.Take(10)) messages.Items.Add(new Msg { Text = "✗  C47 : " + Lang.F("Ligne {0} : {1}", er.Line, C47.Core.Tr.T(er.Message)) });
                }
                if (last.Warnings.Count == 0) messages.Items.Add(new Msg { Text = Lang.T("✓  Conversion réussie") });
                status.Text = Lang.F("{0} pas   ·   {1}/99 labels locaux   ·   pile max {2}/4   ·   {3} variable(s) : {4}", last.Code.Count, last.LocalLabels, last.MaxStack, last.Variables.Count, string.Join(" ", last.Variables));

                string keep = entryBox.SelectedItem as string ?? preferredEntry;
                entryBox.Items.Clear();
                foreach (var e in last.Entries) entryBox.Items.Add(e);
                if (last.Entries.Contains(keep)) entryBox.SelectedItem = keep;
                else if (entryBox.Items.Count > 0) entryBox.SelectedIndex = 0;
            }
            catch (Py.CompileError ex)
            {
                last = null;
                messages.Items.Add(new Msg { Text = ex.Line > 0 ? Lang.F("✖  Ligne {0} : {1}", ex.Line, ex.Message) : "✖  " + ex.Message, Line = ex.Line });
                if (ex.Line > 0 && ex.Line <= pyBox.LinesCount) new Range(pyBox, ex.Line - 1).SetStyle(errorMark);
                rpnBox.ForeColor = Color.FromArgb(130, RpnFore);
                status.Text = Lang.T("Erreur : le listing grisé est celui de la dernière conversion réussie.");
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
            if (last == null) { simOut.Text = Lang.T("Corrigez d'abord les erreurs signalées dans les messages."); return; }
            if (!(entryBox.SelectedItem is string entry)) { simOut.Text = Lang.T("Aucun label à lancer."); return; }
            double[] stack, inputs;
            try { stack = Nums(stackBox.Text); inputs = Nums(inputsBox.Text); }
            catch (FormatException) { simOut.Text = Lang.T("Valeur invalide : séparez les nombres par des espaces ou des points-virgules."); return; }

            if (TargetC47) { RunC47Simulation(entry, stack, inputs); return; }
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
            sb.Append(Lang.F("{0} pas exécutés", r.Steps));
            if (r.MaxCallDepth > 0) sb.Append(Lang.F(", {0} niveau(x) d'appel XEQ", r.MaxCallDepth));
            if (r.MaxCallDepth > 8) sb.Append(Lang.T(" (attention : la HP-42S d'origine n'a que 8 niveaux de retour)"));
            simOut.Text = sb.ToString().Replace("\n", Environment.NewLine).Replace("\r\r", "\r");
        }

        const char LogSep = '¦';

        /// <summary>
        /// Copie de la source C47 pour F5 : après chaque affichage (42AVIEW, 42PRMPT, VIEW), le message est ajouté à la
        /// variable texte PYLOG ; PYSIM vide le journal puis appelle l'étiquette lancée. Sur la pile de 8 niveaux, les deux
        /// valeurs empilées puis retirées ne touchent pas ce que le programme utilise (4 niveaux au plus).
        /// Après chaque saisie, « 0 1 J_y(x) DROP » consomme la touche R/S : le simulateur sans fenêtre la garde enfoncée,
        /// et le premier calcul interruptible qui suit (Bessel, premiers, gamma, solve, ∫…) s'arrêtait aussitôt sur NaN.
        /// </summary>
        static string WithMessageLog(string source, string entry)
        {
            string Log(string what) => $"  RCL ‘PYLOG’ RCL {what} + ‘{LogSep}’ + STO ‘PYLOG’ DROP";
            var lines = source.Replace("\r\n", "\n").Split('\n').ToList();
            var o = new List<string>();
            foreach (var raw in lines)
            {
                string t = raw.Trim();
                if (t == "END") continue;
                o.Add(raw);
                if (t.StartsWith("INPUT ") || t == "STOP" || t.StartsWith("42PRMPT")) o.Add("  0 1 J_y(x) DROP");
                if (t == "42AVIEW" || t.StartsWith("42PRMPT")) o.Add(Log("K"));   // ALPHA des instructions de compatibilité
                else if (t.StartsWith("VIEW "))
                {
                    // VIEW X : X est remonté d'un niveau par RCL ‘PYLOG’
                    string arg = t.Substring(5).Trim();
                    string shifted = arg switch { "X" => "Y", "Y" => "Z", "Z" => "T", "T" => "A", _ => arg };
                    o.Add(Log(shifted));
                }
            }
            o.Add("LBL ‘PYSIM’");
            o.Add("  ‘’ STO ‘PYLOG’ DROP");
            o.Add($"  XEQ ‘{entry}’");
            o.Add("  RTN");
            o.Add("END");
            return string.Join("\r\n", o);
        }

        /// <summary>Texte rendu par le simulateur, en codes de la police C47 : exposants lisibles (×⑽ⅫⅣ → ×10⁻³).</summary>
        static string Readable47(string s) => C47.Core.C47Text.Display(s).Replace("⑽", "10");

        /// <summary>Cible C47 : le programme encodé tourne dans le vrai simulateur C47 (sans fenêtre), qui rend la pile et ALPHA.</summary>
        void RunC47Simulation(string entry, double[] stack, double[] inputs)
        {
            if (lastC47Code == null || !lastC47Code.Ok) { simOut.Text = Lang.T("Le listing C47 contient des erreurs : voir les messages."); return; }
            simOut.Text = Lang.T("Simulateur C47 en cours…");
            simOut.Refresh();
            Cursor = Cursors.WaitCursor;
            Emulator.C47Run r;
            // copie instrumentée : chaque message affiché est ajouté au journal PYLOG, relu à la fin
            var sim = C47.Core.Encoder.Encode(WithMessageLog(lastC47.Source, entry));
            bool logged = sim.Ok;
            try { r = Emulator.RunC47(this, C47.Core.P47File.Write((logged ? sim : lastC47Code).Program), logged ? "PYSIM" : entry, stack, inputs, logged ? "PYLOG" : null); }
            finally { Cursor = Cursors.Default; }
            var sb = new StringBuilder();
            sb.AppendLine(Lang.T("Simulateur C47 (calcul sur 34 chiffres)"));
            var messages = r.Log.Split(LogSep, StringSplitOptions.RemoveEmptyEntries);
            if (messages.Length > 0)
            {
                sb.AppendLine();
                foreach (var m in messages) sb.AppendLine(Readable47(m));
            }
            else if (r.Alpha.Length > 0) sb.AppendLine().AppendLine(Lang.T("Dernier message : ") + Readable47(r.Alpha));
            if (r.Error.Length > 0) sb.AppendLine().AppendLine(r.Error);
            sb.AppendLine();
            sb.AppendLine(Lang.T(r.Stack8 ? "Pile de 8 niveaux (SSIZE8, réglage d'usine de la C47)" : "Pile de 4 niveaux (SSIZE4)"));
            if (r.Stack8)
            {
                sb.AppendLine($"D: {r.D}");
                sb.AppendLine($"C: {r.C}");
                sb.AppendLine($"B: {r.B}");
                sb.AppendLine($"A: {r.A}");
            }
            sb.AppendLine($"T: {r.T}");
            sb.AppendLine($"Z: {r.Z}");
            sb.AppendLine($"Y: {r.Y}");
            sb.AppendLine($"X: {r.X}");
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

        public void LoadExample(int index) => LoadExample(Py.Examples.All[index]);

        /// <summary>Exemple propre à la C47 (récursivité, entiers longs, textes longs) : la cible passe à C47 / R47.</summary>
        public void LoadC47Example(int index)
        {
            targetBox.SelectedIndex = 1;
            LoadExample(Py.ExamplesC47.All[index]);
        }

        /// <summary>Programme de la distribution C47 réécrit en Python : cible C47 / R47.</summary>
        public void LoadC47Program(int index)
        {
            targetBox.SelectedIndex = 1;
            LoadExample(Py.ExamplesC47.Programs[index]);
        }

        void LoadExample(Py.Examples.Example ex)
        {
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
                MessageBox.Show(this, ex.Message, Lang.T("Ouverture impossible"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                status.Text = Lang.T("Enregistré : ") + currentFile;
                UpdateTitle();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Lang.T("Enregistrement impossible"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void SendToEditor()
        {
            if (last == null) { status.Text = Lang.T("Rien à envoyer : corrigez d'abord les erreurs."); return; }
            string baseName = currentFile != null ? Path.GetFileNameWithoutExtension(currentFile)
                            : (progName.Text.Trim().Length > 0 ? progName.Text.Trim() : "MAIN");
            if (TargetC47)
            {
                // onglet C47 (titre en .p47u), prêt pour « Encoder et enregistrer en .p47 »
                sendToEditor?.Invoke(lastC47.Source, baseName + ".p47u", currentFile != null ? Path.ChangeExtension(currentFile, ".p47u") : null);
                status.Text = Lang.F("Source C47 envoyée dans l'onglet « {0}.p47u ». Utilisez « Encoder et enregistrer en .p47 » pour la C47.", baseName);
                return;
            }
            string path = currentFile != null ? Path.ChangeExtension(currentFile, ".free42") : null;
            sendToEditor?.Invoke(last.Listing(numButton.Checked, header: true), baseName + ".free42", path);
            status.Text = Lang.F("RPN envoyé dans l'onglet « {0}.free42 ». Utilisez « Encoder et enregistrer en .raw » pour la DM42.", baseName);
        }

        void CopyRpn()
        {
            if (last == null) { status.Text = Lang.T("Rien à copier : corrigez d'abord les erreurs."); return; }
            try
            {
                Clipboard.SetText(last.Listing(numButton.Checked, header: true));
                status.Text = Lang.T("Listing copié. Dans Free42 : Edit → Paste Program.");
            }
            catch (Exception ex) { status.Text = Lang.T("Presse-papiers indisponible : ") + ex.Message; }
        }

        void UpdateTitle() =>
            Text = currentFile == null ? (TargetC47 ? "Python → RPN C47 / R47" : "Python → RPN DM42") : "Python → RPN — " + Path.GetFileName(currentFile);

        /// <summary>Libellés des boutons selon la cible : .raw pour la DM42, .p47 pour la C47.</summary>
        void UpdateTarget()
        {
            rawButton.Text = Lang.T(TargetC47 ? "Générer .p47  F8" : "Générer .raw  F8");
            rawButton.ToolTipText = Lang.T(TargetC47 ? "Encode directement le programme en fichier .p47 pour la C47 / R47" : "Encode directement le programme en fichier .raw pour la DM42 / Free42");
            // titre du cadre du listing (Framed : bordure dans le panneau, libellé à côté)
            if (rpnBox.Parent?.Parent?.Controls.OfType<Label>().FirstOrDefault() is Label caption)
                caption.Text = TargetC47 ? "RPN   C47 · R47" : "RPN   DM42 · HP-42S · Free42";
            sendButton.ToolTipText = Lang.T(TargetC47 ? "Ouvre la source C47 dans un onglet C47 de l'éditeur, prêt pour « Encoder et enregistrer en .p47 »" : "Ouvre le RPN dans un onglet de DM42 Editor, prêt pour « Encoder et enregistrer en .raw »");
            UpdateTitle();
        }

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
        internal sealed class HpRenderer : ToolStripProfessionalRenderer
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
        /// <summary>Texte à insérer pour une ligne d'aide « SIN(x) : sinus » ou « if FS(n): ... test ».</summary>
        static string HelpSnippet(string help)
        {
            int i = help.IndexOf(" : ", StringComparison.Ordinal);
            string s = i > 0 ? help.Substring(0, i) : help;
            int j = s.IndexOf(": ...", StringComparison.Ordinal);
            return j > 0 ? s.Substring(0, j + 1) : s;
        }

        /// <summary>Insère un appel choisi dans l'aide à l'emplacement du curseur.</summary>
        void InsertFromHelp(string text)
        {
            pyBox.InsertText(text);
            pyBox.Invalidate();
        }

        /// <summary>Instruction produite (après « → ») sous son nom C47, pour l'aide de la cible C47.</summary>
        static readonly (string Hp42, string C47)[] HelpC47Names =
        {
            ("→ SQRT", "→ √x"), ("→ E↑X", "→ ⅇˣ"), ("→ Y↑X", "→ yˣ"), ("→ ABS", "→ |x|"), ("→ N!", "→ x!"), ("→ GAMMA", "→ Γ(x)"),
            ("→ →RAD", "→ deg→rad"), ("→ →DEG", "→ rad→deg"), ("→ NEWMAT", "→ M.NEW"), ("→ MVAR + VARMENU", "→ MVAR + 42VRMNU"),
            ("→ ALPHA + AVIEW", "→ 42α + 42ARCL + 42AVIEW"), ("→ INPUT", "→ INPUT"),
        };

        /// <summary>Aide de la fenêtre Python : cible DM42 (HP-42S, Free42) ou C47 / R47.</summary>
        public static void ShowHelp(IWin32Window owner, bool c47 = false)
        {
            var sb = new StringBuilder();
            var snippets = new Dictionary<string, string>();   // ligne affichée -> texte inséré au double-clic
            void Fn(string help, string suffix = "")
            {
                // C47 : la ligne est traduite ici, puis l'instruction prend son nom C47 (√x, x!…)
                string line = help;
                if (c47)
                {
                    line = Lang.T(help);
                    foreach (var (hp, n) in HelpC47Names) line = line.Replace(hp, n);
                    line += suffix;
                }
                sb.AppendLine("  " + line);
                snippets["  " + line] = HelpSnippet(Lang.T(help));
            }
            if (c47) { AppendC47Help(sb); AppendC47Functions(sb, snippets); }
            sb.AppendLine("PYTHON ACCEPTÉ");
            sb.AppendLine("  Nombres, variables, + - * / ** % //, parenthèses, comparaisons (chaînées), and / or / not");
            if (c47) sb.AppendLine("  if / elif / else → IF ELSE ENDIF ; while (+ else) → DO WHILE ENDDO ; for i in range(...) → FORᵀᴼᴾ NEXT ; break, continue, pass");
            if (c47) sb.AppendLine("  Bit à bit sur des entiers : & | ^ ~ → SINT … AND / OR / XOR / NOT … LINT ; x << n, x >> n → par 2ⁿ (exact en entier long)");
            else sb.AppendLine("  if / elif / else, while (+ else), for i in range(...) (+ else), break, continue, pass");
            sb.AppendLine("  def f(a, b, ...) : 4 paramètres max ; return x ou return a, b (puis a, b = f(...))");
            sb.AppendLine("  global, assert condition, \"message\"");
            sb.AppendLine("  # commentaire  ou bloc \"\"\" … \"\"\" (ou ''') en début de ligne : commentaires ignorés, absents du RPN");
            sb.AppendLine("  x = input(\"...\")  → INPUT \"X\"        x = prompt(\"PRIX ?\") ou PROMPT(\"PRIX ?\")  → texte + PROMPT");
            sb.AppendLine("  print(\"texte\", x) ou print(\"texte\" + str(x))  → ALPHA + AVIEW    print(x) → VIEW \"X\"");
            sb.AppendLine("  import math : sqrt sin cos tan asin acos atan atan2 sinh cosh tanh exp log log10");
            sb.AppendLine("                hypot pow max min abs int floor ceil factorial gamma radians degrees pi e");
            sb.AppendLine("  round(x, n) : arrondi à n décimales (n constant, 0 à 11) ; les demis s'éloignent de zéro, comme RND");
            sb.AppendLine();
            sb.AppendLine("FONCTIONS PYTHON (double-clic : insère l'appel dans le code)");
            Fn("x = float(input(\"X ?\")) : demande une valeur → INPUT");
            Fn("print(\"texte\", x) : affiche du texte et une valeur → ALPHA + AVIEW");
            Fn("round(x, 2) : arrondi à n décimales, affiché en FIX n");
            foreach (var m in new[]
            {
                "sqrt(x) : racine carrée → SQRT", "exp(x) : exponentielle → E↑X", "log(x) : logarithme népérien → LN",
                "log10(x) : logarithme décimal → LOG", "sin(x) : sinus en radians", "cos(x) : cosinus en radians", "tan(x) : tangente en radians",
                "asin(x) : arc sinus en radians", "acos(x) : arc cosinus en radians", "atan(x) : arc tangente en radians",
                "atan2(y, x) : angle de (x, y) en radians", "hypot(x, y) : hypoténuse", "pow(x, y) : x puissance y → Y↑X",
                "max(a, b) : le plus grand", "min(a, b) : le plus petit", "abs(x) : valeur absolue → ABS", "int(x) : partie entière → IP",
                "floor(x) : entier inférieur", "ceil(x) : entier supérieur", "factorial(n) : factorielle → N!", "gamma(x) : fonction gamma → GAMMA",
                "radians(x) : degrés vers radians → →RAD", "degrees(x) : radians vers degrés → →DEG",
                "inv(A) : inverse d'une matrice", "det(A) : déterminant", "solve(A, b) : résout A·x = b", "norm(A) : norme",
                "dot(u, v) : produit scalaire", "cross(u, v) : produit vectoriel", "rows(A) : nombre de lignes", "cols(A) : nombre de colonnes",
                "len(v) : nombre d'éléments", "zeros(l, c) : matrice nulle → NEWMAT",
                "varmenu(a, b, c) : menu de variables → MVAR + VARMENU", "choix = menu(\"ADD\", \"SUB\") : menu de touches, renvoie 1 à 6, 0 pour EXIT"
            }) Fn(m);
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
            sb.AppendLine(c47 ? "MENUS (compatibilité HP-42S de la C47 : 42VRMNU, 42KEY)" : "MENUS DM42");
            sb.AppendLine("  varmenu(a, b, c)            MVAR + VARMENU : tapez une valeur, touche de la variable, puis R/S");
            sb.AppendLine("  choix = menu(\"ADD\", \"SUB\")  menu de touches (6 max) : renvoie 1 à 6, 0 pour EXIT");
            sb.AppendLine();
            sb.AppendLine("DRAPEAUX");
            sb.AppendLine("  SF(n) CF(n)                 lève / baisse le drapeau n");
            sb.AppendLine("  if FS(n): ...  if FC(n):    tests ; FSC(n) / FCC(n) testent puis baissent le drapeau");
            sb.AppendLine();
            sb.AppendLine(c47 ? "COMMANDES DM42 ACCEPTÉES (nom HP-42S, majuscules ou minuscules ; écrites en C47 sous le nom après ⇒)" : "COMMANDES HP42S ( majuscules ou minuscules)");
            if (c47) sb.AppendLine("  Elles complètent les fonctions Python ci-dessus : la C47 reprend les commandes de la HP-42S, parfois sous un autre nom.");
            sb.AppendLine("  Les arguments suivent l'ordre de la pile : le dernier va dans X.");
            sb.AppendLine("  SIN(x), COS(x)... utilisent le mode d'angle de la calculatrice (DEG(), RAD()) ;");
            sb.AppendLine("  math.sin(x) travaille toujours en radians.");
            sb.AppendLine("  Deux résultats : a, b = MEAN()  → a reçoit Y, b reçoit X (ordre de la pile).");
            sb.AppendLine("  Date JJ.MMAAAA : j, m, a = dmy(d)  → 12.052024 donne 12, 5, 2024.");
            sb.AppendLine("  Date du jour : DMY() puis d = DATE() ; le format réglé reste actif après le programme.");
            sb.AppendLine("  Heure : h = toHR(TIME())  → heures décimales.");
            sb.AppendLine("  Exemple : print(\"sine of\", myvar, \"is\", SIN(myvar))");
            foreach (var h in Py.Hp42.AllHelp().OrderBy(k => k.Name, StringComparer.OrdinalIgnoreCase))
                Fn(h.Help, c47 ? C47NameOf(h.Name) : "");
            sb.AppendLine();
            if (c47) AppendC47Catalog(sb, snippets);
            sb.AppendLine("LIMITES");
            if (c47)
            {
                sb.AppendLine("  Expressions sur 4 niveaux de pile (le programme marche en SSIZE4 comme en SSIZE8)");
                sb.AppendLine("  99 registres locaux par fonction · 18 boucles FOR en cours à la fois · 32 indicateurs locaux");
                sb.AppendLine("  Noms de 7 caractères · 99 labels locaux · pas de listes, chaînes manipulées, classes, exceptions");
                sb.AppendLine();
                sb.AppendLine("RACCOURCIS (cible C47) : F5 simulateur C47 · F6 onglet C47 de l'éditeur · F7 émulateur C47 · F8 fichier .p47");
                sb.AppendLine("  F5 exécute le programme dans le simulateur C47 (réglé dans Fichier → Paramètres) : chaque INPUT reçoit la valeur suivante puis R/S.");
                sb.AppendLine("  La pile affichée a 8 niveaux (X Y Z T A B C D, réglage d'usine SSIZE8) ; les menus (varmenu, menu) ne sont pas simulés ainsi.");
                sb.AppendLine("FICHIER .P47 : F8 encode le programme ; sur la C47, READP le charge.");
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("  Pile de 4 niveaux · variables globales (les locales en conflit sont renommées) · pas de récursivité");
                sb.AppendLine("  Noms de 7 caractères · 99 labels locaux · pas de listes, chaînes manipulées, classes, exceptions");
                sb.AppendLine();
            }
            sb.AppendLine("RACCOURCIS : F5 simulation · F6 envoyer vers l'éditeur · F7 envoyer vers l'émulateur · Ctrl+K commenter · Ctrl+N nouveau · Ctrl+O ouvrir · Ctrl+S enregistrer · Ctrl+P imprimer · Ctrl+F rechercher · Ctrl+H remplacer");
            sb.AppendLine("  Menu Fichier : nouveau, ouvrir, enregistrer (sous), exemples, imprimer le code Python ou le listing RPN, quitter.");
            sb.AppendLine("  Clic droit dans le code : couper, copier, coller, effacer la sélection, effacer la page.");
            sb.AppendLine("  Clic droit ou Ctrl+C dans les messages : copier le message (Ctrl+Maj+C : tous les messages).");
            sb.AppendLine("  Menu Affichage : thème clair / sombre du Python, couleurs et fond du listing RPN.");
            sb.AppendLine("FICHIER .RAW : F8 (Générer .raw) encode directement le programme : DM42 → copiez-le sur son disque USB puis Load Program ; Free42 → File → Import Programs.");
            sb.AppendLine("ÉMULATEUR : F7 copie le listing et affiche l'émulateur choisi dans Fichier → Paramètres (Free42, Plus42…) ; en mode PRGM (⇧ R/S), Ctrl+V ajoute le programme.");

            var f = new Form
            {
                Text = Lang.T(c47 ? "Aide Python → RPN C47 / R47" : "Aide Python → RPN DM42"),
                Size = new Size(860, 720),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Body,
                ShowInTaskbar = false
            };
            var hint = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 24,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 0, 0),
                ForeColor = Paper,
                Text = Lang.T("Double-clic ou Entrée sur une ligne en noir : insère l'appel à l'emplacement du curseur · Ctrl+C : copie la ligne")
            };
            var box = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                HorizontalScrollbar = true,
                DrawMode = DrawMode.OwnerDrawFixed,
                Font = new Font("Consolas", 10f),
                BackColor = Paper,
                ForeColor = Color.FromArgb(31, 30, 27),
                BorderStyle = BorderStyle.None
            };
            box.ItemHeight = box.Font.Height + 2;
            var allLines = sb.ToString().Split(new[] { "\r\n" }, StringSplitOptions.None);
            box.Items.AddRange(allLines.Cast<object>().ToArray());
            // recherche : lignes qui contiennent le texte, avec le titre de leur section
            var search = new TextBox { Dock = DockStyle.Top, PlaceholderText = Lang.T("Rechercher dans l'aide (nom, fonction, description)…") };
            search.TextChanged += (s, e) =>
            {
                string q = search.Text.Trim();
                var shown = new List<object>();
                string head = null, sub = null;
                foreach (var line in allLines)
                {
                    if (line.Length > 0 && line[0] != ' ' && !line.StartsWith("· ")) { head = line; sub = null; }
                    else if (line.StartsWith("· ")) sub = line;
                    if (q.Length > 0 && line.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 && Shown(line).IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (q.Length > 0)
                    {
                        // titre de la section, puis sous-titre du domaine, avant la ligne trouvée
                        if (head != null && line != head && !shown.Contains(head)) shown.Add(head);
                        if (sub != null && line != sub && !shown.Contains(sub)) shown.Add(sub);
                    }
                    shown.Add(line);
                }
                box.BeginUpdate();
                box.Items.Clear();
                box.Items.AddRange(shown.ToArray());
                box.EndUpdate();
            };
            // le retrait des lignes ne fait pas partie du texte traduit
            string Shown(string line)
            {
                string t = line.TrimStart();
                return line.Substring(0, line.Length - t.Length) + Lang.T(t);
            }
            int widest = 0;
            foreach (object o in box.Items) widest = Math.Max(widest, TextRenderer.MeasureText(Shown((string)o), box.Font).Width);
            box.HorizontalExtent = widest + 12;
            // lignes insérables en noir, explications en gris, titres en gras
            var title = new Font(box.Font, FontStyle.Bold);
            f.FormClosed += (s, e) => title.Dispose();
            box.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;
                string line = (string)box.Items[e.Index];
                bool selected = (e.State & DrawItemState.Selected) != 0;
                bool head = line.Length > 0 && line[0] != ' ';
                using (var back = new SolidBrush(selected ? Color.FromArgb(214, 226, 240) : box.BackColor))
                    e.Graphics.FillRectangle(back, e.Bounds);
                Color fore = head || snippets.ContainsKey(line) ? box.ForeColor : Color.FromArgb(110, 108, 100);
                TextRenderer.DrawText(e.Graphics, Shown(line), head ? title : box.Font, e.Bounds, fore,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            };
            void Insert()
            {
                if (!(box.SelectedItem is string line) || !snippets.TryGetValue(line, out string text)) return;
                var target = Application.OpenForms.OfType<FrmPython>().FirstOrDefault();
                if (target == null) { hint.Text = Lang.T("Ouvrez d'abord la fenêtre Python pour insérer : ") + text; return; }
                target.InsertFromHelp(text);
                hint.Text = Lang.T("Inséré : ") + text;
            }
            box.DoubleClick += (s, e) => Insert();
            box.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { Insert(); e.SuppressKeyPress = true; }
                else if (e.Control && e.KeyCode == Keys.C && box.SelectedItem is string line && line.Length > 0) Clipboard.SetText(Lang.T(line.Trim()));
            };
            f.Controls.Add(box);
            f.Controls.Add(search);
            f.Controls.Add(hint);
            f.Show(owner);
        }

        /// <summary>
        /// Fonctions Python de la cible C47 (table Py42.Core.C47Functions, chacune vérifiée sur le simulateur), par domaine :
        /// appel Python → instruction C47 : description ; double-clic insère l'appel.
        /// </summary>
        static void AppendC47Functions(StringBuilder sb, Dictionary<string, string> snippets)
        {
            bool en = Py.Tr.English;
            void Line(string call, string rpn, string desc)
            {
                string line = $"  {call} → {rpn} : {desc}";   // retrait hors de l'appel inséré
                sb.AppendLine(line);
                snippets[line] = call;
            }
            // « jv(v, x) : Bessel… » : l'appel est déjà dans la description
            (string Call, string Desc) Split(Py.C47Functions.Fn f)
            {
                string d = en ? f.En : f.Fr;
                int i = d.IndexOf(" : ", StringComparison.Ordinal) is int a && a > 0 ? a : d.IndexOf(": ", StringComparison.Ordinal);
                if (d.StartsWith(f.Python + "(") && i > 0) return (d.Substring(0, i), d.Substring(i).TrimStart(' ', ':'));
                return (f.Python + (f.Args == 1 ? "(x)" : "(x, y)"), d);
            }
            sb.AppendLine("FONCTIONS PYTHON → C47 (math, scipy.special, sympy ; double-clic : insère l'appel)");
            foreach (var g in Py.C47Functions.All.GroupBy(f => f.Domain))
            {
                sb.AppendLine("· " + Lang.T(g.Key).ToUpperInvariant());
                foreach (var f in g) { var (call, desc) = Split(f); Line(call, f.Rpn, desc); }
            }
            sb.AppendLine("· " + Lang.T("Statistiques et probabilités").ToUpperInvariant() + "  (import statistics ; v : liste ou variable liste)");
            foreach (var f in Py.C47Functions.Statistics) Line("statistics." + f.Python + "(v)", "CLΣ, Σ+… " + f.Rpn, en ? f.En : f.Fr);
            sb.AppendLine("· " + (en ? "PROBABILITY DISTRIBUTIONS (import scipy.stats as st)" : "LOIS DE PROBABILITÉ (import scipy.stats as st)"));
            sb.AppendLine(en ? "  st.LAW.pdf (or pmf) / cdf / sf / ppf (x, parameters): the parameters go into registers M N P Q R S, as on the C47"
                             : "  st.LOI.pdf (ou pmf) / cdf / sf / ppf (x, paramètres) : les paramètres vont dans les registres M N P Q R S, comme sur la C47");
            foreach (var d in Py.C47Functions.Distributions)
            {
                string pm = d.Discrete ? "pmf" : "pdf";
                string pars = d.Python switch
                {
                    "norm" or "cauchy" or "logistic" => "x, loc, scale", "expon" => "x, 0, scale", "weibull_min" => "x, c, 0, scale",
                    "t" or "chi2" => "x, df", "f" => "x, dfn, dfd", "binom" or "nbinom" => "k, n, p", "poisson" => "k, mu", _ => "k, p"
                };
                Line($"st.{d.Python}.{pm}({pars})", $"{d.Pdf} {d.Cdf} {d.Sf} {d.Ppf}", en ? d.En : d.Fr);
            }
            Line("st.norm.cdf(x)", "ϕₚ Φ◭ Φ◮ Φ⁻¹", en ? "standard normal distribution (no loc, scale)" : "loi normale centrée réduite (sans loc ni scale)");
            sb.AppendLine("· " + Lang.T("Constantes").ToUpperInvariant() + "  (import scipy.constants)");
            foreach (var f in Py.C47Functions.Constants) Line("scipy.constants." + f.Python, f.Rpn, en ? f.En : f.Fr);
            sb.AppendLine("· TESTS");
            Line("if isprime(n):", "PRIME?", en ? "sympy.isprime: n is prime" : "sympy.isprime : n est premier");
            sb.AppendLine("· APPEL DIRECT (import c47)");
            Line("c47.call(\"ft→m\", x)", "ft→m", en ? "any C47 instruction by its name written as text, symbols included (full list at the end of this help)"
                                                         : "toute instruction C47 par son nom écrit en texte, symboles compris (liste complète en fin d'aide)");
            Line("c47.AGM(a, b)", "AGM", en ? "any C47 instruction by its name: arguments in stack order, the last one in X" : "n'importe quelle instruction C47 par son nom : arguments dans l'ordre de la pile, le dernier dans X");
            sb.AppendLine();
        }

        /// <summary>« ⇒ NOM » : nom C47 d'une commande HP-42S de Py42 quand il diffère (Hp42Converter), vide sinon.</summary>
        static string C47NameOf(string python)
        {
            string rpn = Py.Hp42.Commands.TryGetValue(python, out var cmd) ? cmd.Rpn
                       : Py.Hp42.VarCommands.TryGetValue(python, out var vc) ? vc.Rpn
                       : Py.Hp42.LabelCommands.TryGetValue(python, out var lc) ? lc.Rpn : null;
            if (string.IsNullOrEmpty(rpn)) return "";
            string first = rpn.Split(' ')[0];
            string c47 = C47.Core.Hp42Converter.Convert(first).Source.Trim();
            if (c47.StartsWith("@")) return "  " + Lang.T("⇒ (sans objet sur la C47)");
            return c47 != first ? "  ⇒ " + c47 : "";
        }

        /// <summary>Toutes les instructions C47 sans paramètre, par domaine, appelables par c47.call("nom", …).</summary>
        static void AppendC47Catalog(StringBuilder sb, Dictionary<string, string> snippets)
        {
            sb.AppendLine("TOUTES LES INSTRUCTIONS C47 SANS PARAMÈTRE : c47.call(\"nom\", arguments…) (double-clic : insère l'appel)");
            sb.AppendLine("  Conversions d'unités, dates, bits, réels… : les arguments suivent l'ordre de la pile, le dernier dans X.");
            foreach (string cat in C47.Core.Items.Categories)
            {
                var items = C47Help.Listed().Where(i => i.Kind == C47.Core.ParamKind.None && C47.Core.Items.Help(i).Category == cat)
                                            .OrderBy(i => i.Shown, StringComparer.OrdinalIgnoreCase).ToList();
                if (items.Count == 0) continue;
                sb.AppendLine("· " + Lang.T(cat).ToUpperInvariant() + $"  ({items.Count})");
                foreach (var i in items)
                {
                    string name = C47.Core.Items.Unique(i);
                    string call = $"c47.call(\"{name}\")";
                    string line = $"  {call} : {C47Help.Short(i, 90)}";
                    sb.AppendLine(line);
                    snippets[line] = call;
                }
            }
            sb.AppendLine();
        }

        /// <summary>Début de l'aide de la cible C47 : ce que la C47 change par rapport à la DM42.</summary>
        static void AppendC47Help(StringBuilder sb)
        {
            sb.AppendLine("CIBLE C47 / R47");
            sb.AppendLine("  Le même Python produit un programme pour la C47 : structures, registres locaux, entiers longs, textes longs.");
            sb.AppendLine("  Les exemples propres à la C47 sont dans Fichier → Exemples C47 / R47.");
            sb.AppendLine();
            sb.AppendLine("STRUCTURES (au lieu des GTO de la DM42)");
            sb.AppendLine("  if c: … elif d: … else: …      test IF … ELSE test IF … ENDIF ENDIF");
            sb.AppendLine("  while c: … else: …             DO test WHILE … ENDDO, puis le else");
            sb.AppendLine("  for i in range(a, b, pas):     a, b - 1, pas, FORᵀᴼᴾ i … NEXT i (aucun tour si la plage est vide)");
            sb.AppendLine("  for avec break, ou qui modifie i : DO … ENDDO (sortir d'un FOR par GTO garde son emplacement occupé)");
            sb.AppendLine("  and / or / not : la condition est calculée dans un indicateur local .00, .01… (court-circuit de Python)");
            sb.AppendLine("  Les structures sont numérotées à l'encodage, comme VALID sur la calculatrice.");
            sb.AppendLine();
            sb.AppendLine("FONCTIONS ET RÉCURSIVITÉ");
            sb.AppendLine("  Variables locales d'une fonction dans les registres locaux : LocR n, puis .00, .01… (plus de renommage)");
            sb.AppendLine("  Chaque appel a ses propres registres : la récursivité est permise (def fact(n): … return n * fact(n - 1))");
            sb.AppendLine("  Les variables du programme principal restent des variables nommées, visibles sur la calculatrice.");
            sb.AppendLine();
            sb.AppendLine("NOMBRES ET TEXTES");
            sb.AppendLine("  Un nombre entier est un entier long, exact sans limite de taille : fact(25) = 15511210043330985984000000");
            sb.AppendLine("  Un texte est une valeur poussée dans X, sans limite de 6 caractères : nom = \"CALCULATRICE C47\"");
            sb.AppendLine("  print passe par les instructions de compatibilité HP-42S (42α, 42ARCL, 42AVIEW ; ALPHA = registre K).");
            sb.AppendLine();
        }
    }
}
