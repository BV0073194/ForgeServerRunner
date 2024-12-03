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

        private bool _isServerRunning = false; // Tracks server state
        private bool _isPlayItRunning = false; // Tracks PlayIt state

        private bool _isStopping = false; // Prevent concurrent stop attempts

        private const string ConfigFileName = "AppConfig.json"; // Configuration file for Xmx and Xms
        private AppConfig _config; // Holds user preferences


        public MainWindow()
        {
            InitializeComponent();
            LoadConfiguration(); // Load saved configuration on startup
        }

        private void LoadConfiguration()
        {
            if (File.Exists(ConfigFileName))
            {
                try
                {
                    string json = File.ReadAllText(ConfigFileName);
                    _config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
                catch
                {
                    _config = new AppConfig(); // Defaults if deserialization fails
                }
            }
            else
            {
                _config = new AppConfig(); // Defaults if no config file exists
            }

            // Apply configuration to UI
            XmxTextBox.Text = _config.Xmx;
            XmsTextBox.Text = _config.Xms;
        }

        private void SaveConfiguration()
        {
            try
            {
                _config.Xmx = string.IsNullOrWhiteSpace(XmxTextBox.Text) ? "2G" : XmxTextBox.Text.Trim();
                _config.Xms = string.IsNullOrWhiteSpace(XmsTextBox.Text) ? "2G" : XmsTextBox.Text.Trim();

                string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFileName, json);
            }
            catch (Exception ex)
            {
                AppendToConsole($"[ERROR] Failed to save configuration: {ex.Message}");
            }
        }

        private void StartServer_Click(object sender, RoutedEventArgs e)
        {
            SaveConfiguration(); // Save the configuration first

            if (_isServerRunning)
            {
                AppendToConsole("[WARNING] Server is already running.");
                return;
            }

            // Clear the console and textboxes
            ConsoleOutput.Clear(); // Clear the console output
            CommandTextBox.Clear(); // Clear the command textbox

            string xmx = string.IsNullOrWhiteSpace(XmxTextBox.Text) ? "2G" : XmxTextBox.Text.Trim();
            string xms = string.IsNullOrWhiteSpace(XmsTextBox.Text) ? "2G" : XmsTextBox.Text.Trim();

            AppendToConsole($"[SERVER STARTING] {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _isServerRunning = true; // Set the flag
            StartPlayIt();
            StartMinecraftServer(xmx, xms);
        }


        private async void StopServer_Click(object sender, RoutedEventArgs e)
        {
            if (!_isServerRunning)
            {
                AppendToConsole("[WARNING] Server is not running.");
                return;
            }

            AppendToConsole($"[SERVER STOPPING] {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            // Attempt a graceful stop
            await GracefulStopServerAsync();

            // Stop PlayIt process if it is running
            StopPlayIt();
        }

        private void StartPlayIt()
        {
            if (_isPlayItRunning)
            {
                AppendToConsole("[WARNING] PlayIt is already running.");
                return;
            }

            var playItConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "playItConfig.json");
            if (!File.Exists(playItConfigPath))
            {
                AppendToConsole("[WARNING] PlayIt configuration file not found.");
                return;
            }

            try
            {
                var config = JsonSerializer.Deserialize<PlayItConfig>(File.ReadAllText(playItConfigPath));
                if (config != null && config.PlayItSupportEnabled)
                {
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

                        Dispatcher.Invoke(() =>
                        {
                            if (e.Data.Contains("=>"))
                            {
                                var url = ExtractPlayItURL(e.Data);
                                if (!string.IsNullOrEmpty(url))
                                {
                                    _playItURL = url;
                                    ServerURLTextBox.Text = _playItURL;
                                }
                            }
                        });
                    };

                    _playItProcess.Start();
                    _playItProcess.BeginOutputReadLine();
                    _isPlayItRunning = true; // Set the flag
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting PlayIt.gg: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopPlayIt()
        {
            if (!_isPlayItRunning)
            {
                AppendToConsole("[WARNING] PlayIt is not running.");
                return;
            }

            if (_playItProcess != null && !_playItProcess.HasExited)
            {
                _playItProcess.Kill();
                _playItProcess.Dispose();
            }

            _isPlayItRunning = false; // Reset the flag
        }

        private void PlayIt_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data)) return;

            Dispatcher.Invoke(() =>
            {

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
            if (string.IsNullOrEmpty(e.Data)) return;

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
                string command = CommandTextBox.Text.Trim();
                if (!string.IsNullOrWhiteSpace(command))
                {
                    SendCommand(command);
                    CommandTextBox.Clear();
                }
                e.Handled = true; // Prevent the Enter key's default behavior
            }
        }
        private async void Window_Closing(object sender, CancelEventArgs e)
        {
            // Prevent window from closing until cleanup is done
            e.Cancel = true;

            // Attempt a graceful server stop
            if (_isServerRunning)
            {
                AppendToConsole("[INFO] Closing the application. Attempting to stop the server...");
                await GracefulStopServerAsync();
            }

            // Stop PlayIt process if it is running
            if (_playItProcess != null && !_playItProcess.HasExited)
            {
                AppendToConsole("[INFO] Stopping PlayIt process...");
                try
                {
                    _playItProcess.Kill();
                }
                catch (Exception ex)
                {
                    AppendToConsole($"[WARNING] Error stopping PlayIt process: {ex.Message}");
                }
                finally
                {
                    _playItProcess.Dispose();
                }
            }

            // Close the PlayIt output window if open
            if (_playItOutputWindow != null && _playItOutputWindow.IsVisible)
            {
                _playItOutputWindow.Close();
                this.Close();
            }

            // Allow the window to close
            SaveConfiguration();
            e.Cancel = false;
        }

        private async Task GracefulStopServerAsync()
        {
            if (_isStopping)
            {
                AppendToConsole("[INFO] Server stop is already in progress.");
                return;
            }

            _isStopping = true;

            if (_minecraftProcess == null || _minecraftProcess.HasExited)
            {
                AppendToConsole("[WARNING] No valid server process exists to stop.");
                _isServerRunning = false; // Reset the flag
                _isStopping = false;
                return;
            }

            // Check for world loading phase
            bool isPreparingSpawn = false;
            bool worldLoaded = false;
            Dispatcher.Invoke(() =>
            {
                string consoleText = ConsoleOutput.Text;
                isPreparingSpawn = consoleText.Contains("Preparing spawn area");
                worldLoaded = consoleText.Contains("Time elapsed:");
            });

            if (isPreparingSpawn && !worldLoaded)
            {
                AppendToConsole("[INFO] Cannot stop the server during the 'Preparing spawn area' phase to avoid corruption.");
                _isStopping = false;
                return;
            }

            AppendToConsole("[INFO] Attempting to stop the Minecraft server...");
            bool stopAccepted = false;
            bool stopCompleted = false;

            // Declare the handler outside the try block
            DataReceivedEventHandler onOutput = null;

            try
            {
                // Define the handler
                onOutput = (s, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            // Detect stop command acceptance
                            if (args.Data.Contains("Stopping server"))
                            {
                                AppendToConsole("[INFO] Server accepted the stop command. Waiting for it to finish...");
                                stopAccepted = true;
                            }

                            // Detect stop command completion
                            if (args.Data.Contains("[SERVER STOPPED]"))
                            {
                                stopCompleted = true;
                            }
                        });
                    }
                };

                // Attach the handler
                _minecraftProcess.OutputDataReceived += onOutput;
                _minecraftProcess.ErrorDataReceived += onOutput;

                // Check if the console output contains the readiness message
                bool isServerReady = false;
                Dispatcher.Invoke(() =>
                {
                    isServerReady = ConsoleOutput.Text.Contains("For help, type \"help\"");
                });

                if (isServerReady)
                {
                    AppendToConsole("[INFO] Server is ready to accept commands. Sending /stop to gracefully shutdown...");

                    // Send the /stop command
                    SendCommand("/stop");

                    // Wait for the server to accept the stop command and complete the shutdown
                    DateTime startTime = DateTime.Now;
                    while (!_minecraftProcess.HasExited && (DateTime.Now - startTime).TotalSeconds < 90)
                    {
                        if (stopAccepted && stopCompleted)
                        {
                            AppendToConsole("[INFO] Server shutdown process completed.");
                            break;
                        }

                        await Task.Delay(500); // Asynchronous delay to allow the server to shut down properly
                    }
                }
                else
                {
                    AppendToConsole("[WARNING] Server is not ready. Forcing termination...");
                    _minecraftProcess.Kill();
                }

                // If the server is not stopped, force quit
                if (!_minecraftProcess.HasExited)
                {
                    AppendToConsole("[WARNING] Server did not stop gracefully. Forcing termination...");
                    _minecraftProcess.Kill();
                }
            }
            catch (Exception ex)
            {
                AppendToConsole($"[ERROR] Error stopping the server: {ex.Message}");
                try
                {
                    _minecraftProcess.Kill(); // Attempt to force quit
                }
                catch (InvalidOperationException)
                {
                    AppendToConsole("[WARNING] Process already exited or is invalid.");
                }
            }
            finally
            {
                // Detach the handler in the finally block
                if (onOutput != null)
                {
                    _minecraftProcess.OutputDataReceived -= onOutput;
                    _minecraftProcess.ErrorDataReceived -= onOutput;
                }

                if (_minecraftProcess != null)
                {
                    _minecraftProcess.Dispose();
                }

                _isServerRunning = false; // Reset the flag
                _isStopping = false; // Allow future stop attempts

                stopCompleted = true;
                SaveConfiguration();
            }
        }
    }

    public class AppConfig
    {
        public string Xmx { get; set; } = "2G"; // Default memory allocation
        public string Xms { get; set; } = "2G"; // Default memory allocation
    }

    public class PlayItConfig
    {
        public bool PlayItSupportEnabled { get; set; }
    }
}
