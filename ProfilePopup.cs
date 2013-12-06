using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Collections;
using System.IO;
using System.Diagnostics;

namespace FennyUTILS
{
    public partial class ProfilePopup : Form
    {
        string computerName = "";
        string remoteBackupPath = "";
        ArrayList currentlyLoggedOn;
        ArrayList mappedHives;
        ArrayList profilesToRebuild;
        public ProfilePopup(string computerName)
        {
            this.computerName = computerName;
            InitializeComponent();
        }

        private void ProfilePopup_Load(object sender, EventArgs e)
        {
            currentlyLoggedOn = RemoteTools.getRemoteLoggedOnUsers(computerName);
            mappedHives = RemoteTools.getRemoteUserHiveMappings(computerName);
            profilesToRebuild = RemoteTools.getRemoteProfileList(computerName);
            profilesToRebuild.Sort();
            foreach (String s in profilesToRebuild)
                this.listBox1.Items.Add(s);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            bool userHasMappedHives = false;
            bool userIsLoggedOn = false;
            this.progressBar1.Value = 1;
            currentlyLoggedOn = RemoteTools.getRemoteLoggedOnUsers(computerName);
            this.progressBar1.Value = 2;
            mappedHives = RemoteTools.getRemoteUserHiveMappings(computerName);
            this.progressBar1.Value = 3;
            ArrayList mappedHiveNames = new ArrayList();
            foreach (string s in mappedHives)
            {
                string[] ss = s.Split(':');
                if (((string)listBox1.SelectedItem).StartsWith(ss[0]))
                {
                    userHasMappedHives = true;
                    mappedHiveNames.Add(ss[1]);
                }
            }
            this.progressBar1.Value = 4;
            foreach (string s in currentlyLoggedOn)
            {
                if (((string)listBox1.SelectedItem).StartsWith(s))
                    userIsLoggedOn = true;    
            }
            this.progressBar1.Value = 5;
            if (userIsLoggedOn)
            {
                MessageBox.Show("Please have the user log off.", "Warning");
                //if (!RemoteTools.logoffRemoteUser(computerName, (string)listBox1.SelectedItem))
                this.progressBar1.Value = 0;
                return;
            }
            if (userHasMappedHives)
            {
                foreach (string s in mappedHiveNames)
                {
                    if (!RemoteTools.unmountRemoteHives(computerName, s))
                    {
                        MessageBox.Show("User hives did not unmount, please try again momentarily.", "Warning");
                        this.progressBar1.Value = 0;
                        return;
                    }
                }
            }
            String remotePath = RemoteTools.getRemoteProfilePath(computerName, (string)listBox1.SelectedItem);
            if (remotePath == null)
            {
                MessageBox.Show("Could not locate remote user Profile! Are you sure that you have selected a profile that needs to be recreated?", "Warning");
                return;
            }
            this.progressBar1.Value = 6;
            remoteBackupPath = RemoteTools.moveRemoteUserProfile(computerName, remotePath);
            if (remoteBackupPath != "")
            {
                this.progressBar1.Value = 7;
                if (RemoteTools.delRemoteProfileReg(computerName, (string)listBox1.SelectedItem))
                    MessageBox.Show("The profile has been backed up. Please have the user log into the computer now.", "Success");
                else
                    MessageBox.Show("The profile has been backed up, but the remote computer may suffer the \"temporary profile\" issue if it is not running Windows XP.", "Warning");
            }
            else
            {
                MessageBox.Show("The backup failed, are you sure that the user profile folder hasn't been removed already?", "Warning");
            }
            this.progressBar1.Value = 0;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.progressBar1.Value = 2;
            if (remoteBackupPath == "")
            {
                MessageBox.Show("No backup has been created.", "Warning");
                this.progressBar1.Value = 0;
                return;
            }
            this.progressBar1.Value = 8;
            MessageBox.Show("This is not implemented yet, but you can find your backup at: " + remoteBackupPath, "Warning");
            Process.Start(remoteBackupPath);
            this.progressBar1.Value = 0;
            //Directory.
        }
    }
}
