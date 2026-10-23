namespace dm42editor
{
    partial class FrmAide
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmAide));
            button1 = new System.Windows.Forms.Button();
            label1 = new System.Windows.Forms.Label();
            label2 = new System.Windows.Forms.Label();
            label4 = new System.Windows.Forms.Label();
            label6 = new System.Windows.Forms.Label();
            textBox1 = new System.Windows.Forms.TextBox();
            SuspendLayout();
            // 
            // button1
            // 
            button1.Location = new System.Drawing.Point(272, 418);
            button1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            button1.Name = "button1";
            button1.Size = new System.Drawing.Size(176, 60);
            button1.TabIndex = 0;
            button1.Text = "Close";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
            label1.ForeColor = System.Drawing.Color.White;
            label1.Location = new System.Drawing.Point(241, 34);
            label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(236, 39);
            label1.TabIndex = 2;
            label1.Text = "Free42 Editor";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
            label2.ForeColor = System.Drawing.Color.White;
            label2.Location = new System.Drawing.Point(65, 103);
            label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(592, 25);
            label2.TabIndex = 3;
            label2.Text = "Import Export programs  from/to dm42, Free42 and Py2dm42";
            label2.Click += label2_Click;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
            label4.ForeColor = System.Drawing.Color.White;
            label4.Location = new System.Drawing.Point(13, 439);
            label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(129, 25);
            label4.TabIndex = 5;
            label4.Text = "Version: 2.0";
            label4.Click += label4_Click;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
            label6.ForeColor = System.Drawing.Color.White;
            label6.Location = new System.Drawing.Point(53, 182);
            label6.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label6.Name = "label6";
            label6.Size = new System.Drawing.Size(12, 25);
            label6.TabIndex = 7;
            label6.Text = "\r\n";
            label6.Click += label6_Click;
            // 
            // textBox1
            // 
            textBox1.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
            textBox1.Location = new System.Drawing.Point(37, 154);
            textBox1.Multiline = true;
            textBox1.Name = "textBox1";
            textBox1.Size = new System.Drawing.Size(673, 180);
            textBox1.TabIndex = 8;
            textBox1.Text = resources.GetString("textBox1.Text");
            textBox1.TextChanged += textBox1_TextChanged;
            // 
            // FrmAide
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.SteelBlue;
            ClientSize = new System.Drawing.Size(804, 492);
            Controls.Add(textBox1);
            Controls.Add(label6);
            Controls.Add(label4);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(button1);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            Name = "FrmAide";
            Text = "FrmAide";
            Load += FrmAide_Load;
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox textBox1;
    }
}