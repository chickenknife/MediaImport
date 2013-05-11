using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Diagnostics;
using System.Management;
namespace MediaImport
{
    public partial class driveLabel : UserControl
    {

        #region constractor
        public driveLabel()
        {
            InitializeComponent();
            importDriveWatcher();
        }

        public DriveInfo driveinfo;
        public DirectoryInfo destDirectory;
        public driveLabel(DirectoryInfo _dirInfo,DirectoryInfo _destDirectory)
        {
            InitializeComponent(); 
            driveinfo = new DriveInfo(_dirInfo.Root.Name);
            this.lblDriveLabel.Content = driveinfo.Name;
            this.destDirectory = _destDirectory;
            importDriveWatcher();
        }
        #endregion

        #region drivewatcher
        public void importDriveWatcher()
        {
            try
            {
                WqlEventQuery q = new WqlEventQuery();
                q.EventClassName = "__InstanceModificationEvent";
                q.WithinInterval = new TimeSpan(0, 0, 1);
                q.Condition = @"TargetInstance ISA 'Win32_LogicalDisk' and TargetInstance.DriveType = 5";

                ConnectionOptions opt = new ConnectionOptions();
                opt.EnablePrivileges = true;
                opt.Authority = null;
                opt.Authentication = AuthenticationLevel.Default;
                //opt.Username = "Administrator";
                //opt.Password = "";
                ManagementScope scope = new ManagementScope("\\root\\CIMV2", opt);

                ManagementEventWatcher watcher = new ManagementEventWatcher(scope, q);
                watcher.EventArrived += new EventArrivedEventHandler(watcher_EventArrived);
                watcher.Start();
            }
            catch (ManagementException e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private void watcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            //all drive check
            ManagementBaseObject wmiDevice = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            string driveName = (string)wmiDevice["DeviceID"];
            Console.WriteLine(driveName);
            Console.WriteLine(wmiDevice.Properties["VolumeName"].Value);
            Console.WriteLine((string)wmiDevice["Name"]);

            //detect target drive
            if (wmiDevice.Properties["VolumeName"].Value != null && this.driveinfo.Name.Replace("\\","") == driveName)
            {
                fastcopyStart(driveName,wmiDevice.Properties["VolumeName"].Value.ToString());
            }
            else
            {

            }
        }

        #endregion

        #region  fastcopy
        private void fastcopyStart(string copyroot,string volumelabel)
        {
            lblWrite("fastcopy processing...");
            DirectoryInfo destDir =
                new DirectoryInfo(destDirectory + "\\" + System.DateTime.Now.ToString("yyyyMMdd") + "\\" + volumelabel);

            if (destDir.Exists)
            {
                for (int i = 1; destDir.Exists; i++)
                {
                    destDir =
                        new DirectoryInfo(destDirectory + "\\" + System.DateTime.Now.ToString("yyyyMMdd") + "\\" + volumelabel + "_" + i.ToString());
                }
            }
            
            Process proc = new Process();
           
            ProcessStartInfo procInfo = new ProcessStartInfo();
            procInfo.CreateNoWindow = true;
            procInfo.FileName = @"fastcopy\fastcopy.exe";
            procInfo.Arguments =
                string.Format(@"/auto_close /force_start /filelog /log {0} /to={1}"
                , copyroot + "\\"
                , "\"" + destDir.FullName + "\"");
            proc.StartInfo = procInfo;

            proc.Start();

            proc.WaitForExit();
            lblWrite("fastcopy success");
        }
        #endregion

        #region form
        delegate void formdelegate();

        private void lblWrite(string text)
        {
            Dispatcher.Invoke((formdelegate)delegate()
            {
                this.lblDriveLabel.Content = string.Format("{0} [ {1} ] ({2})", driveinfo.Name + " " + driveinfo.VolumeLabel, text, System.DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
            });
        }
        #endregion
    }
}
