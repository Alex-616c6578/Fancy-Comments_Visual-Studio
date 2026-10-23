using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FancyComments.Model;

namespace FancyComments.Tests
{
    /// <summary>
    /// A throwaway solution folder: a .sln file plus an empty FancyComments store. Deleted on dispose.
    /// </summary>
    internal sealed class TempSolution : IDisposable
    {
        public TempSolution()
        {
            this.Dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "fc-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.Dir);
            File.WriteAllText(this.Path("Test.sln"), "");
            this.Store = CommentStore.ForDocument(this.Path("Code.cs"))!;
        }

        public string Dir { get; }

        public CommentStore Store { get; }

        public string Path(params string[] parts) => System.IO.Path.Combine([this.Dir, .. parts]);

        /// <summary>Writes a file under the solution, creating folders as needed.</summary>
        public string Write(string relativePath, string content)
        {
            string path = this.Path(relativePath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        /// <summary>Adds a comment JSON directly, the way a teammate's commit would.</summary>
        public void AddComment(string id, string markdown = "", params string[] assets) =>
            this.Write($@"FancyComments\Comments\{id}.json", Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                id,
                summary = "summary " + id,
                file = "src/Code.cs",
                markdown,
                assets,
            }));

        public void AddAsset(string name, string content = "x") => this.Write($@"FancyComments\Assets\{name}", content);

        public string[] Assets() =>
            Directory.Exists(this.Store.AssetsDir)
                ? Directory.GetFiles(this.Store.AssetsDir).Select(System.IO.Path.GetFileName).OrderBy(n => n).ToArray()!
                : [];

        public static string Marker(string id, string summary = "summary", string open = "/*", string close = "*/") =>
            new CommentSyntax(open, close).Format(CommentMarker.Tag(id), summary);

        public void Dispose()
        {
            try
            {
                Directory.Delete(this.Dir, recursive: true);
            }
            catch (IOException)
            {
                // A leftover temp folder isn't worth failing a test over.
            }
        }
    }

    /// <summary>
    /// Generated images for optimizer tests. WPF imaging needs an STA thread, so tests call <see cref="Sta"/>.
    /// </summary>
    internal static class TestImages
    {
        public static void Sta(Action action)
        {
            Exception? failure = null;
            Thread thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    failure = e;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure is not null)
            {
                throw new AggregateException(failure);
            }
        }

        /// <summary>Gradient plus noise: compresses badly as PNG, like a photo.</summary>
        public static string Photo(string path, int width, int height) =>
            Png(path, width, height, (x, y, random) =>
            {
                int noise = random.Next(40);
                return (B: (byte)((x * 215 / width) + noise), G: (byte)((y * 215 / height) + noise), R: (byte)(128 + noise), A: (byte)255);
            });

        /// <summary>Flat blocks with an opaque alpha channel, like a screenshot.</summary>
        public static string Screenshot(string path, int width, int height) =>
            Png(path, width, height, (x, y, _) =>
            {
                byte v = (byte)(((x / 200) + (y / 90)) % 2 == 0 ? 240 : 30);
                return (v, v, v, (byte)255);
            });

        public static string Transparent(string path, int width, int height) =>
            Png(path, width, height, (x, _, _) => ((byte)200, (byte)0, (byte)0, (byte)(x % 256)));

        public static string Jpeg(string path, int width, int height, ushort? orientation = null)
        {
            BitmapSource source = Pixels(width, height, (x, y, random) => ((byte)(x % 256), (byte)(y % 256), (byte)random.Next(256), (byte)255));
            JpegBitmapEncoder encoder = new JpegBitmapEncoder { QualityLevel = 95 };
            BitmapMetadata? metadata = null;
            if (orientation is { } value)
            {
                metadata = new BitmapMetadata("jpg");
                metadata.SetQuery("/app1/ifd/{ushort=274}", value);
            }

            encoder.Frames.Add(BitmapFrame.Create(new FormatConvertedBitmap(source, PixelFormats.Bgr24, null, 0), null, metadata, null));
            using FileStream stream = File.Create(path);
            encoder.Save(stream);
            return path;
        }

        public static BitmapFrame Decode(byte[] bytes) =>
            BitmapDecoder.Create(new MemoryStream(bytes), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];

        private static string Png(string path, int width, int height, Func<int, int, Random, (byte B, byte G, byte R, byte A)> pixel)
        {
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(Pixels(width, height, pixel)));
            using FileStream stream = File.Create(path);
            encoder.Save(stream);
            return path;
        }

        private static BitmapSource Pixels(int width, int height, Func<int, int, Random, (byte B, byte G, byte R, byte A)> pixel)
        {
            Random random = new Random(1);
            byte[] data = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    (byte b, byte g, byte r, byte a) = pixel(x, y, random);
                    int i = ((y * width) + x) * 4;
                    data[i] = b;
                    data[i + 1] = g;
                    data[i + 2] = r;
                    data[i + 3] = a;
                }
            }

            return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, data, width * 4);
        }
    }
}
