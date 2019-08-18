using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace ContentManagement
{
    internal static class PersistenceManager
    {
        private const string FileStart = "export const PORTFOLIO = ";

        private static readonly string ImagesPath = Path.GetFullPath(ConfigurationManager.AppSettings["ImagesFolderPath"]);
        private static readonly string PortfolioPath = Path.GetFullPath(ConfigurationManager.AppSettings["PortfolioFilePath"]);
        private static readonly string IdsPath = Path.GetFullPath(ConfigurationManager.AppSettings["IdsFilePath"]);
        private static readonly Registrar Registrar = new Registrar();

        public static int NextId => Registrar.NextId();

        public static Portfolio ImportData()
        {
            InitializeRegistrar();

            var content = File.ReadAllText(PortfolioPath);
            var regEx = new Regex(FileStart + "(.+);", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var match = regEx.Match(content);

            if (!match.Success)
                throw new InvalidOperationException("Cannot import items data. Invalid file format.");

            var arrayText = match.Groups[1];
            var json = $"{{portfolioItems:{arrayText.Value}}}";
            var serializer = JsonSerializer.CreateDefault();

            using (var textReader = new StringReader(json))
            {
                Portfolio result;
                using (var jsonReader = new JsonTextReader(textReader))
                {
                    result = serializer.Deserialize<Portfolio>(jsonReader);
                }
                ProcessCollections(result);

                return result;
            }
        }

        public static void ExportData(Portfolio portfolio)
        {
            string content;

            using (var textWriter = new StringWriter())
            using (var jsonWriter = new JsonTextWriter(textWriter))
            {
                jsonWriter.QuoteChar = '\'';
                jsonWriter.QuoteName = false;

                var serializer = JsonSerializer.CreateDefault();
                serializer.NullValueHandling = NullValueHandling.Ignore;
                serializer.DefaultValueHandling = DefaultValueHandling.Ignore;
                serializer.Formatting = Formatting.Indented;
                serializer.Serialize(jsonWriter, portfolio.PortfolioItems);

                content = $"{FileStart}{textWriter};\n";
            }

            File.WriteAllText(PortfolioPath, content);

            ExportRegistrar();
        }

        public static string RelativeImagePath(string fullPath)
        {
            return FileHelper.RelativePath(fullPath, ImagesPath);
        }

        public static string FullImagePath(string relativePath)
        {
            return Path.Combine(ImagesPath, relativePath);
        }

        public static IEnumerable<string> GetAllImages()
        {
            return FileHelper.FilterFiles(ImagesPath, "png", "jpg", "gif");
        }

        public static Image LoadImage(string imagePath)
        {
            return Image.FromFile(Path.Combine(ImagesPath, imagePath));
        }

        public static string SaveImage(object item, string sourcePath)
        {
            if (string.IsNullOrEmpty(sourcePath))
                return null;

            string subfolder, fileName, currentImage;

            if (item is PortfolioItem previewItem)
            {
                currentImage = previewItem.Image;
                subfolder = string.Empty;
                fileName = CreateFileNameWithoutExtension(previewItem);
            }
            else if (item is ProjectItem portfolioItem)
            {
                currentImage = portfolioItem.Image;
                subfolder = EvaluateOrCreateChildFolderName(portfolioItem.Parent);
                fileName = CreateFileNameWithoutExtension(portfolioItem);
            }
            else
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(currentImage))
            {
                DeleteImage(currentImage);
            }
            fileName += Path.GetExtension(sourcePath);
            var target = Path.Combine(ImagesPath, subfolder, fileName);
            File.Copy(sourcePath, target, true);

            return CreateImageName(subfolder, fileName);
        }

        private static void ProcessCollections(Portfolio portfolio)
        {
            foreach (var portfolioItem in portfolio.PortfolioItems)
            {
                foreach (var projectItem in portfolioItem.ProjectItems)
                {
                    projectItem.Parent = portfolioItem;
                }
            }
        }

        private static void InitializeRegistrar()
        {
            var content = File.ReadAllText(IdsPath);
            var serializer = JsonSerializer.CreateDefault();

            using (var textReader = new StringReader(content))
            {
                IEnumerable<int> result;
                using (var jsonReader = new JsonTextReader(textReader))
                {
                    result = serializer.Deserialize<IEnumerable<int>>(jsonReader);
                }

                Registrar.RegisterIds(result);
            }
        }

        private static void ExportRegistrar()
        {
            using (var textWriter = new StringWriter())
            using (var jsonWriter = new JsonTextWriter(textWriter))
            {
                var serializer = JsonSerializer.CreateDefault();
                serializer.Formatting = Formatting.Indented;
                serializer.Serialize(jsonWriter, Registrar.RegisteredIds());

                File.WriteAllText(IdsPath, textWriter.ToString());
            }
        }

        private static string CreateImageName(string subfolder, string fileName)
        {
            return string.IsNullOrEmpty(subfolder) ? fileName : $"{subfolder}/{fileName}";
        }

        public static void RenamePreviewItemImage(PortfolioItem portfolioItem)
        {
            if (string.IsNullOrEmpty(portfolioItem.Image))
                return;

            portfolioItem.Image = RenameImage(portfolioItem.Image, string.Empty, CreateFileNameWithoutExtension(portfolioItem));

            if (!portfolioItem.ProjectItems.Any())
                return;

            var oldValue = EvaluateChildFolderName(portfolioItem);

            if (string.IsNullOrEmpty(oldValue))
                return;

            var newValue = CreateChildFolderName(portfolioItem);

            foreach (var projectItem in portfolioItem.ProjectItems)
            {
                if (string.IsNullOrEmpty(projectItem.Image))
                    continue;

                projectItem.Image = projectItem.Image.Replace(oldValue + "/", newValue + "/");
            }

            RenameFolder(oldValue, newValue);
        }

        public static void RenamePortfolioItemImage(ProjectItem projectItem)
        {
            if (string.IsNullOrEmpty(projectItem.Image))
                return;

            var childFolder = EvaluateOrCreateChildFolderName(projectItem.Parent);
            projectItem.Image = RenameImage(projectItem.Image, childFolder, CreateFileNameWithoutExtension(projectItem));
        }

        private static string CreateFileNameWithoutExtension(PortfolioItem portfolioItem)
        {
            return string.IsNullOrEmpty(portfolioItem.Title)
              ? Guid.NewGuid().ToString().ToLower()
              : portfolioItem.Title.ToHyphenCase();
        }

        private static string CreateFileNameWithoutExtension(ProjectItem projectItem)
        {
            return string.IsNullOrEmpty(projectItem.Description)
              ? Guid.NewGuid().ToString().ToLower()
              : projectItem.Description.ToHyphenCase();
        }

        private static string EvaluateChildFolderName(PortfolioItem portfolioItem)
        {
            var image = portfolioItem.ProjectItems?.FirstOrDefault(x => !string.IsNullOrEmpty(x.Image))?.Image;

            return image?.Substring(0, image.IndexOf('/'));
        }

        private static string RenameImage(string oldFileName, string subfolder, string newFileNameWithoutExtension)
        {
            var oldPath = Path.Combine(ImagesPath, oldFileName);
            var extension = Path.GetExtension(oldFileName);
            var fileName = newFileNameWithoutExtension + extension;
            var newPath = Path.Combine(ImagesPath, subfolder, fileName);
            File.Move(oldPath, newPath);

            return CreateImageName(subfolder, fileName);
        }

        private static string CreateChildFolderName(PortfolioItem portfolioItem)
        {
            return string.IsNullOrEmpty(portfolioItem.Title)
              ? Guid.NewGuid().ToString().ToLower()
              : portfolioItem.Title.ToHyphenCase();
        }

        private static string EvaluateOrCreateChildFolderName(PortfolioItem portfolioItem)
        {
            var childFolder = EvaluateChildFolderName(portfolioItem);

            if (!string.IsNullOrEmpty(childFolder))
                return childFolder;

            childFolder = CreateChildFolderName(portfolioItem);
            Directory.CreateDirectory(Path.Combine(ImagesPath, childFolder));

            return childFolder;
        }

        private static void RenameFolder(string oldFolderPath, string newFolderPath)
        {
            var oldPath = Path.Combine(ImagesPath, oldFolderPath);
            var newPath = Path.Combine(ImagesPath, newFolderPath);
            Directory.Move(oldPath, newPath);
        }

        public static void DeleteChildFolder(PortfolioItem portfolioItem)
        {
            var childFolder = EvaluateChildFolderName(portfolioItem);

            if (string.IsNullOrEmpty(childFolder))
                return;

            var path = Path.Combine(ImagesPath, childFolder);

            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        public static void DeleteImage(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return;

            var path = Path.Combine(ImagesPath, relativePath);

            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
