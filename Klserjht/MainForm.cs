﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace Klserjht
{
    public partial class MainForm : Form
    {
        private TwitchClient _client;
        private Configuration _configuration;

        private bool _isLoaded = false;

        private const string ConfigurationPath = "Klserjht.json";

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            Text = $"Klserjht-{Program.Version}";

            updateWorker.RunWorkerAsync();
            beatmapWorker.RunWorkerAsync();

            if (File.Exists(ConfigurationPath))
            {
                var contents = File.ReadAllText(ConfigurationPath);
                _configuration = JsonConvert.DeserializeObject<Configuration>(contents);

                usernameTextBox.Text = _configuration.Username;
                tokenTextBox.Text = _configuration.Token;
                channelTextBox.Text = _configuration.Channel;
                formatTextBox.Text = _configuration.Format;
                commandTextBox.Text = _configuration.Command;

                loginButton.Select();
            }
            else
            {
                formatTextBox.Text = "@!sender! !artist! - !title! (!creator!) [!version!] - !link!";
                commandTextBox.Text = "!np";

                usernameTextBox.Select(0, 0);
            }
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode.Equals(Keys.Escape)) Close();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_client != null && _client.Connected)
            {
                _client.Disconnect();

                _configuration = new Configuration
                {
                    Username = usernameTextBox.Text,
                    Token = tokenTextBox.Text,
                    Channel = channelTextBox.Text,
                    Format = formatTextBox.Text,
                    Command = commandTextBox.Text
                };

                var contents = JsonConvert.SerializeObject(_configuration, Formatting.Indented);
                File.WriteAllText(ConfigurationPath, contents);
            }
        }

        private void textBox_TextChanged(object sender, EventArgs e)
        {
            if (_client == null || !_client.Connected) loginButton.Enabled = _isLoaded && usernameTextBox.Text.Length >= 4 && usernameTextBox.Text.Length <= 25 &&
                                  tokenTextBox.Text.Length == 36 && channelTextBox.Text.Length >= 4 &&
                                  channelTextBox.Text.Length <= 25 && formatTextBox.Text.Length > 0 &&
                                  commandTextBox.Text.Length > 0;
        }

        private void textBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (loginButton.Enabled && e.KeyCode.Equals(Keys.Enter)) Login();
        }

        private void helpLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://github.com/TheOmyNomy/Klserjht/blob/master/README.md#Setup");
        }

        private void updateLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(e.Link.LinkData as string);
        }

        private void loginButton_Click(object sender, EventArgs e)
        {
            Login();
        }

        private void exitButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void updateWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            updateLinkLabel.BeginInvoke((Action)(() => updateLinkLabel.Links[0].Enabled = false));

            using (var client = new WebClient())
            {
                client.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/535.2 (KHTML, like Gecko) Chrome/15.0.874.121 Safari/535.2";

                var contents = client.DownloadString("https://api.github.com/repos/TheOmyNomy/Klserjht/releases/latest");
                var latest = new Version(JsonConvert.DeserializeObject<Version>(contents).TagName);

                updateLinkLabel.BeginInvoke((Action)(() =>
                {
                    updateLinkLabel.Links[0].Enabled = Program.Version != latest;

                    updateLinkLabel.Text = updateLinkLabel.Links[0].Enabled
                        ? "Update available! Click here!"
                        : "No updates available.";

                    updateLinkLabel.Links[0].LinkData = $"https://github.com/TheOmyNomy/Klserjht/releases/tag/{latest}";
                }));
            }
        }

        private void beatmapWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Beatmap.Initialise();
            _isLoaded = true;

            loginButton.Enabled = _isLoaded && usernameTextBox.Text.Length >= 4 && usernameTextBox.Text.Length <= 25 &&
                                  tokenTextBox.Text.Length == 36 && channelTextBox.Text.Length >= 4 &&
                                  channelTextBox.Text.Length <= 25 && formatTextBox.Text.Length > 0 &&
                                  commandTextBox.Text.Length > 0;
        }

        private void _client_OnMessageReceived(object sender, TwitchClient.OnMessageReceivedArgs e)
        {
            var name = e.Message.Split()[0];
            if (!name.Equals(commandTextBox.Text, StringComparison.OrdinalIgnoreCase)) return;

            var list = Process.GetProcessesByName("osu!");
            if (list.Length == 0)
            {
                _client.SendMessage("The streamer is not playing osu! or does not have osu! open.");
                return;
            }

            var split = list[0].MainWindowTitle.Split();

            var title = string.Empty;
            for (var i = 3; i < split.Length; i++) title += split[i] + ' ';
            title = title.Trim();

            if (split.Length <= 3)
            {
                _client.SendMessage("The streamer is not playing at the moment.");
                return;
            }

            Beatmap beatmap = null;

            foreach (var item in Beatmap.List)
            {
                var itemTitle = $"{item.Artist} - {item.Title} [{item.Version}]";

                if (itemTitle == title)
                {
                    beatmap = item;
                    break;
                }
            }

            if (beatmap == null)
            {
                // Get the correct capitalisation of their name to use here.
                _client.SendMessage($"@{_configuration.Channel} Unable to find the current beatmap.");
                return;
            }

            if (name.Equals(commandTextBox.Text, StringComparison.OrdinalIgnoreCase))
            {
                var response = formatTextBox.Text.Replace("!artist!", beatmap.Artist).Replace("!title", beatmap.Title)
                    .Replace("!creator!", beatmap.Creator).Replace("!version!", beatmap.Version)
                    .Replace("!link!", "https://osu.ppy.sh/b/" + beatmap.BeatmapId).Replace("!sender!", e.Sender);

                _client.SendMessage(response);
            }
        }

        private void Login()
        {
            usernameTextBox.Enabled = tokenTextBox.Enabled = channelTextBox.Enabled = loginButton.Enabled = false;
            formatTextBox.Select(0, 0);

            _client = new TwitchClient(usernameTextBox.Text, tokenTextBox.Text, channelTextBox.Text);
            _client.OnMessageReceived += _client_OnMessageReceived;
            usernameTextBox.Enabled = tokenTextBox.Enabled = channelTextBox.Enabled = loginButton.Enabled = !_client.Connect();
        }
    }
}