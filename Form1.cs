using System;
using System.IO;
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
        private int logFrequency = 1 * 1000; // 1s by default
        private int versionNumber = 0;
        private int maxLines = 1000 * 1000;

        private Uri uri = new Uri("http://websdr.ewi.utwente.nl:8901/"); // utwente university url by default
        private Uri uriTemp;
        
        private static readonly string firstLineHeader = "\"DateStamp\"; \"TimeStamp\"; \"Frequency\"; \"dbmValue\"; \"dbmPeak\"";
        private static readonly string pathSeparator = "\\";
        private static readonly string dateFormat = "dd-MM-yyy";
        private static readonly string csvSeparatorSemiColon = "; ";
        public static readonly string defaultFolderPath = Directory.GetCurrentDirectory() + pathSeparator + "csv_files" + pathSeparator + DateTime.Today.ToString(dateFormat);

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
            chromeBrowser = new ChromiumWebBrowser(uri.ToString());

            // Add it to the form and fill it to the form window.
            this.Controls.Add(chromeBrowser);
            chromeBrowser.Dock = DockStyle.Fill;
            try
            {
                // create folder if not exists
                Directory.CreateDirectory(defaultFolderPath);
            }
            catch (Exception e)
            {
                ErrorHandle($"[Error creating folder] - {e.Message}");
            }

            try
            {
                // set time lapse log frequency value to display
                textBox1.Text = logFrequency.ToString();

                // set url value to display
                textBox2.Text = uri.ToString();
            }
            catch { }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Cef.Shutdown();
            chromeBrowser.Dispose();
        }

        // START BUTTON
        private void button1_Click(object sender, EventArgs e)
        {
            IsStarted = true;
            
            // disable clicks until stopped
            button1.Click -= button1_Click;
            button3.Click -= button3_Click;

            // hide button until stopped
            button1.Hide();
            button3.Hide();

            // do asynchronously
            WriteData();
        }

        // STOP BUTTON
        private void button2_Click(object sender, EventArgs e)
        {
            IsStarted = false;
            
            // enable click
            button1.Click += button1_Click;
            button3.Click += button3_Click;
            
            // show button
            button1.Show();
            button3.Show();
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

                // get dbm value
                await task_dbmValue.ContinueWith(t =>
                {
                    if (!t.IsFaulted)
                    {
                        var response = t.Result;
                        EvaluateJavaScriptResult = response.Success ? (response.Result != null ? response.Result.ToString() : "null") : response.Message;
                        var splittedResult = EvaluateJavaScriptResult.Split(';');
                        dbmValue = splittedResult.Length > 1 ? splittedResult[splittedResult.Length-1] : EvaluateJavaScriptResult;
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());

                // get dbm peak
                await task_dbmPeak.ContinueWith(t =>
                {
                    if (!t.IsFaulted)
                    {
                        var response = t.Result;
                        EvaluateJavaScriptResult = response.Success ? (response.Result != null ? response.Result.ToString() : "null") : response.Message;
                        var splittedResult = EvaluateJavaScriptResult.Split(';');
                        dbmPeak = splittedResult.Length > 1 ? splittedResult[splittedResult.Length-1] : EvaluateJavaScriptResult;
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
                        if (!Directory.Exists(defaultFolderPath)) Directory.CreateDirectory(defaultFolderPath);
                        if (!File.Exists(GetFilePath)) File.Create(GetFilePath);

                        var lineCount = File.ReadAllLines(GetFilePath).Length;

                        // Increase version number of the file in question.
                        while (lineCount > maxLines)
                        {
                            versionNumber++;
                            if (!File.Exists(GetFilePath)) File.Create(GetFilePath);
                            lineCount = File.ReadAllLines(GetFilePath).Length;
                        }

                        using (StreamWriter writer = new StreamWriter(new FileStream(GetFilePath, FileMode.Append)))
                        {
                            if (lineCount == 0)
                            {
                                writer.WriteLine(firstLineHeader);
                            }
                            var splitDateTime = DateTime.Now.ToString(dateFormat + " HH:mm:ss.fff").Split(' ');
                            writer.WriteLine($"\"{splitDateTime[0]}\"{csvSeparatorSemiColon}\"{splitDateTime[1]}\"{csvSeparatorSemiColon}{frequency}{csvSeparatorSemiColon}{dbmValue}{csvSeparatorSemiColon}{dbmPeak}");
                        }

                        // reset version number to 0
                        versionNumber = 0;
                    }
                    catch (Exception e)
                    {
                        ErrorHandle($"[Error writing to file] - {e.Message}");
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
            radioButton1.Checked = false;
        }

        // change log frequency
        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            var tb = (TextBox)sender;
            if (!int.TryParse(tb.Text, out logFrequency))
            {
                ErrorHandle("Time lapse must be a number");
            }

            if (logFrequency < 100)
            {
                logFrequency = 100;
            }
        }

        // change url
        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            var tb = (TextBox)sender;
            if(!Uri.TryCreate(tb.Text, UriKind.RelativeOrAbsolute, out uriTemp)) // getvalue from text input
            {
                ErrorHandle("Url format not recognized");
            }
        }

        // load url
        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                uri = uriTemp;
                chromeBrowser.Load(uri.ToString());
            }
            catch (Exception exception)
            {
                ErrorHandle($"[Error Loading Url] - {exception.Message}");
            }
        }

        private string GetFilePath => $"{defaultFolderPath}{pathSeparator}{uri.Host}_{DateTime.Today.ToString(dateFormat)}_{versionNumber}.csv";

        private void ErrorHandle(string message)
        {
            MessageBox.Show(message);
            IsStarted = false;

            // enable click
            button1.Click += button1_Click;
            button3.Click += button3_Click;

            // show button
            button1.Show();
            button3.Show();
        }
    }
}
