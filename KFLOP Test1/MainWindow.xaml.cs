using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
// for file dialog libraries
using Microsoft.Win32;
using System.IO;
// for the Dispatch Timer
using System.Windows.Threading;

// for KMotion libraries
using KMotion_dotNet;

// for XML save setup etc
using System.Xml;
using System.Xml.Serialization;
// for the JSON stuff
using Newtonsoft.Json;



namespace KFLOP_Test1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // global variables in the MainWindow class
        static bool ExecutionInProgress = false;
        static KMotion_dotNet.KM_Controller KM; // this is the controller instance!
        static MotionParams_Copy Xparam;
        static ConfigFiles CFiles;
        // these two globals are used in the status timer 
        static bool Connected = false;
        static int skip = 0;
        static int DisCount = 0;
        string GCodeFileName;



        public MainWindow()
        {
            InitializeComponent();

            // create an instance of the KM controller - same instance is used in the entire app
            // ******** VERY IMPORTANT *********
            // inorder to make this work properly 
            // the following files needed to be copied to the Debug1/Release1 directories
            // -- and the build output directories need to be changed to Debug1/Release1 - or at least something besides Debug/Release
            // KMotionDLL.dll
            // KMotion_dotNet.dll
            // KMotion_dotNet_Interop.dll
            // GCodeInterpreter.dll
            // KMotionServer.exe
            // TCC67.exe(This is the compiler for their DSP C code)
            // emc.var
            // \DSP_KFLOP Sub directory.
            //
            // also copy the DSP_KFLOP folder into Debug1/Release1
            // reference this wiki page https://www.dynomotion.com/wiki/index.php?title=PC_Example_Applications
            try
            {
                KM = new KMotion_dotNet.KM_Controller();
            }
            catch(Exception e)
            {
                MessageBox.Show("Unable to load KMotion_dotNet Libraries.  Check Windows PATH or .exe location " + e.Message);
                System.Windows.Application.Current.Shutdown();  // and shut down the application...
                return;
            }

            // copy of the motion parameters that the JSON reader can use.
            Xparam = new MotionParams_Copy();
            // get the configuration file names
            CFiles = new ConfigFiles();
            // check if the the config file exists
            if(File.Exists("KTestConfig.json") == true)
            {
                JsonSerializer Jser = new JsonSerializer();
                StreamReader sr = new StreamReader("KTestConfig.json");
                JsonReader Jreader = new JsonTextReader(sr);
                CFiles = Jser.Deserialize<ConfigFiles>(Jreader);
                sr.Close();
            } else
            {
                MessageBox.Show("No configureation file found");
                // what to do here? 
                // Initialize the strings to null and save the file for next time
                SaveConfig(CFiles);
            }
            if(File.Exists(CFiles.MotionParams) == true)
            {
                JsonSerializer Jser = new JsonSerializer();
                StreamReader sr = new StreamReader(CFiles.MotionParams);
                JsonReader Jreader = new JsonTextReader(sr);
                Xparam = Jser.Deserialize<MotionParams_Copy>(Jreader);
                sr.Close();
                Xparam.CopyParams(KM.CoordMotion.MotionParams); // copy the motion parameters to the KM instance
            }

            // add the callbacks
            AddHandlers();

            // start a timer for the status update
            var Timer = new DispatcherTimer();
            Timer.Interval = TimeSpan.FromMilliseconds(100);    // timer tick every 100 ms (1/10 sec)
            Timer.Tick += dispatchTimer_Tick;
            Timer.Start();
        }

        #region Status Timer Tick
        // Update Status Timer
        // currently set for 100 ms 
        // event handler for the Dispatch timer
        private void dispatchTimer_Tick(object sender, EventArgs e)
        {
            int[] BoardList;
            int nBoards = 0;
            // several sections
            // if the board is connected then 
            // then check certain things every cycle
            // check some things every second 
            if (Connected)
            {

                if (++skip == 10)    // These actions happen every second
                {
                    skip = 0;
                    // update the elapsed time 
                    tbExTime.Text = KM.CoordMotion.TimeExecuted.ToString();
                }
                else // these actions happen every cycle
                {
                    if (KM.WaitToken(100) == KMOTION_TOKEN.KMOTION_LOCKED) // KMOTION_LOCKED means the board is available
                    {
                        try
                        {
                            KM_MainStatus MainStatus = KM.GetStatus(false); // passing false does not lock to board while generating status
                            KM.ReleaseToken();

                            // Set DRO Colors
                            if ((MainStatus.Enables & 1) != 0)
                            {
                                DROX.Foreground = Brushes.Green;
                            }
                            else
                            {
                                DROX.Foreground = Brushes.Red;
                            }
                            if ((MainStatus.Enables & 2) != 0)
                            {
                                DROY.Foreground = Brushes.Green;
                            }
                            else
                            {
                                DROY.Foreground = Brushes.Red;
                            }
                            if ((MainStatus.Enables & 4) != 0)
                            {
                                DROZ.Foreground = Brushes.Green;
                            }
                            else
                            {
                                DROZ.Foreground = Brushes.Red;
                            }

                            // Get Ablosule Machine Coordinates
                            double x = 0, y = 0, z = 0, a = 0, b = 0, c = 0;
                            KM.CoordMotion.UpdateCurrentPositionsABS(ref x, ref y, ref z, ref a, ref b, ref c, false);
                            DROX.Text = String.Format("{0:F4}", x);
                            DROY.Text = String.Format("{0:F4}", y);
                            DROZ.Text = String.Format("{0:F4}", z);

                        }
                        catch (DMException)  // in case disconnect in the middle of reading status
                        {
                            KM.ReleaseToken();  // make sure the token is released
                        }
                    }
                    else
                    {
                        Connected = false;
                    }
                    // Manage the Cycle Start button
                    btnCycleStart.IsEnabled = !ExecutionInProgress;
                }
            }
            else
            {
                if (++skip == 10)    // These actions happen every second - when not connected
                {
                    skip = 0;
                    DisCount++;

                    // check how many boards are connected
                    BoardList = KM.GetBoards(out nBoards);
                    if (nBoards > 0)
                    {
                        Connected = true;
                        Title = String.Format("C# WPF App - Connected = USB location {0:X} count = {1}", BoardList[0], DisCount);
                    }
                    else
                    {
                        Connected = false;
                        Title = String.Format("C# WPF App - KFLOP Disconnected count = {0}", DisCount);
                    }
                }

            }
        }
        #endregion

        #region Callback Handlers

        // console update
        int Console_Msg_Update(string msg)
        {

            // Paragraph par = new Paragraph();
            //par.Inlines.Add(new Run(msg));
            // fdConsole.Blocks.Add(new Paragraph(new Run(msg + "blocks:" + fdConsole.Blocks.Count.ToString())));
            // fdConsole.Blocks.Add(new Paragraph(new Run(msg)));
            Dispatcher.BeginInvoke(new System.Threading.ThreadStart(() => Console_Update2(msg)));
            return 0;
                
        }

        public void Console_Update2(string msg)
        {
            fdConsole.Blocks.Add(new Paragraph(new Run(msg)));
        }

        static void KM_ErrorUpdated(string msg)
        {
            MessageBox.Show(msg);
        }

        //static 
        public void Interpreter_InterpreterCompleted(int status, int lineno, int sequence_number, string err)
        {
            Dispatcher.BeginInvoke(new System.Threading.ThreadStart(() => InterpCompleted2(status, lineno, sequence_number, err)));

        }
        public void InterpCompleted2(int status, int lineno, int seq, string err)
        {
            tbStatus.Text = status.ToString();
            tbLineNo.Text = lineno.ToString();
            tbSeq.Text = seq.ToString();
            tbErr.Text = err;

            if ((status != 0) && status != 1005)
            {
                MessageBox.Show(err); // status 1005 = successful halt
            }
            ExecutionInProgress = false; // not running anymore.
        }

        void InterpStatus(int lineno, string msg)
        {
            Dispatcher.BeginInvoke(new System.Threading.ThreadStart(() => InterpStatus2(lineno, msg)));
        }
        private void InterpStatus2(int lineno, string msg)
        {
            tbLineNo.Text = lineno.ToString();
            tbErr.Text = msg;
        }

        private void AddHandlers()
        {
            // set the callback for various functions
            KM.MessageReceived += new KMotion_dotNet.KMConsoleHandler(Console_Msg_Update);

            // Callback for Errors
            KM.ErrorReceived += new KMotion_dotNet.KMErrorHandler(KM_ErrorUpdated);

            // Coordinated Motion Callback - Gcode file completed
            KM.CoordMotion.Interpreter.InterpreterCompleted += new KMotion_dotNet.KM_Interpreter.KM_GCodeInterpreterCompleteHandler(Interpreter_InterpreterCompleted);
            KM.CoordMotion.Interpreter.InterpreterStatusUpdated += new KMotion_dotNet.KM_Interpreter.KM_GCodeInterpreterStatusHandler(InterpStatus);

            // Other interpreter callbacks.
           // KM.CoordMotion.Interpreter.
        }

        #endregion

        #region // Interpreter setup functions

        private void Set_Fixture_Offset(int Fixture_Number, double X, double Y, double Z)
        {
            // Set GVars for Offsets
            KM.CoordMotion.Interpreter.SetOrigin(Fixture_Number,
                KM.CoordMotion.Interpreter.InchesToUserUnits(X),
                KM.CoordMotion.Interpreter.InchesToUserUnits(Y),
                KM.CoordMotion.Interpreter.InchesToUserUnits(Z), 0, 0, 0);

//            KM.CoordMotion.Interpreter.SetupParams.OriginIndex = -1; // Force update from GCode Vars
//            KM.CoordMotion.Interpreter.ChangeFixtureNumber(Fixture_Number); // Load offset for fixture

        }
        #endregion

        private void BtnCProgram_Click(object sender, RoutedEventArgs e)
        {
            // open a windows dialog to read in the cprogram
            var openFileDlg = new OpenFileDialog();
            openFileDlg.FileName = CFiles.KThread1;
            if(openFileDlg.ShowDialog() == true)
            {
                CFiles.KThread1 = openFileDlg.FileName; // save the filename for next time
                try
                {
                    KM.ExecuteProgram(1, openFileDlg.FileName, true);
                }
                catch(DMException ex)
                {
                    MessageBox.Show("Unable to execute C Program in KFLOP\r\r" + ex.InnerException.Message);
                }
            }
        }

        private void BtnGetBoard_Click(object sender, RoutedEventArgs e)
        {
            // get the status of the KFLOP board
            int[] boardlist;
            int nBoards = 0;

            boardlist = KM.GetBoards(out nBoards);
            if(nBoards > 0)
            {
                Title = String.Format("C# WPF App - Connected = USB location {0:X}", boardlist[0]);
            }
            else
            {
                Title = "C# WPF App - KFLOP Disconnected";
            }

        }

        private void Window_Closed(object sender, EventArgs e)
        {
            KM.Disconnect();    // make sure KM is disposed
            SaveConfig(CFiles);
        }

        private void btnSaveJ_Click(object sender, RoutedEventArgs e)
        {
            SetupWindow St2 = new SetupWindow(KM.CoordMotion.MotionParams);
            St2.Show();
        }

        private void btnOpenJ_Click(object sender, RoutedEventArgs e)
        {
            var openFile = new OpenFileDialog();
            openFile.FileName = CFiles.MotionParams;
            if (openFile.ShowDialog() == true)
            {
                JsonSerializer Jser = new JsonSerializer();
                StreamReader sr = new StreamReader(openFile.FileName);
                JsonReader Jreader = new JsonTextReader(sr);
                Xparam = Jser.Deserialize<MotionParams_Copy>(Jreader);
                sr.Close();
                CFiles.MotionParams = openFile.FileName;

                Xparam.CopyParams(KM.CoordMotion.MotionParams); // copy the motion parameters to the KM instance
                SetupWindow st1 = new SetupWindow(KM.CoordMotion.MotionParams);
                st1.Show();
            }
        }

        // copy a config file to the default config file
        private void btnConfig_Click(object sender, RoutedEventArgs e)
        {
            var openFile = new OpenFileDialog();
            if(openFile.ShowDialog() == true)
            {
                JsonSerializer Jser = new JsonSerializer();
                StreamReader sr = new StreamReader(openFile.FileName);
                JsonReader Jreader = new JsonTextReader(sr);
                CFiles = Jser.Deserialize<ConfigFiles>(Jreader);
                sr.Close();
                SaveConfig(CFiles);
            }
        }

        //  Save the configuration file
        private void SaveConfig(ConfigFiles cf)
        {
            cf.fPath = System.AppDomain.CurrentDomain.BaseDirectory;
            string combinedConfigFile = System.IO.Path.Combine(cf.fPath, "KTestConfig.json");

            JsonSerializer Jser = new JsonSerializer();
            StreamWriter sw = new StreamWriter(combinedConfigFile);
            JsonTextWriter Jwrite = new JsonTextWriter(sw);
            Jser.NullValueHandling = NullValueHandling.Ignore;
            Jser.Formatting = Newtonsoft.Json.Formatting.Indented;
            Jser.Serialize(Jwrite, cf);
            sw.Close();
        }

        private void btnGCode_Click(object sender, RoutedEventArgs e)
        {
            // open a GCode file
            var GFile = new OpenFileDialog();
            GFile.DefaultExt = ".ngc";
            GFile.Filter = "ngc Files (*.ngc)|*.ngc|Text Files (*.txt)|*.txt|Tap Files (*.tap)|*.tap|GCode (*.gcode)|*.gcode|All Files (*.*)|*.*";
            if (GFile.ShowDialog() == true)
            {
                tbGCodeFile.Text = System.IO.Path.GetFileName(GFile.FileName);
                GCodeFileName = GFile.FileName;
            }

        }

        private void btnCycleStart_Click(object sender, RoutedEventArgs e)
        {
            if (ExecutionInProgress == true)
            {

                return;    // if already running then ignore
            }
            else
            {
                // see if it should be simulated
                if(cbSimulate.IsChecked == true)
                {
                    KM.CoordMotion.IsSimulation = true;
                } else
                {
                    KM.CoordMotion.IsSimulation = false;    // don't forget clear when not checked!
                }
                ExecutionInProgress = true;
                KM.CoordMotion.Abort();     // make sure that everything is cleared
                KM.CoordMotion.ClearAbort();
                try
                {
                    // note here - because I'm not running in the standard directory structure I needed to specify the VarsFile
                    // https://www.dynomotion.com/forum/viewtopic.php?f=12&t=1252&p=3638#p3638
                    //
                    KM.CoordMotion.Interpreter.VarsFile = CFiles.EMCVarsFile;
                    KM.CoordMotion.Interpreter.InitializeInterpreter();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"File not found '{ex}'");
                }
                Set_Fixture_Offset(2, 2, 3, 0); // Set X, Y, Z for G55
                KM.CoordMotion.Interpreter.Interpret(GCodeFileName);  // Execute the File!
            }
        }

        private void btnFeedHold_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (KM.WriteLineReadLine("GetStopState") == "0")
                {
                    KM.Feedhold();
                    btnFeedHold.Content = "Resume";
                }
                else
                {
                    KM.ResumeFeedhold();
                    btnFeedHold.Content = "Feed Hold";
                }
            }
            catch(Exception)
            {
                KM.CoordMotion.Interpreter.Halt();
            }
        }

        private void btnHalt_Click(object sender, RoutedEventArgs e)
        {
            KM.CoordMotion.Interpreter.Halt();
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            fdConsole.Blocks.Clear();
        }

        private void btnProgHalt_Click(object sender, RoutedEventArgs e)
        {
            KM.KillProgramThreads(1);
        }
    }
}
