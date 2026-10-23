using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace dm42editor
{
    /// <summary>
    /// Thème clair / sombre de DM42 Editor (fenêtre principale, paramètres, aides, dialogues).
    /// La fenêtre Python garde son propre habillage HP-42S.
    /// </summary>
    static class AppTheme
    {
        public static bool Dark { get; private set; }

        public static readonly Color DarkBack = Color.FromArgb(37, 37, 38);
        public static readonly Color DarkPanel = Color.FromArgb(45, 45, 48);
        public static readonly Color DarkInput = Color.FromArgb(30, 30, 30);
        public static readonly Color DarkFore = Color.FromArgb(232, 232, 228);
        public static readonly Color DarkMuted = Color.FromArgb(150, 150, 146);
        public static readonly Color DarkBorder = Color.FromArgb(78, 78, 82);
        public static readonly Color Accent = Color.FromArgb(224, 137, 47);

        /// <summary>Change de thème : rendu des barres d'outils et des menus de toute l'application.</summary>
        public static void Set(bool dark)
        {
            Dark = dark;
            ToolStripManager.Renderer = dark
                ? new ToolStripProfessionalRenderer(new DarkColors()) { RoundedEdges = false }
                : new ToolStripProfessionalRenderer();
        }

        /// <summary>Applique le thème à une fenêtre ou un contrôle et à tout son contenu.</summary>
        public static void Apply(Control root)
        {
            if (root is Form f)
            {
                Put(f, "Back", f.BackColor, v => f.BackColor = v, DarkBack);
                Put(f, "Fore", f.ForeColor, v => f.ForeColor = v, DarkFore);
                TitleBar(f);
            }
            ApplyTree(root);
        }

        // Valeurs d'origine (concepteur) de chaque propriété modifiée : le thème clair les rétablit telles quelles.
        static readonly ConditionalWeakTable<object, Dictionary<string, object>> saved = new ConditionalWeakTable<object, Dictionary<string, object>>();

        static void Put<T>(object owner, string key, T current, Action<T> set, T darkValue)
        {
            var d = saved.GetOrCreateValue(owner);
            if (!d.ContainsKey(key)) d[key] = current;
            set(Dark ? darkValue : (T)d[key]);
        }

        static void Colors(Control c, Color back, Color fore)
        {
            // une couleur égale à celle du parent est héritée : on garde « vide » pour continuer à hériter
            var p = c.Parent;
            Put(c, "Back", p != null && c.BackColor == p.BackColor ? Color.Empty : c.BackColor, v => c.BackColor = v, back);
            Put(c, "Fore", p != null && c.ForeColor == p.ForeColor ? Color.Empty : c.ForeColor, v => c.ForeColor = v, fore);
        }

        static bool Keep(object tag) => tag is string s && s == "keep";

        static void ApplyTree(Control c)
        {
            switch (c)
            {
                case Dm42.Editor.CodeEditor _:
                    return;   // couleurs du schéma RPN, réglées par Form1
                case ToolStrip ts:
                    Strip(ts);
                    return;
                case Form _:
                    break;
                case DataGridView dgv:
                    Grid(dgv);
                    break;
                case TextBoxBase _: case ListBox _: case ListView _: case ComboBox _: case NumericUpDown _: case TreeView _:
                    Colors(c, DarkInput, DarkFore);
                    if (c is ComboBox cb) Put(cb, "Flat", cb.FlatStyle, v => cb.FlatStyle = v, FlatStyle.Flat);
                    if (c is ListView lv && lv.View == View.Details) DarkListView(lv);
                    WindowTheme(c);
                    break;
                case Button b:
                    if (Keep(b.Tag)) break;   // bouton à la couleur imposée (nuancier...)
                    Put(b, "Flat", b.FlatStyle, v => b.FlatStyle = v, FlatStyle.Flat);
                    Put(b, "Border", b.FlatAppearance.BorderColor, v => b.FlatAppearance.BorderColor = v, DarkBorder);
                    Colors(b, DarkPanel, DarkFore);
                    Put(b, "VisualBack", b.UseVisualStyleBackColor, v => b.UseVisualStyleBackColor = v, false);
                    break;
                case SplitContainer sc:
                    Colors(sc, DarkBorder, DarkFore);
                    break;
                default:
                    // conteneurs, étiquettes, cases à cocher : reprennent les couleurs de la fenêtre
                    if (!Keep(c.Tag)) Colors(c, Color.Empty, Color.Empty);
                    break;
            }
            foreach (Control child in c.Controls) ApplyTree(child);
            if (c.ContextMenuStrip != null) Strip(c.ContextMenuStrip);
        }

        public static void Strip(ToolStrip ts)
        {
            ts.RenderMode = ToolStripRenderMode.ManagerRenderMode;
            Put(ts, "Fore", ts.ForeColor, v => ts.ForeColor = v, DarkFore);
            foreach (ToolStripItem it in ts.Items) Item(it);
        }

        static void Item(ToolStripItem it)
        {
            if (!Keep(it.Tag))
                Put(it, "Fore", it.Owner != null && it.ForeColor == it.Owner.ForeColor ? Color.Empty : it.ForeColor, v => it.ForeColor = v, Color.Empty);
            if (it is ToolStripTextBox tb)
                Put(tb, "Back", tb.BackColor, v => tb.BackColor = v, DarkInput);
            if (it is ToolStripDropDownItem dd)
            {
                var drop = dd.DropDown;
                Put(drop, "Fore", drop.ForeColor, v => drop.ForeColor = v, DarkFore);
                foreach (ToolStripItem sub in dd.DropDownItems) Item(sub);
            }
        }

        /// <summary>
        /// Liste en mode Détails : Windows ignore ses couleurs, on la dessine nous-mêmes en thème sombre
        /// (en thème clair, le dessin natif reprend la main).
        /// </summary>
        static void DarkListView(ListView lv)
        {
            Put(lv, "OwnerDraw", lv.OwnerDraw, v => lv.OwnerDraw = v, true);
            var d = saved.GetOrCreateValue(lv);
            if (d.ContainsKey("handlers")) { lv.Invalidate(); return; }
            d["handlers"] = true;
            var selBack = Color.FromArgb(150, 86, 26);
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            lv.DrawColumnHeader += (s, e) =>
            {
                if (!Dark) { e.DrawDefault = true; return; }
                using (var b = new SolidBrush(DarkPanel)) e.Graphics.FillRectangle(b, e.Bounds);
                using (var p = new Pen(DarkBorder)) e.Graphics.DrawLine(p, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom);
                TextRenderer.DrawText(e.Graphics, e.Header.Text, lv.Font, Rectangle.Inflate(e.Bounds, -4, 0), DarkFore, flags);
            };
            lv.DrawItem += (s, e) => e.DrawDefault = !Dark;
            lv.DrawSubItem += (s, e) =>
            {
                if (!Dark) { e.DrawDefault = true; return; }
                bool sel = e.Item.Selected;
                using (var b = new SolidBrush(sel ? selBack : lv.BackColor)) e.Graphics.FillRectangle(b, e.Bounds);
                if (lv.GridLines)
                    using (var p = new Pen(DarkBorder))
                    {
                        e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                        e.Graphics.DrawLine(p, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom);
                    }
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, lv.Font, Rectangle.Inflate(e.Bounds, -4, 0), sel ? Color.White : DarkFore, flags);
            };
        }

        static void Grid(DataGridView g)
        {
            Put(g, "Headers", g.EnableHeadersVisualStyles, v => g.EnableHeadersVisualStyles = v, false);
            Put(g, "Background", g.BackgroundColor, v => g.BackgroundColor = v, DarkInput);
            Put(g, "Grid", g.GridColor, v => g.GridColor = v, DarkBorder);
            void Cells(DataGridViewCellStyle st, Color back, Color fore, Color selBack)
            {
                Put(st, "Back", st.BackColor, v => st.BackColor = v, back);
                Put(st, "Fore", st.ForeColor, v => st.ForeColor = v, fore);
                Put(st, "SelBack", st.SelectionBackColor, v => st.SelectionBackColor = v, selBack);
                Put(st, "SelFore", st.SelectionForeColor, v => st.SelectionForeColor = v, Color.White);
            }
            Cells(g.DefaultCellStyle, DarkInput, DarkFore, Color.FromArgb(150, 86, 26));
            Cells(g.ColumnHeadersDefaultCellStyle, DarkPanel, DarkFore, DarkPanel);
            Cells(g.RowHeadersDefaultCellStyle, DarkPanel, DarkFore, Color.FromArgb(150, 86, 26));
            WindowTheme(g);
        }

        // ------------------------------------------------------------------
        //  Barre de titre et barres de défilement sombres (Windows 10 1809+ / 11)
        // ------------------------------------------------------------------
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr w, IntPtr l);

        static void TitleBar(Form f)
        {
            void Do()
            {
                int on = Dark ? 1 : 0;
                try
                {
                    if (DwmSetWindowAttribute(f.Handle, 20, ref on, 4) != 0)       // DWMWA_USE_IMMERSIVE_DARK_MODE
                        DwmSetWindowAttribute(f.Handle, 19, ref on, 4);           // anciennes versions de Windows 10
                }
                catch (Exception) { /* Windows trop ancien : barre de titre claire */ }
            }
            if (f.IsHandleCreated) Do(); else f.HandleCreated += (s, e) => Do();
        }

        static void WindowTheme(Control c)
        {
            void Do()
            {
                try
                {
                    if (c is ListView)
                    {
                        // liste : thème « ItemsView » pour le fond et l'en-tête de colonnes
                        SetWindowTheme(c.Handle, Dark ? "DarkMode_ItemsView" : "Explorer", null);
                        IntPtr header = SendMessage(c.Handle, 0x101F, IntPtr.Zero, IntPtr.Zero);   // LVM_GETHEADER
                        if (header != IntPtr.Zero) SetWindowTheme(header, Dark ? "DarkMode_ItemsView" : "Explorer", null);
                    }
                    else SetWindowTheme(c.Handle, Dark ? "DarkMode_Explorer" : "Explorer", null);
                    c.Invalidate(true);
                }
                catch (Exception) { /* uxtheme absent */ }
            }
            if (c.IsHandleCreated) Do(); else c.HandleCreated += (s, e) => Do();
        }

        // ------------------------------------------------------------------
        //  Couleurs des barres d'outils et menus en mode sombre
        // ------------------------------------------------------------------
        sealed class DarkColors : ProfessionalColorTable
        {
            static readonly Color Bar = Color.FromArgb(45, 45, 48);
            static readonly Color Hover = Color.FromArgb(70, 70, 74);
            static readonly Color Press = Color.FromArgb(90, 90, 94);
            static readonly Color Check = Color.FromArgb(102, 74, 42);
            static readonly Color Edge = Color.FromArgb(95, 95, 100);
            static readonly Color Menu = Color.FromArgb(43, 43, 46);
            static readonly Color MenuSel = Color.FromArgb(150, 86, 26);

            public override Color ToolStripGradientBegin => Bar;
            public override Color ToolStripGradientMiddle => Bar;
            public override Color ToolStripGradientEnd => Bar;
            public override Color ToolStripBorder => Bar;
            public override Color ToolStripContentPanelGradientBegin => DarkBack;
            public override Color ToolStripContentPanelGradientEnd => DarkBack;
            public override Color ToolStripPanelGradientBegin => Bar;
            public override Color ToolStripPanelGradientEnd => Bar;
            public override Color MenuStripGradientBegin => Bar;
            public override Color MenuStripGradientEnd => Bar;
            public override Color StatusStripGradientBegin => Bar;
            public override Color StatusStripGradientEnd => Bar;
            public override Color SeparatorDark => Edge;
            public override Color SeparatorLight => Bar;
            public override Color GripDark => Edge;
            public override Color GripLight => Bar;

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
            public override Color ButtonCheckedHighlightBorder => Accent;
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
    }
}
