namespace dm42editor
{
    partial class FrmAideCde
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.button1 = new System.Windows.Forms.Button();
            this.listView1 = new System.Windows.Forms.ListView();
            this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader2 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader3 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader4 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader5 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.SuspendLayout();
            // 
            // listBox1
            // 
            this.listBox1.FormattingEnabled = true;
            this.listBox1.Items.AddRange(new object[] {
            "RCLx\t\t\tRCL×",
            "RCL*\t\t \tRCL×",
            "RCL/ \t\t\tRCL÷",
            "STOx, \t\t\tSTO×",
            "STO*\t \t\tSTO×",
            "STO/\t \t\tSTO÷",
            "BASEx\t\t\tBASE×",
            "BASE*\t \t\tBASE×",
            "BASE/\t \t\tBASE÷",
            "BASE+/-\t \t\tBASE±",
            "R^ \t\t\tR↑",
            "Rv \t\t\t1R↓",
            "",
            "Sigma|SUM|GS)+ \t\tΣ+",
            "Sigma|SUM|GS)-\t\tΣ-",
            "ALL\\Sigma|SUM|GS)\tALLΣ",
            "CL\\Sigma|SUM|GS)\t\tCLΣ",
            "LIN\\(Sigma|SUM|G)\t\tLINΣ",
            "PR\\((Sigma|SUM|GS)\tPRΣ",
            "\\(Sigma|SUM|GS)REG\tΣREG",
            "",
            "",
            "->DEC \t\t\t→DEC",
            "->OCT \t\t\t→OCT",
            "->RAD \t\t\t→RAD",
            "->HMS \t\t\t→HMS",
            "->REC \t\t\t→REC",
            "->DEG\t\t \t→DEG",
            "->POL \t\t\t→POL",
            "",
            "X!=0? \t\t\tX≠0?",
            "X\\\\=0? \t\t\tX≠0?",
            "X#Y? \t\t\tX≠Y?",
            "X#0? \t\t\tX≠0?",
            "X<=0? \t\t\tX≤0?",
            "X>=0?\t\t\tX≥0?",
            "X\\!=Y? \t\t\tX≠Y",
            "X<=Y? \t\t\tX≤Y?",
            "X>=Y? \t\t\tX≥Y?",
            "",
            "10\\^x|X)\t \t\t10↑X",
            "(x|X)\\^2\t \t\tX↑2",
            "Y\\^(x|X) \t\t\tY↑X",
            "(e|E)\\^(x|X)\t\tE↑X",
            "(e|E)\\^(x|X)-1\t\tE↑X-1",
            "<-\t\t \t←",
            "^\t\t \t↑",
            "v\t\t \t↓",
            "->\t\t \t→",
            "x \t\t\t×",
            "/\t\t \t÷",
            "|-\t\t \t⊢",
            "├ \t\t\t⊢"});
            this.listBox1.Location = new System.Drawing.Point(741, 5);
            this.listBox1.MultiColumn = true;
            this.listBox1.Name = "listBox1";
            this.listBox1.Size = new System.Drawing.Size(251, 706);
            this.listBox1.TabIndex = 0;
            this.listBox1.SelectedIndexChanged += new System.EventHandler(this.listBox1_SelectedIndexChanged);
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(105, 737);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(147, 31);
            this.button1.TabIndex = 1;
            this.button1.Text = "OK";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // listView1
            // 
            this.listView1.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1,
            this.columnHeader2,
            this.columnHeader3,
            this.columnHeader4,
            this.columnHeader5});
            this.listView1.FullRowSelect = true;
            this.listView1.GridLines = true;
            this.listView1.HideSelection = false;
            this.listView1.ImeMode = System.Windows.Forms.ImeMode.Disable;
            this.listView1.Location = new System.Drawing.Point(33, 2);
            this.listView1.MultiSelect = false;
            this.listView1.Name = "listView1";
            this.listView1.Size = new System.Drawing.Size(702, 709);
            this.listView1.TabIndex = 2;
            this.listView1.UseCompatibleStateImageBehavior = false;
            this.listView1.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader1
            // 
            this.columnHeader1.Text = "Name";
            this.columnHeader1.Width = 120;
            // 
            // columnHeader2
            // 
            this.columnHeader2.Text = "Description";
            this.columnHeader2.Width = 240;
            // 
            // columnHeader3
            // 
            this.columnHeader3.Text = "Key";
            this.columnHeader3.Width = 100;
            // 
            // columnHeader4
            // 
            this.columnHeader4.Text = "Page";
            // 
            // columnHeader5
            // 
            this.columnHeader5.Text = "Groupe";
            this.columnHeader5.Width = 80;
            // 
            // FrmAideCde
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1008, 787);
            this.Controls.Add(this.listView1);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.listBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "FrmAideCde";
            this.Text = "Fonctions/equivalence";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListBox listBox1;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.ListView listView1;
        private System.Windows.Forms.ColumnHeader columnHeader1;
        private System.Windows.Forms.ColumnHeader columnHeader2;
        private System.Windows.Forms.ColumnHeader columnHeader3;
        private System.Windows.Forms.ColumnHeader columnHeader4;
        private System.Windows.Forms.ColumnHeader columnHeader5;
    }
}