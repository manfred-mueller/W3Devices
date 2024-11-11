using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace W3Devices
{
    partial class Form1
    {
        /// <summary>
        /// Erforderliche Designervariable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Verwendete Ressourcen bereinigen.
        /// </summary>
        /// <param name="disposing">True, wenn verwaltete Ressourcen gelöscht werden sollen; andernfalls False.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Vom Windows Form-Designer generierter Code

        /// <summary>
        /// Erforderliche Methode für die Designerunterstützung.
        /// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
        /// </summary>
        private void InitializeComponent()
        {
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.btnFetchDevices = new System.Windows.Forms.Button();
            this.txtSearch = new System.Windows.Forms.TextBox();
            this.txtApiKey = new System.Windows.Forms.TextBox();
            this.btnSaveApiKey = new System.Windows.Forms.Button();
            this.labelApiKey = new System.Windows.Forms.Label();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.cmbGroupName = new System.Windows.Forms.ComboBox();
            this.messageLabel = new System.Windows.Forms.Label();
            this.btnReload = new System.Windows.Forms.Label();
            this.btnPrint = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.SuspendLayout();
            // 
            // dataGridView1
            // 
            this.dataGridView1.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.AllCells;
            this.dataGridView1.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Location = new System.Drawing.Point(4, 34);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.Size = new System.Drawing.Size(1124, 403);
            this.dataGridView1.TabIndex = 0;
            // 
            // btnFetchDevices
            // 
            this.btnFetchDevices.Location = new System.Drawing.Point(4, 4);
            this.btnFetchDevices.Name = "btnFetchDevices";
            this.btnFetchDevices.Size = new System.Drawing.Size(103, 23);
            this.btnFetchDevices.TabIndex = 3;
            this.btnFetchDevices.Text = global::W3Devices.Properties.Resources.LoadDevices;
            this.btnFetchDevices.UseVisualStyleBackColor = true;
            this.btnFetchDevices.Click += new System.EventHandler(this.btnFetchDevices_Click);
            // 
            // txtSearch
            // 
            this.txtSearch.ForeColor = System.Drawing.Color.Gray;
            this.txtSearch.Location = new System.Drawing.Point(306, 6);
            this.txtSearch.Name = "txtSearch";
            this.txtSearch.Size = new System.Drawing.Size(195, 20);
            this.txtSearch.TabIndex = 4;
            this.txtSearch.Text = W3Devices.Properties.Resources.Search;
            // 
            // txtApiKey
            // 
            this.txtApiKey.Location = new System.Drawing.Point(848, 7);
            this.txtApiKey.Name = "txtApiKey";
            this.txtApiKey.Size = new System.Drawing.Size(200, 20);
            this.txtApiKey.TabIndex = 6;
            this.txtApiKey.TextChanged += new System.EventHandler(this.txtApiKey_TextChanged);
            // 
            // btnSaveApiKey
            // 
            this.btnSaveApiKey.Location = new System.Drawing.Point(1053, 6);
            this.btnSaveApiKey.Name = "btnSaveApiKey";
            this.btnSaveApiKey.Size = new System.Drawing.Size(75, 23);
            this.btnSaveApiKey.TabIndex = 7;
            this.btnSaveApiKey.Text = global::W3Devices.Properties.Resources.Save;
            this.btnSaveApiKey.UseVisualStyleBackColor = true;
            this.btnSaveApiKey.Click += new System.EventHandler(this.btnSaveApiKey_Click);
            // 
            // labelApiKey
            // 
            this.labelApiKey.AutoSize = true;
            this.labelApiKey.Location = new System.Drawing.Point(775, 10);
            this.labelApiKey.Name = "labelApiKey";
            this.labelApiKey.Size = new System.Drawing.Size(43, 13);
            this.labelApiKey.TabIndex = 8;
            this.labelApiKey.Text = W3Devices.Properties.Resources.ApiKey;
            this.labelApiKey.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // progressBar1
            // 
            this.progressBar1.Location = new System.Drawing.Point(0, 0);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(100, 23);
            this.progressBar1.TabIndex = 0;
            // 
            // cmbGroupName
            // 
            this.cmbGroupName.FormattingEnabled = true;
            this.cmbGroupName.Location = new System.Drawing.Point(113, 5);
            this.cmbGroupName.Name = "cmbGroupName";
            this.cmbGroupName.Size = new System.Drawing.Size(166, 21);
            this.cmbGroupName.TabIndex = 9;
            // 
            // messageLabel
            // 
            this.messageLabel.BackColor = System.Drawing.SystemColors.AppWorkspace;
            this.messageLabel.Font = new System.Drawing.Font("Arial", 24F);
            this.messageLabel.Location = new System.Drawing.Point(23, 226);
            this.messageLabel.Name = "messageLabel";
            this.messageLabel.Size = new System.Drawing.Size(1082, 40);
            this.messageLabel.TabIndex = 10;
            this.messageLabel.Text = W3Devices.Properties.Resources.PleaseEnterYourAPIKeyAndSaveItBeforeFetchingData;
            this.messageLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // btnReload
            // 
            this.btnReload.BackColor = System.Drawing.Color.Transparent;
            this.btnReload.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnReload.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F);
            this.btnReload.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btnReload.Location = new System.Drawing.Point(502, 1);
            this.btnReload.Name = "btnReload";
            this.btnReload.Size = new System.Drawing.Size(23, 28);
            this.btnReload.TabIndex = 5;
            this.btnReload.Text = "⟳";
            this.btnReload.Click += new System.EventHandler(this.btnReload_Click);
            // 
            // btnPrint
            // 
            this.btnPrint.BackColor = System.Drawing.Color.Transparent;
            this.btnPrint.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnPrint.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F);
            this.btnPrint.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btnPrint.Location = new System.Drawing.Point(280, 2);
            this.btnPrint.Name = "btnPrint";
            this.btnPrint.Size = new System.Drawing.Size(23, 28);
            this.btnPrint.TabIndex = 5;
            this.btnPrint.Text = "🖶";
            this.btnPrint.Click += new System.EventHandler(this.btnSaveAsPdf_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
            this.ClientSize = new System.Drawing.Size(1132, 441);
            this.Controls.Add(this.messageLabel);
            this.Controls.Add(this.cmbGroupName);
            this.Controls.Add(this.labelApiKey);
            this.Controls.Add(this.btnReload);
            this.Controls.Add(this.btnPrint);
            this.Controls.Add(this.txtSearch);
            this.Controls.Add(this.txtApiKey);
            this.Controls.Add(this.btnFetchDevices);
            this.Controls.Add(this.btnSaveApiKey);
            this.Controls.Add(this.dataGridView1);
            this.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.Icon = global::W3Devices.Properties.Resources.w3coach;
            this.Name = "Form1";
            this.Load += new System.EventHandler(this.Form1_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Button btnFetchDevices;
        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.TextBox txtApiKey;
        private System.Windows.Forms.Button btnSaveApiKey;
        private System.Windows.Forms.Label labelApiKey;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.ComboBox cmbGroupName;
        private System.Windows.Forms.Label messageLabel;
        private System.Windows.Forms.Label btnReload;
        private System.Windows.Forms.Label btnPrint;
    }
}

