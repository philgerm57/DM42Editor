using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Dm42.Editor;

namespace dm42editor
{
    /// <summary>
    /// Saisie des symboles C47 : barre de symboles cliquables sous la barre d'outils (onglets C47 seulement)
    /// et remplacement automatique des suites tapées au clavier (-> devient →, <> devient ⇄…).
    /// </summary>
    public partial class Form1
    {
        /// <summary>Suite tapée et symbole qui la remplace aussitôt, hors textes et commentaires.</summary>
        static readonly (string Typed, string Symbol)[] AutoSymbols =
        {
            ("->", "→"), ("<>", "⇄"), ("<=", "≤"), (">=", "≥"), ("!=", "≠"), ("^2", "²"), ("^3", "³"),
        };

        /// <summary>Symbole inséré, et écriture au clavier rappelée dans l'info-bulle (vide : pas d'équivalent).</summary>
        static readonly (string Symbol, string Typed, string Help)[] SymbolButtons =
        {
            ("→", "->", "Indirection (RCL →R05), conversions (→POLAR)"),
            ("⇄", "<>", "Échange (x⇄y, x⇄ R05)"),
            ("≤", "<=", "Test inférieur ou égal (x≤?)"),
            ("≥", ">=", "Test supérieur ou égal (x≥?)"),
            ("≠", "!=", "Test différent (x≠?)"),
            ("×", "*", "Multiplication (STO×, RCL×)"),
            ("÷", "/", "Division (STO÷, RCL÷)"),
            ("²", "^2", "Carré (x²)"),
            ("³", "^3", "Cube (x³)"),
            ("ˣ", "^x", "Puissance (yˣ, 10ˣ)"),
            ("√", "sqrt", "Racine (√x)"),
            ("π", "pi", "Pi"),
            ("ⅇ", "e", "Constante e (ⅇˣ)"),
            ("|x|", "abs", "Valeur absolue"),
            ("↑", "^", "Vers le haut (R↑)"),
            ("↓", "v", "Vers le bas (R↓)"),
            ("Σ", "", "Somme (Σ+, Σ-)"),
            ("‘’", "' '", "Texte ou nom : ‘LBL’ (le curseur se place entre les guillemets)"),
        };

        ToolStrip symbolBar;

        /// <summary>Barre de symboles sous la barre d'outils, cachée tant que l'onglet n'est pas un programme C47.</summary>
        void InitSymbolBar()
        {
            symbolBar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Visible = false, Padding = new Padding(4, 0, 0, 0) };
            symbolBar.Items.Add(new ToolStripLabel(Lang.T("Symboles C47 :")));
            var font = new Font("Segoe UI Symbol", 11f);
            foreach (var (symbol, typed, help) in SymbolButtons)
            {
                string tip = Lang.T(help) + (typed != "" ? "\r\n" + Lang.F("Au clavier : {0}", typed) : "");
                var b = new ToolStripButton(symbol) { Font = font, ToolTipText = tip, AutoSize = true, Margin = new Padding(1, 1, 1, 1) };
                b.Click += (s, e) => InsertSymbol(symbol);
                symbolBar.Items.Add(b);
            }
            Controls.Add(symbolBar);
            // ordre d'ancrage : menu, barre d'outils, puis la barre de symboles juste dessous
            Controls.SetChildIndex(symbolBar, Controls.GetChildIndex(toolStrip1));
        }

        void InsertSymbol(string symbol)
        {
            var tb = CurrentTB;
            if (tb == null || !IsC47(tb)) return;
            if (symbol == "‘’")
            {
                tb.InsertText("‘’");
                var p = tb.Selection.Start;
                var inside = new Place(p.iChar - 1, p.iLine);
                tb.Selection = new TextRange(tb, inside, inside);
            }
            else tb.InsertText(symbol);
            tb.Focus();
        }

        /// <summary>La fin du début de ligne est-elle dans un texte ‘…’ / "…" (même pas encore fermé) ou un commentaire @ ; # ?</summary>
        static bool InTextOrComment(string before)
        {
            char close = '\0';
            for (int i = 0; i < before.Length; i++)
            {
                char c = before[i];
                bool tokenStart = i == 0 || char.IsWhiteSpace(before[i - 1]);
                if (close != '\0') { if (c == close) close = '\0'; continue; }
                if (c == '‘') close = '’';
                else if (c == '"' && tokenStart) close = '"';
                else if (c == ';' || c == '@' && before.Substring(0, i).Trim() == "") return true;
                else if (c == '#' && tokenStart && (i + 1 == before.Length || char.IsWhiteSpace(before[i + 1]))) return true;
            }
            return close != '\0';
        }

        /// <summary>Après chaque caractère tapé dans un onglet C47 : -> devient →, <> devient ⇄… sauf dans un texte ou un commentaire.</summary>
        void AutoReplaceSymbol(CodeEditor tb, char typed)
        {
            if (!IsC47(tb) || !tb.Selection.IsEmpty) return;
            var caret = tb.Selection.Start;
            string before = tb.GetLineText(caret.iLine).Substring(0, Math.Min(caret.iChar, tb.GetLineText(caret.iLine).Length));
            var rule = AutoSymbols.FirstOrDefault(r => r.Typed[r.Typed.Length - 1] == typed && before.EndsWith(r.Typed));
            if (rule.Typed == null) return;
            if (InTextOrComment(before.Substring(0, before.Length - rule.Typed.Length))) return;
            tb.Selection = new TextRange(tb, new Place(caret.iChar - rule.Typed.Length, caret.iLine), caret);
            tb.InsertText(rule.Symbol);
            var menu = (tb.Tag as TbInfo)?.popupMenu;
            if (menu != null && menu.Visible) menu.Show(true);   // la liste suit le mot devenu x⇄
        }
    }
}
