using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using C47.Core;
using Dm42.Editor;

namespace dm42editor
{
    /// <summary>
    /// Aides à la lecture et à la saisie : infobulle du mot sous la souris (instruction ou symbole),
    /// descriptions de l'autocomplétion HP-42S, et filtre de recherche au-dessus de la liste des fonctions.
    /// </summary>
    public partial class Form1
    {
        // ------------------------------------------------------------------
        //  Infobulle du mot sous la souris
        // ------------------------------------------------------------------

        readonly ToolTip hintTip = new ToolTip { UseAnimation = false, UseFading = false, ShowAlways = true };
        readonly Timer hintTimer = new Timer { Interval = 600 };
        CodeEditor hintTb;
        string hintKey, hintText;
        Point hintPoint;

        void InitHints()
        {
            hintTimer.Tick += (s, e) =>
            {
                hintTimer.Stop();
                if (hintTb != null && !hintTb.IsDisposed && hintText != null)
                    hintTip.Show(hintText, hintTb, hintPoint.X + 12, hintPoint.Y + hintTb.CharHeight + 4, 15000);
            };
        }

        /// <summary>Après un temps d'arrêt sur un mot, affiche ce qu'il désigne ; rien sur un blanc ou un commentaire.</summary>
        void UpdateHint(CodeEditor tb, Point mouse)
        {
            var at = tb.CharAt(mouse);
            var place = at ?? new Place(0, 0);
            string line = tb.GetLineText(place.iLine);
            int i = place.iChar;
            bool onText = at != null && !char.IsWhiteSpace(line[i]);
            int start = i, end = i;
            if (onText)
            {
                while (start > 0 && !char.IsWhiteSpace(line[start - 1])) start--;
                while (end < line.Length && !char.IsWhiteSpace(line[end])) end++;
            }
            string key = onText ? place.iLine + ":" + start + ":" + line.Substring(start, end - start) : null;
            if (key == hintKey && tb == hintTb) return;
            hintKey = key;
            hintTimer.Stop();
            if (hintTb != null && !hintTb.IsDisposed) hintTip.Hide(hintTb);
            hintTb = tb;
            hintText = onText ? (IsC47(tb) ? C47Hint(line, start, end) : Dm42Hint(line.Substring(start, end - start))) : null;
            if (hintText == null) return;
            hintPoint = mouse;
            hintTimer.Start();
        }

        void HideHint()
        {
            hintTimer.Stop();
            hintKey = null;
            if (hintTb != null && !hintTb.IsDisposed) hintTip.Hide(hintTb);
        }

        /// <summary>Instruction C47, ou rôle du symbole (registre, indicateur, nombre) d'après l'instruction qui le précède.</summary>
        static string C47Hint(string line, int start, int end)
        {
            string tok = line.Substring(start, end - start);
            foreach (Match m in C47StringOrComment.Matches(line))
                if (start >= m.Index && start < m.Index + m.Length)
                {
                    if (m.Value.TrimStart().StartsWith("#")) return Lang.T("Remarque : reste dans le programme (pas REM).");
                    if (m.Value.StartsWith("‘") || m.Value.StartsWith("\"")) break;   // texte : peut être le paramètre d'une instruction
                    return null;   // commentaire @ ou ;
                }

            if (tok == "→" || tok == "IND") return Lang.T("Indirection : le paramètre qui suit (R05, ‘nom’) contient l'adresse réelle.");
            var prev = Regex.Matches(line.Substring(0, start), @"\S+").Cast<Match>().Select(m => m.Value).LastOrDefault();
            bool detached = prev == "IND" || prev == "→";   // RCL → R05 : la flèche détachée vaut →R05
            if (detached) prev = Regex.Matches(line.Substring(0, start), @"\S+").Cast<Match>().Select(m => m.Value).Reverse().Skip(1).FirstOrDefault();
            var owner = prev != null ? Items.Find(prev) : null;
            if (owner != null && owner.Kind != ParamKind.None && owner.Kind != ParamKind.Disabled && owner.Kind != ParamKind.Literal)
                return ParamHint(owner, detached ? "→" + tok : tok);

            var item = Items.Find(tok);
            // un nombre (5, 1.5, 3₁₆) n'est pas une instruction ; 42KEY, 1/x ou 10ˣ en sont
            if (item != null && item.Kind != ParamKind.Disabled && LiteralHint(tok) == null) return C47Help.Full(item);
            return LiteralHint(tok);
        }

        /// <summary>Paramètre d'une instruction : registre, indicateur, étiquette ou nombre.</summary>
        static string ParamHint(Item owner, string tok)
        {
            string head = Lang.F("Paramètre de {0}", owner.Shown) + "\r\n";
            switch (owner.Kind)
            {
                case ParamKind.Register:
                case ParamKind.Compare:
                    if (tok == "0." || tok == "1.") return head + Lang.F("Comparaison avec la valeur {0}", tok);
                    return head + RegisterHint(tok, false);
                case ParamKind.Flag:
                    return head + RegisterHint(tok, true);
                case ParamKind.Label:
                case ParamKind.DeclareLabel:
                    if (tok.StartsWith("→")) return head + RegisterHint(tok, false);
                    return head + (tok.StartsWith("‘") || tok.StartsWith("\"") ? Lang.F("Étiquette globale {0}", tok) : Lang.F("Étiquette locale {0}", tok));
                default:
                    string hint = C47Help.ParamHint(owner);
                    return head + (hint != "" ? hint : tok);
            }
        }

        /// <summary>R07, R.02, →R05, F01, X… : ce que désigne l'adresse.</summary>
        static string RegisterHint(string tok, bool flag)
        {
            if (tok.StartsWith("→"))
            {
                string target = tok.Substring(1);
                return target.StartsWith("‘") || target.StartsWith("\"")
                    ? Lang.F("Adressage indirect : l'adresse est dans la variable {0}", target)
                    : Lang.F("Adressage indirect : l'adresse est dans le registre {0}", target);
            }
            if (tok.StartsWith("‘") || tok.StartsWith("\"")) return flag ? Lang.F("Indicateur système {0}", tok) : Lang.F("Variable nommée {0}", tok);
            var m = Regex.Match(tok, @"^[RF]?(\.?)(\d{1,2})$");
            if (m.Success)
            {
                bool local = m.Groups[1].Value == ".";
                string n = (local ? "." : "") + m.Groups[2].Value.PadLeft(2, '0');
                if (flag) return local ? Lang.F("Indicateur local {0}", n) : Lang.F("Indicateur {0}", n);
                return local ? Lang.F("Registre local {0} (créé par LocR)", n) : Lang.F("Registre {0}", n);
            }
            if (tok.Length == 1)
            {
                char c = tok[0];
                if (flag) return Lang.F("Indicateur {0}", c);
                if ("XYZT".IndexOf(c) >= 0) return Lang.F("Registre {0} de la pile", c);
                if (c == 'L') return Lang.T("Registre L (LASTx)");
                return Lang.F("Registre lettré {0}", c);
            }
            return null;
        }

        /// <summary>Nombre saisi seul : type de littéral rangé dans le programme.</summary>
        static string LiteralHint(string tok)
        {
            var m = Regex.Match(tok, @"^-?[0-9A-F]+(?:#(\d+)|([₀-₉]+))$");
            if (m.Success)
            {
                string b = m.Groups[1].Success ? m.Groups[1].Value : string.Concat(m.Groups[2].Value.Select(c => (char)('0' + c - '₀')));
                return Lang.F("Entier court en base {0}", b);
            }
            if (Regex.IsMatch(tok, @"^-?\d+$")) return Lang.T("Entier long");
            if (Regex.IsMatch(tok, @"^-?(\d+\.?\d*|\.\d+)([eE][-+]?\d+)?$")) return Lang.T("Nombre réel");
            if (Regex.IsMatch(tok, @"^-?[\d.]+(e[-+]?\d+)?[°ʳᵍⱦ]$")) return Lang.T("Angle");
            return null;
        }

        /// <summary>Fonction HP-42S de liste_fonctions.csv : description et touche.</summary>
        string Dm42Hint(string tok)
        {
            tok = tok.Trim('"');
            if (!Dm42Functions.TryGetValue(tok, out var f)) return null;
            return f.Name + "\r\n" + f.Description + (f.Key != "" ? "\r\n" + Lang.T("Touche") + " : " + f.Key : "");
        }

        Dictionary<string, (string Name, string Description, string Key)> dm42Help;

        /// <summary>Fonctions HP-42S dans la langue de l'interface : nom, description, touche.</summary>
        Dictionary<string, (string Name, string Description, string Key)> Dm42Functions
        {
            get
            {
                if (dm42Help != null) return dm42Help;
                dm42Help = new Dictionary<string, (string, string, string)>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    string file = Path.Combine(Application.StartupPath, Lang.DataFile("liste_fonctions.csv"));
                    foreach (var line in File.ReadLines(file, Encoding.UTF8).Skip(1))
                    {
                        var f = line.Split(';');
                        if (f[0].Trim() == "") continue;
                        dm42Help.TryAdd(f[0].Trim(), (f[0].Trim(), f.Length > 1 ? f[1].Trim() : "", f.Length > 2 ? f[2].Trim() : ""));
                    }
                }
                catch (IOException) { }
                return dm42Help;
            }
        }

        // ------------------------------------------------------------------
        //  Filtre de la liste des fonctions
        // ------------------------------------------------------------------

        TextBox fctFilter;
        /// <summary>Sections fermées avant la recherche, rouvertes telles quelles quand le filtre est vidé.</summary>
        readonly Dictionary<ListViewGroup, bool> collapsedBeforeFilter = new Dictionary<ListViewGroup, bool>();

        void InitFunctionFilter()
        {
            fctFilter = new TextBox { Dock = DockStyle.Top, PlaceholderText = Lang.T("Rechercher une fonction : nom ou description…") };
            splitContainer2.Panel2.Controls.Add(fctFilter);
            ListViewFct.BringToFront();   // la liste remplit la place laissée par le filtre
            fctFilter.TextChanged += (s, e) => FillFunctionList();
            fctFilter.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) { fctFilter.Clear(); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Down && ListViewFct.Items.Count > 0)
                {
                    ListViewFct.Focus();
                    ListViewFct.Items[0].Selected = ListViewFct.Items[0].Focused = true;
                    e.SuppressKeyPress = true;
                }
            };
        }

        /// <summary>Liste du type d'onglet courant, réduite aux fonctions dont le nom ou la description contient le texte cherché.</summary>
        void FillFunctionList()
        {
            var all = listShowsC47 == true ? c47Functions : dm42Functions;
            if (all == null) return;
            string q = fctFilter?.Text.Trim() ?? "";
            ListViewFct.BeginUpdate();
            ListViewFct.Items.Clear();
            foreach (var it in all)
                if (itemGroups.TryGetValue(it, out var g)) it.Group = g;
            if (q == "")
            {
                foreach (ListViewGroup g in ListViewFct.Groups)
                    if (collapsedBeforeFilter.TryGetValue(g, out bool c)) SetGroupState(g, c);
                collapsedBeforeFilter.Clear();
                ListViewFct.Items.AddRange(all);
            }
            else
            {
                foreach (ListViewGroup g in ListViewFct.Groups)
                {
                    collapsedBeforeFilter.TryAdd(g, g.CollapsedState == ListViewGroupCollapsedState.Collapsed);
                    SetGroupState(g, false);   // résultats visibles sans ouvrir les sections
                }
                ListViewFct.Items.AddRange(all.Where(it => FunctionMatches(it, q)).ToArray());
            }
            ListViewFct.EndUpdate();
        }

        static readonly CompareInfo Compare = CultureInfo.InvariantCulture.CompareInfo;

        /// <summary>Nom, colonnes ou description complète, sans tenir compte des majuscules ni des accents.</summary>
        static bool FunctionMatches(ListViewItem it, string q)
        {
            bool Has(string s) => s != null && Compare.IndexOf(s, q, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
            return Has(it.Text) || it.SubItems.Cast<ListViewItem.ListViewSubItem>().Any(s => Has(s.Text)) || Has(it.ToolTipText);
        }
    }
}
