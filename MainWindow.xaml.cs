using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace ForgeServer_1._16._5__36._2._42_
{
    public partial class MainWindow : Window
    {
        private Process _minecraftProcess;
        private Process _playItProcess;
        private string _playItURL;
        private Thread _playItThread;

        private bool DebugMode = false; // Set to true to enable debug behavior
        private PlayItOutputWindow _playItOutputWindow;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void StartServer_Click(object sender, RoutedEventArgs e)
        {
            string xmx = string.IsNullOrWhiteSpace(XmxTextBox.Text) ? "2G" : XmxTextBox.Text.Trim();
            string xms = string.IsNullOrWhiteSpace(XmsTextBox.Text) ? "2G" : XmsTextBox.Text.Trim();
            AppendToConsole($"[SERVER STARTING] {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            StartPlayIt();
            StartMinecraftServer(xmx, xms);
        }

        private void StopServer_Click(object sender, RoutedEventArgs e)
        {
            if (_minecraftProcess != null && !_minecraftProcess.HasExited)
            {
                _minecraftProcess.Kill();
                _minecraftProcess.Dispose();
                AppendToConsole($"[SERVER STOPPED] {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }

            StopPlayIt();
        }
        private void StartPlayIt()
        {
            var playItConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "playItConfig.json");
            if (!File.Exists(playItConfigPath)) return;

            try
            {
                var config = JsonSerializer.Deserialize<PlayItConfig>(File.ReadAllText(playItConfigPath));
                if (config != null && config.PlayItSupportEnabled)
                {
                    // Open the PlayItOutputWindow only if DebugMode is enabled
                    if (DebugMode && (_playItOutputWindow == null || !_playItOutputWindow.IsVisible))
                    {
                        _playItOutputWindow = new PlayItOutputWindow();
                        _playItOutputWindow.Show();
                    }

                    _playItProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "playit",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    _playItProcess.OutputDataReceived += (sender, e) =>
                    {
                        if (string.IsNullOrEmpty(e.Data)) return;

                        if (DebugMode)
                        {
                            // Append to PlayItOutputWindow only if DebugMode is enabled
                            Dispatcher.Invoke(() =>
                            {
                                _playItOutputWindow?.AppendOutput(e.Data);
                            });
                        }

                        // Extract and update the tunnel URL
                        if (e.Data.Contains("=>"))
                        {
                            var url = ExtractPlayItURL(e.Data);
                            if (!string.IsNullOrEmpty(url))
                            {
                                _playItURL = url;
                                Dispatcher.Invoke(() =>
                                {
                                    ServerURLTextBox.Text = _playItURL; // Set URL directly
                                });
                            }
                        }
                    };

                    _playItProcess.Start();
                    _playItProcess.BeginOutputReadLine();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting PlayIt.gg: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopPlayIt()
        {
            if (_playItProcess != null && !_playItProcess.HasExited)
            {
                _playItProcess.Kill();
                _playItProcess.Dispose();
                _playItProcess = null;
            }

            if (DebugMode && _playItOutputWindow != null && _playItOutputWindow.IsVisible)
            {
                _playItOutputWindow.Close();
                _playItOutputWindow = null;
            }
        }

        private void PlayIt_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data)) return;

            Dispatcher.Invoke(() =>
            {
                AppendToConsole(e.Data);

                // Update PlayItOutputWindow in DebugMode
                if (DebugMode)
                {
                    _playItOutputWindow?.AppendOutput(e.Data);
                }

                // Extract and update the tunnel URL
                if (e.Data.Contains("=>"))
                {
                    var url = ExtractPlayItURL(e.Data);
                    if (!string.IsNullOrEmpty(url))
                    {
                        _playItURL = url;
                        ServerURLTextBox.Text = $"Server-URL: {_playItURL}";
                    }
                }
            });
        }

        private string ExtractPlayItURL(string outputLine)
        {
            // Example format: "length-responded.gl.joinmc.link => 127.0.0.1:25565 (minecraft-java)"
            var parts = outputLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (part.Contains(".joinmc.link") || part.Contains("https://"))
                {
                    return part.Trim();
                }
            }

            return null;
        }

        private void StartMinecraftServer(string xmx, string xms)
        {
            string javaPath = @"./Java/jre1.8.0_421/bin/java.exe";
            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string jarFilePath = Path.Combine(appDirectory, "forge-1.16.5-36.2.42.jar");

            string javaArgs = $"-Xmx{xmx} -Xms{xms} -XX:+UnlockExperimentalVMOptions -XX:+AlwaysPreTouch " +
                               "-XX:NewSize=1G -XX:MaxNewSize=2G -XX:SurvivorRatio=2 -XX:+DisableExplicitGC -d64 " +
                               $"-XX:+UseConcMarkSweepGC -XX:+AggressiveOpts -jar \"{jarFilePath}\" nogui";

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = javaPath,
                Arguments = javaArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _minecraftProcess = new Process
            {
                StartInfo = startInfo
            };

            _minecraftProcess.OutputDataReceived += Process_OutputDataReceived;
            _minecraftProcess.ErrorDataReceived += Process_OutputDataReceived;
            _minecraftProcess.Exited += Server_Exited;

            _minecraftProcess.Start();
            _minecraftProcess.BeginOutputReadLine();
            _minecraftProcess.BeginErrorReadLine();
        }

        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data))
                return;

            Dispatcher.Invoke(() =>
            {
                AppendToConsole(e.Data);
            });
        }

        private void Server_Exited(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                AppendToConsole($"[SERVER STOPPED] {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            });
        }

        private void AppendToConsole(string message)
        {
            ConsoleOutput.AppendText(message + Environment.NewLine);
            ConsoleOutput.ScrollToEnd();
        }

        private void OpenServerConfigPanel_Click(object sender, RoutedEventArgs e)
        {
            ServerConfigurator configPanel = new ServerConfigurator();
            configPanel.Owner = this;
            configPanel.Show();
        }

        public void SendCommand(string command)
        {
            if (_minecraftProcess != null && !_minecraftProcess.HasExited)
            {
                _minecraftProcess.StandardInput.WriteLine(command);
                _minecraftProcess.StandardInput.Flush();
            }
        }

        private void CommandButton_Click(object sender, RoutedEventArgs e)
        {
            string command = CommandTextBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(command))
            {
                SendCommand(command);
                CommandTextBox.Clear();
            }
        }

        private void CommandTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommandButton_Click(sender, e);
                e.Handled = true;
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_minecraftProcess != null && !_minecraftProcess.HasExited)
            {
                _minecraftProcess.Kill();
                _minecraftProcess.Dispose();
            }

            if (_playItProcess != null && !_playItProcess.HasExited)
            {
                _playItProcess.Kill();
                _playItProcess.Dispose();
            }

            if (_playItOutputWindow != null && _playItOutputWindow.IsVisible)
            {
                _playItOutputWindow.Close();
            }
        }
    }

    public class PlayItConfig
    {
        public bool PlayItSupportEnabled { get; set; }
    }
}
