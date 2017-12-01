using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Reflection;
using NLog;
using System.Security;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading;

namespace ProcessKillerConsoleApp
{
    public class Program
    {
        #region Declarations

        public static ProcessKillerService processKillerService;

        public static Logger Logger;

        #endregion

        static void Main(string[] args)
        {
            Logger = LogManager.GetLogger("ApplicationLogger");

            #region Run Only One Instance (Mutex)

            bool isAppInstanceClosed;

            //create mutex object
            var mutex = new Mutex(true, "ProcessKiller", out isAppInstanceClosed);

            if (!isAppInstanceClosed)
            {
                Logger.Info("Only one instance can be executed!");

                return;
            }

            #endregion

            Logger.Info("Application started. Logs are enabled.");
            Logger.Info("--------------------------------------");

            #region Run Application As Administrator (With Local Administrator Account)

            var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());

            var administrativeMode = principal.IsInRole(WindowsBuiltInRole.Administrator);

            if (!administrativeMode)
            {
                var processPath = @"C:\Program Files (x86)\ProcessKiller\ProcessKillerExecutable\ProcessKillerConsoleApp.exe";
                var startInfo = new ProcessStartInfo(processPath);
                startInfo.Verb = "runas";
                startInfo.Domain = Environment.MachineName;
                startInfo.UserName = "EAE";
                startInfo.UseShellExecute = false;
                startInfo.FileName = Assembly.GetExecutingAssembly().CodeBase;
                startInfo.WorkingDirectory = Path.GetDirectoryName(processPath);
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;
                startInfo.ErrorDialog = true;

                var _secureString = new SecureString();
                var _password = "eae1EaE!-*";

                foreach (var s in _password)
                {
                    _secureString.AppendChar(s);
                }

                startInfo.Password = _secureString;

                try
                {
                    Process.Start(startInfo);

                    Logger.Info("Process is running under local administrator privileges.");
                }
                catch (Exception ex)
                {
                    Logger.Info("Process is not running under local administrator privileges. Exception Details : " + ex);

                    Console.ReadLine();
                    return;
                }
            }
            else
            {
                Logger.Info("Process is running under administrator privileges");
            }

            #endregion

            #region Run Application As Windows Service or Console Application

            processKillerService = new ProcessKillerService();

            if (!Environment.UserInteractive)
            {
                var servicesToRun = new ServiceBase[] { processKillerService };

                ServiceBase.Run(servicesToRun);

                return;

            }

            Console.WriteLine(@"Select one of below options");
            Console.WriteLine(@" 1. Run as console application");
            Console.WriteLine(@" 2. Run as windows service");
            Console.WriteLine(@" 3. Exit");
            Console.Write(@"Enter Option : ");

            var input = Console.ReadLine();

            switch (input)
            {
                case "1":
                    processKillerService.Start(args);
                    Console.WriteLine(@"Running as Console Application");
                    Console.ReadLine();
                    break;
                case "2":
                    break;
            }

            Console.WriteLine(@"Closing...");

            #endregion

        }

    }

    public class ProcessKillerService : ServiceBase
    {
        #region Declarations

        public static Logger Logger;

        private static List<Process> ProcessCollection { get; set; }

        private static List<string> ProcessToKillCollection;

        private static List<MrpProcess> RunningMrpProcessCollection;

        private static Timer CheckIdleTimer;

        private static object _timerLock = new object();

        #endregion

        #region Constructor

        public ProcessKillerService()
        {
            Logger = LogManager.GetLogger("ApplicationLogger");
        }

        #endregion

        #region Service Methods 

        public void Start(string[] args) { OnStart(args); }

        protected override void OnContinue()
        {
            base.OnContinue();
        }

        protected override void OnPause()
        {
            base.OnPause();
        }

        protected override void OnStart(string[] args)
        {
            CreateProcessList();

            CheckIdleTimer = new Timer(new TimerCallback(TimerCallback), null, 0, 60000);

            Logger.Info("Timer event set");

        }

        protected override void OnStop()
        {
            base.OnStop();

            //Forcing to end the current process
            ThreadPool.QueueUserWorkItem(state =>
            {
                Console.WriteLine(@"Ending Server Process...");

                while (true)
                {
                    Environment.Exit(0);

                    Thread.Sleep(1000);
                }
            });

        }

        #endregion

        #region Timer Event

        private static void TimerCallback(object sender)
        {
            lock (_timerLock)
            {
                Logger.Info("Timer event started");

                var processCollection = GetProcessCollection();

                if (processCollection.Count > 0)
                {
                    try
                    {
                        if (RunningMrpProcessCollection == null)//create mpr processes
                        {
                            RunningMrpProcessCollection = new List<MrpProcess>();
                        }

                        foreach (var process in ProcessCollection)//add mrp process to list which running at current time
                        {
                            foreach (var pn in ProcessToKillCollection)
                            {
                                if (process.ProcessName.Contains(pn) || pn.Contains(process.ProcessName))
                                {
                                    if (!IsProcessExist(process.ProcessName))
                                    {
                                        var mrpProcess = new MrpProcess();

                                        mrpProcess.Process = process;

                                        mrpProcess.Flag = false;

                                        Logger.Info("Running MRP Process Name : " + mrpProcess.Process.ProcessName);

                                        RunningMrpProcessCollection.Add(mrpProcess);//get running processes to be killed 

                                        var children = GetChildProcesses(process.Id);

                                        Logger.Info("Children Process Count : " + children.Count);

                                        if (children.Count > 0)
                                        {
                                            foreach (var cp in children)
                                            {
                                                if (!IsProcessExist(cp.ProcessName))
                                                {
                                                    var childMrpProcess = new MrpProcess();

                                                    childMrpProcess.Process = cp;

                                                    childMrpProcess.Flag = false;

                                                    Logger.Info("Running Child MRP Process Name : " + cp.ProcessName);

                                                    RunningMrpProcessCollection.Add(childMrpProcess);//get running processes to be killed
                                                }
                                            }
                                        }
                                        else
                                        {
                                            Logger.Info("No child process for " + process.ProcessName);
                                        }
                                    }
                                }
                            }
                        }

                        if (RunningMrpProcessCollection.Count > 0)
                        {
                            foreach (var mrpprocess in RunningMrpProcessCollection)
                            {
                                if (mrpprocess.TotalProcessTime == null && mrpprocess.IdleSince == null)
                                {
                                    mrpprocess.TotalProcessTime = mrpprocess.Process.TotalProcessorTime;

                                    mrpprocess.IdleSince = DateTime.Now;

                                    Logger.Info("Running MRP Process TotalProcessTime (" + mrpprocess.Process.ProcessName + ") : " + mrpprocess.TotalProcessTime);

                                    Logger.Info("Running MRP Process IdleSince (" + mrpprocess.Process.ProcessName + ") : " + mrpprocess.IdleSince);
                                }
                            }

                            foreach (var mrpprocess in RunningMrpProcessCollection)
                            {
                                if (mrpprocess.TotalProcessTime != null && mrpprocess.Process.TotalProcessorTime != null)
                                {
                                    if (mrpprocess.TotalProcessTime == mrpprocess.Process.TotalProcessorTime)
                                    {
                                        if ((int)(DateTime.Now - mrpprocess.IdleSince).TotalSeconds >= 1200)//Idle total
                                        {
                                            mrpprocess.Flag = true;
                                        }

                                        Logger.Info("(" + mrpprocess.Process.ProcessName + ") TotalProcessTime = " + mrpprocess.TotalProcessTime);

                                        Logger.Info("(" + mrpprocess.Process.ProcessName + ") DateTime.Now - mrpprocess.IdleSince = " + (DateTime.Now - mrpprocess.IdleSince).TotalSeconds);
                                    }
                                } 
                            }

                            int counter = 0;

                            foreach (var mrpprocess in RunningMrpProcessCollection)
                            {
                                if (mrpprocess.Flag)
                                {
                                    counter++;
                                }
                                else
                                {
                                    mrpprocess.TotalProcessTime = mrpprocess.Process.TotalProcessorTime;

                                    mrpprocess.IdleSince = DateTime.Now;

                                    Logger.Info("(" + mrpprocess.Process.ProcessName + ") process is not killed. Total Process Time : " + mrpprocess.TotalProcessTime);
                                }
                            }

                            if (RunningMrpProcessCollection.Count == counter)
                            {
                                Logger.Info("Starting process killing ");

                                KillProcesses();

                                Logger.Info("Processes killed.");

                                RunningMrpProcessCollection = new List<MrpProcess>();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error in timer callback. No process to kill. Ex : " + ex);
                    }
                }
            }
        }

        #endregion

        #region Process Methods

        private static List<Process> GetProcessCollection()
        {
            var allProcesses = Process.GetProcesses();

            ProcessCollection = new List<Process>();

            if (allProcesses.Length > 0)
            {
                ProcessCollection.AddRange(allProcesses);

                Logger.Info("All processes has captured.");
            }
            else
            {
                Logger.Info("No process has captured.");
            }

            return ProcessCollection;

        }

        private static Process GetProcess(string process)
        {
            var result = new Process();

            foreach (var p in ProcessCollection)
            {
                if (p.ProcessName.Contains(process) || (process).Contains(p.ProcessName))
                {
                    result = p;

                    Logger.Info("Process found. Process name : " + result.ProcessName.ToString());

                    break;

                }
            }

            return result;

        }

        private static void CreateProcessList()
        {
            ProcessToKillCollection = new List<string>() { "BOM", "INV", "mes", "MRP", "PUR", "wct", "ihracat", "efatura", "purm", "NON_IST", "sysman", "Ozel_Imalat", "sevk_dis", "stknet", "soe_tkp2" };

            Logger.Info("Processes to be killed captured.");

        }

        private static void KillProcesses()
        {
            foreach (var process in ProcessCollection)
            {
                foreach (var pn in ProcessToKillCollection)
                {
                    if (process.ProcessName.Contains(pn) || pn.Contains(process.ProcessName))
                    {
                        KillProcessAndChildren(process.Id);
                    }
                }
            }
        }

        private static void KillProcessAndChildren(int pid)
        {
            var searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid);

            ManagementObjectCollection moc = searcher.Get();

            try
            {
                foreach (ManagementObject mo in moc)
                {
                    if (mo["ProcessID"] != null)
                    {
                        Logger.Info("Management object : " + mo.ToString() + ", Process ID : " + mo["ProcessID"].ToString());

                        KillProcessAndChildren(Convert.ToInt32(mo["ProcessID"]));
                    }
                }

                var proc = Process.GetProcessById(pid);

                proc.Kill();

                Logger.Error(proc.ProcessName.ToString() + " killed.");

            }
            catch (ArgumentException ex)
            {
                Logger.Error("Error occured in KillProcessAndChildren method. Ex : " + ex);
            }
        }

        private static List<Process> GetChildProcesses(int pid)
        {
            var results = new List<Process>();

            var searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid);

            ManagementObjectCollection moc = searcher.Get();

            try
            {
                foreach (ManagementObject mo in moc)
                {
                    if (mo["ProcessID"] != null)
                    {
                        var proc = Process.GetProcessById((int)mo["ProcessID"]);

                        results.Add(proc);

                    }
                }
            }
            catch (ArgumentException ex) { }

            return results;

        }

        /*
        private static List<Process> GetChildProcesses(int pid)
        {
            var results = new List<Process>();
            
            string queryText = string.Format("select processid from win32_process where parentprocessid = {0}", pid);

            using (var searcher = new ManagementObjectSearcher(queryText))
            {
                foreach (var obj in searcher.Get())
                {
                    object data = obj.Properties["processid"].Value;

                    if (data != null)
                    {
                        var childId = Convert.ToInt32(data);
                        var childProcess = Process.GetProcessById(childId);
                        
                        if (childProcess != null)
                            results.Add(childProcess);
                    }
                }
            }
            return results;
        }
        */

        private static bool IsProcessExist(string processName)
        {
            var isExist = false;

            foreach (var mrpp in RunningMrpProcessCollection)
            {
                if (mrpp.Process.ProcessName == processName)
                {
                    isExist = true;
                    break;
                }
            }

            return isExist;

        }

        #endregion

    }

}




