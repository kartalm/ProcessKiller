using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ProcessKiller
{
    public partial class Screen : Form
    {
        #region Declarations

        private System.Windows.Forms.Timer CheckIdleTimer;

        private List<string> ProcessNames;

        public static Logger Logger;

        #endregion

        #region Constructor

        public Screen()
        {
            InitializeComponent();

            Logger = LogManager.GetLogger("ApplicationLogger");
        }

        #endregion

        #region Form Events

        private void Screen_Load(object sender, EventArgs e)
        {  
            CheckIdleTimer = new Timer();
            CheckIdleTimer.Interval = 10000;
            CheckIdleTimer.Tick += new EventHandler(CheckIdleTimer_Tick);
            CheckIdleTimer.Start();

            Logger.Info("Application is running.");
        }

        #endregion

        #region Timer Event

        private void CheckIdleTimer_Tick(object sender, System.EventArgs e)
        { 
            if (Win32.GetIdleTime() >= 1080000)//milliseconds(5 min -> 300000, 20 min -> 1200000)
            {
                CreateProcessList();
                KillProcesses();
            } 
        }

        #endregion

        #region Process Methods

        private void CreateProcessList()
        {
            ProcessNames = new List<string>();

            try
            {
                using (var reader = new StreamReader("UygulamaListesi.txt"))
                {
                    string item = string.Empty;
                    while ((item = reader.ReadLine()) != null)
                    {
                        ProcessNames.Add(item);
                    }
                }
            }
            catch (Exception)
            { }
        }

        private void KillProcesses()
        {
            if ((ProcessNames != null) && (ProcessNames.Count > 0))
            {
                foreach (Process process in Process.GetProcesses())
                {
                    foreach (string str in this.ProcessNames)
                    {
                        var s = str.ToLower();
                        var pn = process.ProcessName.ToLower(); 

                        if (pn.Contains(s) || s.Contains(pn))
                        {
                            KillProcessAndChildren(process.Id);
                        }
                    }
                }
            }
        }

        private static void KillProcessAndChildren(int pid)
        {
            using (ManagementObjectCollection.ManagementObjectEnumerator enumerator = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid).Get().GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    KillProcessAndChildren(Convert.ToInt32(((ManagementObject)enumerator.Current)["ProcessID"]));
                }
            }
            try
            {
                Process.GetProcessById(pid).Kill();
            }
            catch (ArgumentException)
            { }
        }
         
        #endregion

    }

    #region Win32 Library for idle 

    [StructLayout(LayoutKind.Sequential)] 
    public struct LASTINPUTINFO
    {
        public uint cbSize;

        public uint dwTime;
    }

    /// <summary>
    /// Summary description for Win32.
    /// </summary>
    public class Win32
    {
        [DllImport("User32.dll")]
        public static extern bool LockWorkStation();

        [DllImport("User32.dll")]
        public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("Kernel32.dll")]
        public static extern uint GetLastError();
         
        public static uint GetIdleTime()
        {
            LASTINPUTINFO structure = new LASTINPUTINFO();
            structure.cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>(structure);
            GetLastInputInfo(ref structure);
            return (((uint)Environment.TickCount) - structure.dwTime);
        }
        
        public static long GetTickCount()=> ((long) Environment.TickCount);
         
        public static long GetLastInputTime()
        {
            LASTINPUTINFO structure = new LASTINPUTINFO();
            structure.cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>(structure);
            if (!GetLastInputInfo(ref structure))
            {
                throw new Exception(GetLastError().ToString());
            }

            return (long)structure.dwTime;

        }

    }

    #endregion

}
