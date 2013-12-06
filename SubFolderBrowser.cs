using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Collections;
using System.Text.RegularExpressions;

namespace FennyUTILS
{
    public partial class SubFolderBrowser : Form
    {
        public String selectedFolder;
        String UncPath;
        public SubFolderBrowser(String uncPath)
        {
            UncPath = uncPath;
            InitializeComponent();
        }

        private void SubFolderBrowser_Load(object sender, EventArgs e)
        {
            LoadSubdirsRecursive(this.treeView1.Nodes, UncPath);
            this.Text = UncPath;
            foreach (TreeNode theNode in this.treeView1.Nodes.Find(UncPath, false))
            {
                theNode.Expand();
            }
        }
        private void LoadSubdirsRecursive(TreeNodeCollection theNode, string uncPath)
        {
            ArrayList theNodes = new ArrayList();
            theNodes.Add(theNode.Add(uncPath,getFolderName(uncPath)));
            TreeNode node;
            while (theNodes.Count > 0)
            {
                node = (TreeNode)theNodes[0];
                theNodes.Remove(node);
                try 
                {
                    foreach (string path in Directory.EnumerateDirectories(node.Name))
                    {
                        theNodes.Add(node.Nodes.Add(path, getFolderName(path)));
                    }
                } catch { }
            }
        }
        private string getFolderName(string path)
        {
            path = path.TrimEnd('\\');
            return path.Substring(path.LastIndexOf('\\') + 1);
        }

        private void newFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            treeView1.LabelEdit = true;
            TreeNode theNode = this.treeView1.SelectedNode.Nodes.Add(this.treeView1.SelectedNode.Name, "");
            this.treeView1.SelectedNode.Expand();
            theNode.BeginEdit();
        }

        private void treeView1_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            if (e.Label == "" || e.Label == null)
            {
                e.Node.Remove();
                return;
            }
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                if (e.Label.Contains(c))
                {
                    e.Node.BeginEdit();
                    return;
                }
            }

            string path = e.Node.Name + "\\" + e.Label;
            try
            {
                Directory.CreateDirectory(path);
                e.Node.Name = path;
                treeView1.LabelEdit = false;
            }
            catch
            {
                e.Node.BeginEdit();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (treeView1.SelectedNode == null)
            {
                MessageBox.Show("Please Select a Folder.");
                return;
            }
            this.selectedFolder = treeView1.SelectedNode.Name;
            this.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.Close();
        }
    }
}
