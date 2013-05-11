using System;
using System.IO;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace MediaReader
{
    public delegate void DriveEventHandler(object sender, DriveEventArgs args);

    public class DriveEventArgs : EventArgs
    {
        public DriveInfo Drive { get; private set; }

        public DriveEventArgs(DriveInfo drive)
        {
            Drive = drive;
        }
    }

    class RemovableStorageMonitor : Form
    {
        #region declarations for native API call

        //from winuser.h
        private enum WM : uint
        {
            WM_SHNOTIFY = 0x0401,
            WM_DEVICECHANGE = 0x0219,
        }

        #region Shell notification

        [StructLayout(LayoutKind.Sequential)]
        struct SHChangeNotifyEntry
        {
            public IntPtr pIdl;
            [MarshalAs(UnmanagedType.Bool)]
            public Boolean Recursively;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SHNOTIFYSTRUCT
        {
            public uint dwItem1;
            public uint dwItem2;
        }

        [Flags]
        private enum SHCNF
        {
            SHCNF_IDLIST = 0x0000,
            SHCNF_PATHA = 0x0001,
            SHCNF_PRINTERA = 0x0002,
            SHCNF_DWORD = 0x0003,
            SHCNF_PATHW = 0x0005,
            SHCNF_PRINTERW = 0x0006,
            SHCNF_TYPE = 0x00FF,
            SHCNF_FLUSH = 0x1000,
            SHCNF_FLUSHNOWAIT = 0x2000,
        }

        [Flags]
        private enum SHCNE : uint
        {
            SHCNE_RENAMEITEM = 0x00000001,
            SHCNE_CREATE = 0x00000002,
            SHCNE_DELETE = 0x00000004,
            SHCNE_MKDIR = 0x00000008,
            SHCNE_RMDIR = 0x00000010,
            SHCNE_MEDIAINSERTED = 0x00000020,
            SHCNE_MEDIAREMOVED = 0x00000040,
            SHCNE_DRIVEREMOVED = 0x00000080,
            SHCNE_DRIVEADD = 0x00000100,
            SHCNE_NETSHARE = 0x00000200,
            SHCNE_NETUNSHARE = 0x00000400,
            SHCNE_ATTRIBUTES = 0x00000800,
            SHCNE_UPDATEDIR = 0x00001000,
            SHCNE_UPDATEITEM = 0x00002000,
            SHCNE_SERVERDISCONNECT = 0x00004000,
            SHCNE_UPDATEIMAGE = 0x00008000,
            SHCNE_DRIVEADDGUI = 0x00010000,
            SHCNE_RENAMEFOLDER = 0x00020000,
            SHCNE_FREESPACE = 0x00040000,
            SHCNE_EXTENDED_EVENT = 0x04000000,
            SHCNE_ASSOCCHANGED = 0x08000000,
            SHCNE_DISKEVENTS = 0x0002381F,
            SHCNE_GLOBALEVENTS = 0x0C0581E0,
            SHCNE_ALLEVENTS = 0x7FFFFFFF,
            SHCNE_INTERRUPT = 0x80000000,
        }

        [DllImport("shell32.dll", EntryPoint = "#2", CharSet = CharSet.Auto)]
        private static extern uint SHChangeNotifyRegister(
            IntPtr hWnd,
            SHCNF fSources,
            SHCNE fEvents,
            uint wMsg,
            int cEntries,
            ref SHChangeNotifyEntry pFsne);

        [DllImport("shell32.dll", EntryPoint = "#4", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern Boolean SHChangeNotifyUnregister(
            uint hNotify);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHGetPathFromIDList(
            IntPtr pidl,
            StringBuilder Path);

        #endregion Shell notification

        #region Device Broadcast
        //from dbt.h
        private enum DBT
        {
            DBT_DEVICEARRIVAL = 0x8000,
            DBT_DEVICEQUERYREMOVE = 0x8001,
            DBT_DEVICEQUERYREMOVEFAILED = 0x8002,
            DBT_DEVICEREMOVEPENDING = 0x8003,
            DBT_DEVICEREMOVECOMPLETE = 0x8004,
        }

        private enum DBT_DEVTP
        {
            DBT_DEVTYP_OEM = 0x0000,
            DBT_DEVTYP_DEVNODE = 0x0001,
            DBT_DEVTYP_VOLUME = 0x0002,
            DBT_DEVTYP_PORT = 0x0003,
            DBT_DEVTYP_NET = 0x0004,
            DBT_DEVTYP_DEVICEINTERFACE = 0x0005,
            DBT_DEVTYP_HANDLE = 0x0006,
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct DEV_BROADCAST_VOLUME
        {
            public uint dbcv_size;
            public uint dbcv_devicetype;
            public uint dbcv_reserved;
            public uint dbcv_unitmask;
        }

        #endregion Device Broadcast

        #endregion declarations for native API call

        private static readonly Dictionary<uint, char> driveLetters;
        private readonly Dictionary<char, DriveInfo> drives;
        private readonly uint notifyId;
        public event DriveEventHandler DriveInserted;
        public event DriveEventHandler DriveRemoved;

        static RemovableStorageMonitor()
        {
            /*
             * calculate bit masks
             * A : 00000000 00000000 00000000 00000001
             * B : 00000000 00000000 00000000 00000010
             *                     ...
             * Z : 00000010 00000000 00000000 00000000
             */
            const uint initMask = 0x01;
            var upperLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            driveLetters = upperLetters.ToDictionary(c => initMask << (c - upperLetters[0]));
        }

        public RemovableStorageMonitor()
        {
            Visible = false;
            drives = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Removable).ToDictionary(d => d.Name[0]);
            notifyId = RegisterChangeNotify();
        }

        private uint RegisterChangeNotify()
        {
            var notifyEntry = new SHChangeNotifyEntry() { pIdl = IntPtr.Zero, Recursively = true };
            var notifyId = SHChangeNotifyRegister(
                Handle,
                SHCNF.SHCNF_TYPE | SHCNF.SHCNF_IDLIST,
                SHCNE.SHCNE_MEDIAINSERTED | SHCNE.SHCNE_MEDIAREMOVED,
                (uint)WM.WM_SHNOTIFY,
                1,
                ref notifyEntry);

            return notifyId;
        }

        protected override void WndProc(ref Message m)
        {
            switch ((WM)m.Msg)
            {
                case WM.WM_SHNOTIFY:
                    HandleShellNotificationMessage(ref m);
                    break;

                case WM.WM_DEVICECHANGE: //Top-level windows receive this message
                    HandleDeviceChangeMessage(ref m);
                    break;
            }

            base.WndProc(ref m);
        }

        private void HandleShellNotificationMessage(ref Message m)
        {
            DriveEventHandler handler;
            switch ((SHCNE)m.LParam)
            {
                case SHCNE.SHCNE_MEDIAINSERTED:
                    handler = DriveInserted;
                    break;

                case SHCNE.SHCNE_MEDIAREMOVED:
                    handler = DriveRemoved;
                    break;

                default:
                    return;
            }

            var drive = GetDriveInfoFromShellNofication(ref m);
            RaiseDriveEvent(handler, drive);
        }

        private DriveInfo GetDriveInfoFromShellNofication(ref Message m)
        {
            var shNotify = (SHNOTIFYSTRUCT)Marshal.PtrToStructure(m.WParam, typeof(SHNOTIFYSTRUCT));
            var driveRootPathBuffer = new StringBuilder("A:\\");
            if (SHGetPathFromIDList((IntPtr)shNotify.dwItem1, driveRootPathBuffer) == 0) return null;

            if (driveRootPathBuffer.Length == 0) return null;
            var driveLetter = driveRootPathBuffer[0];
            return GetDriveInfoFromDriveLetter(driveLetter);
        }

        private void HandleDeviceChangeMessage(ref Message m)
        {
            DriveEventHandler handler;
            DriveInfo drive;
            switch ((DBT)m.WParam.ToInt32())
            {
                case DBT.DBT_DEVICEARRIVAL:
                    {
                        var driveLetter = GetDriveLetterFromDeviceVolume(ref m);
                        drive = GetDriveInfoFromDriveLetter(driveLetter);
                        drives[driveLetter] = drive;
                        handler = DriveInserted;
                    }
                    break;

                case DBT.DBT_DEVICEREMOVECOMPLETE:
                    {
                        var driveLetter = GetDriveLetterFromDeviceVolume(ref m);
                        drives.TryGetValue(driveLetter, out drive);
                        drives.Remove(driveLetter);
                        handler = DriveRemoved;
                    }
                    break;

                default:
                    return;
            }

            RaiseDriveEvent(handler, drive);
        }

        private static char GetDriveLetterFromDeviceVolume(ref Message m)
        {
            char driveLetter = '\0';
            var volume = (DEV_BROADCAST_VOLUME)Marshal.PtrToStructure(m.LParam, typeof(DEV_BROADCAST_VOLUME));

            if ((DBT_DEVTP)volume.dbcv_devicetype != DBT_DEVTP.DBT_DEVTYP_VOLUME) return driveLetter;

            driveLetters.TryGetValue(volume.dbcv_unitmask, out driveLetter);
            return driveLetter;
        }

        private DriveInfo GetDriveInfoFromDeviceVolume(ref Message m)
        {
            var volume = (DEV_BROADCAST_VOLUME)Marshal.PtrToStructure(m.LParam, typeof(DEV_BROADCAST_VOLUME));
            if ((DBT_DEVTP)volume.dbcv_devicetype != DBT_DEVTP.DBT_DEVTYP_VOLUME) return null;

            if (!driveLetters.ContainsKey(volume.dbcv_unitmask)) return null;
            var driveLetter = driveLetters[volume.dbcv_unitmask];

            return GetDriveInfoFromDriveLetter(driveLetter);
        }

        private DriveInfo GetDriveInfoFromDriveLetter(char driveLetter)
        {
            return DriveInfo.GetDrives().FirstOrDefault(d =>
                d.Name[0] == driveLetter && d.DriveType == DriveType.Removable);
        }

        private void RaiseDriveEvent(DriveEventHandler handler, DriveInfo drive)
        {
            if (handler == null) return;
            if (drive == null) return;
            if (handler == DriveInserted && !drive.IsReady) return;
            if (handler == DriveRemoved && drive.IsReady) return;

            handler(this, new DriveEventArgs(drive));
        }

        protected override void Dispose(bool disposing)
        {
            if (notifyId != 0) SHChangeNotifyUnregister(notifyId);
            base.Dispose(disposing);
        }
    }
}