using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Dm42.Editor;
using Range = Dm42.Editor.TextRange;

namespace dm42editor
{
    /// <summary>
    /// Fenêtre Python → RPN : menu contextuel, effacement de la page, thèmes clair / sombre du code Python,
    /// couleurs du listing RPN, envoi vers Free42 / Plus42. Réglages enregistrés dans
    /// %LOCALAPPDATA%\dm42editor\python-vue.ini.
    /// </summary>
    public partial class FrmPython
    {
        // ==================================================================
        //  Réglages d'affichage
        // ==================================================================
        sealed class RpnColors
        {
            public Color Back, Text, LineNo, Label, Flow, Test, Alpha;
            public RpnColors Clone() => (RpnColors)MemberwiseClone();

            public static RpnColors Lcd() => new RpnColors
            {
                Back = Color.FromArgb(198, 205, 176), Text = Color.FromArgb(30, 36, 24), LineNo = Color.FromArgb(115, 123, 98),
                Label = Color.FromArgb(140, 47, 12), Flow = Color.FromArgb(154, 82, 0), Test = Color.FromArgb(31, 76, 120), Alpha = Color.FromArgb(85, 107, 20)
            };
            public static RpnColors Dark() => new RpnColors
            {
                Back = Color.FromArgb(30, 30, 30), Text = Color.FromArgb(220, 220, 214), LineNo = Color.FromArgb(110, 110, 104),
                Label = Color.FromArgb(240, 150, 70), Flow = Color.FromArgb(230, 190, 90), Test = Color.FromArgb(110, 180, 240), Alpha = Color.FromArgb(160, 210, 120)
            };
            public static RpnColors Paper() => new RpnColors
            {
                Back = Color.FromArgb(251, 250, 246), Text = Color.FromArgb(31, 30, 27), LineNo = Color.FromArgb(160, 156, 145),
                Label = Color.FromArgb(170, 40, 20), Flow = Color.FromArgb(184, 86, 27), Test = Color.FromArgb(45, 94, 158), Alpha = Color.FromArgb(61, 122, 42)
            };
            public static RpnColors Free42() => new RpnColors
            {
                Back = Color.FromArgb(232, 236, 222), Text = Color.Black, LineNo = Color.FromArgb(96, 96, 96),
                Label = Color.FromArgb(0, 0, 160), Flow = Color.FromArgb(160, 0, 0), Test = Color.FromArgb(0, 110, 0), Alpha = Color.FromArgb(120, 60, 0)
            };

            public static readonly (string Name, Func<RpnColors> Make)[] Presets =
            {
                ("Écran LCD (défaut)", Lcd), ("Sombre", Dark), ("Papier", Paper), ("Contraste", Free42)
            };
        }

        bool pyDark;
        RpnColors rpnColors = RpnColors.Lcd();
        ToolStripMenuItem darkItem;

        Color RpnFore => rpnColors.Text;

        static string SettingsFile =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dm42editor", "python-vue.ini");

        void LoadViewSettings()
        {
            try
            {
                if (!File.Exists(SettingsFile)) return;
                var d = File.ReadAllLines(SettingsFile)
                    .Select(l => l.Split(new[] { '=' }, 2)).Where(p => p.Length == 2)
                    .ToDictionary(p => p[0].Trim(), p => p[1].Trim());
                pyDark = d.TryGetValue("PythonSombre", out var v) && v == "1";
                // ancien réglage de l'émulateur : repris par le réglage commun (Paramètres)
                if (d.TryGetValue("Free42", out var f) && f.Length > 0 && Emulator.ExePath.Length == 0) Emulator.ExePath = f;
                Color C(string key, Color def) => d.TryGetValue(key, out var s) && TryColor(s, out var c) ? c : def;
                var r = RpnColors.Lcd();
                rpnColors = new RpnColors
                {
                    Back = C("Rpn.Fond", r.Back), Text = C("Rpn.Texte", r.Text), LineNo = C("Rpn.Numeros", r.LineNo),
                    Label = C("Rpn.Labels", r.Label), Flow = C("Rpn.Sauts", r.Flow), Test = C("Rpn.Tests", r.Test), Alpha = C("Rpn.Textes", r.Alpha)
                };
            }
            catch { /* réglages illisibles : valeurs par défaut */ }
        }

        void SaveViewSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile));
                string H(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                File.WriteAllLines(SettingsFile, new[]
                {
                    "PythonSombre=" + (pyDark ? "1" : "0"),
                    "Rpn.Fond=" + H(rpnColors.Back), "Rpn.Texte=" + H(rpnColors.Text), "Rpn.Numeros=" + H(rpnColors.LineNo),
                    "Rpn.Labels=" + H(rpnColors.Label), "Rpn.Sauts=" + H(rpnColors.Flow), "Rpn.Tests=" + H(rpnColors.Test), "Rpn.Textes=" + H(rpnColors.Alpha)
                });
            }
            catch { /* dossier non accessible : tant pis */ }
        }

        static bool TryColor(string s, out Color c)
        {
            c = Color.Empty;
            if (s.Length == 7 && s[0] == '#' && int.TryParse(s.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v))
            {
                c = Color.FromArgb(255, Color.FromArgb(v));
                return true;
            }
            return false;
        }

        /// <summary>Appelé à la fin de la construction de l'interface.</summary>
        void InitExtras()
        {
            LoadViewSettings();
            pyBox.ContextMenuStrip = BuildPythonMenu();
            rpnBox.ContextMenuStrip = BuildRpnMenu();
            messages.ContextMenuStrip = BuildMessagesMenu();
            messages.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.C) { CopyMessages(!e.Shift); e.Handled = true; }
            };
            // clic droit : sélectionne le message visé avant d'ouvrir le menu
            messages.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                int i = messages.IndexFromPoint(e.Location);
                if (i >= 0 && i != messages.SelectedIndex) messages.SelectedIndex = i;
            };
            ApplyPythonTheme();
            ApplyRpnColors();
            FormClosed += (s, e) => SaveViewSettings();
        }

        // ==================================================================
        //  Menus contextuels
        // ==================================================================
        ContextMenuStrip DarkMenu()
        {
            var m = new ContextMenuStrip { Renderer = new HpRenderer(), BackColor = Color.FromArgb(44, 44, 41), ForeColor = Ink, ShowImageMargin = false };
            return m;
        }

        static ToolStripMenuItem Item(string text, string keys, EventHandler click)
        {
            var it = new ToolStripMenuItem(text) { ShortcutKeyDisplayString = keys, ForeColor = Ink };
            it.Click += click;
            return it;
        }

        ContextMenuStrip BuildPythonMenu()
        {
            var m = DarkMenu();
            var undo = Item("Annuler", "Ctrl+Z", (s, e) => pyBox.Undo());
            var redo = Item("Rétablir", "Ctrl+Y", (s, e) => pyBox.Redo());
            var cut = Item("Couper", "Ctrl+X", (s, e) => pyBox.Cut());
            var copy = Item("Copier", "Ctrl+C", (s, e) => pyBox.Copy());
            var paste = Item("Coller", "Ctrl+V", (s, e) => pyBox.Paste());
            var clearSel = Item("Effacer la sélection", "Suppr", (s, e) => pyBox.ClearSelected());
            var all = Item("Tout sélectionner", "Ctrl+A", (s, e) => pyBox.SelectAll());
            var comment = Item("Commenter / décommenter", "Ctrl+K", (s, e) => ToggleComment());
            var clearAll = Item("Effacer toute la page…", null, (s, e) => ClearPage());
            m.Items.AddRange(new ToolStripItem[] { undo, redo, new ToolStripSeparator(), cut, copy, paste, clearSel, new ToolStripSeparator(),
                                                   all, comment, new ToolStripSeparator(), clearAll });
            m.Opening += (s, e) =>
            {
                bool sel = !pyBox.Selection.IsEmpty;
                undo.Enabled = pyBox.UndoEnabled;
                redo.Enabled = pyBox.RedoEnabled;
                cut.Enabled = copy.Enabled = clearSel.Enabled = sel;
                bool clip;
                try { clip = Clipboard.ContainsText(); } catch { clip = true; }
                paste.Enabled = clip;
            };
            return m;
        }

        ContextMenuStrip BuildMessagesMenu()
        {
            var m = DarkMenu();
            var one = Item("Copier le message", "Ctrl+C", (s, e) => CopyMessages(true));
            var all = Item("Copier tous les messages", "Ctrl+Maj+C", (s, e) => CopyMessages(false));
            m.Items.AddRange(new ToolStripItem[] { one, all });
            m.Opening += (s, e) =>
            {
                one.Enabled = messages.SelectedItem != null;
                all.Enabled = messages.Items.Count > 0;
            };
            return m;
        }

        /// <summary>Copie le message sélectionné (ou tous) dans le presse-papiers.</summary>
        void CopyMessages(bool selectedOnly)
        {
            var items = selectedOnly
                ? (messages.SelectedItem != null ? new[] { messages.SelectedItem } : Array.Empty<object>())
                : messages.Items.Cast<object>().ToArray();
            if (items.Length == 0) { status.Text = "Aucun message à copier."; return; }
            try
            {
                Clipboard.SetText(string.Join(Environment.NewLine, items.Select(i => i.ToString())));
                status.Text = items.Length == 1 ? "Message copié dans le presse-papiers." : $"{items.Length} messages copiés dans le presse-papiers.";
            }
            catch (Exception ex) { status.Text = "Presse-papiers indisponible : " + ex.Message; }
        }

        ContextMenuStrip BuildRpnMenu()
        {
            var m = DarkMenu();
            var copy = Item("Copier", "Ctrl+C", (s, e) => rpnBox.Copy());
            m.Items.AddRange(new ToolStripItem[]
            {
                copy,
                Item("Tout sélectionner", "Ctrl+A", (s, e) => rpnBox.SelectAll()),
                new ToolStripSeparator(),
                Item("Copier tout le listing", null, (s, e) => CopyRpn()),
                Item("Envoyer vers l'émulateur", "F7", (s, e) => SendToFree42()),
                Item("Générer le fichier .raw…", "F8", (s, e) => SaveRaw()),
                Item("Logigramme du listing…", null, (s, e) => ShowFlowchart()),
                new ToolStripSeparator(),
                Item("Couleurs du listing…", null, (s, e) => EditRpnColors())
            });
            m.Opening += (s, e) => copy.Enabled = !rpnBox.Selection.IsEmpty;
            return m;
        }

        /// <summary>Menu Fichier : nouveau, ouvrir, enregistrer, exemples, imprimer, quitter.</summary>
        ToolStripDropDownButton BuildFileMenu(ToolStrip tools)
        {
            var file = new ToolStripDropDownButton("Fichier") { ForeColor = Ink, ShowDropDownArrow = false };
            var examples = Ico("examples", new ToolStripMenuItem("Exemples") { ForeColor = Ink });
            for (int i = 0; i < Py42.Core.Examples.All.Count; i++)
            {
                int idx = i;
                examples.DropDownItems.Add(Item(Py42.Core.Examples.All[i].Title, null, (s, e) => { if (ConfirmDiscard()) LoadExample(idx); }));
            }
            examples.DropDown.Renderer = tools.Renderer;
            examples.DropDown.BackColor = Color.FromArgb(44, 44, 41);

            file.DropDownItems.AddRange(new ToolStripItem[]
            {
                Ico("new", Item("Nouveau", "Ctrl+N", (s, e) => { if (ConfirmDiscard()) NewProgram(); })),
                Ico("open", Item("Ouvrir…", "Ctrl+O", (s, e) => OpenFileDialog())),
                Ico("save", Item("Enregistrer", "Ctrl+S", (s, e) => SavePython(false))),
                Ico("save", Item("Enregistrer sous…", "Ctrl+Maj+S", (s, e) => SavePython(true))),
                new ToolStripSeparator(),
                examples,
                new ToolStripSeparator(),
                Ico("print", Item("Imprimer le code Python…", "Ctrl+P", (s, e) => pyBox.Print(PrintTitle()))),
                Ico("print", Item("Imprimer le listing RPN…", null, (s, e) => PrintRpn())),
                new ToolStripSeparator(),
                Ico("quit", Item("Quitter", "Alt+F4", (s, e) => Close()))
            });
            // le menu déroulant ne reprend pas toujours le rendu de la barre : on le lui donne explicitement
            file.DropDown.Renderer = tools.Renderer;
            file.DropDown.BackColor = Color.FromArgb(44, 44, 41);
            file.DropDown.ForeColor = Ink;
            return file;
        }

        /// <summary>Logigramme du listing RPN ; double-clic sur une forme : ligne Python d'origine.</summary>
        void ShowFlowchart()
        {
            if (last == null) { status.Text = "Rien à représenter : corrigez d'abord les erreurs."; return; }
            var f = new Flowchart.FrmFlowchart(() => rpnBox.IsDisposed ? "" : rpnBox.Text,
                Path.GetFileNameWithoutExtension(PrintTitle()), line =>
                {
                    // ligne du listing → ligne Python qui l'a produite
                    int i = line - HeaderLines;
                    if (last == null || i < 0 || i >= last.Code.Count || last.Code[i].Line <= 0 || last.Code[i].Line > pyBox.LinesCount) return;
                    pyBox.Selection = new Range(pyBox, 0, last.Code[i].Line - 1, 0, last.Code[i].Line - 1);
                    pyBox.DoSelectionVisible();
                    Activate();
                    pyBox.Focus();
                });
            // suit les nouvelles conversions du listing
            EventHandler<Dm42.Editor.TextChangedEventArgs> follow = (s, e) => { if (!f.IsDisposed) f.Reload(); };
            rpnBox.TextChangedDelayed += follow;
            f.FormClosed += (s, e) => rpnBox.TextChangedDelayed -= follow;
            f.Show(this);
        }

        /// <summary>Icône vectorielle (16 px, traits clairs) sur une entrée des menus sombres de la fenêtre.</summary>
        static ToolStripMenuItem Ico(string name, ToolStripMenuItem item)
        {
            item.Image = ToolbarIcons.Make(name, 16, Ink, Color.FromArgb(44, 44, 41));
            item.ImageScaling = ToolStripItemImageScaling.None;
            return item;
        }

        string PrintTitle() => currentFile != null ? Path.GetFileName(currentFile)
                             : (progName.Text.Trim().Length > 0 ? progName.Text.Trim() : "MAIN") + ".py";

        void PrintRpn()
        {
            if (last == null) { status.Text = "Rien à imprimer : corrigez d'abord les erreurs."; return; }
            rpnBox.Print(Path.GetFileNameWithoutExtension(PrintTitle()) + " — listing RPN");
        }

        /// <summary>
        /// Avant de remplacer le code : propose d'enregistrer les modifications.
        /// Renvoie false si l'utilisateur annule (ou n'enregistre pas alors qu'il l'a demandé).
        /// </summary>
        bool ConfirmDiscard()
        {
            if (!pyBox.IsChanged) return true;
            string name = currentFile != null ? Path.GetFileName(currentFile) : "ce programme";
            var r = MessageBox.Show(this, "Enregistrer les modifications de " + name + " ?", "Python → RPN",
                                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (r == DialogResult.Cancel) return false;
            if (r == DialogResult.Yes) { SavePython(false); return !pyBox.IsChanged; }
            return true;
        }

        ToolStripDropDownButton BuildViewMenu(ToolStrip tools)
        {
            var view = new ToolStripDropDownButton("Affichage") { ForeColor = Ink, ShowDropDownArrow = false };
            darkItem = new ToolStripMenuItem("Code Python : thème sombre") { CheckOnClick = true, ForeColor = Ink };
            darkItem.CheckedChanged += (s, e) =>
            {
                if (pyDark == darkItem.Checked) return;
                pyDark = darkItem.Checked;
                ApplyPythonTheme();
                SaveViewSettings();
            };
            view.DropDownItems.Add(darkItem);
            view.DropDownItems.Add(new ToolStripSeparator());
            view.DropDownItems.Add(new ToolStripMenuItem("Couleurs du listing RPN…", null, (s, e) => EditRpnColors()) { ForeColor = Ink });
            var presets = new ToolStripMenuItem("Listing RPN : modèle") { ForeColor = Ink };
            foreach (var p in RpnColors.Presets)
            {
                var make = p.Make;
                presets.DropDownItems.Add(new ToolStripMenuItem(p.Name, null, (s, e) => { rpnColors = make(); ApplyRpnColors(); SaveViewSettings(); }) { ForeColor = Ink });
            }
            presets.DropDown.Renderer = tools.Renderer;
            presets.DropDown.BackColor = Color.FromArgb(44, 44, 41);
            view.DropDownItems.Add(presets);
            view.DropDown.Renderer = tools.Renderer;
            view.DropDown.BackColor = Color.FromArgb(44, 44, 41);
            view.DropDown.ForeColor = Ink;
            return view;
        }

        // ==================================================================
        //  Édition
        // ==================================================================
        void ClearPage()
        {
            if (pyBox.TextLength == 0) return;
            if (MessageBox.Show(this, "Effacer tout le code Python de la page ?\n(Ctrl+Z permet d'annuler.)", "Effacer la page",
                                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            pyBox.SelectAll();
            pyBox.ClearSelected();     // passe par l'historique : Ctrl+Z rétablit le code
            pyBox.Focus();
        }

        /// <summary>Ajoute ou retire « # » au début des lignes sélectionnées.</summary>
        void ToggleComment()
        {
            var sel = pyBox.Selection.Clone();
            sel.Normalize();
            int from = sel.Start.iLine, to = sel.End.iLine;
            if (to > from && sel.End.iChar == 0) to--;
            var lines = Enumerable.Range(from, to - from + 1).Select(i => pyBox.GetLineText(i)).ToList();
            bool uncomment = lines.Where(l => l.Trim().Length > 0).All(l => l.TrimStart().StartsWith("#"));
            var changed = lines.Select(l =>
            {
                if (l.Trim().Length == 0) return l;
                int ind = l.Length - l.TrimStart().Length;
                if (!uncomment) return l.Substring(0, ind) + "# " + l.Substring(ind);
                string rest = l.Substring(ind + 1);
                return l.Substring(0, ind) + (rest.StartsWith(" ") ? rest.Substring(1) : rest);
            });
            pyBox.Selection = new Range(pyBox, 0, from, pyBox.GetLineText(to).Length, to);
            pyBox.InsertText(string.Join("\r\n", changed));
            pyBox.Selection = new Range(pyBox, 0, from, pyBox.GetLineText(to).Length, to);
        }

        // ==================================================================
        //  Thème du code Python
        // ==================================================================
        void ApplyPythonTheme()
        {
            if (darkItem != null && darkItem.Checked != pyDark) darkItem.Checked = pyDark;
            void Fore(TextStyle st, Color c) => st.ForeBrush = new SolidBrush(c);
            if (pyDark)
            {
                pyBox.BackColor = Color.FromArgb(30, 30, 30);
                pyBox.ForeColor = Color.FromArgb(220, 220, 214);
                pyBox.IndentBackColor = Color.FromArgb(37, 37, 36);
                pyBox.LineNumberColor = Color.FromArgb(118, 116, 108);
                pyBox.CurrentLineColor = Color.FromArgb(50, 255, 255, 255);
                pyBox.ServiceLinesColor = Color.FromArgb(60, 60, 58);
                Fore(pyComment, Color.FromArgb(118, 150, 96));
                Fore(pyDoc, Color.FromArgb(118, 150, 96));
                Fore(pyString, Color.FromArgb(214, 157, 120));
                Fore(pyKeyword, Color.FromArgb(240, 150, 70));
                Fore(pyBuiltin, Color.FromArgb(120, 190, 240));
                Fore(pyHp, Color.FromArgb(210, 140, 220));
                Fore(pyNumber, Color.FromArgb(181, 206, 150));
                Fore(pyDef, Color.FromArgb(235, 225, 160));
            }
            else
            {
                pyBox.BackColor = Paper;
                pyBox.ForeColor = Color.FromArgb(31, 30, 27);
                pyBox.IndentBackColor = Color.FromArgb(240, 238, 230);
                pyBox.LineNumberColor = Color.FromArgb(163, 159, 148);
                pyBox.CurrentLineColor = Color.FromArgb(60, 210, 210, 255);
                pyBox.ServiceLinesColor = Color.FromArgb(220, 218, 210);
                Fore(pyComment, Color.FromArgb(138, 134, 118));
                Fore(pyDoc, Color.FromArgb(138, 134, 118));
                Fore(pyString, Color.FromArgb(61, 122, 42));
                Fore(pyKeyword, Color.FromArgb(184, 86, 27));
                Fore(pyBuiltin, Color.FromArgb(45, 94, 158));
                Fore(pyHp, Color.FromArgb(120, 40, 140));
                Fore(pyNumber, Color.FromArgb(138, 59, 143));
                Fore(pyDef, Color.Black);
            }
            pyBox.Invalidate();
        }

        // ==================================================================
        //  Couleurs du listing RPN
        // ==================================================================
        void ApplyRpnColors()
        {
            rpnBox.BackColor = rpnColors.Back;
            rpnBox.IndentBackColor = rpnColors.Back;
            rpnBox.ForeColor = last == null ? Color.FromArgb(130, rpnColors.Text) : rpnColors.Text;
            rpLineNo.ForeBrush = new SolidBrush(rpnColors.LineNo);
            rpLabel.ForeBrush = new SolidBrush(rpnColors.Label);
            rpFlow.ForeBrush = new SolidBrush(rpnColors.Flow);
            rpTest.ForeBrush = new SolidBrush(rpnColors.Test);
            rpAlpha.ForeBrush = new SolidBrush(rpnColors.Alpha);
            rpnBox.Invalidate();
        }

        void EditRpnColors()
        {
            var backup = rpnColors.Clone();
            var work = rpnColors.Clone();
            var rows = new (string Label, Func<Color> Get, Action<Color> Set)[]
            {
                ("Fond de page",          () => work.Back,   c => work.Back = c),
                ("Texte (instructions)",  () => work.Text,   c => work.Text = c),
                ("Numéros de ligne",      () => work.LineNo, c => work.LineNo = c),
                ("LBL (labels)",          () => work.Label,  c => work.Label = c),
                ("GTO, XEQ, RTN, END",    () => work.Flow,   c => work.Flow = c),
                ("Tests (X<Y?, FS?…)",    () => work.Test,   c => work.Test = c),
                ("Textes \"…\"",           () => work.Alpha,  c => work.Alpha = c),
            };

            using (var f = new Form
            {
                Text = "Couleurs du listing RPN",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false,
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Body, ForeColor = Ink, Font = new Font("Segoe UI", 9.5f),
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(12)
            })
            {
                var grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
                var swatches = new List<Button>();
                void Preview() { rpnColors = work.Clone(); ApplyRpnColors(); HighlightRpn(); }
                void Refresh() { for (int i = 0; i < rows.Length; i++) swatches[i].BackColor = rows[i].Get(); Preview(); }

                var presetBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
                presetBox.Items.AddRange(RpnColors.Presets.Select(p => (object)p.Name).ToArray());
                grid.Controls.Add(new Label { Text = "Modèle", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Muted });
                grid.Controls.Add(presetBox);

                foreach (var row in rows)
                {
                    var r = row;
                    var b = new Button { Width = 190, Height = 26, FlatStyle = FlatStyle.Flat, Text = "", Cursor = Cursors.Hand };
                    b.FlatAppearance.BorderColor = Color.FromArgb(110, 108, 100);
                    b.Click += (s, e) =>
                    {
                        using (var dlg = new ColorDialog { Color = r.Get(), FullOpen = true, AnyColor = true })
                            if (dlg.ShowDialog(f) == DialogResult.OK) { r.Set(dlg.Color); Refresh(); }
                    };
                    swatches.Add(b);
                    grid.Controls.Add(new Label { Text = r.Label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 16, 0) });
                    grid.Controls.Add(b);
                }
                presetBox.SelectedIndexChanged += (s, e) =>
                {
                    var p = RpnColors.Presets[presetBox.SelectedIndex].Make();
                    work.Back = p.Back; work.Text = p.Text; work.LineNo = p.LineNo; work.Label = p.Label; work.Flow = p.Flow; work.Test = p.Test; work.Alpha = p.Alpha;
                    Refresh();
                };

                var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, BackColor = Shift, ForeColor = Color.FromArgb(28, 27, 25), FlatStyle = FlatStyle.Flat, AutoSize = true };
                var cancel = new Button { Text = "Annuler", DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat, AutoSize = true };
                var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 0) };
                buttons.Controls.Add(cancel);
                buttons.Controls.Add(ok);
                grid.Controls.Add(new Label { Text = "La fenêtre se met à jour en direct.", AutoSize = true, ForeColor = Muted, Anchor = AnchorStyles.Left, Margin = new Padding(0, 16, 0, 0) });
                grid.Controls.Add(buttons);

                f.Controls.Add(grid);
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                Refresh();

                if (f.ShowDialog(this) == DialogResult.OK) { rpnColors = work.Clone(); SaveViewSettings(); }
                else rpnColors = backup;
                ApplyRpnColors();
                HighlightRpn();
            }
        }

        // ==================================================================
        //  Fichier .raw direct (encodeur dm42dll, comme « Encoder et enregistrer en .raw »)
        // ==================================================================
        string lastRawDir;

        void SaveRaw()
        {
            if (last == null) { status.Text = "Rien à encoder : corrigez d'abord les erreurs."; return; }
            var bytes = Py42.Raw.RawExport.Encode(last, out var errors);
            if (bytes == null)
            {
                MessageBox.Show(this, "Le listing n'a pas pu être encodé :\n\n" + string.Join("\n", errors.Distinct().Take(15)),
                                "Générer .raw", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string name = currentFile != null ? Path.GetFileNameWithoutExtension(currentFile)
                        : (progName.Text.Trim().Length > 0 ? progName.Text.Trim() : "MAIN");
            string dir = lastRawDir ?? (currentFile != null ? Path.GetDirectoryName(currentFile)
                        : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            using (var dlg = new SaveFileDialog
            {
                Title = "Enregistrer le programme pour la DM42 / Free42",
                Filter = "Programme HP-42S (*.raw)|*.raw",
                DefaultExt = ".raw",
                FileName = name + ".raw",
                InitialDirectory = dir
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    File.WriteAllBytes(dlg.FileName, bytes);
                    lastRawDir = Path.GetDirectoryName(dlg.FileName);
                    status.Text = $"{Path.GetFileName(dlg.FileName)} : {bytes.Length} octets, {last.Code.Count} pas. DM42 : copiez-le sur son disque USB puis Load Program ; Free42 : File → Import Programs.";
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Écriture impossible", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        // ==================================================================
        //  Envoi vers l'émulateur réglé dans Paramètres (presse-papiers + fenêtre au premier plan)
        // ==================================================================
        void SendToFree42()
        {
            if (last == null) { status.Text = "Rien à envoyer : corrigez d'abord les erreurs."; return; }
            status.Text = Emulator.Send(this, last.Listing(true, header: true), last.Code.Count);
        }
    }
}
