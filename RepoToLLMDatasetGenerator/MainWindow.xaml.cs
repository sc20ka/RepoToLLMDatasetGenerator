﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.Text.Json;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace RepoToLLMDatasetGenerator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<string> selectedFiles = new List<string>();
        private Dictionary<string, bool> directoryCheckedState = new Dictionary<string, bool>();
        private ObservableCollection<ExtensionItem> extensionListItems = new ObservableCollection<ExtensionItem>();

        public MainWindow()
        {
            InitializeComponent();
            ExtensionListBox.ItemsSource = extensionListItems;
        }

        private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
        {
            //var folderDialog = new System.Windows.Forms.FolderBrowserDialog();
            //System.Windows.Forms.DialogResult result = folderDialog.ShowDialog();

            //if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(folderDialog.SelectedPath))
            //{
            //    LocalFolderPathTextBox.Text = folderDialog.SelectedPath;
            //    PopulateDirectoryTreeView(folderDialog.SelectedPath);
            //}
        }

        private void LocalFolderPathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(LocalFolderPathTextBox.Text) && Directory.Exists(LocalFolderPathTextBox.Text))
            {
                PopulateDirectoryTreeView(LocalFolderPathTextBox.Text);
            }
        }

        private void SourceTypeRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (LocalFolderPathTextBox != null && BrowseFolderButton != null && RemoteRepoUrlTextBox != null && LocalFolderRadioButton != null && RemoteRepoRadioButton != null)
            {
                if (LocalFolderRadioButton.IsChecked == true)
                {
                    LocalFolderPathTextBox.IsEnabled = true;
                    BrowseFolderButton.IsEnabled = true;
                    RemoteRepoUrlTextBox.IsEnabled = false;
                }
                else if (RemoteRepoRadioButton.IsChecked == true)
                {
                    LocalFolderPathTextBox.IsEnabled = false;
                    BrowseFolderButton.IsEnabled = false;
                    RemoteRepoUrlTextBox.IsEnabled = true;
                }
            }
        }


        private void PopulateDirectoryTreeView(string path)
        {
            DirectoryTreeView.Items.Clear();
            selectedFiles.Clear();
            directoryCheckedState.Clear();
            extensionListItems.Clear();
            if (Directory.Exists(path))
            {
                TreeViewItem rootItem = CreateTreeViewItem(path, true);
                rootItem.IsExpanded = true;
                DirectoryTreeView.Items.Add(rootItem);
                PopulateTreeViewItems(rootItem);
            }
            PopulateFileExtensionsListBox();
        }

        private void PopulateTreeViewItems(TreeViewItem parentItem)
        {
            string parentPath = parentItem.Tag.ToString();

            try
            {
                string[] subDirectories = Directory.GetDirectories(parentPath)
                                                    .Where(d => !System.IO.Path.GetFileName(d).StartsWith("."))
                                                    .Where(d => !d.Contains(@"\.git\")).ToArray();

                foreach (string subDirectory in subDirectories)
                {
                    TreeViewItem subDirItem = CreateTreeViewItem(subDirectory, true);
                    subDirItem.Items.Add(null);
                    subDirItem.Expanded += SubDirItem_Expanded;
                    parentItem.Items.Add(subDirItem);
                }

                string[] files = Directory.GetFiles(parentPath)
                                        .Where(f => !System.IO.Path.GetFileName(f).StartsWith(".")).ToArray();
                foreach (string file in files)
                {
                    TreeViewItem fileItem = CreateTreeViewItem(file, false);
                    parentItem.Items.Add(fileItem);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка доступа к директории: {parentPath} - {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private TreeViewItem CreateTreeViewItem(string path, bool isDirectory)
        {
            CheckBox checkBox = new CheckBox() { Content = System.IO.Path.GetFileName(path), Tag = path };
            checkBox.Checked += ItemCheckBox_Checked;
            checkBox.Unchecked += ItemCheckBox_Unchecked;

            TreeViewItem item = new TreeViewItem() { Header = checkBox, Tag = path };
            return item;
        }


        private void SubDirItem_Expanded(object sender, RoutedEventArgs e)
        {
            TreeViewItem item = (TreeViewItem)sender;
            if (item.Items.Count == 1 && item.Items[0] == null)
            {
                item.Items.Clear();
                PopulateTreeViewItems(item);

                string directoryPath = item.Tag.ToString();
                if (directoryCheckedState.ContainsKey(directoryPath) && directoryCheckedState[directoryPath])
                {
                    CheckBox headerCheckBox = (CheckBox)item.Header;
                    if (headerCheckBox != null && headerCheckBox.IsChecked != true)
                    {
                        headerCheckBox.IsChecked = true;
                        SelectAllChildren(item, true);
                    }
                }
            }
        }


        private void GenerateDatasetButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(LocalFolderPathTextBox.Text))
            {
                MessageBox.Show("Пожалуйста, выберите локальную папку.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (selectedFiles.Count == 0)
            {
                MessageBox.Show("Пожалуйста, выберите файлы и/или директории для включения в датасет.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }


            List<string> filteredSelectedFiles = FilterFilesByExtension(selectedFiles);
            DatasetGenerator generator = new DatasetGenerator();
            List<string> datasetStrings = generator.GenerateDatasetStrings(filteredSelectedFiles, LocalFolderPathTextBox.Text);
            string datasetContent = string.Join("\n", datasetStrings);

            string outputFileName = System.IO.Path.GetFileName(LocalFolderPathTextBox.Text) + "_dataset.txt";
            string outputPath = System.IO.Path.Combine(Environment.CurrentDirectory, outputFileName);

            try
            {
                File.WriteAllText(outputPath, datasetContent, Encoding.UTF8);
                MessageBox.Show($"Датасет успешно сгенерирован и сохранен в файл:\n{outputPath}", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ItemCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = (CheckBox)sender;
            string path = checkBox.Tag.ToString();
            TreeViewItem item = (TreeViewItem)checkBox.Parent;

            if (Directory.Exists(path))
            {
                directoryCheckedState[path] = true;
                item.IsExpanded = true; // Expand directory regardless
                SelectAllChildren(item, true); // Call SelectAllChildren immediately
            }
            else
            {
                if (!selectedFiles.Contains(path))
                {
                    selectedFiles.Add(path);
                    Debug.WriteLine($"Файл добавлен в selectedFiles: {path}");
                }
                PopulateFileExtensionsListBox();
            }
        }

        private void ItemCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = (CheckBox)sender;
            string path = checkBox.Tag.ToString();
            TreeViewItem item = (TreeViewItem)checkBox.Parent;

            if (Directory.Exists(path))
            {
                directoryCheckedState[path] = false;
                SelectAllChildren(item, false);
            }
            else
            {
                selectedFiles.Remove(path);
                Debug.WriteLine($"Файл удален из selectedFiles: {path}");
                PopulateFileExtensionsListBox();
            }
        }


        private void SelectAllChildren(TreeViewItem item, bool isChecked)
        {
            if (item == null) return; // Null check for item

            foreach (var childItem in item.Items)
            {
                if (childItem is TreeViewItem treeViewChild)
                {
                    CheckBox childCheckBox = (CheckBox)treeViewChild.Header as CheckBox; // Safe cast
                    if (childCheckBox != null)
                    {
                        childCheckBox.IsChecked = isChecked;
                    }
                    if (Directory.Exists(treeViewChild.Tag.ToString())) // Recursive call for subdirectories
                    {
                        SelectAllChildren(treeViewChild, isChecked);
                    }
                }
            }
        }


        private void DirectoryTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            Debug.WriteLine("DirectoryTreeView_SelectedItemChanged вызван");
            PopulateFileExtensionsListBox();
        }


        private void PopulateFileExtensionsListBox()
        {
            Debug.WriteLine("PopulateFileExtensionsListBox вызван");
            extensionListItems.Clear();
            HashSet<string> extensions = new HashSet<string>();

            Debug.WriteLine($"selectedFiles count in PopulateFileExtensionsListBox: {selectedFiles.Count}");

            foreach (string filePath in selectedFiles)
            {
                if (File.Exists(filePath))
                {
                    string extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
                    if (!string.IsNullOrEmpty(extension))
                    {
                        extensions.Add(extension);
                        Debug.WriteLine($"Добавлено расширение: {extension} для файла: {filePath}");
                    }
                }
            }

            foreach (string ext in extensions.OrderBy(e => e))
            {
                extensionListItems.Add(new ExtensionItem { Extension = ext, IsChecked = true });
                Debug.WriteLine($"ExtensionItem добавлен в ListBox: {ext}");
            }
        }


        private List<string> FilterFilesByExtension(List<string> files)
        {
            List<string> filteredFiles = new List<string>();
            HashSet<string> checkedExtensions = new HashSet<string>();

            foreach (var item in extensionListItems)
            {
                if (item.IsChecked)
                {
                    checkedExtensions.Add(item.Extension);
                }
            }

            foreach (string file in files)
            {
                string extension = System.IO.Path.GetExtension(file).ToLowerInvariant();
                if (string.IsNullOrEmpty(extension) || checkedExtensions.Contains(extension))
                {
                    filteredFiles.Add(file);
                }
            }
            return filteredFiles;
        }


    }


    public class ExtensionItem
    {
        public string Extension { get; set; }
        public bool IsChecked { get; set; }
        public string DisplayName => string.IsNullOrEmpty(Extension) ? "(без расширения)" : Extension;
    }
}