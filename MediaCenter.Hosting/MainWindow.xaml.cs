using MediaCenter.Hosting.Services;
using MediaCenter.Hosting.Windows;
using Microsoft.Owin.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MediaCenter.Hosting
{
    public partial class MainWindow : Window
    {
        private IDisposable app = null;
        public List<IPAddress> Addresses { get; set; }
        readonly IRipDVDService ripDVDService;
        readonly IConvertMediaServices coverMediaService;
        readonly IDVDTrayService dvdTrayService;
        private ProgressWindow progressWindow;
        private NameOfMovie movieName;

        public MainWindow()
        {
            InitializeComponent();

            MinimizeToTray.Enable(this);

            ripDVDService = new RipDVDService();
            coverMediaService = new ConvertMediaServices();
            dvdTrayService = new DVDTrayService();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            dvdTrayService.DiskState += DvdTrayService_DiskState;
            ripDVDService.BackupFailed += RipDVDService_BackupFailed;
            ripDVDService.Progress += RipDVDService_Progress;
            ripDVDService.TitleCopied += RipDVDService_TitleCopied;
            ripDVDService.RippingCompleted += RipDVDService_RippingCompleted;
            coverMediaService.ConvertMediaCompleted += CoverMediaService_ConvertMediaCompleted;
            coverMediaService.ConvertMediaProgress += CoverMediaService_ConvertMediaProgress;

            progressWindow = new ProgressWindow();
            progressWindow.CloseProgressWindow += ProgressWindow_CloseProgressWindow;

            Addresses = new List<IPAddress>();

            IPHostEntry IPHost = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ipAddress in IPHost.AddressList)
            {
                if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    Addresses.Add(ipAddress);
                    cbListIp.Items.Add(ipAddress.ToString());
                }
            }

            if (cbListIp.Items.Count == 1)
            {
                cbListIp.SelectedItem = cbListIp.Items[0];
            }
        }

        private void DvdTrayService_DiskState(bool isEjected)
        {
            if (isEjected == false)
            {
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(async delegate
                {
                    this.Hide();

                    // ask the name of the movie
                    movieName = new NameOfMovie();
                    movieName.DiscName = ripDVDService.GetDiscName().Replace("-", " ").Replace("_", " ");
                    movieName.ShowDialog();

                    // show the progress window
                    progressWindow.Show();

                    // rip the movie
                    await ripDVDService.RipDVD(@"C:\MediaStreamer\Content\Queue", movieName.DiscName);
                }));
            }
        }

        private void RunServer(string address)
        {
            try
            {
                if (app != null)
                {
                    app.Dispose();
                    tbIsOnline.Text = "Offline";
                }

                app = WebApp.Start<Startup>(url: "http://" + address + ":9191");
                if (app != null)
                {
                    tbIsOnline.Text = "Online";
                }
            }
            catch(Exception exp)
            {
                if (exp.InnerException != null)
                {
                    MessageBox.Show(exp.InnerException.Message);
                }
                else
                {
                    MessageBox.Show(exp.Message);
                }
                Application.Current.Shutdown();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            
            this.WindowState = System.Windows.WindowState.Minimized;
            e.Cancel = true;

            //if (app != null)
            //{
            //    app.Dispose();
            //}
        }

        private void cbListIp_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var ipAddress = (string)cbListIp.SelectedItem;
            RunServer(ipAddress);
        }

        private void CoverMediaService_ConvertMediaCompleted()
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                progressWindow.Status = "Converted all media";
                progressWindow.AddToOutput("Converted all media");

                try
                {
                    // close the progress window
                    progressWindow.Close();
                }
                catch (Exception) { }

                try
                {
                    // eject the disc
                    dvdTrayService.OpenDiscTray(ripDVDService.DriveLetter);
                }
                catch (Exception exp)
                {

                }
            }));
        }

        private void CoverMediaService_ConvertMediaProgress(int index, int Length)
        {
            progressWindow.Status = index + " out of " + Length + " completed";
            progressWindow.AddToOutput(index + " out of " + Length + " completed");
        }

        private void ProgressWindow_CloseProgressWindow()
        {
            this.Show();

            try
            {
                ripDVDService.Abort();
            }
            catch(Exception)
            {

            }

            try
            {
                foreach (var dir in Directory.GetDirectories(@"C:\MediaStreamer\Content\Queue"))
                {
                    Directory.Delete(dir, true);
                }
            }
            catch (Exception)
            {

            }
        }

        private void RipDVDService_BackupFailed()
        {
            progressWindow.Status = "Unable to backup disc";
            progressWindow.AddToOutput("Unable to backup disc. This often happens because the disc is dirty, damaged.");
            ProgressWindow_CloseProgressWindow();
            MessageBox.Show("Unable to rip disc");
        }

        private void RipDVDService_RippingCompleted()
        {
            progressWindow.Status = "Disc copied to local drive";
            progressWindow.AddToOutput("Disc copied to local drive");

            // convert all mkv to mp4
            coverMediaService.Convert(@"C:\MediaStreamer\Content\Queue\" + movieName.DiscName);
        }

        private void RipDVDService_TitleCopied(int TitleIndex)
        {
            progressWindow.Status = "Title copied: " + TitleIndex.ToString();
        }

        private void RipDVDService_Progress(string output)
        {
            progressWindow.AddToOutput(output);
        }

        private void btnCloseApplication_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
