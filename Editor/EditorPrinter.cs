using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace Dm42.Editor
{
    /// <summary>
    /// Impression du texte d'un CodeEditor : titre en tête de page, numéro de page en pied,
    /// couleurs de la coloration (le fond d'écran n'est pas imprimé : les couleurs trop claires passent en noir).
    /// </summary>
    static class EditorPrinter
    {
        public static void Print(CodeEditor tb, string title)
        {
            int nextLine = 0, page = 0;
            using (var doc = new PrintDocument { DocumentName = string.IsNullOrEmpty(title) ? "Programme" : title })
            using (var font = new Font(tb.Font.FontFamily, 9.5f))
            using (var bold = new Font(tb.Font.FontFamily, 9.5f, FontStyle.Bold))
            using (var italic = new Font(tb.Font.FontFamily, 9.5f, FontStyle.Italic))
            using (var head = new Font("Segoe UI", 9f, FontStyle.Bold))
            {
                doc.BeginPrint += (s, e) => { nextLine = 0; page = 0; };
                doc.PrintPage += (s, e) =>
                {
                    var g = e.Graphics;
                    var m = e.MarginBounds;
                    page++;
                    g.DrawString(doc.DocumentName, head, Brushes.Black, m.Left, m.Top - head.Height - 10);
                    g.DrawLine(Pens.Gray, m.Left, m.Top - 6, m.Right, m.Top - 6);
                    string foot = "Page " + page;
                    var fs = g.MeasureString(foot, head);
                    g.DrawString(foot, head, Brushes.Gray, m.Right - fs.Width, m.Bottom + 8);

                    var fmt = StringFormat.GenericTypographic;
                    float charW = g.MeasureString("MMMMMMMMMM", font, PointF.Empty, fmt).Width / 10f;
                    float lineH = font.GetHeight(g) + 1;
                    int digits = Math.Max(3, tb.LinesCount.ToString().Length);
                    float textLeft = m.Left + (digits + 2) * charW;
                    float y = m.Top;
                    while (nextLine < tb.LinesCount && y + lineH <= m.Bottom)
                    {
                        g.DrawString((nextLine + 1).ToString().PadLeft(digits), font, Brushes.Gray, m.Left, y, fmt);
                        string text = tb.GetLineText(nextLine);
                        for (int c = 0; c < text.Length; c++)
                        {
                            float x = textLeft + c * charW;
                            if (x + charW > m.Right) break;   // pas de retour à la ligne : on coupe
                            var (color, style) = tb.LookAt(nextLine, c);
                            if (color.GetBrightness() > 0.75f) color = Color.Black;
                            var f = style.HasFlag(FontStyle.Bold) ? bold : style.HasFlag(FontStyle.Italic) ? italic : font;
                            using (var b = new SolidBrush(color))
                                g.DrawString(text[c] == '\t' ? " " : text[c].ToString(), f, b, x, y, fmt);
                        }
                        y += lineH;
                        nextLine++;
                    }
                    e.HasMorePages = nextLine < tb.LinesCount;
                };

                using (var dlg = new PrintDialog { Document = doc, UseEXDialog = true, AllowSomePages = false })
                {
                    if (dlg.ShowDialog(tb.FindForm()) != DialogResult.OK) return;
                    try { doc.Print(); }
                    catch (Exception ex)
                    {
                        MessageBox.Show(tb.FindForm(), ex.Message, "Impression impossible", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
        }
    }
}
