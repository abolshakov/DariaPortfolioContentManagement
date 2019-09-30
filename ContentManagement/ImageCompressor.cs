using System.Configuration;
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

        public static AutoResetEvent AllDone => TinifyClient.AllDone;

        public static async Task<(bool Compressed, byte[] Bytes)> OptimizeImageAsync(byte[] bytes, bool isPreview)
        {
	        var width = isPreview ? PreviewMaxWidth : ImageMaxWidth;
	        var height = isPreview ? PreviewMaxHeight : ImageMaxHeight;

            using (var stream = new MemoryStream(bytes))
            {
                using (var image = Image.FromStream(stream))
                {
                    if (image.RawFormat.Equals(ImageFormat.Gif) || image.Width <= width && image.Height <= height)
                    {
                        return (false, bytes);
                    }
                }
            }
            var result = await TinifyClient.Fit(bytes, width, height).ConfigureAwait(false);
            return (true, result);
        }
    }
}
