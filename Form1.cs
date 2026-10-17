using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using FarsiLibrary.Win;
using FastColoredTextBoxNS;
using System.Threading;
using System.Text.RegularExpressions;
using DM42;
using DM42.decoder;
using DM42.encoder; 


/*
 TODO: detruire le fichier raw avant de creer une nouvelle fenêtre data 
        rendre la fenêtre d'aide sur les fonctions flottante
        etudier fonction avec dessin des sauts 
 
 */


namespace dm42editor
{
    public partial class Form1 : Form
    {


        string[] keywords = { "LBL",
            "ABS",
            "ACOS",
            "ACOSH",
            "ADV",
            "AGRAPH",
            "AIP",
            "ALENG",
            "ALL",
            "ALLΣ",
            "AND",
            "AOFF",
            "AON",
            "ARCL",
            "ARCL IND",
            "ARCL ST",
            "ARCL IND ST",
            "AROT",
            "ASHF",
            "ASIN",
            "ASINH",
            "ASSIGN",
            "ASTO",
            "ASTO IND",
            "ASTO ST",
            "ASTO IND ST",
            "ATAN",
            "ATANH",
            "ATOX",
            "AVIEW",
            "BASE+",
            "BASE-",
            "BASE×",
            "BASE÷",
            "BASE+/-",
            "BEEP",
            "BEST",
            "BINM",
            "BIT?",
            "CF",
            "CF IND",
            "CF IND ST",
            "CLA",
            "CLD",
            "CLKEYS",
            "CLLCD",
            "CLMENU",
            "CLP",
            "CLRG",
            "CLST",
            "CLV IND",
            "CLV IND ST",
            "CLV",
            "CLX",
            "CLΣ",
            "COMB",
            "COMPLEX",
            "CORR",
            "COS",
            "COSH",
            "CPXRES",
            "CPX?",
            "CROSS",
            "CUSTOM",
            "DECM",
            "DEG",
            "DELAY",
            "DELR",
            "DET",
            "DIM IND",
            "DIM IND ST",
            "DIM",
            "DIM?",
            "DOT",
            "DSE",
            "DSE IND",
            "DSE ST",
            "DSE IND ST",
            "EDIT",
            "EDITN IND",
            "EDITN IND ST",
            "EDITN",
            "ENG",
            "ENG IND",
            "ENG IND ST",
            "ENTER",
            "EXITALL",
            "EXPF",
            "E^X",
            "E^X-1",
            "FC?",
            "FC? IND",
            "FC? IND ST",
            @"FC\?C",
            "FC?C IND",
            "FC?C IND ST",
            "FCSTX",
            "FCSTY",
            "FIX",
            "FIX IND",
            "FIX IND ST",
            "FNRM",
            "FP",
            "FS?",
            "FS? IND",
            "FS? IND ST",
            "FS?C",
            "FS?C IND",
            "FS?C IND ST",
            "GAMMA",
            "GETKEY",
            "GETM",
            "GRAD",
            "GROW",
            "GTO",
            "GTO IND",
            "GTO IND ST",
            "HEXM",
            "HMS+",
            "HMS-",
            "I+",
            "I-",
            "INDEX IND",
            "INDEX IND ST",
            "INDEX",
            "INPUT",
            "INPUT IND",
            "INPUT ST",
            "INPUT IND ST",
            "INSR",
            "INTEG IND",
            "INTEG IND ST",
            "INTEG",
            "INVRT",
            "IP",
            "ISG",
            "ISG IND",
            "ISG ST",
            "ISG IND ST",
            "KEYASN",
            "KEY",
            "LASTX",
            "LCLBL",
            "LINF",
            "LINΣ",
            "LN",
            "LN1+X",
            "LOG",
            "LOGF",
            "MAN",
            "MAT?",
            "MEAN",
            "MENU",
            "MOD",
            "MVAR",
            "N!",
            "NEWMAT",
            "NORM",
            "NOT",
            "OCTM",
            "OFF",
            "OLD",
            "ON",
            "OR",
            "PERM",
            "PGMINT IND",
            "PGMINT IND ST",
            "PGMSLV IND",
            "PGMSLV IND ST",
            "PGMSLV",
            "PI",
            "PIXEL",
            "POLAR",
            "POSA",
            "PRA",
            "PRLCD",
            "PROFF",
            "PROMPT",
            "PRON",
            "PRSTK",
            "PRUSR",
            "PRV IND",
            "PRV IND ST",
            "PRV",
            "PRX",
            "PRΣ",
            "PSE",
            "PUTM",
            "PWRF",
            "RAD",
            "RAN",
            "RCL",
            "RCL IND",
            "RCL ST",
            "RCL IND ST",
            "RCL+",
            "RCL+ IND",
            "RCL+ ST",
            "RCL+ IND ST",
            "RCL-",
            "RCL- IND",
            "RCL- ST",
            "RCL- IND ST",
            "RCL×",
            "RCL× IND",
            "RCL× ST",
            "RCL× IND ST",
            "RCL÷",
            "RCL÷ IND",
            "RCL÷ ST",
            "RCL÷ IND ST",
            "RCLEL",
            "RCLIJ",
            "RDX,",
            "RDX.",
            "REALRES",
            "REAL?",
            "RECT",
            "RND",
            "RNRM",
            "ROTXY",
            "RSUM",
            "RTN",
            "R<>R",
            "R^",
            "R↓",
            "SCI",
            "SCI IND",
            "SCI IND ST",
            "SDEV",
            "SEED",
            "SF",
            "SF IND",
            "SF IND ST",
            "SIGN",
            "SIN",
            "SINH",
            "SIZE",
            "SLOPE",
            "SOLVE IND",
            "SOLVE IND ST",
            "SQRT",
            "STO",
            "STO IND",
            "STO ST",
            "STO IND ST",

            "STO+",
            "STO+ IND",
            "STO+ ST",
            "STO+ IND ST",

            "STO-",
            "STO- IND",
            "STO- ST",
            "STO- IND ST",

            "STO×",
            "STO× IND",
            "STO× ST",
            "STO× IND ST",

            "STO÷",
            "STO÷ IND",
            "STO÷ ST",
            "STO÷ IND ST",

            "STOEL",
            "STOIJ",
            "STOP",
            "STR?",
            "SUM",
            "TAN",
            "TANH",
            "TONE",
            "TONE IND",
            "TONE IND ST",

            "TRACE",
            "TRANS",
            "UVEC",
            "VARMENU IND",
            "VARMENU IND ST",
            "VARMENU",
            "VIEW",
            "VIEW IND",
            "VIEW ST",
            "VIEW IND ST",
            "WMEAN",
            "WRAP",
            "X<>",
            "X<> IND",
            "X<> ST",
            "X<> IND ST",
            "X<>Y",
            "X<0?",
            "X<Y?",
            "X≤0?",
            "X≤Y?",
            "X=0?",
            "X=Y?",
            "X≠0?",
            "X≠Y?",
            "X>0?",
            "X>Y?",
            "X≥0?",
            "X≥Y?",
            "XEQ",
            "XEQ IND",
            "XEQ IND ST",

            "XOR",
            "XTOA",
            "X^2",
            "YINT",
            "Y^X",
            "1/X",
            "10^X",
            "+",
            "-",
            "×",
            "÷",
            "+/-",
            "Σ+",
            "Σ-",
            "ΣREG",
            "ΣREG IND",
            "ΣREG IND ST",
            "ΣREG?",
            "→DEC",
            "→DEG",
            "→HMS",
            "→HR",
            "→OCT",
            "→POL",
            "→RAD",
            "→REC",
            "←",
            "^",
            "↓",
            "→",
            "%",
            "%CH",
            "[FIND]",
            "[MAX]",
            "[MIN]",

            "END"


        };


        clParam Parameter=new clParam();

        clParam.lp gllp = new clParam.lp(); // parametre par defaut 

        string rpnStyle = "testHL.xml"; // fichier externe de style 

        Style NumberStyle; 
        Style WordStyle;
        Style StringStyle; 
        Style CommentStyle; 
       
        MarkerStyle SameWordsStyle = new MarkerStyle(new SolidBrush(Color.FromArgb(40, Color.Gray)));

        Regex HP42StringRegex = new Regex(@"""([^\\""]|\\"")*""",RegexOptions.Compiled);
        Regex HP42NumberRegex = new Regex(@"\b(\d+[\.]?\d*)\b", RegexOptions.Compiled);
        Regex HP42KeywordsRegex = new Regex(@"\b(|\-|\+|\÷|LBL|ABS|ACOS|ACOSH|ADV|AGRAPH|AIP|ALENG|ALL|ALLΣ|AND|AOFF|AON|ARCL|IND|ST|AROT|ASHF|ASIN|ASINH|ASSIGN|ASTO|ATAN|ATANH|ATOX|AVIEW|BASE+|BASE-|BASE×|BASE÷|BASE+/-|BEEP|BEST|BINM|BIT?|CF|CLA|CLD|CLKEYS|CLLCD|CLMENU|CLP|CLRG|CLST|CLV|CLX|CLΣ|COMB|COMPLEX|CORR|COS|COSH|CPXRES|CPX?|CROSS|CUSTOM|DECM|DEG|DELAY|DELR|DET|DIM|DIM?|DOT|DSE|EDIT|EDITN|ENG|ENTER|EXITALL|EXPF|E^X|E^X-1|FC?|FC?|FC\\?C|FC?C|FCSTX|FCSTY|FIX|FNRM|FP|FS?|FS?|FS?C|FS?C|GAMMA|GETKEY|GETM|GRAD|GROW|GTO|GTO|HEXM|HMS+|HMS-|I+|I-|INDEX|INPUT|INSR|INTEG|INTEG|INVRT|IP|ISG|ISG|KEYASN|KEY|LASTX|LCLBL|LINF|LINΣ|LN|LN1+X|LOG|LOGF|MAN|MAT?|MEAN|MENU|MOD|MVAR|N!|NEWMAT|NORM|NOT|OCTM|OFF|OLD|ON|OR|PERM|PGMINT|PGMSLV|PI|PIXEL|POLAR|POSA|PRA|PRLCD|PROFF|PROMPT|PRON|PRSTK|PRUSR|PRV|PRX|PRΣ|PSE|PUTM|PWRF|RAD|RAN|RCL|RCL+|RCL+|RCL-|RCL×|RCL÷|RCLEL|RCLIJ|RDX,|RDX.|REALRES|REAL?|RECT|RND|RNRM|ROTXY|RSUM|RTN|R<>R|R^|R↓|SCI|SDEV|SEED|SF|SIGN|SIN|SINH|SIZE|SLOPE|SOLVE|SQRT|STO|STO+|STO-|STO×|STO÷|STOEL|STOIJ|STOP|STR?|SUM|TAN|TANH|TONE|TRACE|TRANS|UVEC|VARMENU|VARMENU|VIEW|WMEAN|WRAP|X<>|X<>Y|X<0?|X<Y?|X≤0?|X≤Y?|X=0?|X=Y?|X≠0?|X≠Y?|X>0?|X>Y?|X≥0?|X≥Y?|XEQ|XOR|XTOA|X^2|YINT|Y^X|1/X|10^X|-|×|÷|Σ+|Σ-|ΣREG|ΣREG?|→DEC|→DEG|→HMS|→HR|→OCT|→POL|→RAD|→REC|←|^|↓|→|%|%CH|[FIND]|[MAX]|[MIN]|END)\b", RegexOptions.Compiled);
     
          Regex HP42CommentRegex1 = new Regex(@"//.*$", RegexOptions.Multiline | RegexOptions.Compiled);
          Regex HP42CommentRegex2= new Regex(@"@.*$", RegexOptions.Multiline | RegexOptions.Compiled);
          Regex HP42CommentRegex3 = new Regex(@"#.*$", RegexOptions.Multiline | RegexOptions.Compiled);

      //  Regex HP42CommentRegex1 = new Regex(@"(//|@|#)(.*?)\r?\n", RegexOptions.Multiline | RegexOptions.Compiled);

        

        Regex dm42NumberRegex = new Regex(@"\b\d+[\.]?\d*([eE]\-?\d+)?[lLdDfF]?\b|\b0x[a-fA-F\d]+\b", RegexOptions.Compiled);

   
        string KeyWords = ""; 

        private Style sameWordsStyle = new MarkerStyle(new SolidBrush(Color.FromArgb(50, Color.DarkMagenta)));

        // private FarsiLibrary.Win.FATabStrip tsFiles;

        DateTime lastNavigatedDateTime = DateTime.Now;

        Color currentLineColor = Color.FromArgb(100, 210, 210, 255);
        Color changedLineColor = Color.FromArgb(255, 230, 230, 255);

        public Form1()
        {
            InitializeComponent();

            Parameter.LoadParam();

            gllp= Parameter.parameters.Params[Parameter.parameters.current]; 

            NumberStyle = new TextStyle(new  SolidBrush(gllp.NumColor), null, (gllp.NumBold) ? FontStyle.Bold : FontStyle.Regular);    // Number 
            WordStyle = new TextStyle(new SolidBrush(gllp.WordColor), null, (gllp.WordBold) ? FontStyle.Bold : FontStyle.Regular);    //word
            StringStyle = new TextStyle(new SolidBrush(gllp.StringColor), null, (gllp.StringBold) ? FontStyle.Bold : FontStyle.Regular);         // String 
            CommentStyle = new TextStyle(new  SolidBrush(gllp.CommentColor), null, (gllp.CommentBold) ? FontStyle.Bold : FontStyle.Regular);       // comment 

            InitFctList(); // initialise la liste de fonctions 

        }



        #region MENU 

        private void quitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Quitter le programme 

            Close();
        }

        private void newProgramToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Nouveau programme 
            // CreateTab(null);
            InitNewFileTab(null);
        }

        private void viewRawDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Encode to raw 
            if (CurrentTB != null)
            {

                List<string> lDoc = new List<string>();

                // encode programme 
                Encoder42.initialize();

                RpnParser Parser = new RpnParser();

                foreach (string line in CurrentTB.Lines)
                {
                    lDoc.Add(Encoder42.FormatRpnLine(line));
                }


                Parser.SetDocument(lDoc);
                Parser.parse();
                if (CurrentTB.Parent.Tag == null)
                {
                    MessageBox.Show("You must save the file before !");
                    return;
                }

                string titre = CurrentTB.Parent.Tag.ToString(); // Recupere le chemin complet du fichier 

                RawViewer tb = new RawViewer();

                tb.SetTabStrib = tsFiles; 

                tb.Dock = DockStyle.Fill;
                tb.Clear();
                string TabName = Path.GetFileName(titre);

                tb.Tag = CurrentTB.Parent;//  TabName; // lien avec la page 

                TabName = Path.ChangeExtension(TabName, "Encode");

                bool tt = tsFiles.Contains(tb);

                // detruit le tab si existant
                //!! a faire 

                var tab = new FATabStripItem(TabName, tb);

                for (int lIdx = 0; lIdx < Parser.programs.Count; lIdx++)
                {
                    foreach (RawLine line in Parser.programs[lIdx].rawLines)
                    {
                        RawLine lLine = line;
                        Encoder42.toRaw(lLine, "free42");
                        if (lLine.raw != null)
                        {
                            //  listBox2.Items.Add(lLine.raw);
                            tb.Add(lLine.docCode, lLine.raw);
                        }
                        else
                        {
                            if (lLine.error != null)
                            {
                                tb.Add(lLine.docCode, "", lLine.error.Message);
                            }
                            else
                            {
                                tb.Add(lLine.docCode, "", "Erreur !");
                            }
                        }
                    }

                }

                //======

                // Afiche la page 

                tsFiles.AddTab(tab);
                tsFiles.SelectedItem = tab;


                //----   
            }

        }

        private void OpenPrgmToolStripMenuItem_Click(object sender, EventArgs e)
        {

            ofdMain.Filter = "free42 file(*.free42)| *.free42|dm42 file(*.s42)|*.s42|HP42s file(*.hp42s)|*.hp42s|Raw file(*.raw)|*.raw|all files(*.*) | *.*";
            // ouverture d'un programme 
            if (ofdMain.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {

                if (Path.GetExtension(ofdMain.FileName).ToUpper() == ".RAW")
                {
                    // decode le fichier raw 
                    string filePath = ofdMain.FileName;


                    string RawContent = "";

                    using (Stream StreamReader = File.OpenRead(filePath))
                    {
                        while (true)
                        {
                            int value = StreamReader.ReadByte();
                            if (value == -1) break;

                            RawContent += string.Format("{0:X2}", value) + " ";
                        }

                        dm42decoder lDecod = new dm42decoder();

                        RawContent.Trim();
                        Decoder42.initialize();
                        lDecod.SetDocument(RawContent);
                        decoderresult lResult = lDecod.decode();
                        int size = lResult.getSize();

                        filePath.Replace('.', '_');
                        filePath += ".free42";

                        InitNewDataTab(lResult.getRpn("\n", true), filePath);
                    }

                }
                else
                {
                    InitNewFileTab(ofdMain.FileName);
                }
            }

        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Sauvegarde du programme courant 
            if (tsFiles.SelectedItem != null)
                Save(tsFiles.SelectedItem);
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Sace As 
            if (tsFiles.SelectedItem != null)
            {
                string oldFile = tsFiles.SelectedItem.Tag as string;
                tsFiles.SelectedItem.Tag = null;
                if (!Save(tsFiles.SelectedItem))
                    if (oldFile != null)
                    {
                        tsFiles.SelectedItem.Tag = oldFile;
                        tsFiles.SelectedItem.Title = Path.GetFileName(oldFile);
                    }
            }
        }

        private void saveAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Save All
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            if (CurrentTB != null)
            {

                List<string> lDoc = new List<string>();

                // encode programme 
                Encoder42.initialize();

                RpnParser Parser = new RpnParser();

                foreach (string line in CurrentTB.Lines)
                {
                    lDoc.Add(Encoder42.FormatRpnLine(line));
                }


                Parser.SetDocument(lDoc);
                Parser.parse();

                string titre = CurrentTB.Parent.Tag.ToString(); // Recupere le chemin complet du fichier 

                for (int lIdx = 0; lIdx < Parser.programs.Count; lIdx++)
                {
                    if (Parser.programs[lIdx].getErrors().Count > 0)
                    {
                        MessageBox.Show("Error to convert RPN Code !");
                        return;
                    }

                }

                // Sauvegarde 

                sfdMain.Title = "Save to Raw file ";

                titre = Path.ChangeExtension(titre, "raw");
                sfdMain.InitialDirectory = Path.GetDirectoryName(titre);
                sfdMain.FileName = Path.GetFileName(titre);
                sfdMain.Filter = "RAW file (*.raw)|*.raw";
                sfdMain.FilterIndex = 1;
                sfdMain.DefaultExt = ".raw";

                if (sfdMain.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {

                    if (sfdMain.FileName != "")
                    {

                        if (Parser.programs.Count == 0)
                        {
                            MessageBox.Show("No program !");
                            return;
                        }

                        using (BinaryWriter writer = new BinaryWriter(File.Open(sfdMain.FileName, FileMode.Create)))
                        {

                            string lRawLine = "";
                            string[] strByte;
                            List<byte> lByteList = new List<byte>();



                            for (int lIdx = 0; lIdx < Parser.programs.Count; lIdx++)
                            {
                                foreach (RawLine line in Parser.programs[lIdx].rawLines)
                                {
                                    RawLine lLine = line;
                                    Encoder42.toRaw(lLine, "free42");
                                    if (lLine.raw != null)
                                    {
                                        lRawLine += lLine.raw + " ";
                                    }

                                }
                            }

                            lRawLine = lRawLine.Trim(' ');
                            strByte = lRawLine.Split(' ');

                            if (strByte.Length > 0)
                            {
                                foreach (string str in strByte)
                                {
                                    lByteList.Add(byte.Parse(str, System.Globalization.NumberStyles.HexNumber));
                                }
                            }

                            writer.Write(lByteList.ToArray());

                            // Change Bytes number in source code 
                            // strByte.Length;
                            string lFirstLine = "{ " + strByte.Length.ToString() + " Bytes Prgm }";

                            CurrentTB.SetSelectedLine(0);
                            string lFirst = CurrentTB.TextSource.GetLines()[0];

                            if (lFirst.Contains('{'))
                            {
                                // Delete remplace first Line 
                                CurrentTB.TextSource.RemoveLine(0);
                            }

                            CurrentTB.Text = lFirstLine + "\r\n" + CurrentTB.Text;

                        }


                    }

                }

            }
        }

        private void printToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // print code 
            if (CurrentTB != null)
            {
                var settings = new PrintDialogSettings();
                settings.Title = tsFiles.SelectedItem.Title;
                settings.Header = "&b&w&b";
                settings.Footer = "&b&p";
                CurrentTB.Print(settings);
            }
        }

        #endregion

        FastColoredTextBox InitNewTab(Color BackColor)
        {
            try
            {
                var tb = new FastColoredTextBox();
                tb.Font = new Font("Consolas", 12.00f, FontStyle.Bold);
                tb.ForeColor = gllp.WordColor;//Color.LightCyan;
                tb.BackColor = gllp.BackColor;
                // tb.Font = new Font("Consolas", 9.75f);
                tb.ContextMenuStrip = cmMain;
                tb.Dock = DockStyle.Fill;
                tb.BorderStyle = BorderStyle.Fixed3D;
                //tb.VirtualSpace = true;
                tb.LeftPadding = 17;
               //!! tb.Language = Language.HP42; //Language.Custom;
                tb.Language =Language.Custom;

                tb.Tag = new TbInfo();

                tb.Focus();
                tb.DelayedTextChangedInterval = 500;
                tb.DelayedEventsInterval = 500;
                tb.TextChangedDelayed += new EventHandler<TextChangedEventArgs>(tb_TextChangedDelayed);
                tb.SelectionChangedDelayed += new EventHandler(tb_SelectionChangedDelayed);
                
                tb.KeyDown += new KeyEventHandler(tb_KeyDown);
                tb.MouseMove += new MouseEventHandler(tb_MouseMove);
                tb.ChangedLineColor = changedLineColor;
                if (btHighlightCurrentLine.Checked)
                    tb.CurrentLineColor = currentLineColor;
                tb.OnSyntaxHighlight(new TextChangedEventArgs(tb.Range));
                // tb.ShowFoldingLines = btShowFoldingLines.Checked;
                tb.HighlightingRangeType = HighlightingRangeType.VisibleRange;
                //create autocomplete popup menu
                AutocompleteMenu popupMenu = new AutocompleteMenu(tb);
                popupMenu.Items.ImageList = ilAutocomplete; // Image list 
                popupMenu.Opening += new EventHandler<CancelEventArgs>(popupMenu_Opening);
                BuildAutocompleteMenu(popupMenu);
                (tb.Tag as TbInfo).popupMenu = popupMenu;
                return tb;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.RetryCancel, MessageBoxIcon.Error);//== System.Windows.Forms.DialogResult.Retry;

                return null;
            }
        }
        private void InitNewFileTab(string fileName)
        {
          //  var tb = InitNewTab(Color.FromArgb(30, 30, 30));

            var tb = InitNewTab(Color.Black);

            var tab = new FATabStripItem(fileName != null ? Path.GetFileName(fileName) : "[new]", tb);
            tab.Tag = fileName;
            if (fileName != null)
                tb.OpenFile(fileName, Encoding.UTF8);
            tab.Title = Path.GetFileName(fileName);
            tsFiles.AddTab(tab);
            tsFiles.SelectedItem = tab;
        }

        private void InitNewDataTab(string Data, string title)
        {
           // var tb = InitNewTab(Color.FromArgb(30, 30, 30));

            var tb = InitNewTab(Color.Black);

            var tab = new FATabStripItem(title, tb);
            tab.Tag = title;

            tb.Text = Data;
            tab.Title = Path.GetFileName(title);
            tsFiles.AddTab(tab);
            tsFiles.SelectedItem = tab;
        }

        private void InitFctList()
        {

            ReadCsvFct("liste_fonctions.csv", ListViewFct);

          
        }

        #region TB EVENT FastColoredTextBox

        void tb_MouseMove(object sender, MouseEventArgs e)
        {
            var tb = sender as FastColoredTextBox;
            var place = tb.PointToPlace(e.Location);
            var r = new Range(tb, place, place);
            string text = r.GetFragment("[a-zA-Z]").Text;
            lbWordUnderMouse.Text = text;
        }

        void tb_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Modifiers == Keys.Control && e.KeyCode == Keys.OemMinus)
            {
                NavigateBackward();
                e.Handled = true;
            }

            if (e.Modifiers == (Keys.Control | Keys.Shift) && e.KeyCode == Keys.OemMinus)
            {
                NavigateForward();
                e.Handled = true;
            }

            if (e.KeyData == (Keys.K | Keys.Control))
            {
                //forced show (MinFragmentLength will be ignored)
                (CurrentTB.Tag as TbInfo).popupMenu.Show(true);
                e.Handled = true;
            }
        }

        void tb_SelectionChangedDelayed(object sender, EventArgs e)
        {
            var tb = sender as FastColoredTextBox;
            //remember last visit time
            if (tb.Selection.IsEmpty && tb.Selection.Start.iLine < tb.LinesCount)
            {
                if (lastNavigatedDateTime != tb[tb.Selection.Start.iLine].LastVisit)
                {
                    tb[tb.Selection.Start.iLine].LastVisit = DateTime.Now;
                    lastNavigatedDateTime = tb[tb.Selection.Start.iLine].LastVisit;
                }
            }

         

            //highlight same words
            tb.VisibleRange.ClearStyle(sameWordsStyle);
            if (!tb.Selection.IsEmpty)
                return;//user selected diapason
            //get fragment around caret
            var fragment = tb.Selection.GetFragment(@"\w");
            string text = fragment.Text;
            if (text.Length == 0)
                return;
            //highlight same words
            Range[] ranges = tb.VisibleRange.GetRanges("\\b" + text + "\\b").ToArray();

            if (ranges.Length > 1)
                foreach (var r in ranges)
                    r.SetStyle(sameWordsStyle);
        }

        void tb_TextChangedDelayed(object sender, TextChangedEventArgs e)
        {
           
            FastColoredTextBox tb = (sender as FastColoredTextBox);
           
            //rebuild object explorer
            string text = (sender as FastColoredTextBox).Text;
            ThreadPool.QueueUserWorkItem(
                (o) => ReBuildObjectExplorer(text)
            );


            e.ChangedRange.SetStyle(CommentStyle, HP42CommentRegex1);
            e.ChangedRange.SetStyle(CommentStyle, HP42CommentRegex2);
            e.ChangedRange.SetStyle(CommentStyle, HP42CommentRegex3);

            //string highlighting
            e.ChangedRange.SetStyle(StringStyle, HP42StringRegex);
            //number highlighting
            e.ChangedRange.SetStyle(NumberStyle, HP42NumberRegex);
            //keywords
            e.ChangedRange.SetStyle(WordStyle, HP42KeywordsRegex);


        }

        #endregion

        #region Gestion EDITION 


        private void undoStripButton_Click(object sender, EventArgs e)
        {
            if (CurrentTB.UndoEnabled)
                CurrentTB.Undo();
        }

        private void redoStripButton_Click(object sender, EventArgs e)
        {
            if (CurrentTB.RedoEnabled)
                CurrentTB.Redo();
        }

        private void cutToolStripButton_Click(object sender, EventArgs e)
        {
            CurrentTB.Cut();
        }

        private void copyToolStripButton_Click(object sender, EventArgs e)
        {
            CurrentTB.Copy();
        }

        private void pasteToolStripButton_Click(object sender, EventArgs e)
        {
            CurrentTB.Paste();
        }

        private void tsFiles_TabStripItemClosing(TabStripItemClosingEventArgs e)
        {
            if ((e.Item.Controls[0] as FastColoredTextBox) != null)

                if ((e.Item.Controls[0] as FastColoredTextBox).IsChanged)
                {
                    switch (MessageBox.Show("Do you want save " + e.Item.Title + " ?", "Save", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information))
                    {
                        case System.Windows.Forms.DialogResult.Yes:
                            if (!Save(e.Item))
                                e.Cancel = true;
                            break;
                        case DialogResult.Cancel:
                            e.Cancel = true;
                            break;
                    }

                }
        }

        private void tsFiles_TabStripItemSelectionChanged(TabStripItemChangedEventArgs e)
        {
            if (CurrentTB != null)
            {
                CurrentTB.Focus();
                string text = CurrentTB.Text;
                ThreadPool.QueueUserWorkItem(
                    (o) => ReBuildObjectExplorer(text)
                );
            }

        }

        private void selectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CurrentTB.Selection.SelectAll();
        }

        private void findToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CurrentTB.ShowFindDialog();
        }

        private void replaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CurrentTB.ShowReplaceDialog();
        }

        private void backStripButton_Click(object sender, EventArgs e)
        {
            NavigateBackward();
        }

        private void forwardStripButton_Click(object sender, EventArgs e)
        {
            NavigateForward();
        }

        private void bookmarkPlusButton_Click(object sender, EventArgs e)
        {
            if (CurrentTB == null)
                return;
            CurrentTB.BookmarkLine(CurrentTB.Selection.Start.iLine);
        }

        private void bookmarkMinusButton_Click(object sender, EventArgs e)
        {
            if (CurrentTB == null)
                return;
            CurrentTB.UnbookmarkLine(CurrentTB.Selection.Start.iLine);
        }

        private void gotoButton_DropDownOpening(object sender, EventArgs e)
        {
            gotoButton.DropDownItems.Clear();
            foreach (Control tab in tsFiles.Items)
            {
                FastColoredTextBox tb = tab.Controls[0] as FastColoredTextBox;
                foreach (var bookmark in tb.Bookmarks)
                {
                    var item = gotoButton.DropDownItems.Add(bookmark.Name + " [" + Path.GetFileNameWithoutExtension(tab.Tag as String) + "]");
                    item.Tag = bookmark;
                    item.Click += (o, a) =>
                    {
                        var b = (Bookmark)(o as ToolStripItem).Tag;
                        try
                        {
                            CurrentTB = b.TB;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                            return;
                        }
                        b.DoVisible();
                    };
                }
            }

        }

        private void btHighlightCurrentLine_Click(object sender, EventArgs e)
        {
            try
            {

                foreach (FATabStripItem tab in tsFiles.Items)
                {
                    if (btHighlightCurrentLine.Checked)
                        (tab.Controls[0] as FastColoredTextBox).CurrentLineColor = currentLineColor;
                    else
                        (tab.Controls[0] as FastColoredTextBox).CurrentLineColor = Color.Transparent;
                }
                if (CurrentTB != null)
                    CurrentTB.Invalidate();
            }
            catch { }
        }

        private void viewCodeLineToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // affiche les lignes de code 
            // format le code
            if (CurrentTB != null && tsFiles.Items.Count > 0)
            {
                var tb = CurrentTB;

                int lLine = tb.Selection.Start.iLine;

                string lText = tb.Text;

                string[] lCode = DM42.FormattingEdit.FormatDoc(lText, true);

                string lResult = "";
                foreach (string str in lCode)
                {
                    lResult += str + "\r\n";
                }

                lResult = lResult.Trim(new char[] { '\n', '\r' });
                tb.Text = lResult;

                tb.SetSelectedLine(lLine + 1);
            }

        }

        private void toolStripMenuItem8_Click(object sender, EventArgs e)
        {
            // masque les lignes de codes 

            // format le code
            if (CurrentTB != null && tsFiles.Items.Count > 0)
            {
                var tb = CurrentTB;

                int lLine = tb.Selection.Start.iLine;

                string lText = tb.Text;

                string[] lCode = DM42.FormattingEdit.FormatDoc(lText, false);

                string lResult = "";
                foreach (string str in lCode)
                {
                    lResult += str + "\r\n";
                }

                lResult = lResult.Trim(new char[] { '\n', '\r' });

                tb.Text = lResult;

                tb.SetSelectedLine(lLine + 1);

            }
        }

        #endregion

        #region PRIVATE 

        private void ReadCsvFct(string fileName, ListView lst)
        {

            List<ListViewItem> items = new List<ListViewItem>();

            using (var reader = new StreamReader(fileName, Encoding.UTF8))
            {

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = line.Split(';');

                    ListViewItem dateItem = new ListViewItem(values[0]);
                    if (values.Length > 1) dateItem.SubItems.Add(values[1]);
                    if (values.Length > 2) dateItem.SubItems.Add(values[2]);
                    if (values.Length > 3) dateItem.SubItems.Add(values[3]);
                    if (values.Length > 4) dateItem.SubItems.Add(values[4]);
                    items.Add(dateItem);


                }
            }

            lst.Items.AddRange(items.ToArray());
           
        }


        /// <summary>
        ///  Page courante selectionnee
        /// </summary>
        FastColoredTextBox CurrentTB
        {
            get
            {
                if (tsFiles.SelectedItem == null)
                    return null;
                return (tsFiles.SelectedItem.Controls[0] as FastColoredTextBox);
            }

            set
            {
                tsFiles.SelectedItem = (value.Parent as FATabStripItem);
                value.Focus();
            }
        }


        List<ExplorerItem> explorerList = new List<ExplorerItem>();

        /// <summary>
        ///  Reconstruit la liste des mots clés
        /// </summary>
        /// <param name="text"></param>
        private void ReBuildObjectExplorer(string text)
        {

            try
            {
                List<ExplorerItem> list = new List<ExplorerItem>();

 
                string[] lRegex = Regex.Split(text, "\r\n");

                // foreach (Match r in lRegex.Matches(text))
                int lIdx = 0;
                foreach (string str in lRegex)
                    try
                    {
                        string s = str;// r.value
 
                        var item = new ExplorerItem() { title = s, position = lIdx };  // r.index 
                        if (Regex.IsMatch(item.title, @"(LBL|VIEW|AVIEW|PROMPT|INPUT|XEQ|GTO)"))
                        {
                            list.Add(item);
                        }


                        lIdx++;
                    }
                    catch
                    {
                    }

                BeginInvoke(new Action(() =>
                   {
                       explorerList = list;

                       dgvObjectExplorer.RowCount = explorerList.Count;

                       if (explorerList.Count > 0)
                       {
                           int lRow = 0;
                           foreach (ExplorerItem exp in explorerList)
                           {
                               dgvObjectExplorer.Rows[lRow].Cells[0].Value = explorerList[lRow].title;
                               dgvObjectExplorer.Rows[lRow].Cells[1].Value = explorerList[lRow].position.ToString();
                               lRow++;
                           }
                       }

                       dgvObjectExplorer.Invalidate();
                   })
                );
            }
            catch {; }


        }

        private bool NavigateBackward()
        {
            DateTime max = new DateTime();
            int iLine = -1;
            FastColoredTextBox tb = null;
            for (int iTab = 0; iTab < tsFiles.Items.Count; iTab++)
            {
                var t = (tsFiles.Items[iTab].Controls[0] as FastColoredTextBox);
                for (int i = 0; i < t.LinesCount; i++)
                    if (t[i].LastVisit < lastNavigatedDateTime && t[i].LastVisit > max)
                    {
                        max = t[i].LastVisit;
                        iLine = i;
                        tb = t;
                    }
            }
            if (iLine >= 0)
            {
                tsFiles.SelectedItem = (tb.Parent as FATabStripItem);
                tb.Navigate(iLine);
                lastNavigatedDateTime = tb[iLine].LastVisit;
                Console.WriteLine("Backward: " + lastNavigatedDateTime);
                tb.Focus();
                tb.Invalidate();
                return true;
            }
            else
                return false;
        }

        private bool NavigateForward()
        {
            DateTime min = DateTime.Now;
            int iLine = -1;
            FastColoredTextBox tb = null;
            for (int iTab = 0; iTab < tsFiles.Items.Count; iTab++)
            {
                var t = (tsFiles.Items[iTab].Controls[0] as FastColoredTextBox);
                for (int i = 0; i < t.LinesCount; i++)
                    if (t[i].LastVisit > lastNavigatedDateTime && t[i].LastVisit < min)
                    {
                        min = t[i].LastVisit;
                        iLine = i;
                        tb = t;
                    }
            }
            if (iLine >= 0)
            {
                tsFiles.SelectedItem = (tb.Parent as FATabStripItem);
                tb.Navigate(iLine);
                lastNavigatedDateTime = tb[iLine].LastVisit;
                Console.WriteLine("Forward: " + lastNavigatedDateTime);
                tb.Focus();
                tb.Invalidate();
                return true;
            }
            else
                return false;
        }

        private void BuildAutocompleteMenu(AutocompleteMenu popupMenu)
        {
            List<AutocompleteItem> items = new List<AutocompleteItem>();

            foreach (var item in keywords)
                items.Add(new AutocompleteItem(item));

            //set as autocomplete source
            popupMenu.Items.SetAutocompleteItems(items);
            popupMenu.SearchPattern = @"[\w\.:=!<>]";


        }

        private bool Save(FATabStripItem tab)
        {
            var tb = (tab.Controls[0] as FastColoredTextBox);
            if (tab.Tag == null)
            {

                sfdMain.Title = "Save free42 file";

                //  sfdMain.FileName = Path.GetFileName(titre);
                sfdMain.Filter = "free2 file (*.free42)|*.free42| dm42 file (*.s42)|*.s42";
                sfdMain.FilterIndex = 1;
                sfdMain.DefaultExt = ".free42";

                if (sfdMain.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return false;
                tab.Title = Path.GetFileName(sfdMain.FileName);
                tab.Tag = sfdMain.FileName;
            }

            try
            {
                File.WriteAllText(tab.Tag as string, tb.Text);
                tb.IsChanged = false;
            }
            catch (Exception ex)
            {
                if (MessageBox.Show(ex.Message, "Error", MessageBoxButtons.RetryCancel, MessageBoxIcon.Error) == DialogResult.Retry)
                    return Save(tab);
                else
                    return false;
            }

            tb.Invalidate();

            return true;
        }

        void popupMenu_Opening(object sender, CancelEventArgs e)
        {
            //---block autocomplete menu for comments
            //get index of green style (used for comments)
            var iGreenStyle = CurrentTB.GetStyleIndex(CurrentTB.SyntaxHighlighter.GreenStyle);
            if (iGreenStyle >= 0)
                if (CurrentTB.Selection.Start.iChar > 0)
                {
                    //current char (before caret)
                    var c = CurrentTB[CurrentTB.Selection.Start.iLine][CurrentTB.Selection.Start.iChar - 1];
                    //green Style
                    var greenStyleIndex = Range.ToStyleIndex(iGreenStyle);
                    //if char contains green style then block popup menu
                    if ((c.style & greenStyleIndex) != 0)
                        e.Cancel = true;
                }
        }

        private void tmUpdateInterface_Tick(object sender, EventArgs e)
        {
            try
            {
                if (CurrentTB != null && tsFiles.Items.Count > 0)
                {
                    var tb = CurrentTB;
                    undoStripButton.Enabled = undoToolStripMenuItem.Enabled = tb.UndoEnabled;
                    redoStripButton.Enabled = redoToolStripMenuItem.Enabled = tb.RedoEnabled;
                    saveToolStripButton.Enabled = saveToolStripMenuItem.Enabled = tb.IsChanged;
                    saveAsToolStripMenuItem.Enabled = true;
                    pasteToolStripButton.Enabled = pasteToolStripMenuItem.Enabled = true;
                    cutToolStripButton.Enabled = cutToolStripMenuItem.Enabled =
                    copyToolStripButton.Enabled = copyToolStripMenuItem.Enabled = !tb.Selection.IsEmpty;
                    printToolStripButton.Enabled = true;
                    printToolStripMenuItem.Enabled = true;
                }
                else
                {
                    saveToolStripButton.Enabled = saveToolStripMenuItem.Enabled = false;
                    saveAsToolStripMenuItem.Enabled = false;
                    cutToolStripButton.Enabled = cutToolStripMenuItem.Enabled =
                    copyToolStripButton.Enabled = copyToolStripMenuItem.Enabled = false;
                    pasteToolStripButton.Enabled = pasteToolStripMenuItem.Enabled = false;
                    printToolStripButton.Enabled = false;
                    printToolStripMenuItem.Enabled = false;
                    undoStripButton.Enabled = undoToolStripMenuItem.Enabled = false;
                    redoStripButton.Enabled = redoToolStripMenuItem.Enabled = false;
                    dgvObjectExplorer.RowCount = 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

       

        private void equivalenceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // equivalence 
            FrmAideCde lFrmAide = new FrmAideCde();


            lFrmAide.ShowDialog();

            lFrmAide = null;

        }

        private void dgvObjectExplorer_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (CurrentTB != null)
            {
                var item = explorerList[e.RowIndex];
            
                CurrentTB.Selection.Start = new Place(0, item.position);
                CurrentTB.DoSelectionVisible();
                CurrentTB.Focus();

            }
        }

        private void dgvObjectExplorer_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            try
            {
                ExplorerItem item = explorerList[e.RowIndex];
                if (e.ColumnIndex == 1)
                    e.Value = item.title;
                else
                    switch (item.type)
                    {
                        case ExplorerItemType.LBL:
                            // e.Value = global::Tester.Properties.Resources.class_libraries;
                            return;
                        case ExplorerItemType.GTO:
                            // e.Value = global::Tester.Properties.Resources.box;
                            return;
                    }
            }
            catch {; }
        }

        private void dgvObjectExplorer_DoubleClick(object sender, EventArgs e)
        {
            // Get current row 
        }

        private void dgvObjectExplorer_KeyPress(object sender, KeyPressEventArgs e)
        {

        }

        private void aProposToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Aide 

            FrmAide lFrmAide = new FrmAide();

            lFrmAide.ShowDialog();
        }

        private void copyWithoutCommentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //string lreg1 = @"^\s*(#|@|//)";
            //string lreg2 = "(//|@|#)(.*?)\r?\n";
            //string lReplace = "(//|@|#)(.*?)\r?\n";

            string lreg1 = @"^\s*(#|//)";
            string lreg2 = "(//|#)(.*?)";
            string lReplace = "(//|#)(.*?)\r?\n";


            // Copy without comment 
            if (CurrentTB != null && tsFiles.Items.Count > 0)
            {
                var tb = CurrentTB;

                string lText = tb.Text;

                string[] lCode = DM42.FormattingEdit.FormatDoc(lText, false);

                string lResult = "";
                foreach (string str in lCode)
                {
                    if (!( str.Length == 0 || Regex.Match(str, "{.*}").Success || Regex.Match(str, lreg1).Success))
                    {
                        if (Regex.IsMatch(str, lreg2))
                        {
                            string lStr = str + "\r\n";
                            lStr =  Regex.Replace(lStr, lReplace, "");
                            lResult += lStr + "\r\n";

                        }
                       
                        else     lResult += str + "\r\n";
                    } 

                }

                lResult = lResult.Trim(new char[] { '\n', '\r' });

                Clipboard.SetText(lResult);

            }
        }

        #endregion

        enum ExplorerItemType
        {
            LBL, GTO
        }

        // classe des  item cle du programme
        class ExplorerItem
        {
            public ExplorerItemType type; // Type LBL ou GTO
            public string title;    // nom de la fonction 
            public int position;   // ligne position du mot cle
        }


        public class TbInfo
        {
            public AutocompleteMenu popupMenu;
        }

        private void parameterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //parameter
            frmParam lParam = new frmParam(); // fenêtre de parametrage 

            lParam.Parameter = Parameter; 

            lParam.ShowDialog();

            Parameter = lParam.Parameter;

            Parameter.SaveParam(); 

            // Set color 

            if (CurrentTB != null)
            {

                int lIdx = Parameter.parameters.current;

                gllp = Parameter.parameters.Params[lIdx]; 

                currentLineColor = gllp.LineColor;
              //  changedLineColor = Parameter.Param.LineColor; // goutiere numero de ligne 

                CurrentTB.BackColor = gllp.BackColor;
                CurrentTB.ForeColor = gllp.WordColor;
                CurrentTB.CurrentLineColor = currentLineColor;
                CurrentTB.ChangedLineColor = changedLineColor; 

                NumberStyle = new TextStyle(new SolidBrush(gllp.NumColor), null, (gllp.NumBold)? FontStyle.Bold : FontStyle.Regular);    // Number 
                WordStyle = new TextStyle(new SolidBrush(gllp.WordColor), null, (gllp.WordBold) ? FontStyle.Bold : FontStyle.Regular);    //word
                StringStyle = new TextStyle(new SolidBrush(gllp.StringColor), null, (gllp.StringBold) ? FontStyle.Bold : FontStyle.Regular);         // String 
                CommentStyle = new TextStyle(new SolidBrush(gllp.CommentColor), null, (gllp.CommentBold) ? FontStyle.Bold : FontStyle.Regular);       // comment 

                CurrentTB.Update(); 


            }

         }


        /// <summary>
        ///  Selectionne une fonction dans la liste et la placer dans le code programme 
        /// </summary>
       ///

        private void ListViewFct_DoubleClick(object sender, EventArgs e)
        {
            if (CurrentTB != null)
            {


                var item = ListViewFct.SelectedItems[0];

               // Range prevSelection = CurrentTB.Selection.Clone();

                int lLine = CurrentTB.Selection.Start.iLine; //Numero de Ligne selectionné 
                //List<int> lLines = new List<int>();
                //lLines.Add(lLine);
               // CurrentTB.ClearCurrentLine(); 

               // CurrentTB.SelectedText = item.Text;

                // CurrentTB.RemoveLines(lLines);
                //!!  CurrentTB.Lines.Insert(lLine, item.Text); // pas implemente 
                CurrentTB.Selection.Start = new Place(0, lLine);
                CurrentTB.SelectedText = item.Text;

                //CurrentTB.InsertText(item.Text);




                //   CurrentTB.Selection.Start = 
                //  CurrentTB.DoSelectionVisible();
                //  CurrentTB.Focus();

            }

        }

        private void toolStripMenuItem10_Click(object sender, EventArgs e)
        {
            // ouvre la page pdf
            FrmPDF lFrmPDF = new FrmPDF();


            lFrmPDF.ShowDialog();

            lFrmPDF = null;

        }
    }

}

