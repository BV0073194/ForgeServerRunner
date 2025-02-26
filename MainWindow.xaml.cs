using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace ForgeServerRunner
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

        private void KillAssociatedProcesses()
        {
            try
            {
                // Kill Minecraft server process
                if (_minecraftProcess != null && !_minecraftProcess.HasExited)
                {
                    _minecraftProcess.Kill();
                    _minecraftProcess.Dispose();
                }

                // Kill PlayIt process
                if (_playItProcess != null && !_playItProcess.HasExited)
                {
                    _playItProcess.Kill();
                    _playItProcess.Dispose();
                }

                AppendToConsole("[INFO] All associated processes have been terminated.");
            }
            catch (Exception ex)
            {
                AppendToConsole($"[ERROR] Failed to terminate associated processes: {ex.Message}");
            }
        }

        private void FullyTerminateApplication()
        {
            try
            {
                KillAssociatedProcesses(); // Kill all associated processes
                AppendToConsole("[INFO] Application is terminating...");

                Application.Current.Shutdown(); // Gracefully shut down the application
                Process.GetCurrentProcess().Kill(); // Forcefully terminate the process
            }
            catch (Exception ex)
            {
                AppendToConsole($"[ERROR] Failed to terminate the application: {ex.Message}");
            }
        }

        private void DisablePlayItSupportAndRestart()
        {
            try
            {
                // Disable PlayIt support in the configuration
                var playItConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "playItConfig.json");
                if (File.Exists(playItConfigPath))
                {
                    var config = JsonSerializer.Deserialize<PlayItConfig>(File.ReadAllText(playItConfigPath)) ?? new PlayItConfig();
                    config.PlayItSupportEnabled = false;

                    File.WriteAllText(
                        playItConfigPath,
                        JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true })
                    );

                    AppendToConsole("[INFO] PlayIt support has been disabled in the configuration.");
                }

                // Kill all associated processes
                KillAssociatedProcesses();

                // Restart the application
                AppendToConsole("[INFO] Restarting the application...");
                string appPath = Process.GetCurrentProcess().MainModule.FileName;
                Process.Start(new ProcessStartInfo
                {
                    FileName = appPath,
                    UseShellExecute = true
                });

                Application.Current.Shutdown();
                Process.GetCurrentProcess().Kill(); // Ensure the current process terminates
            }
            catch (Exception ex)
            {
                AppendToConsole($"[ERROR] Failed to disable PlayIt support and restart: {ex.Message}");
            }
        }

        private async void EnsurePlayItInstalled()
        {
            // Default installation path
            string playItPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "playit_gg", "bin", "playit.exe");

            // Check if PlayIt is installed
            if (File.Exists(playItPath))
            {
                AppendToConsole("[INFO] PlayIt is installed.");
                return;
            }

            // If PlayIt is not installed, check if it exists in PATH
            string pathVariable = Environment.GetEnvironmentVariable("PATH");
            if (pathVariable != null)
            {
                string[] paths = pathVariable.Split(';');
                foreach (string path in paths)
                {
                    if (File.Exists(Path.Combine(path, "playit.exe")))
                    {
                        AppendToConsole("[INFO] PlayIt is found in PATH.");
                        return;
                    }
                }
            }

            // Prompt the user for installation
            var result = MessageBox.Show(
                "PlayIt is not installed. Would you like to install it now? The server and associated processes will be stopped, and the app will terminate after installation.",
                "Install PlayIt",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.No)
            {
                AppendToConsole("[INFO] PlayIt installation was declined by the user.");
                DisablePlayItSupportAndRestart(); // Disable PlayIt support and restart
                return;
            }

            // Stop the server and PlayIt processes
            if (_isServerRunning || _isPlayItRunning)
            {
                AppendToConsole("[INFO] Stopping the server and PlayIt processes before installing PlayIt...");
                await GracefulStopServerAsync(); // Stop the Minecraft server
                StopPlayIt(); // Stop PlayIt process
            }

            // Download and install PlayIt
            string installerUrl = "https://github.com/playit-cloud/playit-agent/releases/latest/download/playit-windows-x86_64-signed.msi";
            string tempInstallerPath = Path.Combine(Path.GetTempPath(), "playit-installer.msi");

            try
            {
                using (var client = new System.Net.WebClient())
                {
                    client.DownloadFile(installerUrl, tempInstallerPath);
                    AppendToConsole("[INFO] PlayIt installer downloaded successfully.");
                }

                // Install PlayIt
                Process installProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "msiexec.exe",
                        Arguments = $"/i \"{tempInstallerPath}\" /quiet",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                installProcess.Start();
                installProcess.WaitForExit();

                // Confirm installation
                if (File.Exists(playItPath))
                {
                    AppendToConsole("[INFO] PlayIt installed successfully.");
                    MessageBox.Show(
                        "PlayIt has been successfully installed. The application will now close. Please restart the application manually to apply changes.",
                        "Installation Successful",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );

                    // Open CMD window and run "playit" from its installed directory
                    string playItDirectory = Path.GetDirectoryName(playItPath); // Extract the directory of playit.exe

                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/k \"{playItPath}\"", // Run playit from its full path
                        WorkingDirectory = playItDirectory, // Set the working directory to playit's directory
                        CreateNoWindow = false, // Show the CMD window
                        UseShellExecute = true  // Allow shell execution
                    });

                    FullyTerminateApplication(); // Terminate the application
                }
                else
                {
                    AppendToConsole("[ERROR] PlayIt installation failed.");
                }
            }
            catch (Exception ex)
            {
                AppendToConsole($"[ERROR] Failed to download or install PlayIt: {ex.Message}");
            }
            finally
            {
                // Clean up installer
                if (File.Exists(tempInstallerPath))
                {
                    File.Delete(tempInstallerPath);
                }
            }
        }

        private void DisplayPublicIPAndPort()
        {
            try
            {
                string serverPropertiesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server.properties");
                string serverIp = null;

                // Check if server.properties exists and read it
                if (File.Exists(serverPropertiesPath))
                {
                    var lines = File.ReadAllLines(serverPropertiesPath);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("server-ip="))
                        {
                            serverIp = line.Split('=')[1].Trim(); // Extract the value of server-ip
                            break;
                        }
                    }
                }

                // If server-ip is set, use it; otherwise, fetch the public IP
                if (!string.IsNullOrEmpty(serverIp))
                {
                    AppendToConsole($"[INFO] Server IP from server.properties: {serverIp}");
                    ServerURLTextBox.Text = $"{serverIp}";
                }
                else
                {
                    // Fetch the public IP
                    using (var client = new System.Net.WebClient())
                    {
                        string publicIp = client.DownloadString("https://api.ipify.org").Trim();
                        AppendToConsole($"[INFO] Public IP: {publicIp}:25565");
                        ServerURLTextBox.Text = $"{publicIp}:25565";
                    }
                }
            }
            catch (Exception ex)
            {
                AppendToConsole($"[ERROR] Failed to retrieve public IP or server.properties: {ex.Message}");
            }
        }


        private void StartPlayIt()
        {
            var playItConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "playItConfig.json");
            PlayItConfig config = null;

            // Load PlayIt configuration
            if (File.Exists(playItConfigPath))
            {
                try
                {
                    config = JsonSerializer.Deserialize<PlayItConfig>(File.ReadAllText(playItConfigPath));
                }
                catch
                {
                    AppendToConsole("[ERROR] Failed to parse PlayIt configuration.");
                }
            }

            // Check if PlayIt support is enabled
            if (config == null || !config.PlayItSupportEnabled)
            {
                DisplayPublicIPAndPort(); // Only display public IP and port if PlayIt is not enabled
                return;
            }

            // Ensure PlayIt is installed and start it
            EnsurePlayItInstalled();

            if (_isPlayItRunning)
            {
                AppendToConsole("[WARNING] PlayIt is already running.");
                return;
            }

            try
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

                _playItProcess.OutputDataReceived += PlayIt_OutputDataReceived;
                _playItProcess.Start();
                _playItProcess.BeginOutputReadLine();
                _isPlayItRunning = true; // Set the flag
            }
            catch (Exception ex)
            {
                AppendToConsole($"[ERROR] Failed to start PlayIt: {ex.Message}");
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
                        ServerURLTextBox.Text = $"{_playItURL}";
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
            //string javaPath = @"./Java/jre1.8.0_421/bin/java.exe";
            string javaPath = Directory.GetFiles("./Java/", "java.exe", SearchOption.AllDirectories).FirstOrDefault();
            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string jarFilePath = Directory.GetFiles(appDirectory, "forge-*.jar", SearchOption.AllDirectories).FirstOrDefault();
            //string jarFilePath = Path.Combine(appDirectory, "forge-1.16.5-36.2.42.jar");

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

            bool isPreparingSpawn = false;
            bool worldLoaded = false;

            // Check console output for the server's state
            Dispatcher.Invoke(() =>
            {
                string consoleText = ConsoleOutput.Text;
                isPreparingSpawn = consoleText.Contains("Preparing spawn area");
                worldLoaded = consoleText.Contains("Time elapsed:");
            });

            if (isPreparingSpawn && !worldLoaded)
            {
                AppendToConsole("[WARNING] Cannot close the window while the world is loading. Please wait for the server to finish loading.");
                return; // Keep the window open
            }

            if (_isServerRunning)
            {
                AppendToConsole("[INFO] Closing the application. Waiting for the server to shut down...");
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
            }

            // Save configuration and allow the window to close
            SaveConfiguration();
            AppendToConsole("[INFO] Shutdown complete. Closing application...");
            e.Cancel = false; // Allow window to close
            Application.Current.Shutdown(); // Ensure application exits
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