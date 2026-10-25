using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace dm42editor
{
    /// <summary>
    /// Icônes de la barre d'outils dessinées en vectoriel (grille 24 × 24, traits arrondis, détail orange HP) :
    /// nettes à toute taille, sans fichier ni licence, et assorties au thème clair / sombre.
    /// </summary>
    static class ToolbarIcons
    {
        public static readonly Color Accent = Color.FromArgb(224, 137, 47);
        static readonly Brush AccentBrush = new SolidBrush(Accent);

        /// <summary>Icône nommée à la taille donnée ; ink : couleur des traits, paper : fond des feuilles (fond de la barre).</summary>
        public static Bitmap Make(string name, int size, Color ink, Color paper)
        {
            var bmp = new Bitmap(size, size);
            float w = size < 20 ? 2.1f : 1.7f;   // trait un peu plus épais en petite taille (menus 16 px)
            using (var g = Graphics.FromImage(bmp))
            using (var pen = new Pen(ink, w) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var acc = new Pen(Accent, w + 0.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var accFill = new SolidBrush(Color.FromArgb(150, Accent)))
            using (var inkBrush = new SolidBrush(ink))
            using (var paperBrush = new SolidBrush(paper))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.ScaleTransform(size / 24f, size / 24f);
                Draw(g, name, pen, acc, accFill, inkBrush, paperBrush);
            }
            return bmp;
        }

        static GraphicsPath Round(float x, float y, float w, float h, float r)
        {
            var p = new GraphicsPath();
            float d = 2 * r;
            p.AddArc(x, y, d, d, 180, 90);
            p.AddArc(x + w - d, y, d, d, 270, 90);
            p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            p.AddArc(x, y + h - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        static void Draw(Graphics g, string name, Pen pen, Pen acc, Brush accFill, Brush ink, Brush paper)
        {
            switch (name)
            {
                case "new":   // feuille cornée + « + »
                    g.DrawPolygon(pen, new[] { new PointF(6, 3), new PointF(14, 3), new PointF(19, 8), new PointF(19, 21), new PointF(6, 21) });
                    g.DrawLines(pen, new[] { new PointF(14, 3), new PointF(14, 8), new PointF(19, 8) });
                    g.DrawLine(acc, 12.5f, 11.5f, 12.5f, 17.5f);
                    g.DrawLine(acc, 9.5f, 14.5f, 15.5f, 14.5f);
                    break;

                case "open":  // dossier ouvert
                    g.DrawLines(pen, new[] { new PointF(3, 18), new PointF(3, 5), new PointF(9, 5), new PointF(11, 7), new PointF(19, 7), new PointF(19, 10) });
                    var flap = new[] { new PointF(3, 19), new PointF(6.5f, 11), new PointF(22, 11), new PointF(18.5f, 19) };
                    g.FillPolygon(accFill, flap);
                    g.DrawPolygon(pen, flap);
                    break;

                case "save":  // disquette
                    using (var body = Round(4, 4, 16, 16, 2)) g.DrawPath(pen, body);
                    g.DrawRectangle(pen, 8, 4, 7, 5);
                    g.FillRectangle(accFill, 7.5f, 13, 9, 7);
                    g.DrawRectangle(pen, 7.5f, 13, 9, 7);
                    break;

                case "print": // imprimante
                    g.DrawRectangle(pen, 7, 3, 10, 5);
                    using (var body = Round(3, 8, 18, 9, 2)) g.DrawPath(pen, body);
                    g.FillRectangle(paper, 7, 14, 10, 7);
                    g.DrawRectangle(pen, 7, 14, 10, 7);
                    g.FillEllipse(accFill, 16.2f, 10.2f, 2.6f, 2.6f);
                    break;

                case "cut":   // ciseaux
                    g.DrawEllipse(pen, 3, 15, 6, 6);
                    g.DrawEllipse(pen, 15, 15, 6, 6);
                    g.DrawLine(pen, 8.2f, 15.8f, 18.5f, 3.5f);
                    g.DrawLine(acc, 15.8f, 15.8f, 5.5f, 3.5f);
                    break;

                case "copy":  // deux feuilles
                    g.DrawLines(pen, new[] { new PointF(5, 16), new PointF(4, 16), new PointF(4, 4), new PointF(16, 4), new PointF(16, 5) });
                    using (var front = Round(8, 8, 12, 13, 2)) { g.FillPath(paper, front); g.DrawPath(pen, front); }
                    g.DrawLine(acc, 11, 12.5f, 17, 12.5f);
                    g.DrawLine(acc, 11, 16.5f, 15, 16.5f);
                    break;

                case "paste": // presse-papiers
                    using (var board = Round(5, 5, 14, 16, 2)) g.DrawPath(pen, board);
                    using (var clip = Round(9, 3, 6, 4, 1)) { g.FillPath(accFill, clip); g.DrawPath(pen, clip); }
                    g.DrawLine(pen, 8.5f, 12, 15.5f, 12);
                    g.DrawLine(pen, 8.5f, 16, 13.5f, 16);
                    break;

                case "highlight":   // ligne du curseur surlignée
                    g.FillRectangle(accFill, 2.5f, 9.5f, 19, 5);
                    g.DrawLine(pen, 4, 5, 20, 5);
                    g.DrawLine(pen, 4, 12, 18, 12);
                    g.DrawLine(pen, 4, 19, 16, 19);
                    break;

                case "undo":
                case "redo":
                {
                    var state = g.Save();
                    if (name == "redo") { g.TranslateTransform(24, 0); g.ScaleTransform(-1, 1); }
                    using (var p = new GraphicsPath())
                    {
                        p.AddLine(4, 9, 14, 9);
                        p.AddArc(8, 9, 12, 11, 270, 180);
                        p.AddLine(14, 20, 10, 20);
                        g.DrawPath(pen, p);
                    }
                    g.DrawLines(acc, new[] { new PointF(8.5f, 4.5f), new PointF(4, 9), new PointF(8.5f, 13.5f) });
                    g.Restore(state);
                    break;
                }

                case "back":
                case "forward":
                {
                    var state = g.Save();
                    if (name == "forward") { g.TranslateTransform(24, 0); g.ScaleTransform(-1, 1); }
                    g.DrawEllipse(pen, 3, 3, 18, 18);
                    g.DrawLine(acc, 16.5f, 12, 7.5f, 12);
                    g.DrawLines(acc, new[] { new PointF(11, 8), new PointF(7.5f, 12), new PointF(11, 16) });
                    g.Restore(state);
                    break;
                }

                case "bookmarkAdd":
                case "bookmarkRemove":
                {
                    var ribbon = new[] { new PointF(6, 3), new PointF(18, 3), new PointF(18, 21), new PointF(12, 16.5f), new PointF(6, 21) };
                    g.DrawPolygon(pen, ribbon);
                    g.DrawLine(acc, 9, 9.5f, 15, 9.5f);
                    if (name == "bookmarkAdd") g.DrawLine(acc, 12, 6.5f, 12, 12.5f);
                    break;
                }

                case "stepsShow":   // lignes numérotées
                case "stepsHide":   // numéros barrés
                {
                    using (var f = new Font("Segoe UI", 5.6f, FontStyle.Bold, GraphicsUnit.Point))
                    using (var num = new SolidBrush(name == "stepsShow" ? Accent : Color.FromArgb(120, ((SolidBrush)ink).Color)))
                    {
                        var fmt = StringFormat.GenericTypographic;
                        g.DrawString("1", f, num, 2.2f, 2.3f, fmt);
                        g.DrawString("2", f, num, 2.2f, 8.8f, fmt);
                        g.DrawString("3", f, num, 2.2f, 15.3f, fmt);
                    }
                    g.DrawLine(pen, 10, 6, 21, 6);
                    g.DrawLine(pen, 10, 12.5f, 19, 12.5f);
                    g.DrawLine(pen, 10, 19, 20, 19);
                    if (name == "stepsHide") g.DrawLine(acc, 2, 21.5f, 8.5f, 2.5f);
                    break;
                }

                case "goto":  // aller à une ligne
                    g.DrawLine(pen, 15, 5, 21, 5);
                    g.DrawLine(pen, 15, 19, 21, 19);
                    g.DrawLine(acc, 3, 12, 18, 12);
                    g.DrawLines(acc, new[] { new PointF(14, 8), new PointF(18, 12), new PointF(14, 16) });
                    g.DrawLine(pen, 21, 9, 21, 15);
                    break;

                // ---------------- icônes des menus ----------------
                case "saveAll":   // deux disquettes
                    using (var back = Round(3, 3, 13, 13, 1.5f)) g.DrawPath(pen, back);
                    using (var front = Round(8, 8, 13, 13, 1.5f)) { g.FillPath(paper, front); g.DrawPath(pen, front); }
                    g.FillRectangle(accFill, 11, 15, 7, 6);
                    break;

                case "settings":  // roue dentée
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4;
                        g.DrawLine(pen, 12 + 6.5f * (float)Math.Cos(a), 12 + 6.5f * (float)Math.Sin(a), 12 + 9.5f * (float)Math.Cos(a), 12 + 9.5f * (float)Math.Sin(a));
                    }
                    g.DrawEllipse(pen, 5.5f, 5.5f, 13, 13);
                    g.DrawEllipse(acc, 9.5f, 9.5f, 5, 5);
                    break;

                case "convert":   // flèches croisées (HP-41 → DM42)
                    g.DrawLine(pen, 3, 8, 20, 8);
                    g.DrawLines(pen, new[] { new PointF(16, 4), new PointF(20, 8), new PointF(16, 12) });
                    g.DrawLine(acc, 21, 16, 4, 16);
                    g.DrawLines(acc, new[] { new PointF(8, 12), new PointF(4, 16), new PointF(8, 20) });
                    break;

                case "encode":    // puce : le programme encodé en octets
                    using (var chip = Round(5, 8, 14, 11, 2)) g.DrawPath(pen, chip);
                    for (int c = 0; c < 3; c++)
                    {
                        float x = 8 + c * 4;
                        g.DrawLine(pen, x, 4.5f, x, 8);
                        g.DrawLine(pen, x, 19, x, 22);
                    }
                    g.FillRectangle(AccentBrush, 9.5f, 11.5f, 5, 4);
                    break;

                case "raw":       // octets à l'écran : grille de cases
                    using (var frame = Round(3, 5, 18, 14, 2)) g.DrawPath(pen, frame);
                    for (int r = 0; r < 2; r++)
                        for (int c = 0; c < 3; c++)
                            g.FillRectangle((r + c) % 2 == 0 ? AccentBrush : ink, 6 + c * 4.6f, 8.5f + r * 4.4f, 3.2f, 2.8f);
                    break;

                case "send":      // calculatrice (émulateur)
                    using (var calc = Round(5, 2, 14, 20, 2)) g.DrawPath(pen, calc);
                    g.FillRectangle(accFill, 7.5f, 4.5f, 9, 4);
                    for (int r = 0; r < 3; r++)
                        for (int c = 0; c < 3; c++)
                            g.FillEllipse(ink, 7.6f + c * 3.4f, 11.3f + r * 3.4f, 2.2f, 2.2f);
                    break;

                case "flowchart": // losange + rectangle reliés
                    var dia = new[] { new PointF(12, 2), new PointF(17.5f, 7), new PointF(12, 12), new PointF(6.5f, 7) };
                    g.FillPolygon(accFill, dia);
                    g.DrawPolygon(pen, dia);
                    g.DrawLine(pen, 12, 12, 12, 15);
                    g.DrawRectangle(pen, 6, 15, 12, 6);
                    break;

                case "quit":      // porte et flèche de sortie
                    g.DrawLines(pen, new[] { new PointF(13, 7), new PointF(13, 3), new PointF(4, 3), new PointF(4, 21), new PointF(13, 21), new PointF(13, 17) });
                    g.DrawLine(acc, 9, 12, 21, 12);
                    g.DrawLines(acc, new[] { new PointF(17, 8), new PointF(21, 12), new PointF(17, 16) });
                    break;

                case "python":    // deux carrés imbriqués, comme le logo Python
                    using (var a = Round(3, 3, 12, 12, 3.5f)) g.DrawPath(pen, a);
                    using (var b = Round(9, 9, 12, 12, 3.5f)) { g.FillPath(paper, b); g.DrawPath(acc, b); }
                    g.FillEllipse(ink, 5.6f, 5.6f, 2.4f, 2.4f);
                    g.FillEllipse(AccentBrush, 16, 16, 2.4f, 2.4f);
                    break;

                case "window":    // fenêtre
                    using (var win = Round(3, 4, 18, 16, 2)) g.DrawPath(pen, win);
                    g.DrawLine(pen, 3, 9, 21, 9);
                    g.FillEllipse(AccentBrush, 5.2f, 5.5f, 2.2f, 2.2f);
                    break;

                case "examples":  // livre ouvert
                    g.DrawLines(pen, new[] { new PointF(12, 6), new PointF(8, 4), new PointF(3, 4), new PointF(3, 18), new PointF(8, 18), new PointF(12, 20) });
                    g.DrawLines(pen, new[] { new PointF(12, 6), new PointF(16, 4), new PointF(21, 4), new PointF(21, 18), new PointF(16, 18), new PointF(12, 20) });
                    g.DrawLine(acc, 12, 6, 12, 20);
                    break;

                case "help":      // ?
                case "info":      // i
                    g.DrawEllipse(pen, 3, 3, 18, 18);
                    using (var f = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point))
                    using (var b = new SolidBrush(Accent))
                        g.DrawString(name == "help" ? "?" : "i", f, b, new RectangleF(3, 3.5f, 18, 18), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    break;

                case "pdf":       // document « PDF »
                    g.DrawPolygon(pen, new[] { new PointF(6, 2.5f), new PointF(14, 2.5f), new PointF(19, 7.5f), new PointF(19, 21.5f), new PointF(6, 21.5f) });
                    g.DrawLines(pen, new[] { new PointF(14, 2.5f), new PointF(14, 7.5f), new PointF(19, 7.5f) });
                    g.FillRectangle(AccentBrush, 2.5f, 12, 15, 7);
                    using (var f = new Font("Segoe UI", 4.2f, FontStyle.Bold, GraphicsUnit.Point))
                        g.DrawString("PDF", f, Brushes.White, new RectangleF(2.5f, 12, 15, 7), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    break;

                case "list":      // liste à puces (équivalences)
                    for (int r = 0; r < 3; r++)
                    {
                        float y = 6 + r * 6;
                        g.FillEllipse(AccentBrush, 3.5f, y - 1.6f, 3.2f, 3.2f);
                        g.DrawLine(pen, 9, y, 20, y);
                    }
                    break;

                case "find":      // loupe
                case "replace":
                    g.DrawEllipse(pen, 3, 3, 12, 12);
                    g.DrawLine(name == "find" ? acc : pen, 13.5f, 13.5f, 20.5f, 20.5f);
                    if (name == "replace")
                    {
                        g.DrawLine(acc, 12, 21, 20, 21);
                        g.DrawLines(acc, new[] { new PointF(17.5f, 18.5f), new PointF(20, 21), new PointF(17.5f, 23.5f) });
                    }
                    break;

                case "selectAll": // cadre pointillé
                    using (var dash = new Pen(((SolidBrush)ink).Color, pen.Width) { DashStyle = DashStyle.Dash })
                        g.DrawRectangle(dash, 3, 3, 18, 18);
                    g.DrawLine(acc, 7, 9, 17, 9);
                    g.DrawLine(acc, 7, 15, 14, 15);
                    break;

                case "align":     // commentaires alignés en colonne
                    g.DrawLine(pen, 3, 6, 9, 6);
                    g.DrawLine(pen, 3, 12, 12, 12);
                    g.DrawLine(pen, 3, 18, 7, 18);
                    g.DrawLine(acc, 15, 6, 21, 6);
                    g.DrawLine(acc, 15, 12, 20, 12);
                    g.DrawLine(acc, 15, 18, 21, 18);
                    break;

                case "copyClean": // copier sans les commentaires
                    g.DrawLines(pen, new[] { new PointF(5, 16), new PointF(4, 16), new PointF(4, 4), new PointF(16, 4), new PointF(16, 5) });
                    using (var front = Round(8, 8, 12, 13, 2)) { g.FillPath(paper, front); g.DrawPath(pen, front); }
                    g.DrawLine(pen, 11, 12.5f, 17, 12.5f);
                    g.DrawLine(acc, 11, 18.5f, 15, 15.5f);
                    g.DrawLine(acc, 13, 18.5f, 17, 15.5f);
                    break;

                default:
                    g.DrawRectangle(pen, 4, 4, 16, 16);
                    break;
            }
        }
    }
}
