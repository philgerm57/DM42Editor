using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

#pragma warning disable WFO1000 // contrôle construit en code

namespace dm42editor.Flowchart
{
    /// <summary>
    /// Logigramme à l'ancienne (HP-41C) : rectangles d'instructions, losanges de test avec O / N,
    /// GTO dans des couloirs à gauche, branches « N » à droite, commentaires dans la marge.
    /// Mise en page en pixels à 100 %, le zoom n'est qu'une mise à l'échelle au dessin.
    /// </summary>
    sealed class FlowchartView : ScrollableControl
    {
        FlowGraph graph = new FlowGraph();
        float zoom = 1f;
        int selected = -1;

        // mise en page
        SizeF size;
        float nodesLeft, nodesRight, commentX, lineH, stepW;
        bool hasComments;
        readonly Dictionary<FlowEdge, float> laneX = new Dictionary<FlowEdge, float>();

        static readonly Font CodeFont = new Font("Consolas", 10f);
        static readonly Font StepFont = new Font("Consolas", 8.5f);
        static readonly Font BoldFont = new Font("Consolas", 10f, FontStyle.Bold);
        static readonly Font CommentFont = new Font("Segoe UI", 9f, FontStyle.Italic);
        static readonly Font MarkFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        static readonly Font LabelFont = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        static readonly Font HeaderFont = new Font("Segoe UI", 9f, FontStyle.Bold);
        static readonly StringFormat Fmt = new StringFormat(StringFormat.GenericTypographic) { FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces };
        static readonly StringFormat Center = new StringFormat(Fmt) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        static readonly Color Paper = Color.White;
        static readonly Color Ink = Color.FromArgb(40, 40, 44);
        static readonly Color BlockFill = Color.FromArgb(244, 247, 255);
        static readonly Color TestFill = Color.FromArgb(255, 246, 210);
        static readonly Color StartFill = Color.FromArgb(222, 241, 222);
        static readonly Color StopFill = Color.FromArgb(246, 226, 226);
        static readonly Color CallFill = Color.FromArgb(240, 233, 250);
        static readonly Color IOFill = Color.FromArgb(224, 243, 245);
        static readonly Color StepInk = Color.FromArgb(150, 150, 150);
        static readonly Color CommentInk = Color.FromArgb(90, 110, 90);
        static readonly Color YesInk = Color.FromArgb(20, 130, 40);
        static readonly Color NoInk = Color.FromArgb(200, 40, 30);
        static readonly Color JumpInk = Color.FromArgb(50, 90, 170);
        static readonly Color Accent = Color.FromArgb(224, 137, 47);

        const float Gap = 38, TopY = 40, LaneStep = 14, Pad = 20, CircleR = 11, Skew = 14;

        /// <summary>Double-clic sur une forme : ligne du texte source (0-based).</summary>
        public event Action<int> NodeActivated;

        public FlowchartView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            AutoScroll = true;
            BackColor = Color.FromArgb(225, 225, 225);
        }

        public FlowGraph Graph
        {
            get => graph;
            set { graph = value ?? new FlowGraph(); selected = -1; Relayout(); }
        }

        public float Zoom
        {
            get => zoom;
            set { zoom = Math.Max(0.25f, Math.Min(4f, value)); UpdateScroll(); Invalidate(); }
        }

        /// <summary>Décalage pour centrer un logigramme plus étroit que la fenêtre.</summary>
        float OffsetX => Math.Max(0, (ClientSize.Width - size.Width * zoom) / 2);

        /// <summary>Taille du logigramme à 100 %.</summary>
        public SizeF ChartSize => size;

        void UpdateScroll() => AutoScrollMinSize = new Size((int)Math.Ceiling(size.Width * zoom) + 2, (int)Math.Ceiling(size.Height * zoom) + 2);

        // ==================================================================
        //  Mise en page
        // ==================================================================
        void Relayout()
        {
            laneX.Clear();
            using (var bmp = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(bmp))
            {
                SizeF M(string s, Font f) => g.MeasureString(s ?? "", f, PointF.Empty, Fmt);
                lineH = CodeFont.GetHeight(g) + 3;
                stepW = M("000 ", StepFont).Width + 4;

                var nodes = graph.Nodes;
                float codeW = 0;
                foreach (var nd in nodes) foreach (var l in nd.Lines) codeW = Math.Max(codeW, M(l.Code, nd.Kind == NodeKind.Block ? CodeFont : BoldFont).Width);
                float blockW = Math.Max(200, stepW + codeW + 24);
                float maxW = blockW;

                // tailles et position verticale (colonne centrée en x = 0 pour l'instant)
                float y = TopY;
                foreach (var nd in nodes)
                {
                    float w, h;
                    switch (nd.Kind)
                    {
                        case NodeKind.Test:
                            w = Math.Max(150, M(nd.Lines[0].Code, BoldFont).Width + 80);
                            h = 62;
                            break;
                        case NodeKind.Start:
                        case NodeKind.Stop:
                            w = Math.Max(110, M(nd.Lines[0].Code, BoldFont).Width + 48);
                            h = 30;
                            break;
                        case NodeKind.IO:
                        case NodeKind.Call:
                            w = Math.Max(150, M(nd.Lines[0].Code, BoldFont).Width + 56);
                            h = 34;
                            break;
                        default:
                            w = blockW;
                            h = nd.Lines.Count * lineH + 12;
                            break;
                    }
                    maxW = Math.Max(maxW, w);
                    nd.Bounds = new RectangleF(-w / 2, y, w, h);
                    y += h + Gap;
                }

                // couloirs : les sauts courts au plus près des formes
                int leftLanes = AssignLanes(graph.Edges.Where(e => e.Kind == EdgeKind.Jump).ToList());
                int rightLanes = AssignLanes(graph.Edges.Where(e => e.Kind == EdgeKind.Skip).ToList());

                float leftRoom = Pad + 30 + leftLanes * LaneStep + 2 * CircleR + 10;
                float cx = leftRoom + maxW / 2;
                foreach (var nd in nodes) nd.Bounds.Offset(cx, 0);
                nodesLeft = cx - maxW / 2;
                nodesRight = cx + maxW / 2;
                foreach (var e in graph.Edges)
                {
                    if (e.Kind == EdgeKind.Jump) laneX[e] = nodesLeft - 2 * CircleR - 16 - e.Lane * LaneStep;
                    else if (e.Kind == EdgeKind.Skip) laneX[e] = nodesRight + 18 + e.Lane * LaneStep;
                }
                // colonne des commentaires : trait de séparation, puis crochet [ commentaire ]
                commentX = nodesRight + 34 + rightLanes * LaneStep + 30;
                float commentW = 0;
                foreach (var nd in nodes) foreach (var l in nd.Lines) if (!string.IsNullOrEmpty(l.Comment)) commentW = Math.Max(commentW, M(l.Comment, CommentFont).Width);
                hasComments = commentW > 0;
                if (hasComments) commentW = Math.Max(commentW, M("Commentaires", HeaderFont).Width);
                size = new SizeF(hasComments ? commentX + commentW + 12 + Pad : commentX - 30, Math.Max(y, 100));
            }
            UpdateScroll();
            Invalidate();
        }

        (float A, float B) Span(FlowEdge e)
        {
            float a = SourceY(e);
            float b = e.To >= 0 ? TargetY(e) : a;
            return (Math.Min(a, b), Math.Max(a, b));
        }

        /// <summary>Attribue un couloir à chaque liaison : deux liaisons qui se chevauchent n'en partagent pas.</summary>
        int AssignLanes(List<FlowEdge> edges)
        {
            var lanes = new List<List<(float A, float B)>>();
            foreach (var e in edges.OrderBy(x => Span(x).B - Span(x).A))
            {
                var s = Span(e);
                int k = 0;
                while (k < lanes.Count && lanes[k].Any(o => s.A <= o.B + 6 && o.A <= s.B + 6)) k++;
                if (k == lanes.Count) lanes.Add(new List<(float, float)>());
                lanes[k].Add(s);
                e.Lane = k;
            }
            return lanes.Count;
        }

        RectangleF B(int i) => graph.Nodes[i].Bounds;

        float SourceY(FlowEdge e)
        {
            var n = graph.Nodes[e.From];
            if (n.Kind == NodeKind.Block) return n.Bounds.Top + 6 + (n.Lines.Count - 0.5f) * lineH;   // la ligne GTO
            return n.Bounds.Top + n.Bounds.Height / 2;
        }

        float TargetY(FlowEdge e) => e.Kind == EdgeKind.Skip ? B(e.To).Top + 12 : B(e.To).Top + CircleR + 2;

        // ==================================================================
        //  Dessin
        // ==================================================================
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            e.Graphics.TranslateTransform(AutoScrollPosition.X + OffsetX, AutoScrollPosition.Y);
            e.Graphics.ScaleTransform(zoom, zoom);
            Draw(e.Graphics, new RectangleF(0, 0, size.Width, size.Height));
        }

        /// <summary>Dessine le logigramme (unités : pixels à 100 %). Sert aussi à l'export et à l'impression.</summary>
        public void Draw(Graphics g, RectangleF area)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using (var paper = new SolidBrush(Paper)) g.FillRectangle(paper, area);
            if (graph.Nodes.Count == 0)
            {
                using (var b = new SolidBrush(StepInk)) g.DrawString("Aucune instruction RPN dans cet onglet.", CommentFont, b, 20, 20, Fmt);
                return;
            }
            if (hasComments)
            {
                // en-tête et séparation de la colonne des commentaires
                float sepX = commentX - 24;
                using (var sep = new Pen(Color.FromArgb(205, 205, 205)))
                    g.DrawLine(sep, sepX, 8, sepX, size.Height - 8);
                using (var hb = new SolidBrush(StepInk))
                    g.DrawString("Commentaires", HeaderFont, hb, commentX - 6, 10, Fmt);
            }
            using (var pen = new Pen(Ink, 1.3f))
            using (var jumpPen = new Pen(JumpInk, 1.3f))
            using (var ink = new SolidBrush(Ink))
            {
                foreach (var edge in graph.Edges) DrawEdge(g, edge, edge.Kind == EdgeKind.Jump ? jumpPen : pen);
                for (int i = 0; i < graph.Nodes.Count; i++) DrawNode(g, graph.Nodes[i], i == selected, pen, ink);
            }
        }

        void DrawNode(Graphics g, FlowNode n, bool sel, Pen pen, Brush ink)
        {
            var r = n.Bounds;
            using (var selPen = new Pen(Accent, 3f))
            {
                switch (n.Kind)
                {
                    case NodeKind.Block:
                        using (var f = new SolidBrush(BlockFill)) g.FillRectangle(f, r);
                        g.DrawRectangle(sel ? selPen : pen, r.X, r.Y, r.Width, r.Height);
                        using (var stepBrush = new SolidBrush(StepInk))
                            for (int i = 0; i < n.Lines.Count; i++)
                            {
                                float y = r.Top + 6 + i * lineH;
                                g.DrawString(n.Lines[i].Step, StepFont, stepBrush, r.Left + 8, y + 1.5f, Fmt);
                                g.DrawString(n.Lines[i].Code, CodeFont, ink, r.Left + 8 + stepW, y, Fmt);
                            }
                        break;
                    case NodeKind.Test:
                    {
                        float cx = r.Left + r.Width / 2, cy = r.Top + r.Height / 2;
                        var pts = new[] { new PointF(cx, r.Top), new PointF(r.Right, cy), new PointF(cx, r.Bottom), new PointF(r.Left, cy) };
                        using (var f = new SolidBrush(TestFill)) g.FillPolygon(f, pts);
                        g.DrawPolygon(sel ? selPen : pen, pts);
                        g.DrawString(n.Lines[0].Code, BoldFont, ink, new RectangleF(r.Left, r.Top, r.Width, r.Height), Center);
                        using (var stepBrush = new SolidBrush(StepInk))
                            g.DrawString(string.Join(" ", n.Lines.Select(l => l.Step)), StepFont, stepBrush, r.Left + 6, r.Top + 4, Fmt);
                        break;
                    }
                    case NodeKind.IO:
                    {
                        // entrée / sortie : parallélogramme
                        var pts = new[]
                        {
                            new PointF(r.Left + Skew, r.Top), new PointF(r.Right, r.Top),
                            new PointF(r.Right - Skew, r.Bottom), new PointF(r.Left, r.Bottom)
                        };
                        using (var f = new SolidBrush(IOFill)) g.FillPolygon(f, pts);
                        g.DrawPolygon(sel ? selPen : pen, pts);
                        g.DrawString(n.Lines[0].Code, BoldFont, ink, r, Center);
                        using (var stepBrush = new SolidBrush(StepInk))
                            g.DrawString(n.Lines[0].Step, StepFont, stepBrush, r.Right - 6, r.Top - 13, Fmt);
                        break;
                    }
                    case NodeKind.Call:
                    {
                        // sous-programme : rectangle avec une double barre verticale de chaque côté
                        using (var f = new SolidBrush(CallFill)) g.FillRectangle(f, r);
                        g.DrawRectangle(sel ? selPen : pen, r.X, r.Y, r.Width, r.Height);
                        g.DrawLine(pen, r.Left + 8, r.Top, r.Left + 8, r.Bottom);
                        g.DrawLine(pen, r.Right - 8, r.Top, r.Right - 8, r.Bottom);
                        g.DrawString(n.Lines[0].Code, BoldFont, ink, r, Center);
                        using (var stepBrush = new SolidBrush(StepInk))
                            g.DrawString(n.Lines[0].Step, StepFont, stepBrush, r.Right - 6, r.Top - 13, Fmt);   // au-dessus : la flèche « N » arrive sur le côté
                        break;
                    }
                    default:
                    {
                        using (var path = Rounded(r, r.Height / 2))
                        {
                            using (var f = new SolidBrush(n.Kind == NodeKind.Start ? StartFill : StopFill)) g.FillPath(f, path);
                            g.DrawPath(sel ? selPen : pen, path);
                        }
                        g.DrawString(n.Lines[0].Code, BoldFont, ink, r, Center);
                        using (var stepBrush = new SolidBrush(StepInk))
                            g.DrawString(n.Lines[0].Step, StepFont, stepBrush, r.Right - 6, r.Top - 13, Fmt);   // au-dessus : la flèche « N » arrive sur le côté
                        break;
                    }
                }
            }

            // labels locaux : cercles à gauche, reliés à la forme
            for (int i = 0; i < n.Labels.Count; i++)
            {
                float cx = EntryX(n) - CircleR, cy = r.Top + CircleR + 2 + i * (2 * CircleR + 4);
                var c = new RectangleF(cx - CircleR, cy - CircleR, 2 * CircleR, 2 * CircleR);
                using (var f = new SolidBrush(Color.White)) g.FillEllipse(f, c);
                g.DrawEllipse(pen, c);
                if (i == 0) g.DrawLine(pen, cx + CircleR, cy, EntryX(n), cy);
                g.DrawString(n.Labels[i], LabelFont, ink, c, Center);
            }

            // colonne des commentaires : liaison — [ commentaire ] (symbole « Commentaire »)
            using (var cb = new SolidBrush(CommentInk))
            using (var lead = new Pen(Color.FromArgb(185, 185, 185)))
            using (var bracket = new Pen(Color.FromArgb(120, 120, 120)))
            {
                for (int i = 0; i < n.Lines.Count; i++)
                {
                    string c = n.Lines[i].Comment;
                    if (string.IsNullOrEmpty(c)) continue;
                    float y = n.Kind == NodeKind.Block ? r.Top + 6 + i * lineH : r.Top + r.Height / 2 - lineH / 2 + i * lineH;
                    float mid = y + lineH / 2, top = y, bottom = y + lineH;
                    float bx = commentX - 6, ex = commentX + g.MeasureString(c, CommentFont, PointF.Empty, Fmt).Width + 5;
                    g.DrawLine(lead, MarginX(n, mid), mid, bx, mid);
                    g.DrawLines(bracket, new[] { new PointF(bx + 4, top), new PointF(bx, top), new PointF(bx, bottom), new PointF(bx + 4, bottom) });
                    g.DrawLines(bracket, new[] { new PointF(ex - 4, top), new PointF(ex, top), new PointF(ex, bottom), new PointF(ex - 4, bottom) });
                    g.DrawString(c, CommentFont, cb, commentX, y + 1, Fmt);
                }
            }
        }

        /// <summary>Bord droit de la forme à la hauteur y (pour les pointillés vers le commentaire).</summary>
        static float MarginX(FlowNode n, float y)
        {
            var r = n.Bounds;
            if (n.Kind == NodeKind.IO) return r.Right - Skew * (y - r.Top) / r.Height + 4;   // côté penché
            if (n.Kind != NodeKind.Test) return r.Right + 4;
            float cy = r.Top + r.Height / 2;
            return r.Left + r.Width / 2 + r.Width / 2 * (1 - Math.Abs(y - cy) / (r.Height / 2)) + 4;
        }

        /// <summary>Point d'entrée gauche d'une forme (bord du rectangle, ou du losange à hauteur du cercle).</summary>
        static float EntryX(FlowNode n)
        {
            var r = n.Bounds;
            float y = r.Top + CircleR + 2;
            if (n.Kind == NodeKind.IO) return r.Left + Skew * (1 - (y - r.Top) / r.Height);   // côté penché
            if (n.Kind != NodeKind.Test) return r.Left;
            float cy = r.Top + r.Height / 2;
            return r.Left + r.Width / 2 - r.Width / 2 * (1 - Math.Abs(y - cy) / (r.Height / 2));
        }

        void DrawEdge(Graphics g, FlowEdge e, Pen pen)
        {
            var from = graph.Nodes[e.From];
            var fr = from.Bounds;
            float fcx = fr.Left + fr.Width / 2, fcy = fr.Top + fr.Height / 2;
            switch (e.Kind)
            {
                case EdgeKind.Down:
                {
                    var to = B(e.To);
                    var a = new PointF(fcx, fr.Bottom);
                    var b = new PointF(to.Left + to.Width / 2, to.Top);
                    g.DrawLine(pen, a, b);
                    Arrow(g, pen, b, 90);
                    Mark(g, e.Mark, a.X + 6, a.Y + 2);
                    break;
                }
                case EdgeKind.Skip:
                {
                    var to = B(e.To);
                    float lx = laneX[e], ty = TargetY(e);
                    var pts = new[] { new PointF(fr.Right, fcy), new PointF(lx, fcy), new PointF(lx, ty), new PointF(MarginX(graph.Nodes[e.To], ty) - 4, ty) };
                    g.DrawLines(pen, pts);
                    Arrow(g, pen, pts[3], 180);
                    Mark(g, e.Mark, fr.Right + 4, fcy - 16);
                    break;
                }
                case EdgeKind.Jump:
                {
                    float lx = laneX[e], sy = SourceY(e);
                    float sx = from.Kind == NodeKind.Test ? fr.Left : fr.Left;
                    if (e.To < 0)
                    {
                        // label introuvable
                        g.DrawLine(pen, sx, sy, lx, sy);
                        Arrow(g, pen, new PointF(lx, sy), 180);
                        using (var red = new SolidBrush(NoInk)) g.DrawString((e.Note ?? "GTO") + " ?", MarkFont, red, lx - 4, sy + 3, new StringFormat(Fmt) { Alignment = StringAlignment.Far });
                    }
                    else
                    {
                        var to = graph.Nodes[e.To];
                        float ty = TargetY(e);
                        // arrive dans le cercle du label s'il y en a un, sinon sur le bord de la forme
                        float ex = to.Labels.Count > 0 ? EntryX(to) - 2 * CircleR : EntryX(to);
                        var pts = new[] { new PointF(sx, sy), new PointF(lx, sy), new PointF(lx, ty), new PointF(ex, ty) };
                        g.DrawLines(pen, pts);
                        Arrow(g, pen, pts[3], 0);
                    }
                    Mark(g, e.Mark, sx - 14, sy - 16);
                    if (e.Mark == "O" && e.To >= 0 && !string.IsNullOrEmpty(e.Note))
                    {
                        // GTO absorbé dans le losange : rappelé sous la flèche
                        using (var jb = new SolidBrush(JumpInk))
                        using (var right = new StringFormat(Fmt) { Alignment = StringAlignment.Far })
                            g.DrawString(e.Note, StepFont, jb, sx - 4, sy + 3, right);
                    }
                    break;
                }
            }
        }

        static void Mark(Graphics g, string mark, float x, float y)
        {
            if (string.IsNullOrEmpty(mark)) return;
            using (var b = new SolidBrush(mark == "O" ? YesInk : NoInk)) g.DrawString(mark, MarkFont, b, x, y, Fmt);
        }

        /// <summary>Pointe de flèche en p, direction en degrés (0 : vers la droite, 90 : vers le bas).</summary>
        static void Arrow(Graphics g, Pen pen, PointF p, float angle)
        {
            double a = angle * Math.PI / 180;
            float L = 8, W = 4;
            var back = new PointF(p.X - L * (float)Math.Cos(a), p.Y - L * (float)Math.Sin(a));
            var n = new PointF(-(float)Math.Sin(a) * W, (float)Math.Cos(a) * W);
            using (var b = new SolidBrush(pen.Color))
                g.FillPolygon(b, new[] { p, new PointF(back.X + n.X, back.Y + n.Y), new PointF(back.X - n.X, back.Y - n.Y) });
        }

        static GraphicsPath Rounded(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // ==================================================================
        //  Souris
        // ==================================================================
        int NodeAt(Point p)
        {
            float x = (p.X - AutoScrollPosition.X - OffsetX) / zoom, y = (p.Y - AutoScrollPosition.Y) / zoom;
            for (int i = 0; i < graph.Nodes.Count; i++)
                if (graph.Nodes[i].Bounds.Contains(x, y)) return i;
            return -1;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            int i = NodeAt(e.Location);
            if (i != selected) { selected = i; Invalidate(); }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            int i = NodeAt(e.Location);
            if (i >= 0) NodeActivated?.Invoke(graph.Nodes[i].TextLine);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Cursor = NodeAt(e.Location) >= 0 ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (ModifierKeys.HasFlag(Keys.Control)) { Zoom *= e.Delta > 0 ? 1.15f : 1 / 1.15f; return; }
            base.OnMouseWheel(e);
            Invalidate();
        }

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            Invalidate();
        }
    }
}
