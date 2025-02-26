using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace ForgeServerRunner
{
    public partial class ServerModBrowser : Window
    {
        private string modsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mods");
        private List<FileItem> files = new List<FileItem>();

        public ServerModBrowser()
        {
            InitializeComponent();
            LoadFiles();
        }

        private void LoadFiles()
        {
            if (!Directory.Exists(modsFolder))
            {
                Directory.CreateDirectory(modsFolder);
            }

            files.Clear();
            foreach (string file in Directory.GetFiles(modsFolder, "*.jar"))
            {
                FileInfo fileInfo = new FileInfo(file);
                files.Add(new FileItem
                {
                    Name = fileInfo.Name,
                    Path = fileInfo.FullName,
                    Size = (fileInfo.Length / 1024).ToString()
                });
            }

            FileListView.ItemsSource = files;
        }

        private void RenameButton_Click(object sender, RoutedEventArgs e)
        {
            if (FileListView.SelectedItem is FileItem selectedFile)
            {
                string newName = Microsoft.VisualBasic.Interaction.InputBox("Enter new file name (with .jar extension):", "Rename File", selectedFile.Name);
                if (!string.IsNullOrWhiteSpace(newName) && newName.EndsWith(".jar"))
                {
                    string newPath = Path.Combine(modsFolder, newName);
                    try
                    {
                        File.Move(selectedFile.Path, newPath);

                        // Update the selected file details
                        selectedFile.Name = newName;
                        selectedFile.Path = newPath;

                        // Refresh the ListView binding
                        FileListView.Items.Refresh();

                        MessageBox.Show("File renamed successfully.");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error renaming file: {ex.Message}");
                    }
                }
                else
                {
                    MessageBox.Show("Invalid file name. Please ensure it ends with '.jar'.");
                }
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (FileListView.SelectedItem is FileItem selectedFile)
            {
                if (MessageBox.Show("Are you sure you want to permanently delete this file?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        File.Delete(selectedFile.Path);

                        // Remove the file from the list and refresh the UI
                        files.Remove(selectedFile);
                        FileListView.Items.Refresh();

                        MessageBox.Show("File deleted successfully.");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error deleting file: {ex.Message}");
                    }
                }
            }
        }

        public class FileItem
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public string Size { get; set; }
        }
    }
}
