using System;
using System.Drawing;
using System.Windows.Forms;

namespace W3Devices
{
    public class SendMessage : Form
    {
        private Label lblDeviceName;
        private Label lblSender;
        private TextBox txtSender;
        private Label lblMessage;
        private TextBox txtMessage;
        private Button btnSend;
        private Button btnCancel;

        public string SenderName => txtSender.Text.Trim();
        public string MessageBody => txtMessage.Text.Trim();

        public SendMessage(string deviceName)
        {
            // ── Layout constants ──────────────────────────────────────────
            const int margin  = 12;
            const int lblW    = 70;
            const int fieldW  = 280;
            const int fieldH  = 22;
            const int btnW    = 80;
            const int btnH    = 26;
            int formW         = margin + lblW + margin + fieldW + margin;  // 446
            int y             = margin;

            // ── Device name header ────────────────────────────────────────
            lblDeviceName = new Label
            {
                Text      = deviceName,
                Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
                Location  = new Point(margin, y),
                Size      = new Size(formW - margin * 2, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };
            y += 26;

            // ── Sender ────────────────────────────────────────────────────
            lblSender = new Label
            {
                Text      = "Sender:",
                Location  = new Point(margin, y + 3),
                Size      = new Size(lblW, fieldH),
                TextAlign = ContentAlignment.MiddleRight
            };
            txtSender = new TextBox
            {
                Location  = new Point(margin + lblW + margin, y),
                Size      = new Size(fieldW, fieldH),
                MaxLength = 100
            };
            y += fieldH + margin;

            // ── Message ───────────────────────────────────────────────────
            lblMessage = new Label
            {
                Text      = "Nachricht:",
                Location  = new Point(margin, y + 3),
                Size      = new Size(lblW, fieldH),
                TextAlign = ContentAlignment.MiddleRight
            };
            txtMessage = new TextBox
            {
                Location    = new Point(margin + lblW + margin, y),
                Size        = new Size(fieldW, 80),
                Multiline   = true,
                ScrollBars  = ScrollBars.Vertical,
                MaxLength   = 1000
            };
            y += 80 + margin;

            // ── Buttons ───────────────────────────────────────────────────
            btnCancel = new Button
            {
                Text         = "Abbrechen",
                Location     = new Point(formW - margin - btnW, y),
                Size         = new Size(btnW, btnH),
                DialogResult = DialogResult.Cancel
            };
            btnSend = new Button
            {
                Text     = "Senden",
                Location = new Point(formW - margin - btnW - margin - btnW, y),
                Size     = new Size(btnW, btnH)
            };
            btnSend.Click += BtnSend_Click;
            y += btnH + margin;

            // ── Form setup ────────────────────────────────────────────────
            Text            = "Nachricht senden";
            ClientSize      = new Size(formW, y);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterParent;
            MaximizeBox     = false;
            MinimizeBox     = false;
            AcceptButton    = btnSend;
            CancelButton    = btnCancel;
            Icon            = Properties.Resources.w3coach;

            Controls.AddRange(new Control[]
            {
                lblDeviceName,
                lblSender, txtSender,
                lblMessage, txtMessage,
                btnSend, btnCancel
            });

            // Focus sender field on open
            Shown += (s, e) => txtSender.Focus();
        }

        private void BtnSend_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSender.Text))
            {
                MessageBox.Show("Bitte geben Sie einen Absendernamen ein.",
                    "Pflichtfeld", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtSender.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(txtMessage.Text))
            {
                MessageBox.Show("Bitte geben Sie eine Nachricht ein.",
                    "Pflichtfeld", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtMessage.Focus();
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
