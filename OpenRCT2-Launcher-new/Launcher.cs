﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
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
        private static JsonBranches[] output;
        private Launcher()
        {
            Title = "OpenRCT2 Branch Launcher";
            ClientSize = new Size(300, 200);
            Resizable = false;
            Content = GetUi();
            //Post GUI Setup Here

        }
        [STAThread]
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
                    output = JsonConvert.DeserializeObject<JsonBranches[]>(
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

            foreach (var b in output)
            {
                if (b.Name == branch)
                {
                    if (_settings.lastCommit != b.commit.sha)
                    {
                        _settings.lastCommit = b.commit.sha;
                        //Falls out of the if, and downloads/updates
                    }
                    else
                    {
                        //Launches the existing downloaded copy.
                        LaunchGame();
                        return;
                    }
                }
            }
            
            
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
                MessageBox.Show("That branch is too old and the builds have been deleted due to Age. Please pick a different branch.", MessageBoxType.Warning);
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
                ZipFile.ExtractToDirectory(tmpFileName, tmpDirectory);
                foreach (var file in Directory.GetFiles(tmpDirectory))
                {
                    if (file.Contains(".exe"))
                    {
                        File.Delete(file);
                    }

                    if (file.Contains("symbols"))
                    {
                        File.Delete(file);
                    }

                    if (file.Contains("portable"))
                    {
                        ZipFile.ExtractToDirectory(file, _settings.install_path);
                    }
                }

                foreach (var file in Directory.GetFiles(_settings.install_path))
                {
                    if (file.EndsWith("exe"))
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
                        Process.Start("/usr/bin/chmod", "+x " + file).WaitForExit();
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
            LaunchGame();
            //Environment.Exit(0);
        }

        private static void LaunchGame()
        {
            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = _settings.executable;
            info.WorkingDirectory = _settings.install_path;
            info.UseShellExecute = true;
            Process.Start(info);
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
                        var dir = Directory.GetCurrentDirectory() + "/Resources/";
                        ImageView logo = new ImageView();
                        logo.Image = new Bitmap(dir + "logo.png");
                        ImageView text = new ImageView();
                        text.Image = new Bitmap(dir + "logo_text.png");
                        logolayout.Items.Add(logo);
                        logolayout.Items.Add(text);
                    }
                }
                {
                    _branch_dropdown = new DropDown();
                    _branch_dropdown.Size = new Size(300, _branch_dropdown.Height);
                    UpdateBranches();
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
                        launch.Size = new Size(150, launch.Size.Height);
                        buttons.Items.Add(launch);
                    }
                    {
                        Button options = new Button();
                        options.Text = "Options";
                        options.Size = new Size(150, options.Size.Height);
                        buttons.Items.Add(options);
                    }
                    layout.Items.Add(buttons);
                }
                {
                    CheckBox shouldShowAllBranches = new CheckBox();
                    shouldShowAllBranches.Checked = _settings.allBranches;
                    shouldShowAllBranches.Text = "Show all branches";
                    shouldShowAllBranches.CheckedChanged += (sender, args) =>
                    {
                        _settings.allBranches = shouldShowAllBranches.Checked.Value;
                        UpdateBranches();
                        SaveSettings();
                    };
                    layout.Items.Add(shouldShowAllBranches);
                }
                panel.Content = layout;
                //Everything ends up on the panel eventually.

                return panel;
            }
        }

        private void UpdateBranches()
        {
            List<string> allowedBranches = new List<string>();
            allowedBranches.Add("develop");
            allowedBranches.Add("master");
            allowedBranches.Add("new-save-format");
            _branch_dropdown.Items.Clear();
            foreach (var item in _branches)
            {
                if (allowedBranches.Contains(item) | _settings.allBranches)
                {
                    //This was made global for access elsewhere, see top of file
                    _branch_dropdown.Items.Add(item);
                    if (item == _settings.branch_name)
                    {
                        _branch_dropdown.SelectedIndex = _branch_dropdown.Items.Count - 1;
                    }
                }
            }
        }

        private void LaunchOnClick(object sender, EventArgs e)
        {
            DownloadAndUnpackBuild(_settings.branch_name);
        }

        private void Branch_dropdownOnSelectedIndexChanged(object sender, EventArgs e)
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
        public bool allBranches = false;
        public string lastCommit = "";
    }
}