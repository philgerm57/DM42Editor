using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using C47.Core;

namespace dm42editor
{
    /// <summary>Liste des instructions C47 : par domaine, avec description française et original anglais.</summary>
    static class C47Help
    {
        static Form window;

        /// <summary>Instructions proposées à la saisie : ni noms vides, ni formes internes &gt;ABS&lt;.</summary>
        internal static IEnumerable<Item> Listed() =>
            Items.Programmable.Where(i => i.Kind != ParamKind.Literal && i.Name.Trim() != "" && !i.Name.StartsWith(">"));

        /// <summary>Tout ce que l'aide présente : les instructions, plus les touches et menus non programmables (EEX, ±ZOOM…),
        /// marqués [touche] et jamais insérés dans un programme.</summary>
        internal static IEnumerable<Item> Documented() => Listed().Concat(Items.KeyOnly).Distinct();

        /// <summary>Marque des fonctions non programmables dans les listes.</summary>
        internal static string KeyMark(Item i) => Items.IsKeyOnly(i) ? Lang.T("[touche]") + " " : "";

        /// <summary>Paramètre accepté en indirect : →R05 (adresse dans un registre) ou →‘nom’ (dans une variable), comme decodeOp.</summary>
        internal static bool AcceptsIndirect(Item i) => i.Kind is ParamKind.Label or ParamKind.Register or ParamKind.Compare or ParamKind.Flag
            or ParamKind.Number8 or ParamKind.Number8_16 or ParamKind.Number16 or ParamKind.KeyGKeyX or ParamKind.Menu;

        /// <summary>Nature du paramètre, suivie de « ou →indirect » quand l'instruction accepte l'indirection.</summary>
        internal static string ParamHint(Item i)
        {
            string hint = ParamKindHint(i);
            return hint != "" && AcceptsIndirect(i) ? hint + Lang.T(", ou →indirect") : hint;
        }

        static string ParamKindHint(Item i)
        {
            switch (i.Kind)
            {
                case ParamKind.DeclareLabel:
                case ParamKind.Label: return Lang.T("étiquette");
                case ParamKind.Register: return Lang.T("registre");
                case ParamKind.Compare: return Lang.T("registre, 0. ou 1.");
                case ParamKind.Flag: return Lang.T("indicateur");
                case ParamKind.Number8:
                case ParamKind.Number8_16:
                case ParamKind.Number16: return Lang.T("nombre");
                case ParamKind.KeyGKeyX: return Lang.T("touche, GTO ou XEQ, étiquette");
                case ParamKind.SkipBack: return Decoder.IsStructOp(i) ? Lang.T("structure, numérotée seule") : Lang.T("nombre de pas");
                case ParamKind.Shuffle: return Lang.T("4 lettres x y z t");
                case ParamKind.Menu: return Lang.T("menu ‘NOM’");
                case ParamKind.Rem: return Lang.T("texte ‘…’");
                default: return "";
            }
        }

        /// <summary>Début de la description, sans renvoi vers une image ni détail entre parenthèses.</summary>
        internal static string Short(Item i, int max = 70)
        {
            string d = Items.Describe(i);
            int cut = d.IndexOf(" -- ");
            if (cut > 0) d = d.Substring(0, cut);
            cut = d.IndexOfAny(new[] { ';', '(' });
            if (cut > 0) d = d.Substring(0, cut).Trim();
            return d.Length > max ? d.Substring(0, max - 1) + "…" : d;
        }

        /// <summary>Texte inséré dans le programme : nom reconnu par l'encodeur, puis espace ou fin de ligne.</summary>
        internal static string Snippet(Item i) => Items.IsKeyOnly(i) ? "" : Items.Unique(i) + (i.Kind == ParamKind.None ? "\r\n" : " ");

        /// <summary>Description complète : instruction, paramètre, domaine, texte, et l'anglais d'origine en français.</summary>
        internal static string Full(Item i)
        {
            var h = Items.Help(i);
            string hint = ParamHint(i);
            string text = Items.Describe(i);
            return i.Shown + (hint != "" ? "   [" + hint + "]" : "") + "   ·   " + Lang.T(h.Category) + "\r\n"
                 + (Items.IsKeyOnly(i) ? Lang.T("Non programmable : touche ou menu de la calculatrice, qu'un programme ne peut pas contenir.") + "\r\n" : "")
                 + (text != "" ? text : Lang.T("Pas de description."))
                 + (text != h.Description && h.Description != "" ? "\r\n\r\nEN : " + h.Description : "")
                 + (AcceptsIndirect(i) ? "\r\n\r\n" + Lang.F("Indirection : {0} →R05 prend l'adresse dans le registre 05, {0} →‘nom’ dans la variable nom (IND 05 est aussi accepté).", i.Shown) : "");
        }

        /// <summary>Ouvre la liste ; insert reçoit le texte à insérer dans l'onglet C47 courant.</summary>
        internal static void Show(IWin32Window owner, Action<string> insert)
        {
            if (window != null && !window.IsDisposed) { window.Activate(); return; }

            var all = Documented().OrderBy(i => i.Shown, StringComparer.OrdinalIgnoreCase).ToList();
            var byCategory = all.GroupBy(i => Items.Help(i).Category).ToDictionary(g => g.Key, g => g.Count());
            var f = new Form { Text = Lang.T("Instructions C47"), Size = new Size(960, 720), MinimumSize = new Size(640, 420), StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false };
            var filter = new TextBox { Dock = DockStyle.Top, Font = new Font("Segoe UI", 10f), PlaceholderText = Lang.T("Filtrer (nom ou description, français ou anglais)…") };
            var hint = new Label { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(6), ForeColor = SystemColors.GrayText, Font = new Font("Segoe UI", 8.5f),
                Text = Lang.T("Double-clic ou Entrée : insère l'instruction. Nombres : 5  1.5e3  FF#16  45°. Chaîne : ‘TEXTE’. Registres : 00  .01  X  ‘NOM’  →05. # texte reste dans le programme, @ et ; sont ignorés.") };
            var desc = new TextBox { Dock = DockStyle.Bottom, Height = 110, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Segoe UI", 10f) };
            var cats = new ListBox { Dock = DockStyle.Left, Width = 270, Font = new Font("Segoe UI", 10f), IntegralHeight = false };
            var box = new ListBox { Dock = DockStyle.Fill, Font = new Font("Consolas", 10.5f), IntegralHeight = false };

            cats.Items.Add(new Row<string>(null, Lang.F("Toutes ({0})", all.Count)));
            foreach (string c in Items.Categories)
                if (byCategory.TryGetValue(c, out int n))
                    cats.Items.Add(new Row<string>(c, Lang.T(c) + "  (" + n + ")"));

            void Fill()
            {
                string q = filter.Text.Trim();
                string cat = (cats.SelectedItem as Row<string>)?.Value;
                box.BeginUpdate();
                box.Items.Clear();
                foreach (var i in all)
                {
                    var h = Items.Help(i);
                    if (cat != null && h.Category != cat) continue;
                    if (q == "" || i.Shown.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || i.Menu.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                        || h.Description.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || h.French.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                        box.Items.Add(new Row<Item>(i, i.Shown.PadRight(12) + " " + KeyMark(i) + Short(i)));
                }
                box.EndUpdate();
                if (box.Items.Count > 0) box.SelectedIndex = 0;
                else desc.Text = "";
            }

            void Insert()
            {
                if (box.SelectedItem is Row<Item> r && !Items.IsKeyOnly(r.Value)) insert(Snippet(r.Value));
                else System.Media.SystemSounds.Beep.Play();   // touche ou menu : rien à insérer
            }

            filter.TextChanged += (s, e) => Fill();
            cats.SelectedIndexChanged += (s, e) => Fill();
            box.SelectedIndexChanged += (s, e) => { if (box.SelectedItem is Row<Item> r) desc.Text = Full(r.Value); };
            box.DoubleClick += (s, e) => Insert();
            box.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { Insert(); e.Handled = true; } };
            f.Controls.Add(box);
            f.Controls.Add(cats);
            f.Controls.Add(filter);
            f.Controls.Add(desc);
            f.Controls.Add(hint);
            if (AppTheme.Dark) AppTheme.Apply(f);
            cats.SelectedIndex = 0;
            window = f;
            f.Show(owner);
            filter.Focus();
        }

        sealed class Row<T>
        {
            public readonly T Value;
            readonly string text;
            public Row(T value, string text) { Value = value; this.text = text; }
            public override string ToString() => text;
        }
    }
}
