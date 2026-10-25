using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Windows.Forms;

namespace dm42editor.Flowchart
{
    /// <summary>
    /// Fenêtre du logigramme d'un programme RPN : zoom, actualisation, copie, export PNG et impression.
    /// Double-clic sur une forme : place le curseur sur la ligne dans l'éditeur.
    /// </summary>
    public sealed class FrmFlowchart : Form
    {
        readonly Func<string> source;
        readonly FlowchartView view = new FlowchartView { Dock = DockStyle.Fill };
        readonly ToolStripLabel zoomLabel = new ToolStripLabel();
        readonly ToolStripStatusLabel status = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        readonly string title;

        /// <param name="source">Texte RPN à représenter (relu par « Actualiser »).</param>
        /// <param name="title">Nom du programme (titre, nom du fichier PNG).</param>
        /// <param name="gotoLine">Appelé au double-clic sur une forme avec la ligne du texte (0-based), ou null.</param>
        public FrmFlowchart(Func<string> source, string title, Action<int> gotoLine)
        {
            this.source = source;
            this.title = string.IsNullOrEmpty(title) ? "Programme" : title;
            Text = "Logigramme — " + this.title;
            Size = new Size(1000, 800);
            StartPosition = FormStartPosition.CenterParent;
            KeyPreview = true;
            ShowIcon = false;

            var tools = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
            ToolStripButton B(string text, string tip, EventHandler click)
            {
                var b = new ToolStripButton(text) { ToolTipText = tip };
                b.Click += click;
                return b;
            }
            tools.Items.Add(B("Actualiser", "Relire le programme (F5)", (s, e) => Reload()));
            tools.Items.Add(new ToolStripSeparator());
            tools.Items.Add(B("−", "Réduire (Ctrl+molette)", (s, e) => SetZoom(view.Zoom / 1.2f)));
            tools.Items.Add(zoomLabel);
            tools.Items.Add(B("+", "Agrandir (Ctrl+molette)", (s, e) => SetZoom(view.Zoom * 1.2f)));
            tools.Items.Add(B("100 %", "Taille réelle", (s, e) => SetZoom(1f)));
            tools.Items.Add(B("Largeur", "Ajuster à la largeur de la fenêtre", (s, e) => FitWidth()));
            tools.Items.Add(new ToolStripSeparator());
            tools.Items.Add(B("Copier l'image", "Copier le logigramme dans le presse-papiers", (s, e) => CopyImage()));
            tools.Items.Add(B("Exporter PNG…", "Enregistrer le logigramme en image", (s, e) => ExportPng()));
            tools.Items.Add(B("Imprimer…", "Imprimer le logigramme (Ctrl+P)", (s, e) => Print()));

            var bar = new StatusStrip();
            bar.Items.Add(status);

            if (gotoLine != null) view.NodeActivated += line => { gotoLine(line); };
            view.MouseWheel += (s, e) => UpdateZoomLabel();

            Controls.Add(view);
            Controls.Add(tools);
            Controls.Add(bar);

            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.F5) { Reload(); e.Handled = true; }
                else if (e.Control && e.KeyCode == Keys.P) { Print(); e.Handled = true; }
                else if (e.Control && (e.KeyCode == Keys.Add || e.KeyCode == Keys.Oemplus)) { SetZoom(view.Zoom * 1.2f); e.Handled = true; }
                else if (e.Control && (e.KeyCode == Keys.Subtract || e.KeyCode == Keys.OemMinus)) { SetZoom(view.Zoom / 1.2f); e.Handled = true; }
                else if (e.KeyCode == Keys.Escape) Close();
            };

            if (AppTheme.Dark) AppTheme.Apply(this);
            Reload();
            UpdateZoomLabel();
        }

        /// <summary>Relit le programme (aussi appelé pendant la frappe dans l'éditeur).</summary>
        internal void Reload()
        {
            var g = FlowGraph.Parse(source?.Invoke() ?? "");
            view.Graph = g;
            int jumps = g.Edges.FindAll(e => e.Kind == EdgeKind.Jump).Count;
            status.Text = $"{g.Count(NodeKind.Block)} blocs · {g.Count(NodeKind.Test)} tests · {g.Count(NodeKind.Call)} appels XEQ · {g.Count(NodeKind.IO)} entrées/sorties · {jumps} sauts   —   " +
                          "O : condition vraie, ligne suivante exécutée · N : fausse, ligne suivante sautée · double-clic : aller à la ligne";
        }

        void SetZoom(float z)
        {
            view.Zoom = z;
            UpdateZoomLabel();
        }

        void UpdateZoomLabel() => zoomLabel.Text = $"{Math.Round(view.Zoom * 100)} %";

        void FitWidth()
        {
            if (view.ChartSize.Width <= 0) return;
            SetZoom((view.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4) / view.ChartSize.Width);
        }

        /// <summary>Image du logigramme entier ; scale 2 : nette une fois imprimée ou collée.</summary>
        Bitmap Render(float scale)
        {
            var s = view.ChartSize;
            var bmp = new Bitmap(Math.Max(1, (int)Math.Ceiling(s.Width * scale)), Math.Max(1, (int)Math.Ceiling(s.Height * scale)));
            bmp.SetResolution(96 * scale, 96 * scale);
            using (var g = Graphics.FromImage(bmp))
            {
                g.ScaleTransform(scale, scale);
                view.Draw(g, new RectangleF(0, 0, s.Width, s.Height));
            }
            return bmp;
        }

        void CopyImage()
        {
            try
            {
                using (var bmp = Render(1.5f)) Clipboard.SetImage(bmp);
                status.Text = "Logigramme copié dans le presse-papiers.";
            }
            catch (Exception ex) { status.Text = "Copie impossible : " + ex.Message; }
        }

        void ExportPng()
        {
            using (var dlg = new SaveFileDialog { Filter = "Image PNG (*.png)|*.png", FileName = Path.GetFileNameWithoutExtension(title) + " - logigramme.png" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    using (var bmp = Render(2f)) bmp.Save(dlg.FileName, ImageFormat.Png);
                    status.Text = "Enregistré : " + dlg.FileName;
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Export impossible", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
        }

        /// <summary>Impression à la largeur de la page, coupée entre deux formes quand c'est possible.</summary>
        void Print()
        {
            var size = view.ChartSize;
            if (size.Width <= 0) return;
            float pageTop = 0;
            int page = 0;
            using (var doc = new PrintDocument { DocumentName = "Logigramme " + title })
            {
                doc.BeginPrint += (s, e) => { pageTop = 0; page = 0; };
                doc.PrintPage += (s, e) =>
                {
                    var m = e.MarginBounds;
                    float scale = Math.Min(1f, m.Width / size.Width);
                    float pageH = (m.Height - 24) / scale;
                    // coupe juste au-dessus de la dernière forme qui commence dans la page
                    float cut = pageTop + pageH;
                    if (cut < size.Height)
                    {
                        float best = pageTop;
                        foreach (var n in view.Graph.Nodes)
                            if (n.Bounds.Top - 6 > pageTop + 40 && n.Bounds.Bottom <= cut) best = Math.Max(best, n.Bounds.Bottom + 6);
                        if (best > pageTop + pageH / 3) cut = best;
                    }
                    page++;
                    var g = e.Graphics;
                    using (var f = new Font("Segoe UI", 8.5f))
                        g.DrawString($"{title} — logigramme, page {page}", f, Brushes.Gray, m.Left, m.Top);
                    var state = g.Save();
                    g.SetClip(new RectangleF(m.Left, m.Top + 20, m.Width, (cut - pageTop) * scale + 2));
                    g.TranslateTransform(m.Left, m.Top + 20);
                    g.ScaleTransform(scale, scale);
                    g.TranslateTransform(0, -pageTop);
                    view.Draw(g, new RectangleF(0, pageTop, size.Width, cut - pageTop));
                    g.Restore(state);
                    pageTop = cut;
                    e.HasMorePages = pageTop < size.Height - 1;
                };
                using (var dlg = new PrintDialog { Document = doc, UseEXDialog = true })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    try { doc.Print(); }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, "Impression impossible", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                }
            }
        }
    }
}
