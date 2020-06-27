using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;

namespace GetDbmData
{
    public partial class Form1 : Form
    {
        public ChromiumWebBrowser chromeBrowser;
        
        private bool IsStarted = false;
        private readonly int logFrequency = 1 * 1000; // 1s by default
        
        private readonly char csvSeparatorComma = ',';
        private readonly char csvSeparatorSemiColon = ';';
        public static readonly string defaultFile = Directory.GetCurrentDirectory() + "\\csv_files";
        private readonly string filepathComma = defaultFile + "\\dbmValuesComma.csv";
        private readonly string filepathSemiColon = defaultFile + "\\dbmValuesSemiColon.csv";
        
        public Form1()
        {
            InitializeComponent();
            // Start the browser after initialize global component
            InitializeChromium();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        public void InitializeChromium()
        {
            CefSettings settings = new CefSettings();
            // Initialize cef with the provided settings
            Cef.EnableHighDPISupport();
            Cef.Initialize(settings);
            
            // Create a browser component
            chromeBrowser = new ChromiumWebBrowser("http://websdr.ewi.utwente.nl:8901/");

            // Add it to the form and fill it to the form window.
            this.Controls.Add(chromeBrowser);
            chromeBrowser.Dock = DockStyle.Fill;
            Directory.CreateDirectory(defaultFile);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Cef.Shutdown();
        }

        // START BUTTON
        private void button1_Click(object sender, EventArgs e)
        {
            IsStarted = true;
            button1.Click -= button1_Click; // disable start click untill stopped
            
            // do asynchronously
            WriteData();
        }

        // STOP BUTTON
        private void button2_Click(object sender, EventArgs e)
        {
            IsStarted = false;
            button1.Click += button1_Click; // enable start click
        }

        private async void WriteData()
        {
            string EvaluateJavaScriptResult;
            while (IsStarted)
            {
                await Task.Delay(logFrequency);
                radioButton1.Checked = !radioButton1.Checked;

                var frame = chromeBrowser.GetMainFrame();
                var task_dbmValue = frame.EvaluateScriptAsync("document.getElementById('numericalsmeter').innerHTML;", null);
                string dbmValue = "no value";
                var task_dbmPeak = frame.EvaluateScriptAsync("document.getElementById('numericalsmeterpeak').innerHTML;", null);
                string dbmPeak = "no value";
                var task_frequency = frame.EvaluateScriptAsync("document.getElementsByName('frequency')[0].value;", null);
                var frequency = "no value";

                // get dbm peak
                await task_dbmValue.ContinueWith(t =>
                {
                    if (!t.IsFaulted)
                    {
                        var response = t.Result;
                        EvaluateJavaScriptResult = response.Success ? (response.Result != null ? response.Result.ToString() : "null") : response.Message;
                        dbmValue = EvaluateJavaScriptResult.Split(';').Length > 1 ? EvaluateJavaScriptResult.Split(';')[1] : EvaluateJavaScriptResult;
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());

                // get dbm peak
                await task_dbmPeak.ContinueWith(t =>
                {
                    if (!t.IsFaulted)
                    {
                        var response = t.Result;
                        EvaluateJavaScriptResult = response.Success ? (response.Result != null ? response.Result.ToString() : "null") : response.Message;
                        dbmPeak = EvaluateJavaScriptResult.Split(';').Length > 1 ? EvaluateJavaScriptResult.Split(';')[1] : EvaluateJavaScriptResult;
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());

                // get Frequency
                await task_frequency.ContinueWith(t =>
                {
                    if (!t.IsFaulted)
                    {
                        var response = t.Result;
                        EvaluateJavaScriptResult = response.Success ? (response.Result != null ? response.Result.ToString() : "null") : response.Message;
                        frequency = EvaluateJavaScriptResult;
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());

                // write lines after all tasks are done
                await Task.WhenAll(task_dbmValue, task_dbmPeak, task_frequency).ContinueWith(t =>
                {
                    try
                    {
                        using (StreamWriter writer = new StreamWriter(new FileStream(filepathComma, FileMode.Append)))
                        {
                            writer.WriteLine(frequency + csvSeparatorComma + dbmValue + csvSeparatorComma + dbmPeak);
                        }
                        using (StreamWriter writer = new StreamWriter(new FileStream(filepathSemiColon, FileMode.Append)))
                        {
                            writer.WriteLine(frequency + csvSeparatorSemiColon + dbmValue + csvSeparatorSemiColon + dbmPeak);
                        }
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show($"[Error] - {e.Message}");
                        IsStarted = false;
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
            radioButton1.Checked = false;
        }
    }
}
