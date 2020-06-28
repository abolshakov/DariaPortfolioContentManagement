using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ContentManagement
{
    internal class ImageCompressor
    {
        private static readonly int PreviewMaxWidth = int.Parse(ConfigurationManager.AppSettings["PreviewImageMaxWidth"]);
        private static readonly int PreviewMaxHeight = int.Parse(ConfigurationManager.AppSettings["PreviewImageMaxHeight"]);
        private static readonly int ImageMaxWidth = int.Parse(ConfigurationManager.AppSettings["ImageMaxWidth"]);
        private static readonly int ImageMaxHeight = int.Parse(ConfigurationManager.AppSettings["ImageMaxHeight"]);

        private readonly HashSet<int> _compressed;

        public ImageCompressor()
        {
	        _compressed = new HashSet<int>();
        }

        public AutoResetEvent AllDone => TinifyClient.AllDone;

        public void RegisterIds(IEnumerable<int> existing)
        {
	        foreach (var value in existing)
	        {
		        _compressed.Add(value);
	        }
        }

        public void RemoveId(int id)
        {
	        _compressed.Remove(id);
        }

        public IEnumerable<int> RegisteredIds()
        {
	        return _compressed;
        }

        public async Task<(bool Compressed, byte[] Bytes)> OptimizeImageAsync(int itemId, byte[] bytes, bool isPreview, bool force = false)
        {
			Debug.WriteLine($"Optimize Image {itemId}...");
	        if (!force && _compressed.Contains(itemId))
	        {
		        Debug.WriteLine($"...image {itemId} already compressed");
		        return (false, bytes);
	        }
	        var width = isPreview ? PreviewMaxWidth : ImageMaxWidth;
	        var height = isPreview ? PreviewMaxHeight : ImageMaxHeight;

            using (var stream = new MemoryStream(bytes))
            {
                using (var image = Image.FromStream(stream))
                {
                    if (image.RawFormat.Equals(ImageFormat.Gif))
                    {
	                    Debug.WriteLine($"...skipped GIF image {itemId}");
	                    _compressed.Add(itemId);
                        return (false, bytes);
                    }
                }
            }
            var result = await TinifyClient.Fit(bytes, width, height).ConfigureAwait(false);
            Debug.WriteLine($"...image {itemId} compressed");
            _compressed.Add(itemId);

            return (true, result);
        }
    }
}
