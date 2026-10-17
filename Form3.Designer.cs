
namespace dm42editor
{
    partial class FrmPDF
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
            this.pdfViewer = new Microsoft.Web.WebView2.WinForms.WebView2();
            ((System.ComponentModel.ISupportInitialize)(this.pdfViewer)).BeginInit();
            this.SuspendLayout();
            //
            // pdfViewer
            //
            this.pdfViewer.AllowExternalDrop = false;
            this.pdfViewer.CreationProperties = null;
            this.pdfViewer.DefaultBackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(86)))), ((int)(((byte)(86)))), ((int)(((byte)(86)))));
            this.pdfViewer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pdfViewer.Location = new System.Drawing.Point(0, 0);
            this.pdfViewer.MinimumSize = new System.Drawing.Size(200, 150);
            this.pdfViewer.Name = "pdfViewer";
            this.pdfViewer.Size = new System.Drawing.Size(852, 631);
            this.pdfViewer.TabIndex = 0;
            this.pdfViewer.ZoomFactor = 1D;
            //
            // FrmPDF
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(852, 631);
            this.Controls.Add(this.pdfViewer);
            this.Name = "FrmPDF";
            this.Text = "PDF editor";
            ((System.ComponentModel.ISupportInitialize)(this.pdfViewer)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private Microsoft.Web.WebView2.WinForms.WebView2 pdfViewer;
    }
}
