using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;

namespace ForgeServerRunner
{
    public partial class ServerConfigurator : Window
    {
        private string serverPropertiesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server.properties");
        private string backupPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server.properties.bak");
        private string modsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mods");
        private string worldFolder = AppDomain.CurrentDomain.BaseDirectory;
        private List<Window> openedWindows = new List<Window>();


        public ServerConfigurator()
        {
            InitializeComponent();
            LoadConfig();
            LoadServerProperties();
            EnsureBackupExists();
            this.Closing += ServerConfigurator_Closing;
        }

        public class PlayItConfig
        {
            public bool PlayItSupportEnabled { get; set; } = false;
        }


        private void EnsureBackupExists()
        {
            if (!File.Exists(backupPath) && File.Exists(serverPropertiesPath))
            {
                File.Copy(serverPropertiesPath, backupPath);
            }
        }

        private void ServerConfigurator_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Close any open FilePickerWindow instances
            foreach (Window window in Application.Current.Windows)
            {
                if (window is FilePickerWindow filePickerWindow)
                {
                    filePickerWindow.Close();
                }
            }

            // Close all tracked windows
            foreach (Window window in openedWindows)
            {
                if (window.IsVisible)
                {
                    window.Close();
                }
            }
        }


        // Load the server.properties file into the text box
        private void LoadServerProperties()
        {
            if (File.Exists(serverPropertiesPath))
            {
                string serverPropertiesContent = File.ReadAllText(serverPropertiesPath);
                ServerPropertiesTextBox.Text = serverPropertiesContent;
            }
            else
            {
                MessageBox.Show("server.properties not found.");
            }
        }


        // Live editing and auto-saving as the text changes in the TextBox
        private void ServerPropertiesTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            File.WriteAllText(serverPropertiesPath, ServerPropertiesTextBox.Text);
        }

        // Add Mods button
        private void AddModsButton_Click(object sender, RoutedEventArgs e)
        {
            FilePickerWindow filePicker = new FilePickerWindow("Jar Files (*.jar)|*.jar", true);
            filePicker.Owner = this;
            filePicker.ShowDialog();

            string[] selectedFiles = filePicker.SelectedFiles;
            if (selectedFiles != null)
            {
                foreach (var file in selectedFiles)
                {
                    if (!Directory.Exists(modsFolder))
                    {
                        Directory.CreateDirectory(modsFolder);
                    }

                    string destinationPath = Path.Combine(modsFolder, Path.GetFileName(file));
                    File.Copy(file, destinationPath, true);
                }
            }
        }

        // Add Modpack (Zip) button
        private void AddModpackButton_Click(object sender, RoutedEventArgs e)
        {
            // Use FilePickerWindow for selecting the modpack (zip file)
            FilePickerWindow filePicker = new FilePickerWindow("Zip Files (*.zip)|*.zip", false);
            filePicker.Owner = this;
            filePicker.ShowDialog();

            string[] selectedFiles = filePicker.SelectedFiles;
            if (selectedFiles != null && selectedFiles.Length > 0)
            {
                string zipFilePath = selectedFiles[0]; // Only take the first file (since multiSelect is false)

                if (File.Exists(zipFilePath))
                {
                    // Check if the zip file contains .jar files and not world directories
                    if (ContainsJarFiles(zipFilePath) && !ContainsWorldDirectories(zipFilePath))
                    {
                        if (!Directory.Exists(modsFolder))
                        {
                            Directory.CreateDirectory(modsFolder);
                        }

                        try
                        {
                            // Extract the modpack (zip file) into the mods folder
                            ZipFile.ExtractToDirectory(zipFilePath, modsFolder);
                            MessageBox.Show("Modpack extracted to mods folder.");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error extracting modpack: {ex.Message}");
                        }
                    }
                    else
                    {
                        MessageBox.Show("The modpack is invalid. Please ensure it contains only .jar files and not world data.");
                    }
                }
            }
        }

        // Check if the zip file contains .jar files
        private bool ContainsJarFiles(string zipFilePath)
        {
            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
                {
                    // Check each entry in the zip file to see if it is a .jar file
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (entry.FullName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading modpack zip: {ex.Message}");
            }
            return false;
        }

        // Check if the zip file contains any directories that resemble world data
        private bool ContainsWorldDirectories(string zipFilePath)
        {
            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
                {
                    // Check for directories that are typical in a world, e.g., DIM1, region, level.dat
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (entry.FullName.Contains("DIM") || entry.FullName.Contains("region") || entry.FullName.Contains("level.dat"))
                        {
                            return true; // This is a world, not a modpack
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading modpack zip: {ex.Message}");
            }
            return false;
        }

        // Add World button (updated for world validation)
        private void AddWorldButton_Click(object sender, RoutedEventArgs e)
        {
            // Use FilePickerWindow to select the world (zip file)
            FilePickerWindow filePicker = new FilePickerWindow("Zip Files (*.zip)|*.zip", false);
            filePicker.Owner = this;
            filePicker.ShowDialog();

            string[] selectedFiles = filePicker.SelectedFiles;
            if (selectedFiles != null && selectedFiles.Length > 0)
            {
                string zipFilePath = selectedFiles[0]; // Only take the first file (since multiSelect is false)

                if (File.Exists(zipFilePath))
                {
                    // Check if the zip file contains world data and not modpack files
                    if (ContainsWorldData(zipFilePath) && !ContainsModpackFiles(zipFilePath))
                    {
                        string worldName = Path.GetFileNameWithoutExtension(zipFilePath);
                        string extractPath = Path.Combine(worldFolder, worldName);

                        if (!Directory.Exists(extractPath))
                        {
                            Directory.CreateDirectory(extractPath);
                        }

                        try
                        {
                            // Extract the world data (zip file) into the world folder
                            ZipFile.ExtractToDirectory(zipFilePath, extractPath);
                            MessageBox.Show($"World '{worldName}' extracted.");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error extracting world: {ex.Message}");
                        }

                        // Update server.properties with the new world name
                        if (File.Exists(serverPropertiesPath))
                        {
                            string[] lines = File.ReadAllLines(serverPropertiesPath);
                            for (int i = 0; i < lines.Length; i++)
                            {
                                if (lines[i].StartsWith("level-name="))
                                {
                                    lines[i] = "level-name=" + worldName;
                                    break;
                                }
                            }
                            File.WriteAllLines(serverPropertiesPath, lines);
                        }

                        LoadServerProperties();
                    }
                    else
                    {
                        MessageBox.Show("The world is invalid. Please ensure it contains only world data and not mod files.");
                    }
                }
            }
        }

        // Check if the zip file contains world data (directories like DIM1, region, level.dat)
        private bool ContainsWorldData(string zipFilePath)
        {
            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
                {
                    // Check for directories and files that are typical in a world, e.g., DIM1, region, level.dat
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (entry.FullName.Contains("DIM") || entry.FullName.Contains("region") || entry.FullName.Contains("level.dat"))
                        {
                            return true; // This is likely a world
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading world zip: {ex.Message}");
            }
            return false;
        }

        // Check if the zip file contains any .jar files (which would indicate a modpack)
        private bool ContainsModpackFiles(string zipFilePath)
        {
            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
                {
                    // Check if there are any .jar files in the zip file
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (entry.FullName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
                        {
                            return true; // This file contains modpack .jar files
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading world zip: {ex.Message}");
            }
            return false;
        }


        private void ServerPropertiesTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            File.WriteAllText(serverPropertiesPath, ServerPropertiesTextBox.Text);
        }

        // Save Configuration Button Click
        private void SaveConfigButton_Click(object sender, RoutedEventArgs e)
        {
            string serverPropSavesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "serverPropSaves");

            // Ensure the save folder exists
            if (!Directory.Exists(serverPropSavesFolder))
            {
                Directory.CreateDirectory(serverPropSavesFolder);
            }

            int saveNumber = Directory.GetFiles(serverPropSavesFolder, "server.properties.sav*").Length + 1;
            string savePath = Path.Combine(serverPropSavesFolder, $"server.properties.sav{saveNumber}");

            try
            {
                File.WriteAllText(savePath, ServerPropertiesTextBox.Text);
                MessageBox.Show($"Configuration saved as: {Path.GetFileName(savePath)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving configuration: {ex.Message}");
            }
        }

        // Load Configuration Button Click
        private void LoadConfigButton_Click(object sender, RoutedEventArgs e)
        {
            string serverPropSavesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "serverPropSaves");

            // Ensure the save folder exists
            if (!Directory.Exists(serverPropSavesFolder))
            {
                Directory.CreateDirectory(serverPropSavesFolder);
            }

            var openFileDialog = new OpenFileDialog
            {
                InitialDirectory = serverPropSavesFolder,
                Filter = "Server Properties Saves (*.sav*)|*.sav*",
                Title = "Select a Configuration to Load"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedFile = openFileDialog.FileName;
                try
                {
                    string configContent = File.ReadAllText(selectedFile);
                    ServerPropertiesTextBox.Text = configContent;

                    // Save the loaded configuration to server.properties
                    File.WriteAllText(serverPropertiesPath, configContent);
                    MessageBox.Show("Configuration loaded and applied.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading configuration: {ex.Message}");
                }
            }
        }

        // Restore from Backup Button Click
        private void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(backupPath))
            {
                File.Copy(backupPath, serverPropertiesPath, true);
                MessageBox.Show("Server properties restored from backup.");
                LoadServerProperties();
            }
            else
            {
                MessageBox.Show("No backup found.");
            }
        }

        private void OpenFileBrowserButton_Click(object sender, RoutedEventArgs e)
        {
            ServerModBrowser modBrowser = new ServerModBrowser();
            modBrowser.Owner = this;
            openedWindows.Add(modBrowser); // Track the opened window
            modBrowser.ShowDialog();
        }

        public void ReloadServerProperties()
        {
            try
            {
                if (File.Exists(serverPropertiesPath))
                {
                    string serverPropertiesContent = File.ReadAllText(serverPropertiesPath);
                    ServerPropertiesTextBox.Text = serverPropertiesContent;
                }
                else
                {
                    MessageBox.Show("server.properties not found.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reloading server.properties: {ex.Message}");
            }
        }

        private void OpenWorldBrowserButton_Click(object sender, RoutedEventArgs e)
        {
            ServerWorldBrowser worldBrowser = new ServerWorldBrowser(this);
            worldBrowser.Owner = this;
            openedWindows.Add(worldBrowser);
            worldBrowser.ShowDialog();
        }

        private string playItConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "playItConfig.json");
        private PlayItConfig playItConfig;

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(playItConfigPath))
                {
                    string jsonContent = File.ReadAllText(playItConfigPath);
                    playItConfig = JsonSerializer.Deserialize<PlayItConfig>(jsonContent) ?? new PlayItConfig();
                }
                else
                {
                    playItConfig = new PlayItConfig();
                }

                EnablePlayItSupportCheckBox.IsChecked = playItConfig.PlayItSupportEnabled;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading PlayIt.gg config: {ex.Message}");
                playItConfig = new PlayItConfig();
            }
        }

        private void SaveConfig()
        {
            try
            {
                if (playItConfig != null)
                {
                    string jsonContent = JsonSerializer.Serialize(playItConfig, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(playItConfigPath, jsonContent);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving PlayIt.gg config: {ex.Message}");
            }
        }

        private void EnablePlayItSupportCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            playItConfig.PlayItSupportEnabled = true;
            SaveConfig();
        }

        private void EnablePlayItSupportCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            playItConfig.PlayItSupportEnabled = false;
            SaveConfig();
        }
    }
}