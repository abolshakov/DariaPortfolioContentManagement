using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ContentManagement
{
    internal static class PersistenceManager
    {
        private const string FileStart = "export const PORTFOLIO = ";

        private static readonly string ImagesPath = Path.GetFullPath(ConfigurationManager.AppSettings["ImagesFolderPath"]);
        private static readonly string PortfolioPath = Path.GetFullPath(ConfigurationManager.AppSettings["PortfolioFilePath"]);
        private static readonly string IdsPath = Path.GetFullPath(ConfigurationManager.AppSettings["IdsFilePath"]);
        private static readonly string CompressedIdsPath = Path.GetFullPath(ConfigurationManager.AppSettings["CompressedIdsFilePath"]);

        private static readonly Registrar Registrar = new Registrar();
        private static readonly ImageCompressor Compressor = new ImageCompressor();

        public static int NextId => Registrar.NextId();

        public static AutoResetEvent AllDone => Compressor.AllDone;

        public static Portfolio ImportData()
        {
            InitializeRegistrar();

            var content = File.ReadAllText(PortfolioPath);
            var regEx = new Regex(FileStart + "(.+);",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var match = regEx.Match(content);

            if (!match.Success)
                throw new InvalidOperationException("Cannot import items data. Invalid file format.");

            var arrayText = match.Groups[1];
            var json = $"{{projects:{arrayText.Value}}}";
            var serializer = JsonSerializer.CreateDefault();

            using (var textReader = new StringReader(json))
            {
                Portfolio result;
                using (var jsonReader = new JsonTextReader(textReader))
                {
                    result = serializer.Deserialize<Portfolio>(jsonReader);
                }

                var loadedIds = ProcessCollectionsAsync(result);
                InitializeImageCompressor(loadedIds);

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
                serializer.Serialize(jsonWriter, portfolio.Projects);

                content = $"{FileStart}{textWriter};\n";
            }

            File.WriteAllText(PortfolioPath, content);

            ExportRegistrar();
            ExportCompressor();
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
            int id;

            if (item is Project previewItem)
            {
                id = previewItem.Id;
                currentImage = previewItem.Image;
                subfolder = string.Empty;
                fileName = CreateFileNameWithoutExtension(previewItem);
            }
            else if (item is ProjectItem projectItem)
            {
                id = projectItem.Id;
                currentImage = projectItem.Image;
                subfolder = EvaluateOrCreateChildFolderName(projectItem.Parent);
                fileName = CreateFileNameWithoutExtension();
            }
            else
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(currentImage))
            {
                DeleteImage(id, currentImage);
            }
            fileName += Path.GetExtension(sourcePath);
            var target = Path.Combine(ImagesPath, subfolder, fileName);
            File.Copy(sourcePath, target, true);

            return CreateImageName(subfolder, fileName);
        }

        public static async Task OptimizeImages(Portfolio portfolio)
        {
            var tasks = new List<Task>();

            foreach (var portfolioItem in portfolio.Projects)
            {
                tasks.AddRange(portfolioItem.Items.Select(projectItem => OptimizeImageAsync(projectItem.Id, projectItem.Image, false)));
                tasks.Add(OptimizeImageAsync(portfolioItem.Id, portfolioItem.Image, true));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        public static async Task OptimizeImageAsync(int itemId, string relativeImagePath, bool isPreview, bool force = false)
        {
            if (string.IsNullOrEmpty(relativeImagePath))
            {
                return;
            }
            var filePath = Path.Combine(ImagesPath, relativeImagePath);
            var bytes = File.ReadAllBytes(filePath);
            var (compressed, result) = await Compressor.OptimizeImageAsync(itemId, bytes, isPreview, force).ConfigureAwait(false);

            if (compressed)
            {
                File.WriteAllBytes(filePath, result);
            }
        }

		private static IEnumerable<int> ProcessCollectionsAsync(Portfolio portfolio)
        {
            var loadedIds = new HashSet<int>();
            foreach (var portfolioItem in portfolio.Projects)
            {
                foreach (var projectItem in portfolioItem.Items)
                {
                    projectItem.Parent = portfolioItem;
                    loadedIds.Add(projectItem.Id);
                }
                loadedIds.Add(portfolioItem.Id);
            }

            return loadedIds;
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

        private static void InitializeImageCompressor(IEnumerable<int> loadedIds)
        {
            var content = File.ReadAllText(CompressedIdsPath);
            var serializer = JsonSerializer.CreateDefault();

            using (var textReader = new StringReader(content))
            {
                IEnumerable<int> compressed;
                using (var jsonReader = new JsonTextReader(textReader))
                {
                    compressed = serializer.Deserialize<IEnumerable<int>>(jsonReader);
                }

                var verified = compressed.Intersect(loadedIds);

                Compressor.RegisterIds(verified);
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

        private static void ExportCompressor()
        {
            using (var textWriter = new StringWriter())
            using (var jsonWriter = new JsonTextWriter(textWriter))
            {
                var serializer = JsonSerializer.CreateDefault();
                serializer.Formatting = Formatting.Indented;
                serializer.Serialize(jsonWriter, Compressor.RegisteredIds());

                File.WriteAllText(CompressedIdsPath, textWriter.ToString());
            }
        }

        private static string CreateImageName(string subfolder, string fileName)
        {
            return string.IsNullOrEmpty(subfolder) ? fileName : $"{subfolder}/{fileName}";
        }

        public static void RenamePreviewItemImage(Project project)
        {
            if (string.IsNullOrEmpty(project.Image))
                return;

            project.Image = RenameImage(project.Image, string.Empty, CreateFileNameWithoutExtension(project));

            if (!project.Items.Any())
                return;

            var oldValue = EvaluateChildFolderName(project);

            if (string.IsNullOrEmpty(oldValue))
                return;

            var newValue = CreateChildFolderName(project);

            foreach (var projectItem in project.Items)
            {
                if (string.IsNullOrEmpty(projectItem.Image))
                    continue;

                projectItem.Image = projectItem.Image.Replace(oldValue + "/", newValue + "/");
            }

            RenameFolder(oldValue, newValue);
        }

        public static void RenameProjectItemImage(ProjectItem projectItem)
        {
            if (string.IsNullOrEmpty(projectItem.Image))
                return;

            var childFolder = EvaluateOrCreateChildFolderName(projectItem.Parent);
            projectItem.Image = RenameImage(projectItem.Image, childFolder, CreateFileNameWithoutExtension());
        }

        private static string CreateFileNameWithoutExtension(Project project)
        {
            return string.IsNullOrEmpty(project.Title)
              ? Guid.NewGuid().ToString().ToLower()
              : project.Title.ToHyphenCase();
        }

        private static string CreateFileNameWithoutExtension()
        {
            return Guid.NewGuid().ToString().ToLower();
        }

        private static string EvaluateChildFolderName(Project project)
        {
            var image = project.Items?.FirstOrDefault(x => !string.IsNullOrEmpty(x.Image))?.Image;

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

        private static string CreateChildFolderName(Project project)
        {
            return string.IsNullOrEmpty(project.Title)
              ? Guid.NewGuid().ToString().ToLower()
              : project.Title.ToHyphenCase();
        }

        private static string EvaluateOrCreateChildFolderName(Project project)
        {
            var childFolder = EvaluateChildFolderName(project);

            if (!string.IsNullOrEmpty(childFolder))
                return childFolder;

            childFolder = CreateChildFolderName(project);
            Directory.CreateDirectory(Path.Combine(ImagesPath, childFolder));

            return childFolder;
        }

        private static void RenameFolder(string oldFolderPath, string newFolderPath)
        {
            var oldPath = Path.Combine(ImagesPath, oldFolderPath);
            var newPath = Path.Combine(ImagesPath, newFolderPath);
            Directory.Move(oldPath, newPath);
        }

        public static void DeleteChildFolder(Project project)
        {
            var childFolder = EvaluateChildFolderName(project);

            if (string.IsNullOrEmpty(childFolder))
                return;

            var path = Path.Combine(ImagesPath, childFolder);

            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        public static void DeleteImage(int id, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return;

            var path = Path.Combine(ImagesPath, relativePath);

            if (File.Exists(path))
            {
                File.Delete(path);
            }
            Compressor.RemoveId(id);
        }
    }
}
