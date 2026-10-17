using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;
using ColorPicker; 

namespace dm42editor
{
    public partial class frmParam : Form
    {

        clParam Param= new clParam();


        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public clParam  Parameter
        {
            get { return Param;  }
            set
            {
                Param = value;

                int lIdx = Param.parameters.current;

               

                clParam.lp llp = Param.parameters.Params[lIdx];

                colorPickerEx1.SelectedText = llp.BackColor.Name;
                colorPickerEx2.SelectedText = llp.WordColor.Name;
                colorPickerEx3.SelectedText = llp.NumColor.Name;
                colorPickerEx4.SelectedText = llp.StringColor.Name;
                colorPickerEx5.SelectedText = llp.CommentColor.Name;
                colorPickerEx6.SelectedText = llp.LineColor.Name;

                checkBox1.Checked = llp.WordBold;
                checkBox2.Checked = llp.NumBold;
                checkBox3.Checked = llp.StringBold;
                checkBox4.Checked = llp.CommentBold;

                Param.parameters.Params[lIdx] = llp;

                comboBox1.SelectedIndex = lIdx;

            }
        }


        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public clParam.lp param
        {
            get {

                int lIdx = Param.parameters.current;

                return Param.parameters.Params[lIdx]; 
            
            }
            set 
            {
                int lIdx = Param.parameters.current;

                clParam.lp llp = value; 

                colorPickerEx1.SelectedText = llp.BackColor.Name;
                colorPickerEx2.SelectedText = llp.WordColor.Name;
                colorPickerEx3.SelectedText = llp.NumColor.Name;
                colorPickerEx4.SelectedText = llp.StringColor.Name;
                colorPickerEx5.SelectedText = llp.CommentColor.Name;
                colorPickerEx6.SelectedText = llp.LineColor.Name;

                checkBox1.Checked = llp.WordBold;
                checkBox2.Checked = llp.NumBold;
                checkBox3.Checked = llp.StringBold;
                checkBox4.Checked = llp.CommentBold;

                Param.parameters.Params[lIdx] = llp; 

            }

        }
        public frmParam()
        {
            InitializeComponent();

            int lIdx = Param.parameters.current;
            clParam.lp lLp = Param.parameters.Params[lIdx];

            colorPickerEx1.Items = new KnownColorCollection(KnownColorFilter.Web);
            colorPickerEx1.SelectedText = lLp.BackColor.Name; 

            colorPickerEx2.Items = new KnownColorCollection(KnownColorFilter.Web);
            colorPickerEx2.SelectedText = lLp.WordColor.Name;
            checkBox1.Checked = lLp.WordBold; 

            colorPickerEx3.Items = new KnownColorCollection(KnownColorFilter.Web);
            colorPickerEx3.SelectedText = lLp.NumColor.Name;
            checkBox2.Checked = lLp.NumBold;

            colorPickerEx4.Items = new KnownColorCollection(KnownColorFilter.Web);
            colorPickerEx4.SelectedText = lLp.StringColor.Name;
            checkBox3.Checked = lLp.StringBold;

            colorPickerEx5.Items = new KnownColorCollection(KnownColorFilter.Web);
            colorPickerEx5.SelectedText = lLp.CommentColor.Name;
            checkBox4.Checked = lLp.CommentBold;

            colorPickerEx6.Items = new KnownColorCollection(KnownColorFilter.Web);
            colorPickerEx6.SelectedText = lLp.LineColor.Name;
            // colorPickerEx2.Items = new KnownColorCollection(KnownColorFilter.System);

            comboBox1.Items.Add("Black");
            comboBox1.Items.Add("White");
            comboBox1.Items.Add("Custom_1");
            comboBox1.Items.Add("Custom_2");

            comboBox1.SelectedIndex = 0; 
        }

        private void colorPickerEx1_SelectedIndexChanged(object sender, EventArgs e)
        {
          //  if (this.colorPickerEx1.SelectedText.Length > 0) this.BackColor = Color.FromName(this.colorPickerEx1.SelectedText);


        }

        private void colorPickerEx2_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void colorPickerEx3_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void colorPickerEx4_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void colorPickerEx5_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {

            // Recupere les parametres
            int lIdx = Param.parameters.current;

            clParam.lp  lLp= Param.parameters.Params[lIdx];

            lLp.BackColor = Color.FromName(this.colorPickerEx1.SelectedText);
            lLp.WordColor = Color.FromName(this.colorPickerEx2.SelectedText);
            lLp.NumColor = Color.FromName(this.colorPickerEx3.SelectedText);
            lLp.StringColor = Color.FromName(this.colorPickerEx4.SelectedText);
            lLp.CommentColor = Color.FromName(this.colorPickerEx5.SelectedText);
            lLp.LineColor = Color.FromName(this.colorPickerEx6.SelectedText);

            lLp.WordBold = checkBox1.Checked;
            lLp.NumBold = checkBox2.Checked;
            lLp.StringBold = checkBox3.Checked;
            lLp.CommentBold = checkBox4.Checked;

            Param.parameters.Params[lIdx] = lLp; 

            Param.SaveParam(); 

            this.Close(); 
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Recupereles parametres
            clParam.lp lLp = Param.parameters.Params[Param.parameters.current];
            lLp.BackColor = Color.FromName(colorPickerEx1.SelectedText);
            lLp.WordColor = Color.FromName(colorPickerEx2.SelectedText)  ;
            lLp.NumColor = Color.FromName(colorPickerEx3.SelectedText)  ;
            lLp.StringColor = Color.FromName(colorPickerEx4.SelectedText) ;
            lLp.CommentColor= Color.FromName(colorPickerEx5.SelectedText)  ;
            lLp.LineColor = Color.FromName(colorPickerEx6.SelectedText)  ;
            Param.parameters.Params[Param.parameters.current] = lLp; 

            // Changement de schéma
            Param.parameters.current = comboBox1.SelectedIndex;

             lLp = Param.parameters.Params[Param.parameters.current];
            // affiche les couleurs du schéma 
            colorPickerEx1.SelectedText = lLp.BackColor.Name;
            colorPickerEx2.SelectedText = lLp.WordColor.Name;
            checkBox1.Checked = lLp.WordBold;
            colorPickerEx3.SelectedText = lLp.NumColor.Name;
            checkBox2.Checked = lLp.NumBold;
            colorPickerEx4.SelectedText = lLp.StringColor.Name;
            checkBox3.Checked = lLp.StringBold;
            colorPickerEx5.SelectedText = lLp.CommentColor.Name;
            checkBox4.Checked = lLp.CommentBold;
            colorPickerEx6.SelectedText = lLp.LineColor.Name;



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

        }

        [Serializable]
        public class pram
        {
            public int current=0;
            public List<lp> Params = new List<lp>();

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
