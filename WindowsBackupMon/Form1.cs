using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Security;
using Microsoft.Management.Infrastructure;
using Microsoft.Management.Infrastructure.Options;
using System.Security.Cryptography;

namespace WindowsBackupMon
{
    public partial class Form1 : Form
    {
        List<EventInfo> MyEvents = new List<EventInfo>();
        List<ServerInfo> MyServers = new List<ServerInfo>();

        private DataProtectionScope Scope = DataProtectionScope.CurrentUser;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            LoadServers();
            LoadMenu();
            foreach (ServerInfo si in MyServers)
            {
                GetEvent(si);
            }

        }
        private void RemoveHandler(object sender, EventArgs e)
        {
            ToolStripMenuItem clickedItem = (ToolStripMenuItem)sender;

            Microsoft.Win32.RegistryKey WindowsBackupMon_key;
            WindowsBackupMon_key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey("WindowsBackupMon");

            WindowsBackupMon_key.DeleteValue(clickedItem.Text);
            refresh();

        }
        void LoadMenu()
        {
            foreach (ServerInfo si in MyServers)
            {
                ToolStripMenuItem item = new ToolStripMenuItem();
                item.Name = si.Name;
                item.Tag = si.Name;
                item.Text = si.Name;
                item.Click += new EventHandler(RemoveHandler);

                removeServerToolStripMenuItem.DropDownItems.Add(item);
            }
        }
        void LoadServers()
        {
            Microsoft.Win32.RegistryKey WindowsBackupMon_key;
            WindowsBackupMon_key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey("WindowsBackupMon");

            if (WindowsBackupMon_key != null)
            {
                foreach (string name in WindowsBackupMon_key.GetValueNames())
                {
                    try
                    {
                        string[] values = (string[])WindowsBackupMon_key.GetValue(name);
                        ServerInfo si = new ServerInfo();
                        si.Name = name;
                        si.Password = Decrypt(values[1]);
                        si.User = values[0];
                        MyServers.Add(si);
                    }
                    catch (Exception ex) { }
                }
            }

        }
        void GetEvent(ServerInfo si)
        {
            
            // Start the child process.
            Process p = new Process();
            // Redirect the output stream of the child process.
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.FileName = "c:\\windows\\system32\\wevtutil.exe";
            p.StartInfo.Arguments = String.Format(" qe Microsoft-Windows-Backup /rd:true /f:text /r:{0} /u:{1} /p:{2} ",si.Name,si.User,si.Password);
            p.Start();
            // Do not wait for the child process to exit before
            // reading to the end of its redirected stream.
            // p.WaitForExit();
            // Read the output stream first and then wait.
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            if(p.ExitCode == 5)
            {
                MessageBox.Show("Error with Username or Password for server" + si.Name);
                return;
            }
            if (p.ExitCode == 1722)
            {
                MessageBox.Show("Error connecting to server " + si.Name);
                return;
            }
            if(p.ExitCode != 0)
            {
                MessageBox.Show("Unknown Error: " + p.ExitCode.ToString() + " for server "  + si.Name);
                return;
            }

            TreeNode tn = new TreeNode(si.Name);
            treeView1.Nodes.Add(tn);
            List<string> events = output.Split(new string[] { "Event[" }, StringSplitOptions.None).ToList();
            bool FirstEvent = false;
            foreach(String block in events)
            {
                List<String> lines = block.Split(new string[] {"\r\n"},StringSplitOptions.None).ToList();
                if(lines.Count > 1)
                {
                    EventInfo ei = new EventInfo();
                    foreach (String line1 in lines)
                    {
                        String line = line1.Trim(' ');
                        if(line.IndexOf("Date:") == 0)
                        {
                            String parse = line.Substring("Date:".Length).Trim(' ');
                            try
                            {
                                ei.TimeStamp = DateTime.Parse(parse);
                            }
                            catch { };

                        }
                        if (line.IndexOf("Event ID:") == 0)
                        {
                            String parse = line.Substring("Event ID:".Length).Trim(' ');
                            try
                            {
                                ei.EventID = int.Parse(parse);
                            }
                            catch { };

                        }
                        if (line.IndexOf("Computer:") == 0)
                        {
                            String parse = line.Substring("Computer:".Length).Trim(' ');
                            try
                            {
                                ei.Server = parse;
                            }
                            catch { };

                        }

                    }
                    ei.Description = lines[13];
                    if(DateTime.Compare(ei.TimeStamp,DateTime.Now.AddDays(-14)) > 0)
                    {
                        MyEvents.Add(ei);
                        tn.Nodes.Add(String.Format("{0} {1} {2}", ei.TimeStamp.ToShortDateString(), ei.EventID.ToString(), ei.Description));
                        if (FirstEvent == false)
                        {
                            tn.Text = tn.Text + " " + String.Format("{0} {1}", ei.TimeStamp.ToShortDateString(), ei.Description);
                            FirstEvent = true;
                        }

                    }

                }
            }
            
        }

        public string Encrypt(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException("text");
            }

            //encrypt data
            var data = Encoding.Unicode.GetBytes(text);
            byte[] encrypted = ProtectedData.Protect(data, null, Scope);

            //return as base64 string
            return Convert.ToBase64String(encrypted);
        }

        public string Decrypt(string cipher)
        {
            if (cipher == null)
            {
                throw new ArgumentNullException("cipher");
            }

            //parse base64 string
            byte[] data = Convert.FromBase64String(cipher);

            //decrypt data
            byte[] decrypted = ProtectedData.Unprotect(data, null, Scope);
            return Encoding.Unicode.GetString(decrypted);
        }

        private void addServerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddServer dlg = new AddServer();
            if(dlg.ShowDialog() == DialogResult.OK)
            {
                Microsoft.Win32.RegistryKey WindowsBackupMon_key;
                WindowsBackupMon_key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey("WindowsBackupMon");

                String Servername = dlg.textBox_server.Text;
                String User = dlg.textBox_user.Text;
                String Pass = dlg.textBox_password.Text;
                Pass = Encrypt(Pass);

                WindowsBackupMon_key.SetValue(Servername, new string[] { User, Pass },Microsoft.Win32.RegistryValueKind.MultiString);
                WindowsBackupMon_key.Close();


            }
        }

        private void refreshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            refresh();

        }
        void refresh()
        {
            treeView1.Nodes.Clear();
            MyEvents.Clear();
            MyServers.Clear();
            removeServerToolStripMenuItem.DropDownItems.Clear();

            LoadServers();
            LoadMenu();
            foreach (ServerInfo si in MyServers)
            {
                GetEvent(si);
            }

        }
    }
    class EventInfo
    {
        public String Server = "";
        public DateTime TimeStamp = new DateTime();
        public int EventID = -1;
        public String Description = "";

    }
    class ServerInfo
    {
        public string Name;
        public string User;
        public String Password;
    }
}
