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
// for the Dispatch Timer
using System.Windows.Threading;

// for KMotion libraries
using KMotion_dotNet;


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
        // these two globals are used in the status timer 
        static bool Connected = false;
        static int skip = 0;
        static int DisCount = 0;


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

            // add the callbacks
            AddHandlers();

            // start a timer for the status update
            var Timer = new DispatcherTimer();
            Timer.Interval = TimeSpan.FromMilliseconds(100);    // timer tick every 100 ms (1/10 sec)
            Timer.Tick += dispatchTimer_Tick;
            Timer.Start();

        }

        // Update Status Timer
        // event handler for the Dispatch timer
        private void dispatchTimer_Tick(object sender, EventArgs e)
        {
            int[] BoardList;
            int nBoards = 0;
            // check the board status every timer tick if it is connected 
            if((!Connected) && (++skip == 10))
            {
                skip = 0;
                DisCount++;

                // check how many boards are connected
                BoardList = KM.GetBoards(out nBoards);
                if(nBoards > 0)
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
            if (Connected && KM.WaitToken(100) == KMOTION_TOKEN.KMOTION_LOCKED) // KMOTION_LOCKED means the board is available
            {
                try
                {
                    KM_MainStatus MainStatus = KM.GetStatus(false); // passing false does not lock to board while generating status
                    KM.ReleaseToken();

                }
                catch(DMException)  // in case disconnect in the middle of reading status
                {
                    KM.ReleaseToken();  // make sure the token is released
                }
            }
            else
            {
                Connected = false;
            }
            // enable the Run button etc.
        }

        static void KM_ErrorUpdated(string msg)
        {
            MessageBox.Show(msg);
        }

        private void AddHandlers()
        {
            // set the callback for various functions

            // Callback for Errors
            KM.ErrorReceived += new KMotion_dotNet.KMErrorHandler(KM_ErrorUpdated);
        }

        private void BtnCProgram_Click(object sender, RoutedEventArgs e)
        {
            // open a windows dialog to read in the cprogram
            var openFileDlg = new OpenFileDialog();
            if(openFileDlg.ShowDialog() == true)
            {
                try
                {
                    KM.ExecuteProgram(1, openFileDlg.FileName, false);
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
        }
    }
}
