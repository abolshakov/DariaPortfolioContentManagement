using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;

namespace ContentManagement
{
    public static class ExtensionMethods
    {
        public static string ToHyphenCase(this string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;

            var builder = new StringBuilder();
            builder.Append(str[0]);

            for (var index = 1; index < str.Length; index++)
            {
                var c = str[index];
                var previous = builder[builder.Length - 1];

                if (!char.IsDigit(c) && !char.IsLetter(c))
                {
                    if (previous != '-')
                    {
                        builder.Append('-');
                    }

                    continue;
                }

                if (char.IsLower(c) && char.IsLower(previous) ||
                    previous == '-' ||
                    char.IsUpper(c) && char.IsUpper(previous) ||
                    char.IsDigit(c) && char.IsDigit(previous) ||
                    char.IsLower(c) && char.IsUpper(previous) &&
                    (index == 1 || index > 1 && builder[builder.Length - 2] == '-'))
                {
                    builder.Append(c);
                    continue;
                }

                if (previous != '-')
                {
                    builder.Append('-');
                }

                builder.Append(c);
            }

            return builder.ToString().Trim('-').ToLower();
        }

        public static Image Resize(this Image image, int width, int height, bool crop = false)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            float imageWidth = image.Width;
            float imageHeight = image.Height;

            if (image.Width == 0 || image.Height == 0 || width == 0 || height == 0)
                return new Bitmap(width, height);

            var aspect = imageWidth / imageHeight;
            var deltaWidth = width / imageWidth;
            var deltaHeight = height / imageHeight;

            float newHeight;
            float newWidth;

            if (deltaWidth < deltaHeight)
            {
                newWidth = width;
                newHeight = newWidth / aspect;
            }
            else
            {
                newHeight = height;
                newWidth = aspect * newHeight;
            }

            Rectangle destRect;
            Bitmap destImage;

            if (crop)
            {
                destRect = new Rectangle(0, 0, (int)newWidth, (int)newHeight);
                destImage = new Bitmap((int)newWidth, (int)newHeight);
            }
            else
            {
                destRect = new Rectangle((int)((width - newWidth) / 2), (int)((height - newHeight) / 2), (int)newWidth,
                  (int)newHeight);
                destImage = new Bitmap(width, height);
            }

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, imageWidth, imageHeight, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }

        public static Image ReduceToFit(this Image image, int width, int height)
        {
            if (width >= image.Width && height >= image.Height)
            {
                width = image.Width;
                height = image.Height;
            }

            return image.Resize(width, height, true);
        }
    }
}
