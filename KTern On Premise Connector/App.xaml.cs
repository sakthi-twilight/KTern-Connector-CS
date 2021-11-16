using System;
using System.IO;
using System.Windows;
using WebSocketSharp;
using System.Timers;
using System.ComponentModel;
using log4net;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using System.Net;
using System.Text;

namespace KTern_On_Premise_Connector
{

    public partial class App : Application
    {
        private System.Windows.Forms.NotifyIcon _notifyIcon;
        private WebSocket WebSocket;
        private Timer ApplicationTimer;
        private bool _isExit;
        private String User;
        private String Domain;
        private static readonly ILog log = LogManager.GetLogger(typeof(App));
        private static readonly Random random = new Random();
        private WebClient client;
        private String argument;
        private String executable;
        private int timeout = 30000;
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
           
            log4net.Config.XmlConfigurator.Configure();
            log.Info(KTernUtils.StartupFunction);

            String ProcessName = Process.GetCurrentProcess().ProcessName;
            log.Info(KTernUtils.ProcessName + ProcessName);

            if (Process.GetProcesses().Count(p => p.ProcessName == ProcessName) > 1)
            {
                MessageBox.Show(KTernUtils.KTernAppIsAlreadyRunning);
                Application.Current.Shutdown();
                return;
            }

            MainWindow = new MainWindow();
            MainWindow.Closing += MainWindow_Closing;
            MainWindow.Show();
            ApplicationTimer = new Timer();
            ApplicationTimer.Interval = 8000;
            ApplicationTimer.Elapsed += new ElapsedEventHandler(OnWindowCloseEvent);
            ApplicationTimer.Enabled = true;
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            _notifyIcon.DoubleClick += (s, args) => ShowMainWindow();
            _notifyIcon.Icon = KTern_On_Premise_Connector.Properties.Resources.ktern;
            _notifyIcon.BalloonTipTitle = KTernUtils.TrayTitle;
            _notifyIcon.BalloonTipText =  KTernUtils.TrayText;
            _notifyIcon.Visible = true;
            CreateContextMenu();
            PrerequesiteForSocket();
        }

        
        private void CreateContextMenu()
        {
            log.Info(KTernUtils.CreateContextMenuFunction);
            _notifyIcon.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            _notifyIcon.ContextMenuStrip.Items.Add(KTernUtils.SBRString).Click += (s, e) => ShowMainWindow();
            _notifyIcon.ContextMenuStrip.Items.Add(KTernUtils.ShowString).Click += (s, e) => ShowMainWindow();
            _notifyIcon.ContextMenuStrip.Items.Add(KTernUtils.ExitString).Click += (s, e) => ExitApplication();
        }

        private void PrerequesiteForSocket()
        {
            log.Info(KTernUtils.PrerequesiteSocketFunction);
            User = GetUser();
            Domain = GetDomain();
            InitateSocketConnectionToGCP();

        }

        private async void InitateSocketConnectionToGCP()
        {
            log.Info(KTernUtils.InitateSocketFunction);
            
            try
            {
                await Task.Run(() =>
                {
                    client = new WebClient();

                    WebSocket = new WebSocket(Domain);
                    WebSocket.SetCookie(new WebSocketSharp.Net.Cookie("user", User));
                    WebSocket.OnMessage += (sender, e) =>
                    {
                        log.Info(KTernUtils.OnMessageEvent);
                        String RequestString = e.Data;
                        dynamic ReqStringObj = JsonConvert.DeserializeObject(RequestString);

                        int target = ReqStringObj["target"];
                        //target can by 0 or 1 
                        //If the target is 0 -> pyRFC
                        //If the target is 1 -> Ktern_browsers 

                        if(target == 0)
                        {
                            if ( ReqStringObj["emit"] == KTernUtils.AddSystem) {
                                ReqStringObj["password"] = KTernUtils.PasswordHash;
                                log.Info(ReqStringObj.ToString());
                            }
                            else
                            {
                                log.Info(RequestString);
                            }

                            String FileName = GetGuid();
                            String FilePath = KTernUtils.JsonPath + FileName;
                            //RequestString = KTernCipher.Encrypt(RequestString); //Commenting Encryption
                            File.WriteAllText(FilePath, RequestString);
                            argument = FileName;
                            executable = "libs\\ktern\\ktern.exe";

                        }
                        else if(target == 1)
                        {
                            log.Info(RequestString);
                            String FilePath = ReqStringObj["file_path"];
                            String Browser = ReqStringObj["browser"];
                            String FileName = GetGuid();
                            String LocalPath = "./libs/ktern/files/" + FileName;
                            client.DownloadFile(FilePath, LocalPath);
                            argument = "--file " + LocalPath + " --browser " + Browser + " --user "+ User;
                            executable = "libs\\ktern\\ktern_browser.exe";
                            log.Info(argument);
                        }

                        using (Process process = new Process())
                        {
                            process.StartInfo.RedirectStandardOutput = true;
                            process.StartInfo.UseShellExecute = false;
                            process.StartInfo.CreateNoWindow = true;
                            process.StartInfo.RedirectStandardError = true;
                            process.StartInfo.FileName = executable;
                            process.StartInfo.Arguments = argument;
                            process.Start();
                            String output = process.StandardOutput.ReadToEnd();
                            String err = process.StandardError.ReadToEnd();
                            log.Info(output);
                            log.Error(err);
                            process.WaitForExit();
                            WebSocket.Send(output);
                        }

                    };

                    WebSocket.OnError += (sender, e) =>
                    {

                        log.Error(KTernUtils.OnErrorEvent);
                        log.Error(e.Message);
                        log.Error(e.Exception.ToString());
                    };

                    WebSocket.OnOpen += (sender, e) =>
                    {
                        log.Info(KTernUtils.OnOpenEvent);
                        log.Info(e.ToString());

                    };

                    WebSocket.OnClose += (sender, e) =>
                    {

                        log.Info(KTernUtils.OnCloseEvent);
                        log.Info(e.Reason);
                        log.Info(e.ToString());
                        log.Info(KTernUtils.ReinitiateSocketConnection);
                        InitateSocketConnectionToGCP();
                    };

                    WebSocket.Connect();

                });
            }
            catch (Exception ex)
            {
                log.Error(KTernUtils.ExceptionInSocketGCP);
                log.Error(ex.ToString());

            }
        }

        private string GetUser()
        {
            log.Info(KTernUtils.GetUserFunction);
            log.Info("User File " + KTernUtils.UserFile);

            String[] user = new String[0];
            if (File.Exists(KTernUtils.UserFile))
            {
                user = File.ReadAllLines(KTernUtils.UserFile);
                log.Info("User ID " + String.Join(" ", user));
            }
            return String.Join(" ", user);
        }

        private string GetDomain()
        {
            log.Info(KTernUtils.GetDomainFunction);
            log.Info("Domain File " + KTernUtils.DomainFile);

            String[] domain = new String[0];
            if (File.Exists(KTernUtils.DomainFile))
            {
                domain = File.ReadAllLines(KTernUtils.DomainFile);
                log.Info("Domain Name " + String.Join(" ", domain));
            }
            return String.Join(" ", domain);
        }



        private void OnWindowCloseEvent(object source, ElapsedEventArgs e)
        {
            log.Info(KTernUtils.OnWindowCloseEvent);
            this.Dispatcher.Invoke(() =>
            {
                MainWindow.Close();
                _notifyIcon.ShowBalloonTip(KTernUtils.BallonTimeout);
                ApplicationTimer.Stop();

            });
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (!_isExit)
            {
                log.Info(KTernUtils.MainWindowClosingFunction);
                e.Cancel = true;
                MainWindow.Hide();
            }
        }

        private void ShowMainWindow()
        {
            log.Info(KTernUtils.ShowMainWindowFunction);
            if (MainWindow.IsVisible)
            {
                if (MainWindow.WindowState == WindowState.Minimized)
                {
                    MainWindow.WindowState = WindowState.Normal;
                }
                MainWindow.Activate();
            }
            else
            {
                MainWindow.Show();
            }
        }


        private void ExitApplication()
        {
            log.Info(KTernUtils.ExitApplicationFunction);
            _isExit = true;
            MainWindow.Close();
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
        

        public static String GetGuid()
        {
            Guid g = Guid.NewGuid();
            return g.ToString();
        }
    }
}
