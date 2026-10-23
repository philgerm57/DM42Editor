using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Dm42.Editor
{
    /// <summary>Proposition d'autocomplétion, avec une aide facultative affichée sous la liste.</summary>
    public sealed class AutocompleteItem
    {
        public string Text { get; }
        public string ToolTipTitle { get; set; }
        public string ToolTipText { get; set; }
        public AutocompleteItem(string text) { Text = text; }
        public override string ToString() => Text;
    }

    /// <summary>
    /// Liste d'autocomplétion d'un CodeEditor : apparaît pendant la frappe (ou par Show(true)),
    /// ↑ ↓ pour choisir, Entrée ou Tab pour insérer, Échap pour fermer. Le focus reste dans l'éditeur.
    /// </summary>
    public sealed class AutocompleteMenu : IDisposable
    {
        readonly CodeEditor tb;
        readonly PopupForm popup;
        readonly Timer appearTimer = new Timer { Interval = 500 };
        List<AutocompleteItem> items = new List<AutocompleteItem>();
        List<AutocompleteItem> shown = new List<AutocompleteItem>();
        int selected, top;
        Regex charRegex = new Regex(@"[\w\.]");
        Form hookedForm;
        const int MaxVisible = 10;

        public int MinFragmentLength { get; set; } = 2;
        public string SearchPattern { get => charRegex.ToString(); set => charRegex = new Regex(value); }
        public int AppearInterval { get => appearTimer.Interval; set => appearTimer.Interval = Math.Max(1, value); }
        /// <summary>Avant l'affichage : Cancel = true pour ne pas ouvrir la liste.</summary>
        public event EventHandler<CancelEventArgs> Opening;

        public AutocompleteMenu(CodeEditor tb)
        {
            this.tb = tb;
            popup = new PopupForm(this);
            appearTimer.Tick += (s, e) => { appearTimer.Stop(); Show(false); };
            tb.EarlyKeyDown += OnKeyDown;
            tb.KeyPressed += (s, e) =>
            {
                if (popup.Visible) Refilter();
                else if (charRegex.IsMatch(e.KeyChar.ToString())) { appearTimer.Stop(); appearTimer.Start(); }
            };
            tb.LostFocus += (s, e) => Hide();
            tb.MouseDown += (s, e) => Hide();
            tb.Resize += (s, e) => Hide();
            tb.Disposed += (s, e) => Dispose();
        }

        public void SetItems(IEnumerable<AutocompleteItem> list) => items = list.ToList();

        public bool Visible => popup.Visible;

        public void Dispose()
        {
            appearTimer.Dispose();
            popup.Dispose();
        }

        // ------------------------------------------------------------------
        TextRange Fragment()
        {
            var caret = tb.Selection.Start;
            string line = tb.GetLineText(caret.iLine);
            int a = Math.Min(caret.iChar, line.Length);
            while (a > 0 && charRegex.IsMatch(line[a - 1].ToString())) a--;
            return new TextRange(tb, new Place(a, caret.iLine), caret);
        }

        /// <summary>Ouvre la liste ; forced : même si le mot commencé est plus court que MinFragmentLength.</summary>
        public void Show(bool forced)
        {
            appearTimer.Stop();
            if (!tb.Selection.IsEmpty || tb.ReadOnly) return;
            string frag = Fragment().Text;
            if (!forced && frag.Length < MinFragmentLength) { Hide(); return; }
            if (!Filter(frag, forced)) { Hide(); return; }
            var ev = new CancelEventArgs();
            Opening?.Invoke(this, ev);
            if (ev.Cancel) return;

            var form = tb.FindForm();
            if (form != null && hookedForm != form)
            {
                hookedForm = form;
                form.Deactivate += (s, e) => Hide();
                form.Move += (s, e) => Hide();
            }
            popup.Font = tb.Font;
            popup.LayoutFor(tb);
            var pt = tb.PointToScreen(tb.CaretLocation);
            pt.Offset(-4, tb.CharHeight + 2);
            var screen = Screen.FromControl(tb).WorkingArea;
            if (pt.Y + popup.Height > screen.Bottom) pt.Y -= popup.Height + tb.CharHeight + 4;
            if (pt.X + popup.Width > screen.Right) pt.X = screen.Right - popup.Width;
            popup.Location = pt;
            if (!popup.Visible) popup.Show(form);
            popup.Invalidate();
        }

        public void Hide()
        {
            appearTimer.Stop();
            if (popup.Visible) popup.Hide();
        }

        bool Filter(string frag, bool forced)
        {
            shown = items.Where(i => i.Text.StartsWith(frag, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(i => i.Text, StringComparer.OrdinalIgnoreCase).ToList();
            if (shown.Count == 0) return false;
            if (!forced && shown.Count == 1 && string.Equals(shown[0].Text, frag, StringComparison.Ordinal)) return false;
            selected = 0;
            top = 0;
            return true;
        }

        void Refilter()
        {
            string frag = Fragment().Text;
            if (frag.Length == 0 || !Filter(frag, true)) { Hide(); return; }
            Show(true);
        }

        void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (!popup.Visible)
            {
                if (e.KeyCode == Keys.Escape) appearTimer.Stop();
                return;
            }
            switch (e.KeyCode)
            {
                case Keys.Up: Move(-1); break;
                case Keys.Down: Move(1); break;
                case Keys.PageUp: Move(-MaxVisible); break;
                case Keys.PageDown: Move(MaxVisible); break;
                case Keys.Enter:
                case Keys.Tab: Accept(selected); break;
                case Keys.Escape: Hide(); break;
                case Keys.Back: tb.BeginInvoke(new Action(Refilter)); return;
                case Keys.Left: case Keys.Right: case Keys.Home: case Keys.End: Hide(); return;
                default: return;
            }
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        void Move(int delta)
        {
            selected = Math.Max(0, Math.Min(shown.Count - 1, selected + delta));
            if (selected < top) top = selected;
            if (selected >= top + MaxVisible) top = selected - MaxVisible + 1;
            popup.LayoutFor(tb);
            popup.Invalidate();
        }

        void Accept(int index)
        {
            if (index < 0 || index >= shown.Count) return;
            var item = shown[index];
            Hide();
            var frag = Fragment();
            tb.Selection = new TextRange(tb, frag.End, frag.Start);
            tb.InsertText(item.Text);
            tb.Focus();
        }

        // ------------------------------------------------------------------
        //  Fenêtre de la liste : ne prend jamais le focus
        // ------------------------------------------------------------------
        sealed class PopupForm : Form
        {
            readonly AutocompleteMenu menu;
            int itemHeight, listHeight;
            Color back, fore;

            public PopupForm(AutocompleteMenu menu)
            {
                this.menu = menu;
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                StartPosition = FormStartPosition.Manual;
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            }

            protected override bool ShowWithoutActivation => true;

            protected override CreateParams CreateParams
            {
                get
                {
                    var cp = base.CreateParams;
                    cp.ExStyle |= 0x08000000 | 0x00000080;   // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
                    cp.ClassStyle |= 0x00020000;            // CS_DROPSHADOW
                    return cp;
                }
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == 0x21) { m.Result = (IntPtr)3; return; }   // WM_MOUSEACTIVATE → MA_NOACTIVATE
                base.WndProc(ref m);
            }

            AutocompleteItem Current => menu.selected < menu.shown.Count ? menu.shown[menu.selected] : null;

            public void LayoutFor(CodeEditor tb)
            {
                back = tb.BackColor;
                fore = tb.ForeColor;
                itemHeight = tb.CharHeight + 4;
                int visible = Math.Min(MaxVisible, menu.shown.Count);
                listHeight = visible * itemHeight + 2;
                int longest = menu.shown.Count == 0 ? 10 : menu.shown.Max(i => i.Text.Length);
                int width = Math.Max(220, Math.Min(520, (longest + 4) * tb.CharWidth));
                int helpHeight = 0;
                var cur = Current;
                if (cur != null && !string.IsNullOrEmpty(cur.ToolTipText))
                {
                    using (var small = new Font("Segoe UI", 8.5f))
                        helpHeight = TextRenderer.MeasureText(cur.ToolTipText, small, new Size(width - 12, 400), TextFormatFlags.WordBreak).Height + 10;
                }
                Size = new Size(width, listHeight + helpHeight);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(back);
                bool darkBack = back.GetBrightness() < 0.5f;
                var selBack = darkBack ? Color.FromArgb(150, 86, 26) : SystemColors.Highlight;
                var selFore = darkBack ? Color.White : SystemColors.HighlightText;
                for (int i = 0; i < MaxVisible && menu.top + i < menu.shown.Count; i++)
                {
                    int index = menu.top + i;
                    var r = new Rectangle(1, 1 + i * itemHeight, Width - 2, itemHeight);
                    bool sel = index == menu.selected;
                    if (sel) using (var b = new SolidBrush(selBack)) g.FillRectangle(b, r);
                    TextRenderer.DrawText(g, menu.shown[index].Text, Font, new Rectangle(r.X + 6, r.Y, r.Width - 8, r.Height), sel ? selFore : fore,
                                          TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                }
                if (menu.shown.Count > MaxVisible)
                {
                    // repère de position dans la liste
                    float h = listHeight * (float)MaxVisible / menu.shown.Count;
                    float y = listHeight * (float)menu.top / menu.shown.Count;
                    using (var b = new SolidBrush(Color.FromArgb(110, fore))) g.FillRectangle(b, Width - 4, y, 3, Math.Max(8, h));
                }
                var cur = Current;
                if (cur != null && !string.IsNullOrEmpty(cur.ToolTipText) && Height > listHeight)
                {
                    using (var p = new Pen(Color.FromArgb(90, fore))) g.DrawLine(p, 0, listHeight, Width, listHeight);
                    using (var small = new Font("Segoe UI", 8.5f))
                        TextRenderer.DrawText(g, cur.ToolTipText, small, new Rectangle(6, listHeight + 5, Width - 12, Height - listHeight - 6),
                                              Color.FromArgb(200, fore), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                }
                using (var p = new Pen(Color.FromArgb(120, fore))) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Y >= listHeight) return;
                int index = menu.top + (e.Y - 1) / Math.Max(1, itemHeight);
                if (index < menu.shown.Count) menu.Accept(index);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (e.Y >= listHeight) return;
                int index = menu.top + (e.Y - 1) / Math.Max(1, itemHeight);
                if (index < menu.shown.Count && index != menu.selected) { menu.selected = index; LayoutFor(menu.tb); Invalidate(); }
            }
        }
    }
}
