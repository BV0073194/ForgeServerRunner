using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace ForgeServerRunner
{
    public partial class WorldFolderExplorer : Window
    {
        private string rootFolderPath; // Root folder path (world folder)
        private string currentFolderPath; // Current folder path

        public WorldFolderExplorer(string worldFolderPath)
        {
            InitializeComponent();
            rootFolderPath = worldFolderPath;
            currentFolderPath = worldFolderPath;
            UpdateBackButton(); // Set initial state for the Back/Quit button
            LoadFolderContents();
        }

        private void LoadFolderContents()
        {
            try
            {
                if (!Directory.Exists(currentFolderPath))
                {
                    MessageBox.Show("The folder does not exist.");
                    Close();
                    return;
                }

                CurrentPathText.Text = currentFolderPath;

                var items = new List<FolderItem>();

                // Add subdirectories
                foreach (var directory in Directory.GetDirectories(currentFolderPath))
                {
                    items.Add(new FolderItem { Name = Path.GetFileName(directory), Path = directory, Type = "Folder" });
                }

                // Add files
                foreach (var file in Directory.GetFiles(currentFolderPath))
                {
                    items.Add(new FolderItem { Name = Path.GetFileName(file), Path = file, Type = "File" });
                }

                FolderContentsListView.ItemsSource = items;
                UpdateBackButton(); // Update Back/Quit button state
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading folder contents: {ex.Message}");
            }
        }

        private void FolderContentsListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (FolderContentsListView.SelectedItem is FolderItem selectedItem)
            {
                if (selectedItem.Type == "Folder")
                {
                    // Navigate into the folder if valid
                    if (Path.GetFullPath(selectedItem.Path).StartsWith(rootFolderPath, StringComparison.OrdinalIgnoreCase))
                    {
                        currentFolderPath = selectedItem.Path;
                        LoadFolderContents();
                    }
                    else
                    {
                        MessageBox.Show("Navigation outside the parent folder is not allowed.");
                    }
                }
                else
                {
                    // Open the file with its default associated program
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = selectedItem.Path,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error opening file: {ex.Message}");
                    }
                }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentFolderPath != rootFolderPath)
            {
                // Navigate to the parent folder
                currentFolderPath = Directory.GetParent(currentFolderPath)?.FullName ?? rootFolderPath;
                LoadFolderContents();
            }
            else
            {
                // Quit if at root folder
                Close();
            }
        }

        private void RenameButton_Click(object sender, RoutedEventArgs e)
        {
            if (FolderContentsListView.SelectedItem is FolderItem selectedItem)
            {
                string newName = Microsoft.VisualBasic.Interaction.InputBox("Enter new name:", "Rename", selectedItem.Name);
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    string newPath = Path.Combine(currentFolderPath, newName);
                    try
                    {
                        if (selectedItem.Type == "Folder")
                        {
                            Directory.Move(selectedItem.Path, newPath);
                        }
                        else
                        {
                            File.Move(selectedItem.Path, newPath);
                        }

                        LoadFolderContents();
                        MessageBox.Show("Renamed successfully.");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error renaming: {ex.Message}");
                    }
                }
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (FolderContentsListView.SelectedItem is FolderItem selectedItem)
            {
                if (MessageBox.Show($"Are you sure you want to delete '{selectedItem.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        if (selectedItem.Type == "Folder")
                        {
                            Directory.Delete(selectedItem.Path, true);
                        }
                        else
                        {
                            File.Delete(selectedItem.Path);
                        }

                        LoadFolderContents();
                        MessageBox.Show("Deleted successfully.");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error deleting: {ex.Message}");
                    }
                }
            }
        }

        private void UpdateBackButton()
        {
            BackButton.Content = currentFolderPath == rootFolderPath ? "Quit" : "Back";
        }

        public class FolderItem
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public string Type { get; set; }
        }
    }
}
