using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using System.Windows.Forms;

namespace W3Devices
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Windows.Forms;

    public class CellEdit : Form
    {
        private TextBox textBox;
        private Button saveButton;

        // Property to access the edited text
        public string EditedText { get; private set; }

        // Constructor with column header text as the form title
        public CellEdit(string initialText, string columnHeader, string deviceName)
        {
            // Initialize the form controls
            textBox = new TextBox
            {
                Multiline = true,
                Text = initialText,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Vertical
            };

            saveButton = new Button
            {
                Text = Properties.Resources.SaveAndExit,
                Dock = DockStyle.Bottom
            };

            saveButton.Click += SaveButton_Click;

            // Add controls to the form
            Controls.Add(textBox);
            Controls.Add(saveButton);

            // Set form properties, including the title with the column header text
            Text = String.Format(Properties.Resources.Edit0, columnHeader, deviceName);
            Size = new System.Drawing.Size(400, 300);
            StartPosition = FormStartPosition.CenterParent;
            Icon = Properties.Resources.w3coach;
            // Remove minimize and maximize buttons
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;


        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            // Save the edited text and close the form
            EditedText = textBox.Text;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
