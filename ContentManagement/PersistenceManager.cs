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
		private static readonly string ImagesPath = Path.GetFullPath(ConfigurationManager.AppSettings["ImagesFolderPath"]);
		private static readonly string ItemsPath = Path.GetFullPath(ConfigurationManager.AppSettings["ItemsFilePath"]);

		private static int _id;

		public static int NextId => ++_id;

		public static Portfolio ImportData()
		{
			var content = File.ReadAllText(ItemsPath);
			var regEx = new Regex("export const PREVIEW_ITEMS = (.+);", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
			var match = regEx.Match(content);

			if (!match.Success)
				throw new InvalidOperationException("Cannot import items data. Invalid file format.");

			var arrayText = match.Groups[1];
			var json = $"{{previewItems:{arrayText.Value}}}";
			var serializer = JsonSerializer.CreateDefault();

			using (var textReader = new StringReader(json))
			{
				Portfolio result;
				using (var jsonReader = new JsonTextReader(textReader))
				{
					result = serializer.Deserialize<Portfolio>(jsonReader);
				}
				AssignImageOwnerIds(result);

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
				serializer.Serialize(jsonWriter, portfolio.PreviewItems);

				content = $"export const PREVIEW_ITEMS = {textWriter};";
			}

			File.WriteAllText(ItemsPath, content);
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

			if (item is PreviewItem previewItem)
			{
				currentImage = previewItem.Image;
				subfolder = string.Empty;
				fileName = CreateFileNameWithoutExtension(previewItem);
			}
			else if (item is PortfolioItem portfolioItem)
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

		private static void AssignImageOwnerIds(Portfolio portfolio)
		{

			foreach (var previewItem in portfolio.PreviewItems)
			{
				previewItem.Id = NextId;
				foreach (var portfolioItem in previewItem.PortfolioItems)
				{
					portfolioItem.Parent = previewItem;
					portfolioItem.Id = NextId;
				}
			}
		}

		private static string CreateImageName(string subfolder, string fileName)
		{
			return string.IsNullOrEmpty(subfolder) ? fileName : $"{subfolder}/{fileName}";
		}

		public static void RenamePreviewItemImage(PreviewItem previewItem)
		{
			if (string.IsNullOrEmpty(previewItem.Image))
				return;

			previewItem.Image = RenameImage(previewItem.Image, string.Empty, CreateFileNameWithoutExtension(previewItem));

			if (!previewItem.PortfolioItems.Any())
				return;

			var oldValue = EvaluateChildFolderName(previewItem);

			if (string.IsNullOrEmpty(oldValue))
				return;

			var newValue = CreateChildFolderName(previewItem);

			foreach (var portfolioItem in previewItem.PortfolioItems)
			{
				if (string.IsNullOrEmpty(portfolioItem.Image))
					continue;

				portfolioItem.Image = portfolioItem.Image.Replace(oldValue + "/", newValue + "/");
			}

			RenameFolder(oldValue, newValue);
		}

		public static void RenamePortfolioItemImage(PortfolioItem portfolioItem)
		{
			if (string.IsNullOrEmpty(portfolioItem.Image))
				return;

			var childFolder = EvaluateOrCreateChildFolderName(portfolioItem.Parent);
			portfolioItem.Image = RenameImage(portfolioItem.Image, childFolder, CreateFileNameWithoutExtension(portfolioItem));
		}

		private static string CreateFileNameWithoutExtension(PreviewItem previewItem)
		{
			return string.IsNullOrEmpty(previewItem.Title)
			  ? Guid.NewGuid().ToString().ToLower()
			  : previewItem.Title.ToHyphenCase();
		}

		private static string CreateFileNameWithoutExtension(PortfolioItem portfolioItem)
		{
			return string.IsNullOrEmpty(portfolioItem.Description)
			  ? Guid.NewGuid().ToString().ToLower()
			  : portfolioItem.Description.ToHyphenCase();
		}

		private static string EvaluateChildFolderName(PreviewItem previewItem)
		{
			var image = previewItem.PortfolioItems?.FirstOrDefault(x=>!string.IsNullOrEmpty(x.Image))?.Image;

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

		private static string CreateChildFolderName(PreviewItem previewItem)
		{
			return string.IsNullOrEmpty(previewItem.Title)
			  ? Guid.NewGuid().ToString().ToLower()
			  : previewItem.Title.ToHyphenCase();
		}

		private static string EvaluateOrCreateChildFolderName(PreviewItem previewItem)
		{
			var childFolder = EvaluateChildFolderName(previewItem);

			if (!string.IsNullOrEmpty(childFolder))
				return childFolder;

			childFolder = CreateChildFolderName(previewItem);
			Directory.CreateDirectory(Path.Combine(ImagesPath, childFolder));

			return childFolder;
		}

		private static void RenameFolder(string oldFolderPath, string newFolderPath)
		{
			var oldPath = Path.Combine(ImagesPath, oldFolderPath);
			var newPath = Path.Combine(ImagesPath, newFolderPath);
			Directory.Move(oldPath, newPath);
		}

		public static void DeleteChildFolder(PreviewItem previewItem)
		{
			var childFolder = EvaluateChildFolderName(previewItem);

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
