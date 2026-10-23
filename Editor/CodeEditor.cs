using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

#pragma warning disable WFO1000 // contrôle construit en code, pas dans le concepteur

namespace Dm42.Editor
{
    public sealed class TextChangedEventArgs : EventArgs
    {
        public TextRange ChangedRange { get; }
        public TextChangedEventArgs(TextRange changedRange) { ChangedRange = changedRange; }
    }

    /// <summary>
    /// Éditeur de code de DM42 Editor : texte à police fixe, coloration par styles, numéros de ligne,
    /// annuler / rétablir, signets, recherche, autocomplétion et impression.
    /// Remplace FastColoredTextBox ; l'interface reprend ses noms pour garder le code appelant lisible.
    /// </summary>
    [ToolboxItem(true)]
    public class CodeEditor : UserControl
    {
        readonly List<Line> lines = new List<Line> { new Line("") };
        readonly TextRange selection;
        readonly List<Style> styles = new List<Style>();
        const int MaxStyles = 31;

        int charWidth = 8, lineHeight = 16;
        int maxLineLength;
        bool maxLenDirty = true;
        int desiredColumn = -1;
        readonly Dictionary<FontStyle, Font> fonts = new Dictionary<FontStyle, Font>();

        readonly Timer textTimer = new Timer(), selTimer = new Timer(), caretTimer = new Timer { Interval = 530 }, dragTimer = new Timer { Interval = 40 };
        bool caretOn = true, dragging, isChanged;

        sealed class Snapshot { public string[] Lines; public Place Start, End; }
        readonly List<Snapshot> undo = new List<Snapshot>(), redo = new List<Snapshot>();
        bool lastWasTyping;
        DateTime lastTypingTime;
        Place lastTypingCaret;
        const int MaxUndo = 300;

        FindReplaceForm findForm;

        public CodeEditor()
        {
            selection = new TextRange(this, new Place(0, 0), new Place(0, 0)) { IsSelection = true };
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            AutoScroll = true;
            BackColor = Color.White;
            ForeColor = Color.Black;
            Cursor = Cursors.IBeam;
            Font = new Font("Consolas", 11f);
            DelayedTextChangedInterval = 100;
            DelayedEventsInterval = 100;
            textTimer.Tick += (s, e) => { textTimer.Stop(); TextChangedDelayed?.Invoke(this, new TextChangedEventArgs(Range)); };
            selTimer.Tick += (s, e) => { selTimer.Stop(); SelectionChangedDelayed?.Invoke(this, EventArgs.Empty); };
            caretTimer.Tick += (s, e) => { caretOn = !caretOn; InvalidateCaretLine(); };
            dragTimer.Tick += (s, e) => DragScroll();
            UpdateMetrics();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                textTimer.Dispose(); selTimer.Dispose(); caretTimer.Dispose(); dragTimer.Dispose();
                foreach (var f in fonts.Values) f.Dispose();
                findForm?.Dispose();
            }
            base.Dispose(disposing);
        }

        // ==================================================================
        //  Événements
        // ==================================================================

        /// <summary>Texte modifié : ChangedRange couvre les lignes touchées.</summary>
        public new event EventHandler<TextChangedEventArgs> TextChanged;
        /// <summary>Texte modifié, après une pause de DelayedTextChangedInterval ms : ChangedRange couvre tout le texte.</summary>
        public event EventHandler<TextChangedEventArgs> TextChangedDelayed;
        public event EventHandler SelectionChanged;
        public event EventHandler SelectionChangedDelayed;
        /// <summary>Caractère tapé, après son insertion (Entrée comprise).</summary>
        public event KeyPressEventHandler KeyPressed;
        /// <summary>Touches vues avant tout traitement (autocomplétion).</summary>
        internal event KeyEventHandler EarlyKeyDown;

        // ==================================================================
        //  Réglages d'affichage
        // ==================================================================
        public bool ReadOnly { get; set; }
        public bool ShowLineNumbers { get => showLineNumbers; set { showLineNumbers = value; UpdateScrollSize(); Invalidate(); } }
        bool showLineNumbers = true;
        public Color LineNumberColor { get; set; } = Color.Teal;
        /// <summary>Fond de la marge des numéros de ligne.</summary>
        public Color IndentBackColor { get; set; } = Color.WhiteSmoke;
        /// <summary>Trait qui sépare la marge du texte.</summary>
        public Color ServiceLinesColor { get; set; } = Color.Silver;
        /// <summary>Fond de la ligne du curseur (Transparent : aucun).</summary>
        public Color CurrentLineColor { get; set; } = Color.Transparent;
        /// <summary>Repère des lignes modifiées depuis l'enregistrement (Transparent : aucun).</summary>
        public Color ChangedLineColor { get; set; } = Color.Transparent;
        public Color SelectionColor { get; set; } = Color.FromArgb(90, 51, 153, 255);
        public Color CaretColor { get; set; } = Color.Black;
        public Color BookmarkColor { get; set; } = Color.FromArgb(180, 230, 120, 40);
        public int LeftPadding { get => leftPadding; set { leftPadding = value; UpdateScrollSize(); Invalidate(); } }
        int leftPadding;
        public int TabLength { get; set; } = 4;
        /// <summary>Entrée reprend l'indentation de la ligne.</summary>
        public bool AutoIndent { get; set; } = true;
        public int DelayedTextChangedInterval { get => textTimer.Interval; set => textTimer.Interval = Math.Max(1, value); }
        public int DelayedEventsInterval { get => selTimer.Interval; set => selTimer.Interval = Math.Max(1, value); }

        public int CharWidth => charWidth;
        public int CharHeight => lineHeight;

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            foreach (var f in fonts.Values) f.Dispose();
            fonts.Clear();
            UpdateMetrics();
        }

        void UpdateMetrics()
        {
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
            var ten = TextRenderer.MeasureText("MMMMMMMMMM", Font, new Size(int.MaxValue, int.MaxValue), flags);
            var one = TextRenderer.MeasureText("M", Font, new Size(int.MaxValue, int.MaxValue), flags);
            charWidth = Math.Max(1, (int)Math.Round(ten.Width / 10.0));
            lineHeight = Math.Max(one.Height, Font.Height) + 1;
            UpdateScrollSize();
            Invalidate();
        }

        Font FontFor(FontStyle st)
        {
            // style exact demandé : « Regular » reste maigre même si la police de base est en gras
            if (st == Font.Style) return Font;
            if (!fonts.TryGetValue(st, out var f)) fonts[st] = f = new Font(Font.FontFamily, Font.Size, st, Font.Unit);
            return f;
        }

        int GutterWidth => showLineNumbers ? Math.Max(2, lines.Count.ToString().Length) * charWidth + 16 : (ChangedLineColor.A > 0 || HasBookmarks ? 8 : 0);
        int TextLeft => GutterWidth + leftPadding + 3;
        bool HasBookmarks => lines.Any(l => l.BookmarkName != null);
        int ScrollX => -AutoScrollPosition.X;
        int ScrollY => -AutoScrollPosition.Y;

        void UpdateScrollSize()
        {
            if (maxLenDirty) { maxLineLength = lines.Max(l => l.Count); maxLenDirty = false; }
            AutoScrollMinSize = new Size(TextLeft + (maxLineLength + 2) * charWidth, (lines.Count + 1) * lineHeight);
        }

        // ==================================================================
        //  Texte
        // ==================================================================
        public override string Text
        {
            get => string.Join("\r\n", lines.Select(l => l.Text));
            set
            {
                if (selection == null) return;   // appel du constructeur de Control
                PushUndo(false);
                ReplaceRange(new Place(0, 0), EndPlace, value ?? "");
                selection.Set(new Place(0, 0), new Place(0, 0));
                OnSelectionMoved();
                UpdateScrollSize();
                AutoScrollPosition = Point.Empty;
            }
        }

        public int LinesCount => lines.Count;
        public IList<string> Lines => lines.Select(l => l.Text).ToList();
        public int TextLength => lines.Sum(l => l.Count) + 2 * (lines.Count - 1);
        public Line this[int iLine] => lines[iLine];
        public string GetLineText(int iLine) => iLine >= 0 && iLine < lines.Count ? lines[iLine].Text : "";
        public int GetLineLength(int iLine) => iLine >= 0 && iLine < lines.Count ? lines[iLine].Count : 0;
        public Place EndPlace => new Place(lines[lines.Count - 1].Count, lines.Count - 1);

        /// <summary>Tout le texte.</summary>
        public TextRange Range => new TextRange(this, new Place(0, 0), EndPlace);

        /// <summary>Lignes visibles à l'écran.</summary>
        public TextRange VisibleRange
        {
            get
            {
                int first = Math.Max(0, ScrollY / lineHeight);
                int last = Math.Min(lines.Count - 1, (ScrollY + ClientSize.Height) / lineHeight);
                return new TextRange(this, new Place(0, first), new Place(lines[last].Count, last));
            }
        }

        public TextRange Selection
        {
            get => selection;
            set { selection.Set(Clamp(value.Start), Clamp(value.End)); OnSelectionMoved(); }
        }

        public string SelectedText
        {
            get => selection.Text;
            set => InsertText(value);
        }

        public bool IsChanged
        {
            get => isChanged;
            set
            {
                isChanged = value;
                if (!value) { foreach (var l in lines) l.Changed = false; Invalidate(); }
            }
        }

        internal Place Clamp(Place p)
        {
            int line = Math.Max(0, Math.Min(lines.Count - 1, p.iLine));
            int ch = Math.Max(0, Math.Min(lines[line].Count, p.iChar));
            return new Place(ch, line);
        }

        internal string GetText(Place from, Place to)
        {
            from = Clamp(from); to = Clamp(to);
            if (from.iLine == to.iLine) return lines[from.iLine].Chars.ToString(from.iChar, Math.Max(0, to.iChar - from.iChar));
            var sb = new StringBuilder();
            sb.Append(lines[from.iLine].Chars.ToString(from.iChar, lines[from.iLine].Count - from.iChar));
            for (int i = from.iLine + 1; i < to.iLine; i++) sb.Append("\r\n").Append(lines[i].Chars);
            sb.Append("\r\n").Append(lines[to.iLine].Chars.ToString(0, to.iChar));
            return sb.ToString();
        }

        internal void ForEachChar(Place from, Place to, Action<Line, int> action)
        {
            from = Clamp(from); to = Clamp(to);
            for (int l = from.iLine; l <= to.iLine; l++)
            {
                var line = lines[l];
                int a = l == from.iLine ? from.iChar : 0;
                int b = l == to.iLine ? to.iChar : line.Count;
                for (int i = a; i < b; i++) action(line, i);
            }
        }

        /// <summary>Remplace le texte entre deux positions ; renvoie la position après le texte inséré.</summary>
        Place ReplaceRange(Place from, Place to, string text)
        {
            from = Clamp(from); to = Clamp(to);
            if (from > to) { var t = from; from = to; to = t; }
            text = text.Replace("\r\n", "\n").Replace('\r', '\n');

            var first = lines[from.iLine];
            var last = lines[to.iLine];
            string tail = last.Chars.ToString(to.iChar, last.Count - to.iChar);
            var tailStyles = last.Styles.GetRange(to.iChar, last.Count - to.iChar);

            // retire la portion
            first.Chars.Length = from.iChar;
            first.Styles.RemoveRange(from.iChar, first.Styles.Count - from.iChar);
            if (to.iLine > from.iLine) lines.RemoveRange(from.iLine + 1, to.iLine - from.iLine);

            // insère le nouveau texte
            var parts = text.Split('\n');
            first.Chars.Append(parts[0]);
            first.Styles.AddRange(new int[parts[0].Length]);
            first.Changed = true;
            var cur = first;
            int lineIndex = from.iLine;
            for (int i = 1; i < parts.Length; i++)
            {
                cur = new Line(parts[i]) { Changed = true };
                lines.Insert(++lineIndex, cur);
            }
            var end = new Place(cur.Count, lineIndex);
            cur.Chars.Append(tail);
            cur.Styles.AddRange(tailStyles);

            isChanged = true;
            maxLenDirty = true;
            UpdateScrollSize();
            Invalidate();
            TextChanged?.Invoke(this, new TextChangedEventArgs(new TextRange(this, new Place(0, from.iLine), new Place(lines[lineIndex].Count, lineIndex))));
            textTimer.Stop();
            textTimer.Start();
            return end;
        }

        /// <summary>Remplace la sélection par le texte (curseur après le texte inséré).</summary>
        public void InsertText(string text)
        {
            if (text == null) return;
            PushUndo(false);
            var end = ReplaceRange(selection.From, selection.To, text);
            selection.Set(end, end);
            OnSelectionMoved();
            EnsureCaretVisible();
        }

        void TypeText(string text)
        {
            PushUndo(true);
            var end = ReplaceRange(selection.From, selection.To, text);
            selection.Set(end, end);
            lastTypingCaret = end;
            OnSelectionMoved();
            EnsureCaretVisible();
        }

        public void ClearSelected()
        {
            if (ReadOnly || selection.IsEmpty) return;
            InsertText("");
        }

        public void RemoveLine(int iLine)
        {
            if (iLine < 0 || iLine >= lines.Count) return;
            PushUndo(false);
            if (lines.Count == 1) ReplaceRange(new Place(0, 0), EndPlace, "");
            else if (iLine < lines.Count - 1) ReplaceRange(new Place(0, iLine), new Place(0, iLine + 1), "");
            else ReplaceRange(new Place(lines[iLine - 1].Count, iLine - 1), new Place(lines[iLine].Count, iLine), "");
            selection.Set(Clamp(selection.Start), Clamp(selection.End));
            OnSelectionMoved();
        }

        public void OpenFile(string path, Encoding encoding)
        {
            Text = File.ReadAllText(path, encoding);
            ClearUndo();
            IsChanged = false;
        }

        public void SelectAll()
        {
            selection.Set(EndPlace, new Place(0, 0));
            OnSelectionMoved();
        }

        /// <summary>Sélectionne la ligne iLine et la rend visible.</summary>
        public void SetSelectedLine(int iLine)
        {
            iLine = Math.Max(0, Math.Min(lines.Count - 1, iLine));
            selection.Set(new Place(0, iLine), new Place(lines[iLine].Count, iLine));
            OnSelectionMoved();
            DoSelectionVisible();
        }

        // ==================================================================
        //  Presse-papiers
        // ==================================================================
        public void Copy()
        {
            if (selection.IsEmpty) return;
            try { Clipboard.SetText(selection.Text); } catch (Exception) { /* presse-papiers occupé */ }
        }

        public void Cut()
        {
            if (selection.IsEmpty) return;
            Copy();
            if (!ReadOnly) InsertText("");
        }

        public void Paste()
        {
            if (ReadOnly) return;
            string t;
            try { t = Clipboard.ContainsText() ? Clipboard.GetText() : null; } catch (Exception) { t = null; }
            if (t != null) InsertText(t);
        }

        // ==================================================================
        //  Annuler / rétablir
        // ==================================================================
        public bool UndoEnabled => undo.Count > 0;
        public bool RedoEnabled => redo.Count > 0;

        Snapshot Snap() => new Snapshot { Lines = lines.Select(l => l.Text).ToArray(), Start = selection.Start, End = selection.End };

        void PushUndo(bool typing)
        {
            // les caractères tapés à la suite forment une seule étape d'annulation
            bool merge = typing && lastWasTyping && selection.IsEmpty && selection.Start == lastTypingCaret
                         && (DateTime.Now - lastTypingTime).TotalSeconds < 2;
            if (!merge)
            {
                undo.Add(Snap());
                if (undo.Count > MaxUndo) undo.RemoveAt(0);
            }
            redo.Clear();
            lastWasTyping = typing;
            lastTypingTime = DateTime.Now;
        }

        public void ClearUndo()
        {
            undo.Clear();
            redo.Clear();
            lastWasTyping = false;
        }

        public void Undo()
        {
            if (undo.Count == 0) return;
            redo.Add(Snap());
            Restore(undo[undo.Count - 1]);
            undo.RemoveAt(undo.Count - 1);
        }

        public void Redo()
        {
            if (redo.Count == 0) return;
            undo.Add(Snap());
            Restore(redo[redo.Count - 1]);
            redo.RemoveAt(redo.Count - 1);
        }

        void Restore(Snapshot s)
        {
            lastWasTyping = false;
            var marks = lines.Select((l, i) => (l.BookmarkName, i)).Where(b => b.BookmarkName != null).ToList();
            ReplaceRange(new Place(0, 0), EndPlace, string.Join("\n", s.Lines));
            foreach (var (name, i) in marks) if (i < lines.Count) lines[i].BookmarkName = name;
            selection.Set(Clamp(s.Start), Clamp(s.End));
            OnSelectionMoved();
            EnsureCaretVisible();
        }

        // ==================================================================
        //  Styles
        // ==================================================================
        internal int StyleBit(Style style)
        {
            int i = styles.IndexOf(style);
            if (i >= 0) return 1 << i;
            if (styles.Count < MaxStyles) { styles.Add(style); return 1 << (styles.Count - 1); }
            // plus de place : réutilise l'emplacement d'un style qui n'est plus employé
            int used = 0;
            foreach (var l in lines) foreach (int m in l.Styles) used |= m;
            for (i = 0; i < styles.Count; i++)
                if ((used & (1 << i)) == 0) { styles[i] = style; return 1 << i; }
            // tous employés : on repart de zéro, la coloration suivante reposera ses styles
            foreach (var l in lines) for (int c = 0; c < l.Styles.Count; c++) l.Styles[c] = 0;
            styles.Clear();
            styles.Add(style);
            return 1;
        }

        internal int ExistingStyleBit(Style style)
        {
            int i = styles.IndexOf(style);
            return i >= 0 ? 1 << i : 0;
        }

        // ==================================================================
        //  Signets
        // ==================================================================
        public void BookmarkLine(int iLine)
        {
            if (iLine < 0 || iLine >= lines.Count || lines[iLine].BookmarkName != null) return;
            string t = lines[iLine].Text.Trim();
            if (t.Length > 30) t = t.Substring(0, 30) + "…";
            lines[iLine].BookmarkName = $"Ligne {iLine + 1} : {t}";
            UpdateScrollSize();
            Invalidate();
        }

        public void UnbookmarkLine(int iLine)
        {
            if (iLine < 0 || iLine >= lines.Count) return;
            lines[iLine].BookmarkName = null;
            UpdateScrollSize();
            Invalidate();
        }

        public IEnumerable<Bookmark> Bookmarks =>
            lines.Select((l, i) => (l, i)).Where(x => x.l.BookmarkName != null).Select(x => new Bookmark(this, x.l.BookmarkName, x.i)).ToList();

        // ==================================================================
        //  Position, défilement
        // ==================================================================
        public Place PointToPlace(Point p)
        {
            int line = Math.Max(0, Math.Min(lines.Count - 1, (p.Y + ScrollY) / lineHeight));
            int ch = (int)Math.Round((p.X + ScrollX - TextLeft) / (double)charWidth);
            return Clamp(new Place(ch, line));
        }

        Point PlaceToPoint(Place p) => new Point(TextLeft + p.iChar * charWidth - ScrollX, p.iLine * lineHeight - ScrollY);

        /// <summary>Position du curseur dans le contrôle (haut de la ligne).</summary>
        internal Point CaretLocation => PlaceToPoint(selection.Start);

        internal void OnSelectionMoved()
        {
            caretOn = true;
            if (Focused) { caretTimer.Stop(); caretTimer.Start(); }
            Invalidate();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            selTimer.Stop();
            selTimer.Start();
        }

        void ScrollTo(int x, int y)
        {
            AutoScrollPosition = new Point(Math.Max(0, x), Math.Max(0, y));
            Invalidate();
        }

        void EnsureCaretVisible()
        {
            var c = selection.Start;
            int y = c.iLine * lineHeight, sy = ScrollY;
            if (y < sy) sy = y;
            else if (y + lineHeight > sy + ClientSize.Height) sy = y + lineHeight - ClientSize.Height;
            int x = c.iChar * charWidth, sx = ScrollX, width = ClientSize.Width - TextLeft - charWidth;
            if (x < sx) sx = Math.Max(0, x - 4 * charWidth);
            else if (width > 0 && x > sx + width) sx = x - width + 4 * charWidth;
            if (sx != ScrollX || sy != ScrollY) ScrollTo(sx, sy);
        }

        /// <summary>Fait défiler pour que la portion soit visible (centrée si elle était hors de l'écran).</summary>
        public void DoRangeVisible(TextRange r)
        {
            int y = r.From.iLine * lineHeight;
            int sy = ScrollY;
            if (y < sy || y + lineHeight > sy + ClientSize.Height)
                sy = y - ClientSize.Height / 2 + lineHeight;
            int x = r.From.iChar * charWidth, sx = ScrollX;
            if (x < sx || x > sx + ClientSize.Width - TextLeft - charWidth) sx = Math.Max(0, x - 4 * charWidth);
            ScrollTo(sx, sy);
        }

        public void DoSelectionVisible() => DoRangeVisible(new TextRange(this, selection.Start, selection.Start));

        /// <summary>Place le curseur au début de la ligne et la rend visible.</summary>
        public void Navigate(int iLine)
        {
            var p = Clamp(new Place(0, iLine));
            selection.Set(p, p);
            OnSelectionMoved();
            DoSelectionVisible();
        }

        void InvalidateCaretLine()
        {
            var pt = PlaceToPoint(selection.Start);
            Invalidate(new Rectangle(pt.X - 2, pt.Y, 5, lineHeight));
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            caretOn = true;
            caretTimer.Start();
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            caretTimer.Stop();
            Invalidate();
        }

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            Invalidate();
        }

        // ==================================================================
        //  Dessin
        // ==================================================================
        static Color ColorOf(Brush b, Color def) => b is SolidBrush sb ? sb.Color : def;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var back = new SolidBrush(BackColor)) g.FillRectangle(back, ClientRectangle);

            int gutter = GutterWidth, textLeft = TextLeft, sx = ScrollX, sy = ScrollY;
            int first = Math.Max(0, sy / lineHeight);
            int last = Math.Min(lines.Count - 1, (sy + ClientSize.Height) / lineHeight + 1);
            var selFrom = selection.From;
            var selTo = selection.To;
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.PreserveGraphicsClipping;

            g.SetClip(new Rectangle(gutter, 0, Math.Max(0, ClientSize.Width - gutter), ClientSize.Height));
            for (int i = first; i <= last; i++)
            {
                var line = lines[i];
                int y = i * lineHeight - sy;

                // ligne du curseur
                if (CurrentLineColor.A > 0 && i == selection.Start.iLine && selection.IsEmpty)
                    using (var b = new SolidBrush(CurrentLineColor)) g.FillRectangle(b, gutter, y, ClientSize.Width - gutter, lineHeight);

                // fonds des styles
                for (int c = 0; c < line.Count; c++)
                {
                    int m = line.Styles[c];
                    if (m == 0) continue;
                    for (int s = 0; s < styles.Count; s++)
                        if ((m & (1 << s)) != 0 && styles[s].BackgroundBrush != null)
                            g.FillRectangle(styles[s].BackgroundBrush, textLeft + c * charWidth - sx, y, charWidth, lineHeight);
                }

                // sélection
                if (!selection.IsEmpty && i >= selFrom.iLine && i <= selTo.iLine)
                {
                    int a = i == selFrom.iLine ? selFrom.iChar : 0;
                    int b = i == selTo.iLine ? selTo.iChar : line.Count + 1;
                    using (var br = new SolidBrush(SelectionColor))
                        g.FillRectangle(br, textLeft + a * charWidth - sx, y, Math.Max(0, b - a) * charWidth, lineHeight);
                }

                // texte, par segments de même couleur
                int start = 0;
                while (start < line.Count)
                {
                    var (fore, fs) = CharLook(line.Styles[start]);
                    bool ascii = line.Chars[start] < 128;
                    int end = start + 1;
                    if (ascii)
                        while (end < line.Count && line.Chars[end] < 128 && CharLook(line.Styles[end]) == (fore, fs)) end++;
                    string run = line.Chars.ToString(start, end - start).Replace('\t', ' ');
                    TextRenderer.DrawText(g, run, FontFor(fs), new Point(textLeft + start * charWidth - sx, y), fore, flags);
                    start = end;
                }
            }
            g.ResetClip();

            // marge
            if (gutter > 0)
            {
                using (var b = new SolidBrush(IndentBackColor)) g.FillRectangle(b, 0, 0, gutter, ClientSize.Height);
                using (var p = new Pen(ServiceLinesColor)) g.DrawLine(p, gutter - 1, 0, gutter - 1, ClientSize.Height);
                for (int i = first; i <= last; i++)
                {
                    int y = i * lineHeight - sy;
                    if (showLineNumbers)
                        TextRenderer.DrawText(g, (i + 1).ToString(), Font, new Rectangle(0, y, gutter - 8, lineHeight), LineNumberColor,
                                              TextFormatFlags.Right | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                    if (lines[i].Changed && ChangedLineColor.A > 0)
                        using (var b = new SolidBrush(ChangedLineColor)) g.FillRectangle(b, gutter - 5, y, 3, lineHeight);
                    if (lines[i].BookmarkName != null)
                        using (var b = new SolidBrush(BookmarkColor))
                        {
                            int d = Math.Min(10, lineHeight - 4);
                            g.FillEllipse(b, 2, y + (lineHeight - d) / 2, d, d);
                        }
                }
            }

            // curseur
            if (Focused && caretOn)
            {
                var pt = PlaceToPoint(selection.Start);
                if (pt.X >= gutter)
                    using (var p = new Pen(CaretColor, 2)) g.DrawLine(p, pt.X, pt.Y + 1, pt.X, pt.Y + lineHeight - 2);
            }
        }

        /// <summary>Couleur et graisse d'un caractère : le premier style de texte enregistré l'emporte.</summary>
        (Color, FontStyle) CharLook(int mask)
        {
            if (mask != 0)
                for (int s = 0; s < styles.Count; s++)
                    if ((mask & (1 << s)) != 0 && styles[s] is TextStyle ts && ts.ForeBrush != null)
                        return (ColorOf(ts.ForeBrush, ForeColor), ts.FontStyle);
            return (ForeColor, Font.Style);   // texte sans style : police de base
        }

        // ==================================================================
        //  Clavier
        // ==================================================================
        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down:
                case Keys.Tab: case Keys.Enter: case Keys.Home: case Keys.End:
                case Keys.PageUp: case Keys.PageDown: case Keys.Escape:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            EarlyKeyDown?.Invoke(this, e);
            if (e.Handled) return;
            base.OnKeyDown(e);          // gestionnaires KeyDown de l'application
            if (e.Handled) return;

            bool ctrl = e.Control && !e.Alt, shift = e.Shift;
            var caret = selection.Start;
            bool handled = true;
            switch (e.KeyCode)
            {
                case Keys.Left:
                    if (!shift && !selection.IsEmpty && !ctrl) MoveCaret(selection.From, false);
                    else MoveCaret(ctrl ? WordLeft(caret) : PrevPlace(caret), shift);
                    break;
                case Keys.Right:
                    if (!shift && !selection.IsEmpty && !ctrl) MoveCaret(selection.To, false);
                    else MoveCaret(ctrl ? WordRight(caret) : NextPlace(caret), shift);
                    break;
                case Keys.Up:
                    if (ctrl) ScrollTo(ScrollX, ScrollY - lineHeight);
                    else MoveVertical(-1, shift);
                    break;
                case Keys.Down:
                    if (ctrl) ScrollTo(ScrollX, ScrollY + lineHeight);
                    else MoveVertical(1, shift);
                    break;
                case Keys.PageUp: MoveVertical(-Math.Max(1, ClientSize.Height / lineHeight - 1), shift); break;
                case Keys.PageDown: MoveVertical(Math.Max(1, ClientSize.Height / lineHeight - 1), shift); break;
                case Keys.Home:
                    if (ctrl) MoveCaret(new Place(0, 0), shift);
                    else
                    {
                        string t = lines[caret.iLine].Text;
                        int ind = t.Length - t.TrimStart().Length;
                        MoveCaret(new Place(caret.iChar == ind ? 0 : ind, caret.iLine), shift);
                    }
                    break;
                case Keys.End:
                    MoveCaret(ctrl ? EndPlace : new Place(lines[caret.iLine].Count, caret.iLine), shift);
                    break;
                case Keys.Back:
                    if (ReadOnly) break;
                    if (selection.IsEmpty)
                    {
                        if (caret == new Place(0, 0)) break;
                        selection.Set(ctrl ? WordLeft(caret) : PrevPlace(caret), caret);
                    }
                    TypeText("");
                    break;
                case Keys.Delete:
                    if (shift && !ctrl) { Cut(); break; }
                    if (ReadOnly) break;
                    if (selection.IsEmpty)
                    {
                        if (caret == EndPlace) break;
                        selection.Set(caret, ctrl ? WordRight(caret) : NextPlace(caret));
                    }
                    TypeText("");
                    break;
                case Keys.Insert:
                    if (ctrl) Copy();
                    else if (shift) Paste();
                    break;
                case Keys.Tab:
                    if (ReadOnly || ctrl) { handled = false; break; }
                    if (shift) Unindent();
                    else if (selection.From.iLine != selection.To.iLine) Indent();
                    else TypeText(new string(' ', TabLength - caret.iChar % TabLength));
                    break;
                case Keys.F3:
                    GetFindForm().FindNext(shift);
                    break;
                default:
                    handled = false;
                    if (ctrl)
                    {
                        handled = true;
                        switch (e.KeyCode)
                        {
                            case Keys.A: SelectAll(); break;
                            case Keys.C: Copy(); break;
                            case Keys.X: Cut(); break;
                            case Keys.V: Paste(); break;
                            case Keys.Z: if (!ReadOnly) { if (shift) Redo(); else Undo(); } break;
                            case Keys.Y: case Keys.R: if (!ReadOnly) Redo(); break;
                            case Keys.F: ShowFindDialog(); break;
                            case Keys.H: if (!ReadOnly) ShowReplaceDialog(); break;
                            case Keys.B:   // Ctrl+B : signet, Ctrl+Maj+B : le retirer
                                if (shift) UnbookmarkLine(selection.Start.iLine); else BookmarkLine(selection.Start.iLine);
                                break;
                            case Keys.Home: MoveCaret(new Place(0, 0), shift); break;
                            case Keys.End: MoveCaret(EndPlace, shift); break;
                            default: handled = false; break;
                        }
                    }
                    break;
            }
            if (handled) { e.Handled = true; e.SuppressKeyPress = true; }
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            if (e.Handled || ReadOnly) return;
            char c = e.KeyChar;
            if (c == '\r' || c == '\n')
            {
                string indent = "";
                if (AutoIndent)
                {
                    string t = lines[selection.From.iLine].Text;
                    indent = t.Substring(0, t.Length - t.TrimStart().Length);
                    if (indent.Length > selection.From.iChar) indent = indent.Substring(0, selection.From.iChar);
                }
                PushUndo(false);
                var end = ReplaceRange(selection.From, selection.To, "\n" + indent);
                selection.Set(end, end);
                OnSelectionMoved();
                EnsureCaretVisible();
            }
            else if (c >= ' ' && c != (char)127)
                TypeText(c.ToString());
            else return;
            e.Handled = true;
            KeyPressed?.Invoke(this, e);
        }

        void MoveCaret(Place p, bool extend)
        {
            p = Clamp(p);
            selection.Set(p, extend ? selection.End : p);
            desiredColumn = -1;
            OnSelectionMoved();
            EnsureCaretVisible();
        }

        void MoveVertical(int delta, bool extend)
        {
            var c = selection.Start;
            if (desiredColumn < 0) desiredColumn = c.iChar;
            int col = desiredColumn;
            var p = Clamp(new Place(col, c.iLine + delta));
            selection.Set(p, extend ? selection.End : p);
            OnSelectionMoved();
            EnsureCaretVisible();
            desiredColumn = col;
        }

        Place PrevPlace(Place p) => p.iChar > 0 ? new Place(p.iChar - 1, p.iLine) : p.iLine > 0 ? new Place(lines[p.iLine - 1].Count, p.iLine - 1) : p;
        Place NextPlace(Place p) => p.iChar < lines[p.iLine].Count ? new Place(p.iChar + 1, p.iLine) : p.iLine < lines.Count - 1 ? new Place(0, p.iLine + 1) : p;

        static bool IsWord(char c) => char.IsLetterOrDigit(c) || c == '_';

        Place WordLeft(Place p)
        {
            if (p.iChar == 0) return PrevPlace(p);
            string t = lines[p.iLine].Text;
            int i = p.iChar;
            while (i > 0 && char.IsWhiteSpace(t[i - 1])) i--;
            if (i > 0 && IsWord(t[i - 1])) while (i > 0 && IsWord(t[i - 1])) i--;
            else if (i > 0) i--;
            return new Place(i, p.iLine);
        }

        Place WordRight(Place p)
        {
            string t = lines[p.iLine].Text;
            if (p.iChar >= t.Length) return NextPlace(p);
            int i = p.iChar;
            if (IsWord(t[i])) while (i < t.Length && IsWord(t[i])) i++;
            else i++;
            while (i < t.Length && char.IsWhiteSpace(t[i])) i++;
            return new Place(i, p.iLine);
        }

        void Indent()
        {
            int a = selection.From.iLine, b = selection.To.iLine;
            if (b > a && selection.To.iChar == 0) b--;
            var changed = Enumerable.Range(a, b - a + 1).Select(i => lines[i].Count == 0 ? "" : new string(' ', TabLength) + lines[i].Text);
            ReplaceLines(a, b, changed);
        }

        void Unindent()
        {
            int a = selection.From.iLine, b = selection.To.iLine;
            if (b > a && selection.To.iChar == 0) b--;
            var changed = Enumerable.Range(a, b - a + 1).Select(i =>
            {
                string t = lines[i].Text;
                int n = 0;
                while (n < TabLength && n < t.Length && t[n] == ' ') n++;
                return t.Substring(n);
            });
            ReplaceLines(a, b, changed);
        }

        void ReplaceLines(int a, int b, IEnumerable<string> newLines)
        {
            PushUndo(false);
            ReplaceRange(new Place(0, a), new Place(lines[b].Count, b), string.Join("\n", newLines));
            selection.Set(new Place(lines[b].Count, b), new Place(0, a));
            OnSelectionMoved();
        }

        // ==================================================================
        //  Souris
        // ==================================================================
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            var p = PointToPlace(e.Location);
            if (e.Button == MouseButtons.Right)
            {
                if (p < selection.From || p > selection.To) { selection.Set(p, p); OnSelectionMoved(); }
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            desiredColumn = -1;
            if (e.X < GutterWidth)
            {
                // clic dans la marge : sélectionne la ligne
                var next = p.iLine < lines.Count - 1 ? new Place(0, p.iLine + 1) : new Place(lines[p.iLine].Count, p.iLine);
                selection.Set(next, new Place(0, p.iLine));
            }
            else if (e.Clicks == 2)
            {
                var w = new TextRange(this, p, p).GetFragment(@"[\w]");
                selection.Set(w.End, w.Start);
            }
            else
            {
                selection.Set(p, ModifierKeys.HasFlag(Keys.Shift) ? selection.End : p);
                dragging = true;
                Capture = true;
            }
            OnSelectionMoved();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Cursor = e.X < GutterWidth ? Cursors.Arrow : Cursors.IBeam;
            if (!dragging) return;
            var p = PointToPlace(e.Location);
            if (p != selection.Start) { selection.Set(p, selection.End); OnSelectionMoved(); }
            bool outside = e.Y < 0 || e.Y > ClientSize.Height || e.X < GutterWidth || e.X > ClientSize.Width;
            if (outside) dragTimer.Start(); else dragTimer.Stop();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragging = false;
            dragTimer.Stop();
            Capture = false;
        }

        void DragScroll()
        {
            if (!dragging) { dragTimer.Stop(); return; }
            var m = PointToClient(MousePosition);
            int dy = m.Y < 0 ? -lineHeight : m.Y > ClientSize.Height ? lineHeight : 0;
            int dx = m.X < GutterWidth ? -charWidth * 2 : m.X > ClientSize.Width ? charWidth * 2 : 0;
            ScrollTo(ScrollX + dx, ScrollY + dy);
            var p = PointToPlace(m);
            selection.Set(p, selection.End);
            OnSelectionMoved();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (ModifierKeys.HasFlag(Keys.Control))
            {
                // Ctrl + molette : zoom
                float size = Math.Max(6f, Math.Min(40f, Font.Size + (e.Delta > 0 ? 1f : -1f)));
                Font = new Font(Font.FontFamily, size, Font.Style);
                return;
            }
            int linesPerNotch = Math.Max(1, SystemInformation.MouseWheelScrollLines);
            if (ModifierKeys.HasFlag(Keys.Shift)) ScrollTo(ScrollX - e.Delta / 120 * charWidth * 6, ScrollY);
            else ScrollTo(ScrollX, ScrollY - e.Delta / 120 * linesPerNotch * lineHeight);
        }

        // ==================================================================
        //  Recherche, impression
        // ==================================================================
        FindReplaceForm GetFindForm()
        {
            if (findForm == null || findForm.IsDisposed) findForm = new FindReplaceForm(this);
            return findForm;
        }

        public void ShowFindDialog() => GetFindForm().ShowFor(false, selection.IsEmpty || selection.From.iLine != selection.To.iLine ? null : selection.Text);
        public void ShowReplaceDialog() => GetFindForm().ShowFor(true, selection.IsEmpty || selection.From.iLine != selection.To.iLine ? null : selection.Text);

        /// <summary>Imprime le texte (dialogue d'impression, titre en haut de page, numéro de page en bas).</summary>
        public void Print(string title) => EditorPrinter.Print(this, title);

        /// <summary>Styles visibles d'un caractère, pour l'impression.</summary>
        internal (Color, FontStyle) LookAt(int iLine, int iChar) => CharLook(lines[iLine].Styles[iChar]);
    }
}
