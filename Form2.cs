using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace dm42editor
{
    /// <summary>
    /// Paramètres : thème clair / sombre de l'application et couleurs du code RPN (4 schémas).
    /// Les couleurs se choisissent comme dans la fenêtre Python : une pastille par élément, qui ouvre
    /// le sélecteur de couleurs Windows ; l'éditeur se met à jour en direct.
    /// </summary>
    public partial class frmParam : Form
    {
        static readonly string[] SchemaNames = { "Noir", "Blanc", "Personnalisé 1", "Personnalisé 2" };
        static readonly string[] RowNames = { "Fond de page", "Instructions (mots-clés)", "Nombres", "Textes \"…\"", "Commentaires", "Tests (X<Y?, FS?…)", "Ligne du curseur" };

        clParam Param = new clParam();
        clParam.pram work = new clParam().parameters;     // copie modifiée dans la fenêtre

        readonly ComboBox schemaBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        readonly CheckBox darkBox = new CheckBox { Text = "Thème sombre (menus, panneaux et fenêtres)", AutoSize = true };
        readonly List<Button> swatches = new List<Button>();
        readonly List<CheckBox> bolds = new List<CheckBox>();
        bool loading;

        /// <summary>Aperçu en direct : schéma affiché et thème sombre.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Action<clParam.lp, bool> Preview { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public clParam Parameter
        {
            get => Param;
            set
            {
                Param = value;
                work = Copy(value.parameters);
                LoadUi();
            }
        }

        /// <summary>Schéma en cours d'édition.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public clParam.lp param
        {
            get => work.Params[work.current];
            set { work.Params[work.current] = value; LoadUi(); }
        }

        public frmParam()
        {
            InitializeComponent();
            BuildUi();
            LoadUi();
        }

        static clParam.pram Copy(clParam.pram p) =>
            new clParam.pram { current = p.current, Params = new List<clParam.lp>(p.Params), DarkMode = p.DarkMode };

        void BuildUi()
        {
            Text = "Paramètres";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9.5f);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(14);

            var grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Dock = DockStyle.Fill };
            Label L(string t, bool title = false) => new Label
            {
                Text = t, AutoSize = true, Anchor = AnchorStyles.Left,
                Margin = new Padding(0, title ? 14 : 6, 16, 4),
                Font = title ? new Font("Segoe UI", 9.5f, FontStyle.Bold) : null
            };

            // chaque élément a sa cellule : une case « Gras » masquée ne décale pas la ligne
            int r = 0;
            void Span(Control c) { grid.Controls.Add(c, 0, r++); grid.SetColumnSpan(c, 3); }
            Span(L("Apparence", true));
            Span(darkBox);

            Span(L("Couleurs du code RPN", true));
            grid.Controls.Add(L("Schéma"), 0, r);
            schemaBox.Items.AddRange(SchemaNames);
            grid.Controls.Add(schemaBox, 1, r);
            var reset = new Button { Text = "Valeurs par défaut", AutoSize = true };
            reset.Click += (s, e) =>
            {
                work.Params[work.current] = new clParam().parameters.Params[work.current];
                LoadUi();
                DoPreview();
            };
            grid.Controls.Add(reset, 2, r++);

            for (int i = 0; i < RowNames.Length; i++)
            {
                int row = i;
                var b = new Button { Width = 200, Height = 26, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Tag = "keep", Font = new Font("Segoe UI", 8.5f) };
                b.FlatAppearance.BorderColor = Color.FromArgb(110, 108, 100);
                b.Click += (s, e) =>
                {
                    using (var dlg = new ColorDialog { Color = GetColor(param, row), FullOpen = true, AnyColor = true })
                        if (dlg.ShowDialog(this) == DialogResult.OK) { SetColor(row, dlg.Color); LoadUi(); DoPreview(); }
                };
                swatches.Add(b);
                var bold = new CheckBox { Text = "Gras", AutoSize = true, Anchor = AnchorStyles.Left, Visible = GetBold(param, row) != null };
                bold.CheckedChanged += (s, e) => { if (!loading) { SetBold(row, bold.Checked); DoPreview(); } };
                bolds.Add(bold);
                grid.Controls.Add(L(RowNames[row]), 0, r);
                grid.Controls.Add(b, 1, r);
                grid.Controls.Add(bold, 2, r++);
            }

            schemaBox.SelectedIndexChanged += (s, e) =>
            {
                if (loading) return;
                work.current = schemaBox.SelectedIndex;
                LoadUi();
                DoPreview();
            };
            darkBox.CheckedChanged += (s, e) =>
            {
                if (loading) return;
                work.DarkMode = darkBox.Checked;
                AppTheme.Set(work.DarkMode);
                AppTheme.Apply(this);
                DoPreview();
            };

            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(90, 0) };
            var cancel = new Button { Text = "Annuler", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(90, 0) };
            ok.Click += (s, e) => { Param.parameters = Copy(work); Param.SaveParam(); };
            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 16, 0, 0) };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            var hint = new Label { Text = "L'éditeur se met à jour en direct.", AutoSize = true, ForeColor = Color.Gray, Tag = "keep", Anchor = AnchorStyles.Left, Margin = new Padding(0, 20, 0, 0) };
            grid.Controls.Add(hint, 0, r);
            grid.SetColumnSpan(hint, 2);
            grid.Controls.Add(buttons, 2, r);

            Controls.Add(grid);
            AcceptButton = ok;
            CancelButton = cancel;
            if (AppTheme.Dark) AppTheme.Apply(this);
        }

        void LoadUi()
        {
            if (schemaBox.Items.Count == 0) return;
            loading = true;
            schemaBox.SelectedIndex = Math.Max(0, Math.Min(SchemaNames.Length - 1, work.current));
            darkBox.Checked = work.DarkMode;
            var p = param;
            for (int i = 0; i < swatches.Count; i++)
            {
                var c = GetColor(p, i);
                swatches[i].BackColor = c;
                swatches[i].ForeColor = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B > 140 ? Color.Black : Color.White;
                swatches[i].Text = c.IsNamedColor ? c.Name : $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                var b = GetBold(p, i);
                if (b != null) bolds[i].Checked = b.Value;
            }
            loading = false;
        }

        void DoPreview() => Preview?.Invoke(param, work.DarkMode);

        static Color GetColor(clParam.lp p, int row) => row switch
        {
            0 => p.BackColor, 1 => p.WordColor, 2 => p.NumColor, 3 => p.StringColor, 4 => p.CommentColor, 5 => p.TestColor, _ => p.LineColor
        };

        void SetColor(int row, Color c)
        {
            var p = param;
            switch (row)
            {
                case 0: p.BackColor = c; break;
                case 1: p.WordColor = c; break;
                case 2: p.NumColor = c; break;
                case 3: p.StringColor = c; break;
                case 4: p.CommentColor = c; break;
                case 5: p.TestColor = c; break;
                default: p.LineColor = c; break;
            }
            work.Params[work.current] = p;
        }

        static bool? GetBold(clParam.lp p, int row) => row switch
        {
            1 => p.WordBold, 2 => p.NumBold, 3 => p.StringBold, 4 => p.CommentBold, 5 => p.TestBold, _ => (bool?)null
        };

        void SetBold(int row, bool on)
        {
            var p = param;
            switch (row)
            {
                case 1: p.WordBold = on; break;
                case 2: p.NumBold = on; break;
                case 3: p.StringBold = on; break;
                case 4: p.CommentBold = on; break;
                case 5: p.TestBold = on; break;
            }
            work.Params[work.current] = p;
        }
    }

    public class clParam
    {
        [Serializable]
        public struct lp
        {
            public Color BackColor; 
            public Color  NumColor;
            public Color WordColor;
            public Color StringColor;
            public Color CommentColor;
            public Color LineColor;


            public bool NumBold ;
            public bool  WordBold;
            public bool StringBold;
            public bool CommentBold;

            /// <summary>Tests : X&lt;Y?, X=0?, FS? 01…</summary>
            public Color TestColor;
            public bool TestBold;
        }

        /// <summary>Couleur des tests par défaut : doré sur fond sombre, orange brûlé sur fond clair (distincte des mots-clés bleus).</summary>
        public static Color DefaultTestColor(Color back) =>
            back.GetBrightness() < 0.5f ? Color.FromArgb(240, 190, 80) : Color.FromArgb(184, 86, 27);

        [Serializable]
        public class pram
        {
            public int current=0;
            public List<lp> Params = new List<lp>();
            /// <summary>Thème sombre de l'application.</summary>
            public bool DarkMode;

            public pram()
            {

            }
        }


        public pram parameters = new pram();   


        public clParam()
        {

            // Black
            lp lLp = new lp();

            lLp.BackColor = Color.Black;
            lLp.NumColor = Color.LightBlue;
            lLp.WordColor = Color.CornflowerBlue;
            lLp.StringColor = Color.Red;
            lLp.CommentColor = Color.Green;
            lLp.LineColor = Color.Aqua;
            lLp.TestColor = DefaultTestColor(lLp.BackColor);

            lLp.NumBold = false;
            lLp.WordBold = false;
            lLp.StringBold = false;
            lLp.CommentBold = false;

            parameters.Params.Add(lLp);


            // White 

            lLp = new lp();

            lLp.BackColor = Color.White;
            lLp.NumColor = Color.LightBlue;
            lLp.WordColor = Color.CornflowerBlue;
            lLp.StringColor = Color.Red;
            lLp.CommentColor = Color.Green;
            lLp.LineColor = Color.Aqua;
            lLp.TestColor = DefaultTestColor(lLp.BackColor);

            lLp.NumBold = false;
            lLp.WordBold = false;
            lLp.StringBold = false;
            lLp.CommentBold = false;

            parameters.Params.Add(lLp);

            // Custom 1
            lLp = new lp();

            lLp.BackColor = Color.White;
            lLp.NumColor = Color.LightBlue;
            lLp.WordColor = Color.CornflowerBlue;
            lLp.StringColor = Color.Red;
            lLp.CommentColor = Color.Green;
            lLp.LineColor = Color.Aqua;
            lLp.TestColor = DefaultTestColor(lLp.BackColor);

            lLp.NumBold = false;
            lLp.WordBold = false;
            lLp.StringBold = false;
            lLp.CommentBold = false;

            parameters.Params.Add(lLp);

            // Custom 2
            lLp = new lp();

            lLp.BackColor = Color.White;
            lLp.NumColor = Color.LightBlue;
            lLp.WordColor = Color.CornflowerBlue;
            lLp.StringColor = Color.Red;
            lLp.CommentColor = Color.Green;
            lLp.LineColor = Color.Aqua;
            lLp.TestColor = DefaultTestColor(lLp.BackColor);

            lLp.NumBold = false;
            lLp.WordBold = false;
            lLp.StringBold = false;
            lLp.CommentBold = false;

            parameters.Params.Add(lLp);

            parameters.current = 0; 

        }

        public void LoadParam()
        {
            DeserializeParam(ref parameters, "param.json");
            // param.json d'une version sans couleur des tests : couleur par défaut selon le fond
            for (int i = 0; i < parameters.Params.Count; i++)
            {
                var p = parameters.Params[i];
                if (p.TestColor.A == 0) { p.TestColor = DefaultTestColor(p.BackColor); parameters.Params[i] = p; }
            }
        }

        public void SaveParam()
        {
            SerializeParam(parameters, "param.json");
        }

        // BinaryFormatter n'existe plus en .NET 9+ : les paramètres sont sauvés en JSON
        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            IncludeFields = true,
            WriteIndented = true,
            Converters = { new ColorJsonConverter() }
        };

        // une couleur nommée est sauvée par son nom (utilisé par les ColorPickerEx), sinon en #AARRGGBB
        private class ColorJsonConverter : JsonConverter<Color>
        {
            public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                string s = reader.GetString();
                if (s.StartsWith("#"))
                    return Color.FromArgb(Convert.ToInt32(s.Substring(1), 16));
                return Color.FromName(s);
            }

            public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.IsNamedColor ? value.Name : "#" + value.ToArgb().ToString("X8"));
            }
        }

        private void SerializeParam(pram lpt, string FileName)
        {
            File.WriteAllText(FileName, JsonSerializer.Serialize(lpt, jsonOptions));
        }

        /// <summary>
        ///  Relecture du dictionnaire 
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        private void DeserializeParam(ref pram lpt, string FileName)
        {
            if (File.Exists(FileName))
            {
                lpt = JsonSerializer.Deserialize<pram>(File.ReadAllText(FileName), jsonOptions);
            }
        }
    }
}
