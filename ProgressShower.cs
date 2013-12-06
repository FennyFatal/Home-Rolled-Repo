using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace FennyUTILS
{
    public interface ProgressChanged
    {
        void updateProgress(int total, int progress);
    }
    public partial class ProgressShower : Form, ProgressChanged
    {
        int Total = 0;
        string WindowTitle;
        public ProgressShower(int total, string windowTitle)
        {
            InitializeComponent();
            this.Text = WindowTitle = windowTitle;
            this.progressBar1.Maximum = Total = total;
        }

        public void updateProgress(int total, int progress)
        {
            if (Total != total)
            {
                this.progressBar1.Maximum = Total = total;
            }
            if (this.progressBar1.Value != progress)
                this.progressBar1.Value = progress;
        }

        private void ProgressShower_Load(object sender, EventArgs e)
        {

        }
    }
}
