using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Eto.Drawing;
using Eto.Forms;
using Newtonsoft.Json;
using Application = Eto.Forms.Application;

namespace OpenRCT2_Launcher_new
{
    class Launcher : Form
    {
        private static List<string> _branches = new List<string>();
        private static bool _internetConnected = true;
        private static Settings _settings = new Settings();
        private static readonly string _settingsPath =
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/OpenRCT2Launcher/settings.json";
        private static WebClient _wc = new WebClient();
        private static DropDown _branch_dropdown;
        
        private Launcher()
        {
            Title = "OpenRCT2 Launcher";
            ClientSize = new Size(300, 300);
            Content = GetUi();
            //Post GUI Setup Here

        }
        static void Main(string[] args)
        {
            if (File.Exists(_settingsPath))
            {
                _settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(_settingsPath));
            }
            
            
            _wc.Headers["User-Agent"] = "Mozilla/5.0 (X11; Linux x86_64; rv:89.0) Gecko/20100101 Firefox/89.0";
            //GitHub demands we have a browser to do use this API, so I have a browser now! :D
            //Pre GUI Setup Here
            try
            {
                Ping ping = new Ping();
                var reply = ping.Send("github.com");
                Console.WriteLine(reply.Status);
                if (reply.Status == IPStatus.Success)
                {
                    JsonBranches[] output = JsonConvert.DeserializeObject<JsonBranches[]>(
                        _wc.DownloadString("https://api.github.com/repos/OpenRCT2/OpenRCT2/branches"));
                    foreach (var b in output)
                    {
                        _branches.Add(b.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Internet not found");
                _internetConnected = false;
                _branches.Add("Offline");
                Console.WriteLine(ex.Message);
            }
            
            new Application().Run(new Launcher());
        }

        private static void DownloadAndUnpackBuild(string branch)
        {
            string buildFileName;
            string OS;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                buildFileName = "OpenRCT2-AppImage.zip";
                OS = "Linux";
            }
            else if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)){
                if (Environment.Is64BitOperatingSystem == true)
                { 
                    buildFileName = "OpenRCT2-Windows-x64.zip";
                }
                else
                { 
                    buildFileName = "OpenRCT2-Windows-win32.zip";
                } 
                OS = "Windows";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                buildFileName = "OpenRCT2-macOS-cmake.zip";
                OS = "MacOS";
            }
            else
            {

                buildFileName = "OpenRCT2-Windows-win32.zip";
                //Honestly this should work *most* often.
                //This should only ever be run in cases where the OS is completely foreign, so...
                OS = "Unknown";
            }
            Console.WriteLine("Determined OS is " + OS);
            //https://nightly.link/OpenRCT2/OpenRCT2/workflows/ci/new-save-format/OpenRCT2-Windows-x64.zip
            string url = "https://nightly.link/OpenRCT2/OpenRCT2/workflows/ci/" + branch  + "/"+ buildFileName;
            string tmpFileName = Path.GetTempFileName();
            string tmpDirectory = Path.GetTempPath() + "/OpenRCT2/";
            if (!Directory.Exists(tmpDirectory))
                Directory.CreateDirectory(tmpDirectory);
            try
            {
                _wc.DownloadFile(url, tmpFileName);
            }
            catch
            {
                Console.WriteLine("That branch is too old and the builds have been deleted due to Age. Please pick a different branch.");
                return;
            }

            if (!Directory.Exists(Path.GetDirectoryName(_settings.install_path)))
            {
                Directory.CreateDirectory(_settings.install_path);
            }
            else
            {
                Directory.Delete(_settings.install_path, true);
                Directory.CreateDirectory(_settings.install_path);
            }
            
            if (OS == "Windows")
            {
                ZipFile.ExtractToDirectory(tmpFileName, _settings.install_path);
                foreach (var file in Directory.GetFiles(_settings.install_path))
                {
                    if (file.Contains(".exe"))
                    {
                        _settings.executable = file;
                    }
                }
            }

            if (OS == "Linux")
            {
                ZipFile.ExtractToDirectory(tmpFileName, _settings.install_path);
                foreach(var file in Directory.GetFiles(_settings.install_path)){
                    if (file.Contains(".AppImage"))
                    {
                        Process.Start("/usr/bin/chmod", "+x " + file);
                        _settings.executable = file;
                    }
                }
            }

            if (OS == "MacOS")
            {
                //OpenRCT2.app/Contents/MacOS/OpenRCT2
                if (_settings.install_path == Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +
                    "/OpenRCT2/bin/")
                {
                    _settings.install_path = "/Users/" + Environment.UserName + "/Applications/";
                }
                ZipFile.ExtractToDirectory(tmpFileName, tmpDirectory);
                ZipFile.ExtractToDirectory(tmpDirectory + "/openrct2-macos.zip", tmpDirectory);
                File.Delete(tmpFileName);
                File.Delete(tmpDirectory + "/openrct2-macs.zip");
                Directory.Move(tmpDirectory + "/OpenRCT2.app", _settings.install_path);
                _settings.executable = _settings.install_path + "/OpenRCT2.app";
            }

            SaveSettings();
            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = _settings.executable;
            info.WorkingDirectory = _settings.install_path;
            Process.Start(info);
            //Environment.Exit(0);
        }



        private Control GetUi()
        {
            Panel panel = new Panel();
            {
                StackLayout layout = new StackLayout();
                {
                    StackLayout logolayout = new StackLayout();
                    layout.Items.Add(logolayout);
                    logolayout.Orientation = Orientation.Horizontal;
                    {
                        ImageView logo = new ImageView();
                        logo.Image = new Bitmap("./Resources/logo.png");
                        ImageView text = new ImageView();
                        text.Image = new Bitmap("./Resources/logo_text.png");
                        logolayout.Items.Add(logo);
                        logolayout.Items.Add(text);
                    }
                }
                {
                    _branch_dropdown = new DropDown();
                    foreach (var item in _branches)
                    {
                        //This was made global for access elsewhere, see top of file
                        _branch_dropdown.Items.Add(item);
                        if (item == _settings.branch_name)
                        {
                            _branch_dropdown.SelectedIndex = _branch_dropdown.Items.Count - 1;
                        }
                    }
                    layout.Items.Add(_branch_dropdown);
                    _branch_dropdown.SelectedIndexChanged += Branch_dropdownOnSelectedIndexChanged; 
                }
                {
                    StackLayout buttons = new StackLayout();
                    buttons.Orientation = Orientation.Horizontal;
                    {
                        Button launch = new Button();
                        launch.Text = "Launch";
                        launch.Click += LaunchOnClick;
                        buttons.Items.Add(launch);
                        
                    }
                    {
                        Button options = new Button();
                        options.Text = "Options";
                        buttons.Items.Add(options);
                    }
                    layout.Items.Add(buttons);

                }
                panel.Content = layout;
            }
           
            
            
            //Everything ends up on the panel eventually.

            
            
            
            return panel;
        }

        private void LaunchOnClick(object? sender, EventArgs e)
        {
            DownloadAndUnpackBuild(_settings.branch_name);
        }

        private void Branch_dropdownOnSelectedIndexChanged(object? sender, EventArgs e)
        {
            //MessageBox.Show(_branch_dropdown.SelectedIndex.ToString());
            _settings.branch_name = _branch_dropdown.Items[_branch_dropdown.SelectedIndex].Text;
            SaveSettings();
        }

        private static void SaveSettings()
        {
            if(!Directory.Exists(Path.GetDirectoryName(_settingsPath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath));
            }
            File.WriteAllText(_settingsPath, JsonConvert.SerializeObject(_settings, Formatting.Indented));
        }
    }
    

    public class JsonBranches
    { 
        [JsonProperty("name")]
        public string Name;
        [JsonProperty("commit")]
        public Commit commit;
        [JsonProperty("protected")]
        public string prot;
    }

    public class Commit
    {
        [JsonProperty("sha")]
        public string sha;
        [JsonProperty("url")]
        public string url;
    }


    public class Settings
    {
        public string branch_name = "develop";
        public string install_path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/OpenRCT2/bin/";
        public string executable = "";
    }
}