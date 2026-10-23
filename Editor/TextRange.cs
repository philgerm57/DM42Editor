using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;

namespace Dm42.Editor
{
    /// <summary>Position dans le texte : colonne (iChar) et ligne (iLine), à partir de 0.</summary>
    public readonly struct Place : IEquatable<Place>, IComparable<Place>
    {
        public readonly int iChar;
        public readonly int iLine;

        public Place(int iChar, int iLine) { this.iChar = iChar; this.iLine = iLine; }

        public int CompareTo(Place other) => iLine != other.iLine ? iLine.CompareTo(other.iLine) : iChar.CompareTo(other.iChar);
        public bool Equals(Place other) => iChar == other.iChar && iLine == other.iLine;
        public override bool Equals(object obj) => obj is Place p && Equals(p);
        public override int GetHashCode() => iLine * 4099 + iChar;
        public static bool operator ==(Place a, Place b) => a.Equals(b);
        public static bool operator !=(Place a, Place b) => !a.Equals(b);
        public static bool operator <(Place a, Place b) => a.CompareTo(b) < 0;
        public static bool operator >(Place a, Place b) => a.CompareTo(b) > 0;
        public static bool operator <=(Place a, Place b) => a.CompareTo(b) <= 0;
        public static bool operator >=(Place a, Place b) => a.CompareTo(b) >= 0;
        public override string ToString() => $"({iChar},{iLine})";
    }

    /// <summary>Style de coloration appliqué à des caractères.</summary>
    public abstract class Style
    {
        /// <summary>Fond dessiné sous les caractères (null : aucun).</summary>
        public Brush BackgroundBrush { get; set; }
    }

    /// <summary>Couleur et graisse du texte, fond facultatif.</summary>
    public class TextStyle : Style
    {
        public Brush ForeBrush { get; set; }
        public FontStyle FontStyle { get; set; }

        public TextStyle(Brush foreBrush, Brush backgroundBrush, FontStyle fontStyle)
        {
            ForeBrush = foreBrush;
            BackgroundBrush = backgroundBrush;
            FontStyle = fontStyle;
        }
    }

    /// <summary>Fond seul (surlignage), le texte garde sa couleur.</summary>
    public class MarkerStyle : Style
    {
        public MarkerStyle(Brush backgroundBrush) { BackgroundBrush = backgroundBrush; }
    }

    /// <summary>Une ligne de texte, avec le masque de styles de chaque caractère.</summary>
    public sealed class Line
    {
        internal StringBuilder Chars;
        internal List<int> Styles;          // un bit par style enregistré dans l'éditeur
        public DateTime LastVisit { get; set; }
        public string BookmarkName { get; internal set; }
        internal bool Changed;

        internal Line(string text)
        {
            Chars = new StringBuilder(text);
            Styles = new List<int>(new int[text.Length]);
        }

        public string Text => Chars.ToString();
        public int Count => Chars.Length;
    }

    /// <summary>Signet posé sur une ligne (suit la ligne quand le texte change).</summary>
    public sealed class Bookmark
    {
        public CodeEditor TB { get; }
        public string Name { get; }
        public int LineIndex { get; }

        internal Bookmark(CodeEditor tb, string name, int line) { TB = tb; Name = name; LineIndex = line; }

        public void DoVisible()
        {
            TB.Selection = new TextRange(TB, 0, LineIndex, 0, LineIndex);
            TB.DoSelectionVisible();
            TB.Focus();
        }
    }

    /// <summary>
    /// Portion de texte entre deux positions. La sélection de l'éditeur est elle-même un TextRange :
    /// modifier son début ou sa fin déplace le curseur.
    /// </summary>
    public sealed class TextRange
    {
        readonly CodeEditor tb;
        Place start, end;
        internal bool IsSelection;

        public TextRange(CodeEditor tb, Place start, Place end) { this.tb = tb; this.start = start; this.end = end; }

        public TextRange(CodeEditor tb, int iStartChar, int iStartLine, int iEndChar, int iEndLine)
            : this(tb, new Place(iStartChar, iStartLine), new Place(iEndChar, iEndLine)) { }

        /// <summary>Toute la ligne iLine.</summary>
        public TextRange(CodeEditor tb, int iLine)
            : this(tb, new Place(0, iLine), new Place(tb.GetLineLength(iLine), iLine)) { }

        public CodeEditor TB => tb;

        /// <summary>Début. Sur la sélection, l'affecter place le curseur (sélection vide).</summary>
        public Place Start
        {
            get => start;
            set
            {
                start = IsSelection ? tb.Clamp(value) : value;
                if (IsSelection) { end = start; tb.OnSelectionMoved(); }
            }
        }

        public Place End
        {
            get => end;
            set
            {
                end = IsSelection ? tb.Clamp(value) : value;
                if (IsSelection) tb.OnSelectionMoved();
            }
        }

        internal void Set(Place s, Place e) { start = s; end = e; }

        public bool IsEmpty => start == end;
        public Place From => start <= end ? start : end;
        public Place To => start <= end ? end : start;

        public TextRange Clone() => new TextRange(tb, start, end);

        /// <summary>Met le début avant la fin.</summary>
        public void Normalize()
        {
            if (start > end) { var t = start; start = end; end = t; }
        }

        public string Text => tb.GetText(From, To);

        public void SelectAll()
        {
            if (IsSelection) tb.SelectAll();
            else { start = new Place(0, 0); end = tb.EndPlace; }
        }

        // ------------------------------------------------------------------
        //  Styles
        // ------------------------------------------------------------------

        /// <summary>Applique un style à toute la portion.</summary>
        public void SetStyle(Style style)
        {
            int bit = tb.StyleBit(style);
            tb.ForEachChar(From, To, (line, i) => line.Styles[i] |= bit);
            tb.Invalidate();
        }

        public void SetStyle(Style style, string regex, RegexOptions options = RegexOptions.None) =>
            SetStyle(style, new Regex(regex, options));

        /// <summary>Applique un style aux correspondances de l'expression régulière.</summary>
        public void SetStyle(Style style, Regex regex)
        {
            int bit = tb.StyleBit(style);
            foreach (var r in GetRanges(regex))
                tb.ForEachChar(r.From, r.To, (line, i) => line.Styles[i] |= bit);
            tb.Invalidate();
        }

        public void ClearStyle(params Style[] styles)
        {
            int mask = 0;
            foreach (var s in styles) mask |= tb.ExistingStyleBit(s);
            if (mask == 0) return;
            tb.ForEachChar(From, To, (line, i) => line.Styles[i] &= ~mask);
            tb.Invalidate();
        }

        // ------------------------------------------------------------------
        //  Recherche
        // ------------------------------------------------------------------

        public IEnumerable<TextRange> GetRanges(string regex, RegexOptions options = RegexOptions.None) =>
            GetRanges(new Regex(regex, options));

        /// <summary>Correspondances de l'expression dans la portion (lignes séparées par \n).</summary>
        public IEnumerable<TextRange> GetRanges(Regex regex)
        {
            var from = From;
            string text = Text.Replace("\r\n", "\n");
            var result = new List<TextRange>();
            if (text.Length == 0) return result;
            // position de début de chaque ligne dans le texte
            var lineStarts = new List<int> { 0 };
            for (int i = 0; i < text.Length; i++) if (text[i] == '\n') lineStarts.Add(i + 1);

            Place ToPlace(int index)
            {
                int l = lineStarts.BinarySearch(index);
                if (l < 0) l = ~l - 1;
                int ch = index - lineStarts[l];
                return l == 0 ? new Place(from.iChar + ch, from.iLine) : new Place(ch, from.iLine + l);
            }

            foreach (Match m in regex.Matches(text))
            {
                if (m.Length == 0) continue;
                result.Add(new TextRange(tb, ToPlace(m.Index), ToPlace(m.Index + m.Length)));
            }
            return result;
        }

        /// <summary>Mot autour du début de la portion, fait des caractères acceptés par l'expression (un caractère).</summary>
        public TextRange GetFragment(string allowedCharPattern)
        {
            var re = new Regex(allowedCharPattern);
            var p = Start;
            string line = tb.GetLineText(p.iLine);
            int a = Math.Min(p.iChar, line.Length), b = a;
            while (a > 0 && re.IsMatch(line[a - 1].ToString())) a--;
            while (b < line.Length && re.IsMatch(line[b].ToString())) b++;
            return new TextRange(tb, a, p.iLine, b, p.iLine);
        }

        public override string ToString() => $"{start} - {end}";
    }
}
