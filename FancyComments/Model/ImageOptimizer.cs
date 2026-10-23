using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FancyComments.Model
{
    /// <summary>
    /// A file ready to be written to Assets, possibly shrunk from the original.
    /// </summary>
    internal sealed class PreparedAsset
    {
        public string FileName { get; set; } = "";

        public byte[] Bytes { get; set; } = [];

        public long OriginalSize { get; set; }

        /// <summary>True if the bytes were re-encoded rather than copied as is.</summary>
        public bool Optimized { get; set; }
    }

    /// <summary>
    /// Shrinks images before they go into the repository, where every committed version stays forever.
    /// Anything wider than <see cref="MaxWidth"/> is scaled down; photos are saved as JPEG at <see cref="JpegQuality"/>,
    /// while screenshots and images with transparency stay PNG.
    /// </summary>
    internal static class ImageOptimizer
    {
        public const int MaxWidth = 1600;
        public const int JpegQuality = 85;

        // GIFs may be animated and SVGs are vector graphics, so those (and anything else) are stored untouched.
        private static readonly HashSet<string> Optimizable = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff",
        };

        public static PreparedAsset Prepare(string path, bool optimize)
        {
            byte[] original = File.ReadAllBytes(path);
            PreparedAsset asset = new PreparedAsset { FileName = Path.GetFileName(path), Bytes = original, OriginalSize = original.Length };
            string extension = Path.GetExtension(path);
            if (!optimize || !Optimizable.Contains(extension))
            {
                return asset;
            }

            BitmapSource image;
            try
            {
                image = Load(original);
            }
            catch (Exception)
            {
                // Not decodable after all (corrupt, or an unusual variant of the format): store it as is.
                return asset;
            }

            BitmapSource resized = Resize(image);
            byte[] bytes;
            string newExtension;
            if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                (bytes, newExtension) = (Jpeg(resized), ".jpg");
            }
            else if (HasTransparency(resized))
            {
                (bytes, newExtension) = (Png(resized), ".png");
            }
            else
            {
                // Screenshots compress well as PNG and stay crisp; only switch to JPEG when it's much smaller, as with photos.
                byte[] png = Png(resized);
                byte[] jpeg = Jpeg(resized);
                (bytes, newExtension) = jpeg.Length * 2 < png.Length ? (jpeg, ".jpg") : (png, ".png");
            }

            // Re-encoding an image that didn't need resizing is only worth it if it's clearly smaller.
            if (resized == image && bytes.Length > original.Length * 0.9)
            {
                return asset;
            }

            asset.Bytes = bytes;
            asset.FileName = Path.GetFileNameWithoutExtension(path) + newExtension;
            asset.Optimized = true;
            return asset;
        }

        private static BitmapSource Load(byte[] bytes)
        {
            using MemoryStream stream = new MemoryStream(bytes);
            BitmapFrame frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];

            // Phone photos are often stored sideways with an EXIF tag saying how to turn them.
            // Re-encoding drops the tag, so apply the rotation to the pixels instead.
            int rotation = Orientation(frame) switch
            {
                3 => 180,
                6 => 90,
                8 => 270,
                _ => 0,
            };
            return rotation == 0 ? frame : new TransformedBitmap(frame, new RotateTransform(rotation));
        }

        private static int Orientation(BitmapFrame frame)
        {
            try
            {
                return frame.Metadata is BitmapMetadata metadata && metadata.GetQuery("/app1/ifd/{ushort=274}") is ushort orientation
                    ? orientation
                    : 1;
            }
            catch (Exception e) when (e is NotSupportedException or InvalidOperationException or ArgumentException)
            {
                // Formats without EXIF (PNG, BMP) don't support the query.
                return 1;
            }
        }

        private static BitmapSource Resize(BitmapSource image)
        {
            if (image.PixelWidth <= MaxWidth)
            {
                return image;
            }

            int height = (int)Math.Round(image.PixelHeight * (double)MaxWidth / image.PixelWidth);
            DrawingVisual visual = new DrawingVisual();
            RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
            using (DrawingContext context = visual.RenderOpen())
            {
                context.DrawImage(image, new Rect(0, 0, MaxWidth, height));
            }

            RenderTargetBitmap target = new RenderTargetBitmap(MaxWidth, height, 96, 96, PixelFormats.Pbgra32);
            target.Render(visual);
            return target;
        }

        private static bool HasTransparency(BitmapSource image)
        {
            // Many screenshots have an alpha channel that's fully opaque, so check the pixels, not just the format.
            FormatConvertedBitmap bgra = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
            int stride = bgra.PixelWidth * 4;
            byte[] pixels = new byte[stride * bgra.PixelHeight];
            bgra.CopyPixels(pixels, stride, 0);
            for (int i = 3; i < pixels.Length; i += 4)
            {
                if (pixels[i] != 255)
                {
                    return true;
                }
            }

            return false;
        }

        private static byte[] Png(BitmapSource image) => Encode(new PngBitmapEncoder(), image);

        private static byte[] Jpeg(BitmapSource image) =>
            Encode(new JpegBitmapEncoder { QualityLevel = JpegQuality }, new FormatConvertedBitmap(image, PixelFormats.Bgr24, null, 0));

        private static byte[] Encode(BitmapEncoder encoder, BitmapSource image)
        {
            encoder.Frames.Add(BitmapFrame.Create(image));
            using MemoryStream stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }
    }
}
