using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace ForgeServerRunner
{
    public partial class ServerWorldBrowser : Window
    {
        private string serverPropertiesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server.properties");
        private string serverFolder = AppDomain.CurrentDomain.BaseDirectory;
        private string[] nonWorldFolders = { "libraries", "logs", "config", "Java", "mods", "defaultconfigs", "serverPropSaves" };
        private List<WorldFolder> worlds = new List<WorldFolder>();
        private ServerConfigurator configurator;

        public ServerWorldBrowser(ServerConfigurator parentConfigurator)
        {
            InitializeComponent();
            configurator = parentConfigurator;
            LoadWorlds();
        }

        private void LoadWorlds()
        {
            worlds.Clear();
            foreach (string folder in Directory.GetDirectories(serverFolder))
            {
                string folderName = Path.GetFileName(folder);

                // Exclude non-world folders
                if (Array.Exists(nonWorldFolders, name => name.Equals(folderName, StringComparison.OrdinalIgnoreCase)))
                    continue;

                // Check for valid world folders (e.g., containing level.dat)
                if (File.Exists(Path.Combine(folder, "level.dat")))
                {
                    worlds.Add(new WorldFolder { Name = folderName, Path = folder });
                }
            }

            WorldListView.ItemsSource = worlds;
        }

        private void WorldListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (WorldListView.SelectedItem is WorldFolder selectedWorld)
            {
                WorldFolderExplorer explorer = new WorldFolderExplorer(selectedWorld.Path);
                explorer.Owner = this;
                explorer.ShowDialog();
            }
        }

        private void SetAsCurrentWorldButton_Click(object sender, RoutedEventArgs e)
        {
            if (WorldListView.SelectedItem is WorldFolder selectedWorld)
            {
                if (File.Exists(serverPropertiesPath))
                {
                    try
                    {
                        string[] lines = File.ReadAllLines(serverPropertiesPath);

                        for (int i = 0; i < lines.Length; i++)
                        {
                            if (lines[i].StartsWith("level-name="))
                            {
                                lines[i] = $"level-name={selectedWorld.Name}";
                                break;
                            }
                        }

                        File.WriteAllLines(serverPropertiesPath, lines);
                        configurator?.ReloadServerProperties();
                        MessageBox.Show($"World '{selectedWorld.Name}' set as the current world.");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error updating server.properties: {ex.Message}");
                    }
                }
                else
                {
                    MessageBox.Show("server.properties file not found.");
                }
            }
            else
            {
                MessageBox.Show("Please select a world to set as the current world.");
            }
        }

        private void RenameButton_Click(object sender, RoutedEventArgs e)
        {
            if (WorldListView.SelectedItem is WorldFolder selectedWorld)
            {
                string newName = Microsoft.VisualBasic.Interaction.InputBox("Enter new folder name:", "Rename Folder", selectedWorld.Name);
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    string newPath = Path.Combine(serverFolder, newName);
                    try
                    {
                        Directory.Move(selectedWorld.Path, newPath);
                        string oldName = selectedWorld.Name;
                        selectedWorld.Name = newName;
                        selectedWorld.Path = newPath;
                        WorldListView.Items.Refresh();

                        if (File.Exists(serverPropertiesPath))
                        {
                            string[] lines = File.ReadAllLines(serverPropertiesPath);
                            for (int i = 0; i < lines.Length; i++)
                            {
                                if (lines[i].StartsWith("level-name=") && lines[i] == $"level-name={oldName}")
                                {
                                    lines[i] = $"level-name={newName}";
                                    File.WriteAllLines(serverPropertiesPath, lines);
                                    configurator?.ReloadServerProperties();
                                    MessageBox.Show("Server updated to reflect the renamed world.");
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error renaming folder: {ex.Message}");
                    }
                }
            }
            else
            {
                MessageBox.Show("Please select a world to rename.");
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (WorldListView.SelectedItem is WorldFolder selectedWorld)
            {
                if (MessageBox.Show($"Are you sure you want to delete '{selectedWorld.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        Directory.Delete(selectedWorld.Path, true);
                        worlds.Remove(selectedWorld);
                        WorldListView.Items.Refresh();

                        if (File.Exists(serverPropertiesPath))
                        {
                            string[] lines = File.ReadAllLines(serverPropertiesPath);
                            bool worldDeleted = false;

                            for (int i = 0; i < lines.Length; i++)
                            {
                                if (lines[i].StartsWith("level-name=") && lines[i] == $"level-name={selectedWorld.Name}")
                                {
                                    lines[i] = "level-name=world";
                                    worldDeleted = true;
                                    break;
                                }
                            }

                            if (worldDeleted || worlds.Count == 0)
                            {
                                File.WriteAllLines(serverPropertiesPath, lines);
                                configurator?.ReloadServerProperties();
                                MessageBox.Show("Current world was deleted, server defaulted to 'world'.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error deleting folder: {ex.Message}");
                    }
                }
            }
            else
            {
                MessageBox.Show("Please select a world to delete.");
            }
        }

        public class WorldFolder
        {
            public string Name { get; set; }
            public string Path { get; set; }
        }
    }
}
