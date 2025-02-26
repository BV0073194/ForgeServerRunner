using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace ForgeServerRunner
{
    public partial class FilePickerWindow : Window
    {
        private readonly string _filter;
        private readonly bool _multiSelect;

        public string[] SelectedFiles { get; private set; }

        public FilePickerWindow(string filter, bool multiSelect)
        {
            InitializeComponent();
            _filter = filter;
            _multiSelect = multiSelect;
            FileListBox.AllowDrop = true;
            FileListBox.DragOver += FileListBox_DragOver;
            FileListBox.Drop += FileListBox_Drop;
        }

        // Enable drag-and-drop support
        private void FileListBox_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        // Handle dropped files
        private void FileListBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var file in files)
                {
                    // Validate the file based on the type set (either .jar or .zip)
                    if (ValidateFile(file))
                    {
                        SelectedFiles = files;
                        this.Close();
                    }
                    else
                    {
                        MessageBox.Show("Invalid file type. Please select a valid file.");
                    }
                }
            }
        }

        // Open the file dialog when the user clicks
        private void OpenFileDialogButton_Click(object sender, RoutedEventArgs e)
        {
            string[] files = OpenFileDialog(_filter, _multiSelect);
            if (files != null && files.Length > 0)
            {
                SelectedFiles = files;
                this.Close();
            }
        }

        // Open a file dialog
        public string[] OpenFileDialog(string filter, bool multiSelect)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = filter,
                Multiselect = multiSelect
            };
            return openFileDialog.ShowDialog() == true ? openFileDialog.FileNames : null;
        }

        // Validate the file type
        private bool ValidateFile(string file)
        {
            string extension = Path.GetExtension(file).ToLower();
            return (extension == ".jar" && _filter.Contains("Jar Files")) || (extension == ".zip" && _filter.Contains("Zip Files"));
        }
    }
}
