using System;
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

namespace RepoToLLMDatasetGenerator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private List<string> selectedFiles = new List<string>();
        private Dictionary<string, bool> directoryCheckedState = new Dictionary<string, bool>();
        private HashSet<string> discoveredExtensions = new HashSet<string>(); // Список найденных расширений
        private Dictionary<string, bool> extensionFilterState = new Dictionary<string, bool>(); // Состояние фильтра расширений
        private ObservableCollection<ExtensionItem> extensionListItems = new ObservableCollection<ExtensionItem>(); // Для ListBox с чекбоксами

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

        private void PopulateDirectoryTreeView(string path)
        {
            DirectoryTreeView.Items.Clear();
            selectedFiles.Clear();
            directoryCheckedState.Clear();
            discoveredExtensions.Clear(); // Очищаем список расширений
            extensionFilterState.Clear();   // Очищаем состояние фильтра расширений

            if (Directory.Exists(path))
            {
                TreeViewItem rootItem = CreateTreeViewItem(path, true);
                rootItem.IsExpanded = true;
                DirectoryTreeView.Items.Add(rootItem);
                PopulateTreeViewItems(rootItem);
                PopulateFileExtensionsListBox(); // Заполняем ListBox расширениями после обхода дерева
            }
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
                    string extension = System.IO.Path.GetExtension(file).ToLowerInvariant();
                    if (!string.IsNullOrEmpty(extension))
                    {
                        discoveredExtensions.Add(extension); // Собираем расширения файлов
                    }
                    TreeViewItem fileItem = CreateTreeViewItem(file, false);
                    parentItem.Items.Add(fileItem);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка доступа к директории: {parentPath} - {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void ExtensionCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = (CheckBox)sender;
            string extension = checkBox.Tag.ToString();
            extensionFilterState[extension] = true;
        }

        private void ExtensionCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = (CheckBox)sender;
            string extension = checkBox.Tag.ToString();
            extensionFilterState[extension] = false;
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
                    }
                }
            }
        }

        private void SourceTypeRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (LocalFolderPathTextBox != null && BrowseFolderButton != null && RemoteRepoUrlTextBox != null)
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


            List<string> filteredSelectedFiles = FilterFilesByExtension(selectedFiles); // Фильтруем файлы по расширениям
            DatasetGenerator generator = new DatasetGenerator();
            List<string> datasetStrings = generator.GenerateDatasetStrings(filteredSelectedFiles, LocalFolderPathTextBox.Text); // Используем отфильтрованный список
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

        private List<string> FilterFilesByExtension(List<string> files, Dictionary<string, bool> extensionFilter)
        {
            List<string> filteredFiles = new List<string>();
            foreach (string file in files)
            {
                string extension = System.IO.Path.GetExtension(file).ToLowerInvariant();
                if (string.IsNullOrEmpty(extension) || extensionFilter.ContainsKey(extension) && extensionFilter[extension])
                {
                    filteredFiles.Add(file); // Включаем файл, если расширение пустое или есть в фильтре и выбрано
                }
            }
            return filteredFiles;
        }


        private void ItemCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = (CheckBox)sender;
            string path = checkBox.Tag.ToString();
            TreeViewItem item = (TreeViewItem)checkBox.Parent;

            if (Directory.Exists(path))
            {
                directoryCheckedState[path] = true;
                SelectAllChildren(item, true);
            }
            else
            {
                if (!selectedFiles.Contains(path))
                {
                    selectedFiles.Add(path);
                }
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
            }
            PopulateFileExtensionsListBox();
        }


        private void SelectAllChildren(TreeViewItem item, bool isChecked)
        {
            foreach (var childItem in item.Items)
            {
                if (childItem is TreeViewItem treeViewChild)
                {
                    CheckBox childCheckBox = (CheckBox)treeViewChild.Header;
                    if (childCheckBox != null)
                    {
                        childCheckBox.IsChecked = isChecked;
                    }
                }
            }
        }

        private void DirectoryTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            PopulateFileExtensionsListBox(); // Обновляем список расширений при смене выделения в дереве (можно изменить логику вызова)
        }


        private void PopulateFileExtensionsListBox()
        {
            extensionListItems.Clear(); // Очищаем список расширений перед заполнением
            HashSet<string> extensions = new HashSet<string>();

            foreach (string filePath in selectedFiles)
            {
                if (File.Exists(filePath)) // Проверяем, что это файл
                {
                    string extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
                    if (!string.IsNullOrEmpty(extension))
                    {
                        extensions.Add(extension);
                    }
                }
            }

            foreach (string ext in extensions.OrderBy(e => e))
            {
                extensionListItems.Add(new ExtensionItem { Extension = ext, IsChecked = true }); // По умолчанию все расширения выбраны
            }
        }

        private List<string> FilterFilesByExtension(List<string> files)
        {
            List<string> filteredFiles = new List<string>();
            HashSet<string> checkedExtensions = new HashSet<string>();

            // Собираем выбранные расширения из ListBox
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
                if (string.IsNullOrEmpty(extension) || checkedExtensions.Contains(extension)) // Включаем файлы без расширения или с выбранным расширением
                {
                    filteredFiles.Add(file);
                }
            }
            return filteredFiles;
        }

        public class ExtensionItem
        {
            public string Extension { get; set; }
            public bool IsChecked { get; set; }
            public string DisplayName => string.IsNullOrEmpty(Extension) ? "(без расширения)" : Extension; // Для отображения в ListBox
        }


        private void LocalFolderPathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            
            if (!string.IsNullOrWhiteSpace(LocalFolderPathTextBox.Text) && Directory.Exists(LocalFolderPathTextBox.Text))
            {
                PopulateDirectoryTreeView(LocalFolderPathTextBox.Text);
            }
        }
    }

}
