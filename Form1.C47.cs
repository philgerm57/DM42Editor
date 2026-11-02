using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using C47.Core;
using Dm42.Editor;
using FarsiLibrary.Win;
using Decoder = C47.Core.Decoder;
using Encoder = C47.Core.Encoder;
using Range = Dm42.Editor.TextRange;

namespace dm42editor
{
    /// <summary>
    /// Onglets C47 : même éditeur que les onglets DM42 (barre d'outils, menu contextuel, signets, recherche),
    /// avec la coloration, l'encodage .p47 et la liste d'instructions de la C47 (bibliothèque C47.Core).
    /// </summary>
    public partial class Form1
    {
        const string C47Starter = "@ Programme C47 : Fichier > Encoder et enregistrer en .p47 génère le fichier pour READP\r\n" +
                                  "LBL ‘CARRE’\r\n" +
                                  "  # calcule x² et le range en R00\r\n" +
                                  "  ENTER ×\r\n" +
                                  "  STO R00\r\n" +
                                  "  RTN\r\n" +
                                  "END\r\n";

        // chaînes et commentaires lus ensemble, de gauche à droite : le ; d'une chaîne ne commence pas un commentaire
        static readonly Regex C47StringOrComment = new Regex(@"^[ \t]*@.*$|‘[^’\n]*’|""[^""\n]*""|;.*$|(?<!\S)#([ \t].*)?$", RegexOptions.Multiline | RegexOptions.Compiled);
        static readonly Regex C47Test = new Regex(@"(?<!\S)\S+\?(?!\S)", RegexOptions.Compiled);
        static readonly Regex C47Number = new Regex(@"(?<!\S)-?(\d+\.?\d*|\.\d+)([eE][-+]?\d+)?(?!\S)", RegexOptions.Compiled);
        readonly MarkerStyle c47ErrorStyle = new MarkerStyle(new SolidBrush(Color.FromArgb(90, 214, 64, 40)));

        ListViewItem[] dm42Functions, c47Functions;
        bool? listShowsC47;   // null : liste pas encore rangée en sections

        /// <summary>
        /// Pas de menu à part : « Nouveau programme C47 » sous « Nouveau programme HP42 » (Fichier > Ouvrir lit déjà les .p47 / .p47u)
        /// et « Instructions C47 » dans Aide, à côté de la liste HP-42S.
        /// </summary>
        private void InitC47Menu()
        {
            int at = fileToolStripMenuItem.DropDownItems.IndexOf(newProgramToolStripMenuItem);
            fileToolStripMenuItem.DropDownItems.Insert(at + 1, new ToolStripMenuItem("Nouveau programme C47", null, (s, e) => NewC47Tab()));

            at = helpToolStripMenuItem.DropDownItems.IndexOf(equivalenceToolStripMenuItem);
            helpToolStripMenuItem.DropDownItems.Insert(at + 1, new ToolStripMenuItem("Instructions C47", null, (s, e) => C47Help.Show(this, InsertInC47Tab)));

            InitFunctionListMenu();
            InitFunctionFilter();
            InitHints();
            InitSymbolBar();
            SyncWithTabType();   // liste HP-42S rangée en sections dès le démarrage
        }

        static bool IsC47(CodeEditor tb) => tb?.Tag is TbInfo info && info.C47;

        void NewC47Tab()
        {
            InitNewDataTab(C47Starter, null, c47: true);
            CurrentTB.IsChanged = false;
        }

        /// <summary>Ouvre une source .p47u, ou décode un .p47 dans un onglet à enregistrer comme source.</summary>
        void OpenC47File(string path)
        {
            try
            {
                if (string.Equals(Path.GetExtension(path), ".p47", StringComparison.OrdinalIgnoreCase))
                {
                    var lines = Decoder.Decode(P47File.Read(File.ReadAllBytes(path)));
                    // pas de chemin : « Enregistrer » demande où ranger la source, pour ne pas écraser un .p47u existant
                    InitNewDataTab(string.Join("\r\n", lines) + "\r\n", null, c47: true);
                    tsFiles.SelectedItem.Title = Path.GetFileNameWithoutExtension(path) + ".p47u";
                }
                else
                {
                    InitNewDataTab(C47Text.Plain(File.ReadAllText(path, Encoding.UTF8)), path, c47: true);
                }
                CurrentTB.IsChanged = false;
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is FormatException || ex is OverflowException || ex is UnauthorizedAccessException)
            {
                MessageBox.Show(this, C47.Core.Tr.T(ex.Message), "C47", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void InsertInC47Tab(string text)
        {
            var tb = CurrentSourceTB;   // depuis l'onglet des octets, l'insertion va dans sa source
            if (!IsC47(tb)) { lbWordUnderMouse.Text = Lang.T("Ouvrez d'abord un onglet C47."); return; }
            if (tb != CurrentTB) CurrentTB = tb;
            tb.InsertText(text);
            tb.Focus();
        }

        // ------------------------------------------------------------------
        //  Coloration et contrôle pendant la saisie
        // ------------------------------------------------------------------

        /// <summary>Coloration C47 avec les couleurs du schéma choisi (le texte normal est déjà de la couleur des mots-clés).</summary>
        void HighlightC47(Range r)
        {
            r.ClearStyle(CommentStyle, StringStyle, TestStyle, NumberStyle, WordStyle);
            var none = new Range(r.TB, 0, 0, 0, 0);
            none.SetStyle(CommentStyle);   // ordre des styles : commentaire et chaîne l'emportent sur le reste
            none.SetStyle(StringStyle);
            foreach (var m in r.GetRanges(C47StringOrComment))
                m.SetStyle(m.Text.StartsWith("‘") || m.Text.StartsWith("\"") ? StringStyle : CommentStyle);
            r.SetStyle(TestStyle, C47Test);
            r.SetStyle(NumberStyle, C47Number);
        }

        /// <summary>Encode l'onglet, marque les lignes en erreur et résume le résultat dans la barre d'état.</summary>
        void CheckC47(CodeEditor tb)
        {
            var info = (TbInfo)tb.Tag;
            info.C47Result = Encoder.Encode(tb.Text);
            tb.Range.ClearStyle(c47ErrorStyle);
            foreach (var e in info.C47Result.Errors.Where(x => x.Line > 0 && x.Line <= tb.LinesCount))
                new Range(tb, 0, e.Line - 1, tb.GetLineLength(e.Line - 1), e.Line - 1).SetStyle(c47ErrorStyle);
            tb.Invalidate();
            if (tb == CurrentSourceTB) lbWordUnderMouse.Text = C47Status(tb, tb.Selection.Start.iLine);
            // l'onglet des octets suit la source
            foreach (var tab in C47ViewerTabs(tb).ToList()) FillC47Viewer((RawViewer)tab.Controls[0], tb);
        }

        /// <summary>Erreur de la ligne si elle en a une, sinon nombre d'erreurs ou taille du programme.</summary>
        string C47Status(CodeEditor tb, int iLine)
        {
            var r = (tb.Tag as TbInfo)?.C47Result;
            if (r == null) return "";
            var here = r.Errors.FirstOrDefault(e => e.Line == iLine + 1);
            if (here != null) return Lang.F("Ligne {0} : {1}", here.Line, C47.Core.Tr.T(here.Message));
            if (!r.Ok)
            {
                var first = r.Errors[0];
                return Lang.F("{0} erreur(s) ; la première, ligne {1} : {2}", r.Errors.Count, first.Line, C47.Core.Tr.T(first.Message));
            }
            return Lang.F("C47 : {0} pas, {1} octets", r.Steps, r.Program.Length);
        }

        void BuildC47Autocomplete(AutocompleteMenu popupMenu)
        {
            popupMenu.SetItems(C47Help.Listed().Where(i => i.Shown.Length > 1 || Items.TypedForms(i).Any()).GroupBy(i => i.Shown).Select(g => g.First())
                                      .OrderBy(i => i.Shown, StringComparer.OrdinalIgnoreCase)
                                      .Select(i => new AutocompleteItem(i.Shown)
                                      {
                                          Description = C47Help.Short(i, 60), ToolTipText = Items.Describe(i),
                                          Aliases = Items.TypedForms(i).ToArray(),   // x<>y, ABS, ->POLAR… retrouvent x⇄y, |x|, →POLAR
                                      }));
            // tout sauf les blancs, textes et commentaires : x<>, ->, |x|, STO* font partie du mot tapé
            popupMenu.SearchPattern = @"[^\s‘’""'@;]";
        }

        // ------------------------------------------------------------------
        //  Enregistrement, encodage et visualisation des octets
        // ------------------------------------------------------------------

        /// <summary>« Encoder et enregistrer » d'un onglet C47 : refusé tant qu'il reste des erreurs.</summary>
        void SaveC47P47(CodeEditor tb)
        {
            CheckC47(tb);
            var r = ((TbInfo)tb.Tag).C47Result;
            if (!r.Ok)
            {
                MessageBox.Show(this, Lang.F("Le programme contient {0} erreur(s) : corrigez-les avant de générer le .p47.", r.Errors.Count) + "\n\n"
                    + string.Join("\n", r.Errors.Take(10).Select(e => Lang.F("Ligne {0} : {1}", e.Line, C47.Core.Tr.T(e.Message)))), "C47", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (r.Program.Length == 0) { MessageBox.Show(this, Lang.T("Aucun programme !")); return; }

            string source = tb.Parent?.Tag as string;
            sfdMain.Title = Lang.T("Enregistrer le programme pour la C47");
            sfdMain.Filter = Lang.T("Programme C47 (*.p47)|*.p47");
            sfdMain.FilterIndex = 1;
            sfdMain.DefaultExt = ".p47";
            sfdMain.InitialDirectory = source != null ? Path.GetDirectoryName(source) : "";
            sfdMain.FileName = (source != null ? Path.GetFileNameWithoutExtension(source) : C47ProgramName(tb)) + ".p47";
            if (sfdMain.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                File.WriteAllBytes(sfdMain.FileName, P47File.Write(r.Program));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                MessageBox.Show(this, Lang.F("Impossible d'écrire {0}\n{1}", sfdMain.FileName, ex.Message), Lang.T("Erreur"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            lbWordUnderMouse.Text = Lang.F("{0} enregistré : {1} pas, {2} octets", Path.GetFileName(sfdMain.FileName), r.Steps, r.Program.Length);
        }

        /// <summary>F7 sur un onglet C47 : encode, copie le .p47 dans le dossier PROGRAMS de l'émulateur C47 et l'affiche.</summary>
        void SendC47ToSimulator(CodeEditor tb)
        {
            CheckC47(tb);
            var r = ((TbInfo)tb.Tag).C47Result;
            if (!r.Ok)
            {
                MessageBox.Show(this, Lang.F("Le programme contient {0} erreur(s) : corrigez-les avant de l'envoyer.", r.Errors.Count) + "\n\n"
                    + string.Join("\n", r.Errors.Take(10).Select(e => Lang.F("Ligne {0} : {1}", e.Line, C47.Core.Tr.T(e.Message)))), Lang.T("Émulateur"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (r.Program.Length == 0) { lbWordUnderMouse.Text = Lang.T("Rien à envoyer : l'onglet ne contient pas d'instruction."); return; }
            lbWordUnderMouse.Text = Emulator.SendC47(this, P47File.Write(r.Program), C47ProgramName(tb));
        }

        /// <summary>Première étiquette globale du programme, nom proposé par défaut comme WRITEP.</summary>
        static string C47ProgramName(CodeEditor tb)
        {
            var m = Regex.Match(tb.Text, @"^\s*LBL\s+[‘""']([^’""']+)[’""']", RegexOptions.Multiline);
            string name = m.Success ? m.Groups[1].Value : "untitled";
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }

        /// <summary>Onglet des octets : chaque pas décodé, ses octets et sa ligne source (double-clic pour y aller).</summary>
        void ViewC47Bytes(CodeEditor tb)
        {
            CheckC47(tb);   // remplit aussi un onglet d'octets déjà ouvert pour cette source
            var tab = C47ViewerTabs(tb).FirstOrDefault();
            if (tab == null)
            {
                var viewer = new RawViewer { SetTabStrib = tsFiles, Dock = DockStyle.Fill, Tag = tb.Parent, BytesTitle = Lang.T("Octets .p47") };
                FillC47Viewer(viewer, tb);
                tab = new FATabStripItem(Path.ChangeExtension(tsFiles.SelectedItem.Title, "Encode"), viewer);
                tsFiles.AddTab(tab);
            }
            tsFiles.SelectedItem = tab;
        }

        /// <summary>Onglets d'octets ouverts pour cet onglet source.</summary>
        IEnumerable<FATabStripItem> C47ViewerTabs(CodeEditor tb) =>
            tsFiles.Items.Cast<FATabStripItem>().Where(t => t.Controls.Count > 0 && t.Controls[0] is RawViewer v && v.Tag == tb.Parent);

        /// <summary>Un pas par ligne : texte décodé, octets, et ligne source pour le double-clic ; les erreurs sinon.</summary>
        void FillC47Viewer(RawViewer viewer, CodeEditor tb)
        {
            var r = ((TbInfo)tb.Tag).C47Result;
            viewer.Refill(() =>
            {
                viewer.Clear();
                if (!r.Ok)
                {
                    foreach (var e in r.Errors) viewer.Add(tb.GetLineText(e.Line - 1).Trim(), "", C47.Core.Tr.T(e.Message), e.Line - 1);
                    return;
                }
                for (int k = 0; k < r.StepLines.Count; k++)
                {
                    int from = r.StepLines[k].Offset;
                    int to = k + 1 < r.StepLines.Count ? r.StepLines[k + 1].Offset : r.Program.Length;
                    var bytes = r.Program.Skip(from).Take(to - from).ToArray();
                    string text = Decoder.Decode(bytes).FirstOrDefault() ?? "";
                    viewer.Add(text, string.Join(" ", bytes.Select(b => b.ToString("X2"))), "", r.StepLines[k].Line - 1);
                }
            });
        }

        /// <summary>Éditeur de l'onglet courant, ou celui de la source quand l'onglet courant montre ses octets.</summary>
        CodeEditor CurrentSourceTB
        {
            get
            {
                var tab = tsFiles.SelectedItem;
                if (EditorOf(tab) is CodeEditor tb) return tb;
                return tab != null && tab.Controls.Count > 0 && tab.Controls[0] is RawViewer v && v.Tag is FATabStripItem src ? EditorOf(src) : null;
            }
        }

        /// <summary>Copie le programme sans commentaires : lignes @, fins de ligne ; et commentaires # retirés.</summary>
        static string C47WithoutComments(string text)
        {
            var o = new List<string>();
            foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
            {
                if (line.TrimStart().StartsWith("@")) continue;
                string code = C47StringOrComment.Replace(line, m => m.Value.StartsWith("‘") || m.Value.StartsWith("\"") ? m.Value : "").TrimEnd();
                if (code.Trim().Length > 0) o.Add(code);
            }
            return string.Join("\r\n", o);
        }

        // ------------------------------------------------------------------
        //  Liste des fonctions : HP-42S ou C47 selon l'onglet
        // ------------------------------------------------------------------

        /// <summary>Met la liste des fonctions, les libellés d'encodage et les commandes propres au DM42 en accord avec l'onglet.</summary>
        void SyncWithTabType()
        {
            var source = CurrentSourceTB;   // un onglet d'octets prend le type de sa source
            bool c47 = IsC47(source);
            if (c47 && CurrentTB == null) lbWordUnderMouse.Text = C47Status(source, source.Selection.Start.iLine);
            string encode = Lang.T(c47 ? "Encoder et enregistrer en .p47…" : "Encoder et enregistrer en .raw…");
            toolStripMenuItem2.Text = encodeAndSaveRAWToolStripMenuItem.Text = encode;
            viewRawDataToolStripMenuItem.Text = Lang.T(c47 ? "Voir les octets .p47" : "Voir les octets .raw");
            viewCodeLineToolStripMenuItem.Enabled = toolStripMenuItem8.Enabled = !c47;   // numérotation des pas HP-42S
            if (symbolBar != null) symbolBar.Visible = c47;                               // barre de symboles C47
            if (hp41Menu != null) hp41Menu.Enabled = hp41Popup.Enabled = !c47;          // conversion HP-41 → DM42
            if (listShowsC47 == c47) return;

            if (dm42Functions == null) dm42Functions = GroupDm42Functions(out dm42Groups);
            if (c47 && c47Functions == null) c47Functions = BuildC47Functions(out c47Groups);
            ListViewFct.BeginUpdate();
            ListViewFct.Items.Clear();
            ListViewFct.Groups.Clear();
            // mêmes objets de section à chaque retour : une section ouverte le reste
            ListViewFct.Groups.AddRange(c47 ? c47Groups : dm42Groups);
            ListViewFct.ShowGroups = true;
            ListViewFct.EndUpdate();
            listShowsC47 = c47;
            FillFunctionList();   // en gardant le texte cherché (Form1.Hints.cs)
        }

        ListViewGroup[] dm42Groups, c47Groups;

        /// <summary>Section de chaque fonction : la liste l'oublie quand un élément la quitte (filtre), FillFunctionList la remet.</summary>
        static readonly Dictionary<ListViewItem, ListViewGroup> itemGroups = new Dictionary<ListViewItem, ListViewGroup>();

        /// <summary>Sections repliables, fermées au départ ; le titre donne le nombre d'instructions.</summary>
        static ListViewGroup[] MakeGroups(IEnumerable<ListViewItem> items, Func<ListViewItem, string> category)
        {
            var groups = new List<ListViewGroup>();
            foreach (string c in Items.Categories)
            {
                var members = items.Where(it => category(it) == c).ToList();
                if (members.Count == 0) continue;
                var g = new ListViewGroup(c, "") { Tag = Lang.T(c) + "  (" + members.Count + ")" };
                SetGroupState(g, true);
                foreach (var it in members) it.Group = itemGroups[it] = g;
                groups.Add(g);
            }
            return groups.ToArray();
        }

        /// <summary>Ouvre ou ferme une section ; ► / ▼ devant le titre, Windows n'affichant pas toujours sa flèche.</summary>
        static void SetGroupState(ListViewGroup g, bool collapsed)
        {
            g.CollapsedState = collapsed ? ListViewGroupCollapsedState.Collapsed : ListViewGroupCollapsedState.Expanded;
            g.Header = (collapsed ? "► " : "▼ ") + g.Tag;
        }

        /// <summary>Menu de la liste des fonctions : tout ouvrir, tout fermer ; le titre suit aussi un clic sur la section.</summary>
        void InitFunctionListMenu()
        {
            var menu = new ContextMenuStrip();
            void All(bool collapsed)
            {
                ListViewFct.BeginUpdate();
                foreach (ListViewGroup g in ListViewFct.Groups) SetGroupState(g, collapsed);
                ListViewFct.EndUpdate();
            }
            menu.Items.Add(new ToolStripMenuItem("Tout ouvrir", null, (s, e) => All(false)));
            menu.Items.Add(new ToolStripMenuItem("Tout fermer", null, (s, e) => All(true)));
            menu.Opening += (s, e) => { if (AppTheme.Dark) AppTheme.Strip(menu); };
            ListViewFct.ContextMenuStrip = menu;
            ListViewFct.GroupCollapsedStateChanged += (s, e) =>
            {
                var g = ListViewFct.Groups[e.GroupIndex];
                SetGroupState(g, g.CollapsedState == ListViewGroupCollapsedState.Collapsed);
            };
        }

        // groupe de liste_fonctions.csv -> domaine de l'aide C47 (le fichier a des groupes vides et quelques fautes)
        static readonly Dictionary<string, string> Dm42GroupDomains = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MATRICE"] = "Matrices et vecteurs", ["MATRIX"] = "Matrices et vecteurs", ["STAT"] = "Statistiques et probabilités",
            ["PROB"] = "Statistiques et probabilités", ["PRINT"] = "Entrées, sorties et impression", ["ALPHA"] = "Texte (ALPHA)",
            ["CLEAR"] = "Pile, registres et mémoire", ["BASE"] = "Entiers, bases et bits", ["TRIGO"] = "Trigonométrie et angles",
            ["MODE"] = "Affichage et modes", ["DISPLAY"] = "Affichage et modes", ["PRGM"] = "Programmation", ["PGM.FCN"] = "Programmation",
            ["FLAGS"] = "Tests et indicateurs", ["COMPLEX"] = "Nombres complexes", ["SOLVER"] = "Solveur, intégrales, dérivées",
            ["CONVERT"] = "Fonctions mathématiques", ["CONVEERT"] = "Fonctions mathématiques", ["MATH"] = "Fonctions mathématiques",
        };

        /// <summary>Domaine d'une fonction HP-42S sans groupe dans le fichier, d'après son nom (écritures ASCII de liste_fonctions.csv).</summary>
        static string Dm42NameDomain(string name)
        {
            if (Regex.IsMatch(name, @"^\d{2}(-\d{2})?$")) return "Tests et indicateurs";   // pages des indicateurs 00-99
            if (Regex.IsMatch(name, @"^(X\^2|Y\^X|1/X|10\^X|E\^X(-1)?|LN1\+X|\+/-|x|/|÷|\+|-|%|%CH|SIGN|SQRT|LN|LOG|SIN|SINH|TAN|TANH)$")) return "Fonctions mathématiques";
            if (Regex.IsMatch(name, @"^(>DEG|>RAD|>POL|>REC|>HMS|>HR)$")) return "Trigonométrie et angles";
            if (Regex.IsMatch(name, @"^(>DEC|>OCT)$")) return "Entiers, bases et bits";
            if (Regex.IsMatch(name, @"^(ENTER|LASTX|R\^|Rv|X<>|X<>Y|RCL.?|STO.?|STO×.*|SIZE)$")) return "Pile, registres et mémoire";
            if (Regex.IsMatch(name, @"^(S\+|S-|SREG\??|CORR|YINT|SEED)$")) return "Statistiques et probabilités";
            if (Regex.IsMatch(name, @"^(ADATE|ATIME|AT24|CL12|CL24|DATE\+?|DDAYS|DMY|DOW|MDY|TIME|HMS[+-])$")) return "Date, heure et chronomètre";
            if (Regex.IsMatch(name, @"^(GTO\.{0,2}|XEQ|LBL|RTN|R/S|SST|STOP|PSE|DSE|ISG|INPUT|VARMENU|MENU|KEYG|KEYX|GETKEY|CUSTOM|EXITALL|PGMINT|INTEG)$")) return "Programmation";
            if (Regex.IsMatch(name, @"^(RDX[,.]|MAN|SCI|SHOW|VIEW)$")) return "Affichage et modes";
            if (Regex.IsMatch(name, @"^(NEWMAT|STOEL|STOIJ|RNRM|FRNM|MAT\?)$")) return "Matrices et vecteurs";
            if (Regex.IsMatch(name, @"^(RECT|POLAR)$")) return "Nombres complexes";
            if (Regex.IsMatch(name, @"^(E|PI)$")) return "Constantes";
            if (Regex.IsMatch(name, @"^(ON|OFF|TONE|XTOA)$")) return "Entrées, sorties et impression";
            return null;
        }

        /// <summary>
        /// Fonctions HP-42S rangées dans les mêmes domaines que la C47 : groupe du fichier s'il existe, sinon domaine de
        /// l'instruction C47 du même nom, sinon « Autres ». La ligne d'en-tête du fichier n'est plus une fonction.
        /// </summary>
        ListViewItem[] GroupDm42Functions(out ListViewGroup[] groups)
        {
            var items = ListViewFct.Items.Cast<ListViewItem>().Skip(1).ToArray();   // ligne 1 : Nom;Description;…
            ListViewFct.Items.Clear();   // un élément encore dans la liste ne peut pas changer de section
            var category = new Dictionary<ListViewItem, string>();
            foreach (var it in items)
            {
                string g = it.SubItems.Count > 4 ? it.SubItems[4].Text.Trim() : "";
                string name = it.Text.Trim();
                if (Dm42GroupDomains.TryGetValue(g, out string d)) category[it] = d;
                else if (Dm42NameDomain(name) is string byName) category[it] = byName;
                else if (Items.Find(name) is Item c47 && c47.Kind != ParamKind.Disabled) category[it] = Items.Help(c47).Category;
                else if (name.EndsWith("?")) category[it] = "Tests et indicateurs";
                else category[it] = "Autres";
            }
            var sorted = items.OrderBy(it => Array.IndexOf(Items.Categories, category[it])).ThenBy(it => it.Text, StringComparer.OrdinalIgnoreCase).ToArray();
            groups = MakeGroups(sorted, it => category[it]);
            return sorted;
        }

        ListViewItem[] BuildC47Functions(out ListViewGroup[] groups)
        {
            var list = new List<ListViewItem>();
            foreach (var i in C47Help.Listed().OrderBy(i => Array.IndexOf(Items.Categories, Items.Help(i).Category)).ThenBy(i => i.Shown, StringComparer.OrdinalIgnoreCase))
            {
                var h = Items.Help(i);
                var it = new ListViewItem(i.Shown) { Tag = i, ToolTipText = C47Help.Full(i) };
                it.SubItems.Add(C47Help.Short(i, 90));
                it.SubItems.Add("");
                it.SubItems.Add(C47Help.ParamHint(i));
                it.SubItems.Add(new ListViewItem.ListViewSubItem(it, Lang.T(h.Category)) { Tag = h.Category });
                list.Add(it);
            }
            groups = MakeGroups(list, it => (string)it.SubItems[4].Tag);
            return list.ToArray();
        }
    }
}
