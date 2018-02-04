using NLog;
using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading;
using System.Windows.Forms;

namespace ProcessKiller
{
    public static class Program
    {
        #region Declarations
        
        public static Logger Logger;

        #endregion

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Logger = LogManager.GetLogger("ApplicationLogger");

            #region Run Only One Instance (Mutex)

            bool isAppInstanceClosed;

            //create mutex object
            var mutex = new Mutex(true, "ProcessKiller", out isAppInstanceClosed);

            if (!isAppInstanceClosed)
            {
                return;
            }

            #endregion

            #region Run Application As Administrator (With Local Administrator Account)

           // var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());

            //var administrativeMode = principal.IsInRole(WindowsBuiltInRole.Administrator);

            //if (!administrativeMode)
            //{
                var processPath = @"C:\Program Files (x86)\ProcessKiller\ProcessKillerExecutable\ProcessKiller.exe";
                var startInfo = new ProcessStartInfo(processPath);
                startInfo.Verb = "runas";
                startInfo.Domain = "eaeserver"; //Environment.MachineName;
                startInfo.UserName = "mrpkiller";
                startInfo.UseShellExecute = false;
                startInfo.FileName = "ProcessKiller.exe";// Assembly.GetExecutingAssembly().CodeBase;
                startInfo.WorkingDirectory = Path.GetDirectoryName(processPath);
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;
                startInfo.ErrorDialog = true;

                var _secureString = new SecureString();
                var _password = "Mrp*eae*2018";//"eae1EaE!-*";//"Mrp*eae*2018";

                foreach (var s in _password)
                {
                    _secureString.AppendChar(s);
                }

                startInfo.Password = _secureString;

                try
                {
                    Process.Start(startInfo);

                    Logger.Info("Process started via mrpkiller process.");
                }
                catch (Exception ex)
                { 
                    Logger.Error("Error occured while process is starting. " + ex);

                    return;
                }
            //}

            #endregion

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var mainForm = new Screen
            {
                Opacity = 0.0,
                ShowInTaskbar = false,
                Visible = false
            };

            Application.Run(mainForm);

            Logger.Info("Application is running.");

        }

    }
}


