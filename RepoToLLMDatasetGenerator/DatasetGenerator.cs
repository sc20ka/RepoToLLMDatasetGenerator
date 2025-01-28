using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RepoToLLMDatasetGenerator
{
    public class DatasetGenerator
    {
        public List<string> GenerateDatasetStrings(List<string> selectedFiles, string rootDirectory)
        {
            List<string> outputStrings = new List<string>();

            outputStrings.Add("This file is a merged representation of the entire codebase, combining all repository files into a single document.\n");
            outputStrings.Add("================================================================");
            outputStrings.Add("File Summary");
            outputStrings.Add("================================================================");
            outputStrings.Add("(Metadata and usage AI instructions)\n");

            outputStrings.Add("================================================================");
            outputStrings.Add("Directory Structure");
            outputStrings.Add("================================================================");
            outputStrings.AddRange(GetDirectoryStructureStrings(selectedFiles, rootDirectory));
            outputStrings.Add("\n");

            outputStrings.Add("================================================================");
            outputStrings.Add("Files");
            outputStrings.Add("================================================================");
            outputStrings.AddRange(GetFileContentStrings(selectedFiles, rootDirectory));

            return outputStrings;
        }

        private List<string> GetDirectoryStructureStrings(List<string> selectedFiles, string rootDirectory)
        {
            List<string> directoryStructureStrings = new List<string>();
            HashSet<string> directories = new HashSet<string>();

            foreach (string filePath in selectedFiles)
            {
                string directoryPath = System.IO.Path.GetDirectoryName(filePath);
                if (directoryPath != null && directoryPath.StartsWith(rootDirectory))
                {
                    string relativeDirectoryPath = directoryPath.Substring(rootDirectory.Length).TrimStart(System.IO.Path.DirectorySeparatorChar);
                    if (!string.IsNullOrEmpty(relativeDirectoryPath))
                    {
                        directories.Add(relativeDirectoryPath);
                    }
                }
            }

            foreach (string dir in directories.OrderBy(d => d))
            {
                directoryStructureStrings.Add(dir + "/");
            }
            return directoryStructureStrings;
        }

        private List<string> GetFileContentStrings(List<string> selectedFiles, string rootDirectory)
        {
            List<string> fileContentStrings = new List<string>();

            foreach (string filePath in selectedFiles)
            {
                if (!File.Exists(filePath)) continue;
                string file = filePath.Substring(rootDirectory.Length).TrimStart(System.IO.Path.DirectorySeparatorChar);
                fileContentStrings.Add($"\n================");
                fileContentStrings.Add($"File: {file}");
                fileContentStrings.Add($"================");
                try
                {
                    string fileContent = File.ReadAllText(filePath);
                    fileContentStrings.Add(fileContent);
                }
                catch (Exception ex)
                {
                    fileContentStrings.Add($"\n--- Ошибка чтения файла: {file} ---\n{ex.Message}\n---");
                }
            }
            return fileContentStrings;
        }
    }
}