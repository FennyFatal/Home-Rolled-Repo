using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Management;
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;
using System.Collections;
using Cassia;
using System.Threading;
using System.Net.NetworkInformation;
using System.Runtime.Remoting.Messaging;
using System.Runtime.InteropServices;

namespace FennyUTILS
{
    class RemoteTools
    {
        public enum RebootMethod
        {
            NiceBoot, ForceBoot, KillWinlogon
        };
        public static bool reboot (String computerName, RebootMethod method, int time = 2)
        {
            bool success = false;
            ProcessStartInfo pi;
            switch (method)
            {
                case RebootMethod.NiceBoot:
                    pi = new ProcessStartInfo("shutdown", " /m \\\\" + computerName + " /r /t " + time);
                    pi.CreateNoWindow = true;
                    //success = ExecRemote(computerName, "shutdown /r /t " + time);
                    Process.Start(pi);
                    Process.Start("ping", "-t " + computerName);
                    break;
                case RebootMethod.ForceBoot:
                    pi = new ProcessStartInfo("shutdown", " /m \\\\" + computerName + " /r /f /t " + time);
                    //success = ExecRemote(computerName, "shutdown /r /f /t " + time);
                    pi.CreateNoWindow = true;
                    Process.Start(pi);
                    Process.Start("ping", "-t " + computerName);
                    break;
                case RebootMethod.KillWinlogon:
                    terminateRemoteProcess(computerName, "lsass.exe");
                    /*
                    rebootRemoteSystem(computerName);
                    pi = new ProcessStartInfo("taskkill", "/s " + computerName + " /F /IM lasass.exe");
                    pi.CreateNoWindow = true;
                    Process.Start(pi);
                    success = ExecRemote(computerName, "taskkill /F /IM winlogon.exe");
                     */
                    Process.Start("ping", "-t " + computerName);
                    break;
                default:
                    success = false;
                    break;
            }
            return success;
        }
        public static bool rebootRemoteSystem(string computerName)
        {
            var connection = new ConnectionOptions();
            var wmiScope = new ManagementScope(String.Format("\\\\{0}\\root\\cimv2", computerName), connection);
            var query = new ObjectQuery("SELECT * FROM win32_operatingsystem");
            var wmiOS = new ManagementObjectSearcher(wmiScope, query, new EnumerationOptions());
            try
            {
                var results = wmiOS.Get();
                object[] args = { 2 + 4, null };
                foreach (ManagementObject m in results)
                {
                    object result = m.InvokeMethod("Win32Shutdown", args);
                    return ((UInt32)result == 0);
                }
            }
            catch { }
            return false;
        }
        private static bool ExecRemote(String computerName, String command, String directory = "")
        {
            bool result = true;
            var processToRun = new[] { command};
            var connection = new ConnectionOptions();
            var wmiScope = new ManagementScope(String.Format("\\\\{0}\\root\\cimv2", computerName), connection);
            var wmiProcess = new ManagementClass(wmiScope, new ManagementPath("Win32_Process"), new ObjectGetOptions());
            try
            {
             Object obj = wmiProcess.InvokeMethod("Create", processToRun);
            }
            catch (Exception)
            {
                result = false;
            }
            //finally { result = false; }
            return result;
        }

        class Patsy
        {
            public ManualResetEvent mre = new ManualResetEvent(false);
            public uint ProcessId;
            public int ExitCode;
            public bool EventArrived = false;
            public void ProcessStoptEventArrived(object sender, EventArrivedEventArgs e)
            {
                if ((uint)e.NewEvent.Properties["ProcessId"].Value == ProcessId)
                {
                    ExitCode = (int)(uint)e.NewEvent.Properties["ExitStatus"].Value;
                    EventArrived = true;
                    mre.Set();
                }
            }
        }

        private static bool ExecRemoteWait(String computerName, String command, int waitTime)
        {
            bool result = true;
            var processToRun = new[] { command };
            var connection = new ConnectionOptions();
            var wmiScope = new ManagementScope(String.Format("\\\\{0}\\root\\cimv2", computerName), connection);
            var wmiProcess = new ManagementClass(wmiScope, new ManagementPath("Win32_Process"), new ObjectGetOptions());
            try
            {
                ManagementBaseObject inParams = wmiProcess.GetMethodParameters("Create");
                inParams["CommandLine"] = processToRun[0];
                ManagementBaseObject obj = wmiProcess.InvokeMethod("Create", inParams, null);
                
                Patsy myPatsy = new Patsy();
                myPatsy.ProcessId = (uint)obj["processId"];

                //Let's make sure that the process didn't actually run.
                SelectQuery CheckProcess = new SelectQuery("Select * from Win32_Process Where ProcessId = " + myPatsy.ProcessId);
                using (ManagementObjectSearcher ProcessSearcher = new ManagementObjectSearcher(wmiScope, CheckProcess))
                {
                    using (ManagementObjectCollection MoC = ProcessSearcher.Get())
                    {
                        if (MoC.Count == 0)
                        {
                            myPatsy.EventArrived = true;
                        }
                    }
                }

                if (!myPatsy.EventArrived)
                {
                    myPatsy.mre = new ManualResetEvent(false);
                    WqlEventQuery q = new WqlEventQuery("Win32_ProcessStopTrace");
                    using (ManagementEventWatcher w = new ManagementEventWatcher(wmiScope, q))
                    {
                        w.EventArrived += new EventArrivedEventHandler(myPatsy.ProcessStoptEventArrived);
                        w.Start();
                        if (!myPatsy.mre.WaitOne(waitTime, false))
                        {
                            w.Stop();
                            myPatsy.EventArrived = false;
                        }
                        else
                            w.Stop();
                    }
                }
            }
            catch (Exception)
            {
                result = false;
            }
            //finally { result = false; }
            return result;
        }

        public static void launchRemotePrintMgmt(String computerName)
        {
            String pmc_xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?> <pmc-configuration xmlns=\"http://schemas.microsoft.com/2003/print/pmc/config/1.0\"><_locDefinition><_locDefault _loc=\"locNone\"/><_locTag _loc=\"locData\" _locAttrData=\"name\">filter</_locTag></_locDefinition><print-servers-standalone><server xmlns=\"\" uncname=\"\\\\"
                + computerName
                + "\" servername=\""
                + computerName
                + "\"/></print-servers-standalone><filters><filter name=\"Printers With Jobs\" description=\"filter:jobs_in_queue GREATER_THAN '0'\" shownumprinters=\"false\"><notification emailenabled=\"false\" emailto=\"\" emailfrom=\"\" emailsmtpserver=\"\" emailmessage=\"\" scriptenabled=\"false\" scriptpath=\"\" scriptargs=\"\"/><sexp neg=\"false\" fld=\"jobs_in_queue\" op=\"greater_than\" value=\"0\"/></filter><filter name=\"Printers Not Ready\" description=\"filter:NOT queue_status IS_EXACTLY 'ready'\" shownumprinters=\"false\"><notification emailenabled=\"false\" emailto=\"\" emailfrom=\"\" emailsmtpserver=\"\" emailmessage=\"\" scriptenabled=\"false\" scriptpath=\"\" scriptargs=\"\"/><sexp neg=\"false\" fld=\"queue_status\" op=\"is_exactly_not\" value=\"0\"/></filter></filters><miscellaneous-settings><printers-folder-show-extended-view value=\"0\"/></miscellaneous-settings></pmc-configuration>";
            String MSC_Cnts = "<?xml version=\"1.0\"?><MMC_ConsoleFile ConsoleVersion=\"3.0\" ProgramMode=\"UserSDI\"><ConsoleFileID>{E7773B4A-9E7E-4642-B707-F87D531C1A1D}</ConsoleFileID><FrameState ShowStatusBar=\"true\" PreventViewCustomization=\"true\"><WindowPlacement ShowCommand=\"SW_SHOWNORMAL\"><Point Name=\"MinPosition\" X=\"-1\" Y=\"-1\"/><Point Name=\"MaxPosition\" X=\"-1\" Y=\"-1\"/><Rectangle Name=\"NormalPosition\" Top=\"219\" Bottom=\"979\" Left=\"416\" Right=\"1856\"/> </WindowPlacement> </FrameState> <Views> <View ID=\"2\" ScopePaneWidth=\"292\" ActionsPaneWidth=\"-1\"> <BookMark Name=\"RootNode\" NodeID=\"2\"> <DynamicPath> <Segment String=\"Print Servers\"/> <Segment String=\""
                + computerName
                + "\"/> </DynamicPath> </BookMark> <BookMark Name=\"SelectedNode\" NodeID=\"2\"> <DynamicPath> <Segment String=\"Print Servers\"/> <Segment String=\""
                + computerName
                + "\"/> </DynamicPath> </BookMark> <WindowPlacement WPF_RESTORETOMAXIMIZED=\"true\" ShowCommand=\"SW_SHOWMAXIMIZED\"> <Point Name=\"MinPosition\" X=\"-1\" Y=\"-1\"/> <Point Name=\"MaxPosition\" X=\"-8\" Y=\"-30\"/> <Rectangle Name=\"NormalPosition\" Top=\"25\" Bottom=\"495\" Left=\"25\" Right=\"1211\"/> </WindowPlacement> <ViewOptions ViewMode=\"Report\" ScopePaneVisible=\"true\" ActionsPaneVisible=\"true\" DescriptionBarVisible=\"false\" DefaultColumn0Width=\"200\" DefaultColumn1Width=\"0\"/> </View> </Views> <VisualAttributes> <String Name=\"ApplicationTitle\" ID=\"2\"/> </VisualAttributes> <Favorites> <Favorite TYPE=\"Group\"> <String Name=\"Name\" ID=\"1\"/> <Favorites/> </Favorite> </Favorites> <ScopeTree> <SnapinCache> <Snapin CLSID=\"{7C606A3F-8AA8-4E36-92D6-2B6AFEC0B732}\" AllExtensionsEnabled=\"false\"> <Extensions/> </Snapin> <Snapin CLSID=\"{C96401CC-0E17-11D3-885B-00C04F72C717}\" AllExtensionsEnabled=\"true\"/> <Snapin CLSID=\"{D06342BD-9057-4673-B43A-0E9BBBE99F11}\" AllExtensionsEnabled=\"true\"/> </SnapinCache><Nodes> <Node ID=\"2\" ImageIdx=\"0\" CLSID=\"{D06342BD-9057-4673-B43A-0E9BBBE99F11}\" Preload=\"true\"> <Nodes/> <String Name=\"Name\" ID=\"2\"/> <SnapinProperties/> <ComponentDatas> <ComponentData> <GUID Name=\"Snapin\">{D06342BD-9057-4673-B43A-0E9BBBE99F11}</GUID> <Stream BinaryRefIndex=\"0\"/> </ComponentData> </ComponentDatas> <Components/> </Node></Nodes> </ScopeTree> <ConsoleTaskpads/> <ViewSettingsCache> <TargetView ViewID=\"2\" NodeTypeGUID=\"{E29E1970-5CFD-4513-9086-458E353BD439}\"/> <ViewSettings Flag_TaskPadID=\"true\" Age=\"1\"> <GUID>{00000000-0000-0000-0000-000000000000}</GUID> </ViewSettings> </ViewSettingsCache> <ColumnSettingsCache/> <StringTables> <IdentifierPool AbsoluteMin=\"1\" AbsoluteMax=\"65535\" NextAvailable=\"4\"/> <StringTable> <GUID>{71E5B33E-1064-11D2-808F-0000F875A9CE}</GUID> <Strings> <String ID=\"1\" Refs=\"1\">Favorites</String> <String ID=\"2\" Refs=\"1\">Print Management</String> <String ID=\"3\" Refs=\"2\">Console Root</String> </Strings> </StringTable> </StringTables> <BinaryStorage> <Binary>"
                + Convert.ToBase64String(Encoding.UTF8.GetBytes(pmc_xml))
                + "</Binary> </BinaryStorage></MMC_ConsoleFile>";
            String MYMSC_File = Path.GetTempFileName();
            File.WriteAllText(MYMSC_File, MSC_Cnts);
            Process.Start("mmc", MYMSC_File);
        }

        public static void launchRemoteCompMgmt(String computerName)
        {
            Process.Start("mmc", @"c:\windows\system32\compmgmt.msc /computer:\\" + computerName);
        }

        private static RegistryKey getRemoteMountedHives(string computername)
        {
            try
            {
                RegistryKey environmentKey = RegistryKey.OpenRemoteBaseKey(
                RegistryHive.LocalMachine, @"\\" + computername);
                return environmentKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\hivelist\", false);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string[] GetRemoteMountedHiveNames(string computerName)
        {
            RegistryKey hives = getRemoteMountedHives(computerName);
            if (hives != null)
            {
                string[] bob = new string[hives.ValueCount];
                int index = 0;
                foreach (string s in hives.GetValueNames())
                {
                    try
                    {
                        bob[index] = s + ':' + (String)hives.GetValue(s);
                        index++;
                    }
                    finally { }
                }
                return bob;
            }
            return null;
        }

        public static string getRemoteTempPath(string computername)
        {
            RegistryKey environmentKey = RegistryKey.OpenRemoteBaseKey(
            RegistryHive.LocalMachine, @"\\" + computername);
            RegistryKey key = environmentKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\", false);
            string systemRoot = (string)key.GetValue("SystemRoot");
            key = environmentKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", false);
            return ((string)key.GetValue("TEMP")).Replace(@"%SystemRoot%", systemRoot);
        }

        public static ArrayList getRemoteUserHiveMappings(String computerName)
        {
            ArrayList UserHiveMappings = new ArrayList();
            string[] remotehives = GetRemoteMountedHiveNames(computerName);
            if (remotehives == null)
                return UserHiveMappings;
            foreach (string s in remotehives)
            {
                string[] st = s.Split(':');
                if (st.Length == 2)
                {
                    string username = "";
                    string queryString = "";
                    if (st[1].ToUpper().Contains("USRCLASS.DAT"))
                    {
                        queryString = (st[1].ToLower().Contains("appdata")) ? "\\appdata\\" : "\\local settings\\";
                    }
                    else if (st[1].ToUpper().Contains("NTUSER.DAT"))
                    {
                        queryString = "\\ntuser.dat";
                    }
                    if (queryString != "")
                    {
                        int end = st[1].ToLower().IndexOf(queryString);
                        int start = 0;
                        for (int i = end - 1; i > 0; i--)
                            if (st[1][i] == '\\')
                            { start = i + 1; break; }
                        username = st[1].Substring(start, (end - start));
                    }
                    if (username != "")
                        UserHiveMappings.Add(username + ":" + st[0]);
                }
            }
            return UserHiveMappings;
        }

        delegate object WorkDelegate(string arg);

        static public object checkForFail(string arg, int timeout, Func<string,object> method)
        {
            WorkDelegate d = new WorkDelegate(method);
            IAsyncResult res = d.BeginInvoke(arg, null, null);
            if (res.IsCompleted == false)
            {
                res.AsyncWaitHandle.WaitOne(timeout, false);
                if (res.IsCompleted == false)
                    throw new ApplicationException("Timeout");
            }
            return d.EndInvoke((AsyncResult)res);
        }

        static private object GetExplorerProcesses(string computerName)
        {
            return Process.GetProcessesByName("explorer", computerName);
        }
        static bool terminateRemoteProcess(string computerName, string processName)
        {
            ConnectionOptions connection = new ConnectionOptions();
            ManagementScope wmiScope = new ManagementScope(String.Format("\\\\{0}\\root\\cimv2", computerName), connection);
            ObjectQuery query = new ObjectQuery("SELECT * FROM win32_process where name = '" + processName + "'");
            ManagementObjectSearcher wmiOS = new ManagementObjectSearcher(wmiScope, query, new EnumerationOptions());
            int count = 0;
            try
            {
                foreach (ManagementObject process in wmiOS.Get())
                {
                    try
                    {
                        object[] obj = new object[] { 0 };
                        object result = process.InvokeMethod("Terminate", obj);
                        count++;
                    } catch { }
                }
            } catch { }
            return (count > 0);
        }
        public static ArrayList getSystemStats (string computerName)
        {
            var connection = new ConnectionOptions();
            var wmiScope = new ManagementScope(String.Format("\\\\{0}\\root\\cimv2", computerName), connection);
            var query = new ObjectQuery("SELECT * FROM win32_operatingsystem");
            var wmiOS = new ManagementObjectSearcher(wmiScope, query, new EnumerationOptions());
            try
            {
                var results = wmiOS.Get();
                foreach (ManagementObject m in results)
                {
                    ArrayList retval = new ArrayList();
                    try {
                        string arch = "";
                        try
                        {
                            arch = " (" + m["OSArchitecture"] + ") ";
                        }catch { };
                        string[] liver = m["Version"].ToString().Split('.');
                        retval.Add("OS: " + m["Caption"] + " " + liver[0] + '.' + liver[1] + " (" + liver[2] + ")" + arch);
                    } catch { } try {
                    retval.Add("ServicePack: " + m["CSDVersion"]);
                    } catch { } try {
                    retval.Add("Physical Memory (RAM): " + m["FreePhysicalMemory"] + " / " + m["TotalVisibleMemorySize"]);
                    } catch { } try {
                    retval.Add("Virtual Memory: " + m["FreeVirtualMemory"] + " / " + m["TotalVirtualMemorySize"]);
                    } catch { } try {

                    //retval.Add("Windows Directory: " + m["WindowsDirectory"]);
                    } catch { } try {

                    //int timezone = Int32.Parse(m["CurrentTimeZone"].ToString()) / 60;
                    //retval.Add("TimeZone: GMT" + ((timezone != 0) ? timezone.ToString() : ""));
                    } catch { } try {
                        string currentTime = m["LocalDateTime"].ToString();
                        int curYear = int.Parse(currentTime.Substring(0,4));
                        int curMonth = int.Parse(currentTime.Substring(4, 2));
                        int curDay = int.Parse(currentTime.Substring(6, 2));
                        int curHour = int.Parse(currentTime.Substring(8, 2));
                        int curMin = int.Parse(currentTime.Substring(10, 2));
                        int curSec = int.Parse(currentTime.Substring(12, 2));
                        string bootTime = m["LastBootUpTime"].ToString();
                        int bootYear = int.Parse(bootTime.Substring(0, 4));
                        int bootMonth = int.Parse(bootTime.Substring(4, 2));
                        int bootDay = int.Parse(bootTime.Substring(6, 2));
                        int bootHour = int.Parse(bootTime.Substring(8, 2));
                        int bootMin = int.Parse(bootTime.Substring(10, 2));
                        int bootSec = int.Parse(bootTime.Substring(12, 2));
                        retval.Add("Uptime: " + (new DateTime(curYear,curMonth,curDay,curHour,curMin,curSec) - new DateTime(bootYear,bootMonth,bootDay,bootHour,bootMin,bootSec)));
                    } catch { } 
                    return retval;
                }
            }
            catch (Exception)
            {
                
            }
            return null;
        }
        public static ArrayList getRemoteLoggedOnUsers(String computerName)
        {
            ArrayList processnames = new ArrayList();
            try
            {
                if (new System.Net.NetworkInformation.Ping().Send(computerName).Status != IPStatus.Success)
                    return processnames;
                Process[] pra = (Process[])checkForFail(computerName, 10000, GetExplorerProcesses);
                foreach (Process pr in pra)
                {
                    //if (pr.ProcessName.ToLower().Contains("explorer"))
                    //{
                    String username = "";
                    username = GetProcessInfoByPID(computerName, pr.Id);
                    if (username != "" && !processnames.Contains(username))
                        processnames.Add(username);
                    //}
                }
                return processnames;
            }
            catch (Exception)
            {
                return processnames;
            }
        }

        public static string GetProcessInfoByPID(string computerName, int PID)
        {
            string User = String.Empty;
            string processname = String.Empty;
            try
            {
                ObjectQuery sq = new ObjectQuery
                    ("Select * from Win32_Process Where ProcessID = '" + PID + "'");
                var wmiScope = new ManagementScope(String.Format("\\\\{0}\\root\\cimv2", computerName));
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(wmiScope, sq);
                if (searcher.Get().Count == 0)
                    return User;
                foreach (ManagementObject oReturn in searcher.Get())
                {
                    string[] o = new String[2];
                    //Invoke the method and populate the o var with the user name and domain
                    oReturn.InvokeMethod("GetOwner", (object[])o);
                    //int pid = (int)oReturn["ProcessID"];
                    processname = (string)oReturn["Name"];
                    //dr[2] = oReturn["Description"];
                    User = o[0];
                    if (User == null)
                        User = String.Empty;
                }
            }
            catch
            {
                return User;
            }
            return User;
        }
        public static ArrayList getRemoteProfileList (String computerName)
        {
            ArrayList retval = new ArrayList();
            RegistryKey environmentKey = RegistryKey.OpenRemoteBaseKey(
                RegistryHive.LocalMachine, @"\\" + computerName);
            RegistryKey key = environmentKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList");
            foreach (string s in key.GetSubKeyNames())
            {
                try {
                string[] chunks = ((string)key.OpenSubKey(s).GetValue("ProfileImagePath")).Split('\\');
                retval.Add(chunks[chunks.Length - 1]);
                } catch (Exception){}
            }
            return retval;
        }

        public static bool unmountRemoteHives(string computerName, string REGPATH)
        {
            try
            {
                ExecRemote(computerName, "reg unload " + REGPATH);
                foreach (string s in getRemoteMountedHives(computerName).GetValueNames())
                {
                    string[] reg = REGPATH.Split('\\');
                    if (s.EndsWith(reg[reg.Length - 1]))
                        return false;
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        public static bool doLogoffRemoteUser(string computerName, string username)
        {
            string remoteTempPath = getRemoteTempPath(computerName);
            ExecRemoteWait(computerName, "c:\\windows\\system32\\CMD.EXE /S /C query session > " + remoteTempPath + "\\sessions.txt", 2000);
            string sessionID = "";
            try
            {
                
                foreach (string sessionLine in File.ReadAllLines("\\\\" + computerName + "\\" +remoteTempPath.Replace(':','$')+ "\\sessions.txt"))
                {
                    int start = sessionLine.LastIndexOf(username) + username.Length;
                    if (sessionLine.Contains(username))
                    {
                        sessionID = sessionLine.Substring(start, sessionLine.Length - start).TrimStart(' ');
                        sessionID = sessionID.Substring(0, sessionID.IndexOf(' '));
                        break;
                    }
                }
            }
            catch { }
            if (sessionID != "")
            {
                ExecRemote(computerName, "logoff " + sessionID);
                return true;
            }
            else
            {
                //We failed to get a session id. :(
            }
            return false;
        }
        /*
        public static bool logoffRemoteUser(string computerName, string username)
        {
            ITerminalServicesManager manager = new TerminalServicesManager();
            ITerminalServer server = manager.GetRemoteServer(computerName);
            server.Open();
            //foreach (ITerminalServicesSession s in server.GetSessions())
            //{
                //if (s.UserName.Equals(username))
                    //s.Logoff(true);
            //}
            server.Shutdown(ShutdownType.LogoffAllSessions);
            return false;
        }
        */
        public static ArrayList getPrintersWithDrivers(String computerName)
        {
            ArrayList retval = new ArrayList();
            const string loc_system_printers = @"SYSTEM\CurrentControlSet\Control\Print\Printers";
            try
            {
                RegistryKey environmentKey = RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, @"\\" + computerName);
                RegistryKey key = environmentKey.OpenSubKey(loc_system_printers);
                foreach (string s in key.GetSubKeyNames())
                {
                    try
                    {
                        string driverPair = (string)key.OpenSubKey(s).GetValue("Printer Driver") + ':' + s;
                        if (!retval.Contains(driverPair))
                            retval.Add(driverPair);
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception) { }
            return retval;
        }

        public static bool doDeleteRemotePrinterDriver(String computerName, String driverName)
        {
            ExecRemoteWait(computerName, "net stop \"print spooler\"", 10000);
            const string loc_x64 = @"SYSTEM\CurrentControlSet\Control\Print\Environments\Windows x64\Drivers\Version-3";
            const string loc_ia64 = @"SYSTEM\CurrentControlSet\Control\Print\Environments\Windows IA64\Drivers\Version-3";
            const string loc_x86 = @"SYSTEM\CurrentControlSet\Control\Print\Environments\Windows NT x86\Drivers\Version-3";
            const string loc_40 = @"SYSTEM\CurrentControlSet\Control\Print\Environments\Windows 4.0\Drivers\Version-2";
            RegistryKey environmentKey = RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, @"\\" + computerName);
            RegistryKey keyx64 = environmentKey.OpenSubKey(loc_x64,true);
            RegistryKey keyia64 = environmentKey.OpenSubKey(loc_ia64,true);
            RegistryKey keyx86 = environmentKey.OpenSubKey(loc_x86,true);
            RegistryKey keynt40 = environmentKey.OpenSubKey(loc_40,true);
            bool deleted = false;
            try {
            keyia64.DeleteSubKeyTree(driverName);
            deleted = true;
            } catch {} try {
            keyx64.DeleteSubKeyTree(driverName);
            deleted = true;
            } catch {} try {
            keyx86.DeleteSubKeyTree(driverName);
            deleted = true;
            } catch {} try {
            keynt40.DeleteSubKeyTree(driverName);
            deleted = true;
            } catch {}
            if (!deleted)
            {
                Process.Start("reg", "delete /f \\\\" + computerName + "HKLM\\" + loc_x64 + "\\" + driverName);
                Process.Start("reg", "delete /f \\\\" + computerName + "HKLM\\" + loc_ia64 + "\\" + driverName);
                Process.Start("reg", "delete /f \\\\" + computerName + "HKLM\\" + loc_x86 + "\\" + driverName);
                Process.Start("reg", "delete /f \\\\" + computerName + "HKLM\\" + loc_40 + "\\" + driverName);
            }
            ExecRemote(computerName, "net start \"print spooler\"");
            return true;
        }

        public static ArrayList getUserPrinterDriverList(String computerName, String userName=null)
        {
            ArrayList RetVal = new ArrayList();
            try
            {
                foreach (string s in getRemoteUserHiveMappings(computerName))
                {
                    if (userName == null || s.StartsWith(userName))
                    {
                        string[] chopped = s.Split('\\');
                        string uid = chopped[chopped.Length - 1];
                        string path = uid + @"\Printers\Connections";
                        try
                        {
                            RegistryKey userKey = RegistryKey.OpenRemoteBaseKey(RegistryHive.Users, @"\\" + computerName);
                            RegistryKey environmentKey = RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, @"\\" + computerName);
                            userKey = userKey.OpenSubKey(path);
                            try
                            {
                                foreach (string ss in userKey.GetSubKeyNames())
                                {
                                    try
                                    {
                                        string[] printerPath = ss.TrimStart(',').Split(',');
                                        string remotePrinter = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Print\Providers\Client Side Rendering Print Provider\Servers\"
                                            + printerPath[0]
                                            + @"\Printers\"
                                            + (string)userKey.OpenSubKey(ss).GetValue("GuidPrinter");
                                        printerPath[0] = (string)environmentKey.OpenSubKey(remotePrinter).GetValue("Printer Driver");
                                        string retstring = printerPath[0] + ':' + printerPath[1] + (userName == null ? ':' + s.Split(':')[0] : "");
                                        if (!RetVal.Contains(retstring))
                                            RetVal.Add(retstring);
                                    } catch { }
                                }
                            } catch { }
                        } catch { }
                    }
                }
            } catch { }
            return RetVal;
        }
        public static ArrayList getPrinterDriverList(String computerName)
        {
            ArrayList retval = new ArrayList();
            const string loc_x64 = @"SYSTEM\CurrentControlSet\Control\Print\Environments\Windows x64\Drivers\Version-3";
            const string loc_ia64 = @"SYSTEM\CurrentControlSet\Control\Print\Environments\Windows IA64\Drivers\Version-3";
            const string loc_x86 = @"SYSTEM\CurrentControlSet\Control\Print\Environments\Windows NT x86\Drivers\Version-3";
            const string loc_40 = @"SYSTEM\CurrentControlSet\Control\Print\Environments\Windows 4.0\Drivers\Version-3";
            RegistryKey environmentKey = RegistryKey.OpenRemoteBaseKey( RegistryHive.LocalMachine, @"\\" + computerName);
            RegistryKey keyx64 = environmentKey.OpenSubKey(loc_x64);
            RegistryKey keyia64 = environmentKey.OpenSubKey(loc_ia64);
            RegistryKey keyx86 = environmentKey.OpenSubKey(loc_x86);
            RegistryKey keynt40 = environmentKey.OpenSubKey(loc_40);
            try {
            foreach (string s in keyia64.GetSubKeyNames())
            { if (!retval.Contains(s)) retval.Add(s); }
            } catch { } try {
            foreach (string s in keyx64.GetSubKeyNames())
            { if (!retval.Contains(s)) retval.Add(s); }
            } catch { } try {
            foreach (string s in keyx86.GetSubKeyNames())
            { if (!retval.Contains(s)) retval.Add(s); }
            } catch { } try {
            foreach (string s in keynt40.GetSubKeyNames())
            { if (!retval.Contains(s)) retval.Add(s); }
            } catch { }
            return retval;
        }
        public static string getRemoteProfilePath(String computerName, string profileName)
        {
            RegistryKey environmentKey = RegistryKey.OpenRemoteBaseKey(
                RegistryHive.LocalMachine, @"\\" + computerName);
            RegistryKey key = environmentKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList");
            foreach (string s in key.GetSubKeyNames())
            {
                try
                {
                    string path = (string)key.OpenSubKey(s).GetValue("ProfileImagePath");
                    if (path.EndsWith(profileName))
                        return path;
                }
                catch (Exception) { }
            }
            return null;
        }
        public static bool delRemoteProfileReg(String computerName, string profileName)
        {
            RegistryKey environmentKey = RegistryKey.OpenRemoteBaseKey(
                RegistryHive.LocalMachine, @"\\" + computerName);
            RegistryKey key = environmentKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList",true);
            foreach (string s in key.GetSubKeyNames())
            {
                try
                {
                    RegistryKey lkey = key.OpenSubKey(s);
                    if (((string)lkey.GetValue("ProfileImagePath")).EndsWith(profileName))
                    {
                        lkey.Close();
                        try
                        {
                            key.DeleteSubKeyTree(s);
                        }
                        catch (Exception)
                        {
                            return ExecRemote(computerName, "reg","delete HKLM\\" + @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList" + '\\' + s);
                        }
                        return true;
                    }
                }
                catch (Exception) { }
            }
            return false;
        }
        public static string moveRemoteUserProfile(string computerName, string profilePath)
        {
            string[] chunks = profilePath.Split('\\');
            string backupLocation = "\\\\" + computerName + "\\c$\\tmp";
            if (!Directory.Exists(backupLocation))
                Directory.CreateDirectory(backupLocation);
            
            string profileDirname = chunks[chunks.Length - 1] + ".bak";
            int counter = 0;
            while (Directory.Exists(backupLocation + "\\" + profileDirname))
                profileDirname = chunks[chunks.Length - 1] + ".bak" + counter++;
            backupLocation += "\\" + profileDirname;
            try
            {
                Directory.Move("\\\\" + computerName + "\\" + profilePath.Replace("%SystemDrive%", "c:").Replace(':', '$'), backupLocation);
                return backupLocation;
            }
            catch (Exception) { return ""; }
        }

        public class GetNetShares
        {
            #region External Calls
            [DllImport("Netapi32.dll", SetLastError = true)]
            static extern int NetApiBufferFree(IntPtr Buffer);
            [DllImport("Netapi32.dll", CharSet = CharSet.Unicode)]
            private static extern int NetShareEnum(
                 StringBuilder ServerName,
                 int level,
                 ref IntPtr bufPtr,
                 uint prefmaxlen,
                 ref int entriesread,
                 ref int totalentries,
                 ref int resume_handle
                 );
            #endregion
            #region External Structures
            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            public struct SHARE_INFO_1
            {
                public string shi1_netname;
                public uint shi1_type;
                public string shi1_remark;
                public SHARE_INFO_1(string sharename, uint sharetype, string remark)
                {
                    this.shi1_netname = sharename;
                    this.shi1_type = sharetype;
                    this.shi1_remark = remark;
                }
                public override string ToString()
                {
                    return shi1_netname;
                }
            }
            #endregion
            const uint MAX_PREFERRED_LENGTH = 0xFFFFFFFF;
            const int NERR_Success = 0;
            private enum NetError : uint
            {
                NERR_Success = 0,
                NERR_BASE = 2100,
                NERR_UnknownDevDir = (NERR_BASE + 16),
                NERR_DuplicateShare = (NERR_BASE + 18),
                NERR_BufTooSmall = (NERR_BASE + 23),
            }
            private enum SHARE_TYPE : uint
            {
                STYPE_DISKTREE = 0,
                STYPE_PRINTQ = 1,
                STYPE_DEVICE = 2,
                STYPE_IPC = 3,
                STYPE_SPECIAL = 0x80000000,
            }
            public static SHARE_INFO_1[] EnumNetShares(string Server)
            {
                List<SHARE_INFO_1> ShareInfos = new List<SHARE_INFO_1>();
                int entriesread = 0;
                int totalentries = 0;
                int resume_handle = 0;
                int nStructSize = Marshal.SizeOf(typeof(SHARE_INFO_1));
                IntPtr bufPtr = IntPtr.Zero;
                StringBuilder server = new StringBuilder(Server);
                int ret = NetShareEnum(server, 1, ref bufPtr, MAX_PREFERRED_LENGTH, ref entriesread, ref totalentries, ref resume_handle);
                if (ret == NERR_Success)
                {
                    IntPtr currentPtr = bufPtr;
                    for (int i = 0; i < entriesread; i++)
                    {
                        SHARE_INFO_1 shi1 = (SHARE_INFO_1)Marshal.PtrToStructure(currentPtr, typeof(SHARE_INFO_1));
                        ShareInfos.Add(shi1);
                        currentPtr = new IntPtr(currentPtr.ToInt32() + nStructSize);
                    }
                    NetApiBufferFree(bufPtr);
                    return ShareInfos.ToArray();
                }
                else
                {
                    ShareInfos.Add(new SHARE_INFO_1("ERROR=" + ret.ToString(), 10, string.Empty));
                    return ShareInfos.ToArray();
                }
            }
        }
    }
}
