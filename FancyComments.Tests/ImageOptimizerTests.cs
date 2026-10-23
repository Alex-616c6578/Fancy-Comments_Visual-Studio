using System.IO;
using System.Windows.Media.Imaging;
using FancyComments.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyComments.Tests
{
    [TestClass]
    public class ImageOptimizerTests
    {
        private string dir = "";

        [TestInitialize]
        public void CreateFolder()
        {
            this.dir = Path.Combine(Path.GetTempPath(), "fc-images-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.dir);
        }

        [TestCleanup]
        public void DeleteFolder() => Directory.Delete(this.dir, recursive: true);

        [TestMethod]
        public void Large_photos_are_scaled_to_1600_wide_and_saved_as_jpeg() => TestImages.Sta(() =>
        {
            string path = TestImages.Photo(Path.Combine(this.dir, "photo.png"), 4000, 3000);

            PreparedAsset asset = ImageOptimizer.Prepare(path, optimize: true);

            Assert.IsTrue(asset.Optimized);
            Assert.AreEqual("photo.jpg", asset.FileName);
            BitmapFrame image = TestImages.Decode(asset.Bytes);
            Assert.AreEqual(1600, image.PixelWidth);
            Assert.AreEqual(1200, image.PixelHeight);
            Assert.IsTrue(asset.Bytes.Length < asset.OriginalSize / 10, $"{asset.OriginalSize} -> {asset.Bytes.Length}");
        });

        [TestMethod]
        public void Screenshots_stay_png_so_text_stays_sharp() => TestImages.Sta(() =>
        {
            string path = TestImages.Screenshot(Path.Combine(this.dir, "screenshot.png"), 2560, 1440);

            PreparedAsset asset = ImageOptimizer.Prepare(path, optimize: true);

            Assert.AreEqual("screenshot.png", asset.FileName);
            Assert.AreEqual(1600, TestImages.Decode(asset.Bytes).PixelWidth);
        });

        [TestMethod]
        public void Transparent_images_stay_png() => TestImages.Sta(() =>
        {
            string path = TestImages.Transparent(Path.Combine(this.dir, "logo.png"), 2000, 800);

            PreparedAsset asset = ImageOptimizer.Prepare(path, optimize: true);

            Assert.AreEqual("logo.png", asset.FileName);
        });

        [TestMethod]
        public void Small_images_that_dont_shrink_much_are_kept_as_they_are() => TestImages.Sta(() =>
        {
            string path = TestImages.Screenshot(Path.Combine(this.dir, "small.png"), 300, 168);

            PreparedAsset asset = ImageOptimizer.Prepare(path, optimize: true);

            Assert.IsFalse(asset.Optimized);
            CollectionAssert.AreEqual(File.ReadAllBytes(path), asset.Bytes);
        });

        [TestMethod]
        public void Sideways_phone_photos_are_turned_upright() => TestImages.Sta(() =>
        {
            // EXIF orientation 6: stored 2000x1000, meant to be shown rotated 90° (1000x2000).
            string path = TestImages.Jpeg(Path.Combine(this.dir, "phone.jpg"), 2000, 1000, orientation: 6);

            BitmapFrame image = TestImages.Decode(ImageOptimizer.Prepare(path, optimize: true).Bytes);

            Assert.IsTrue(image.PixelHeight > image.PixelWidth, $"{image.PixelWidth}x{image.PixelHeight}");
        });

        [TestMethod]
        public void The_same_input_always_gives_the_same_bytes() => TestImages.Sta(() =>
        {
            // The duplicate check compares stored bytes, so optimizing must be deterministic.
            string path = TestImages.Photo(Path.Combine(this.dir, "photo.png"), 2400, 1600);

            CollectionAssert.AreEqual(ImageOptimizer.Prepare(path, true).Bytes, ImageOptimizer.Prepare(path, true).Bytes);
        });

        [TestMethod]
        public void Turning_optimization_off_keeps_the_original() => TestImages.Sta(() =>
        {
            string path = TestImages.Photo(Path.Combine(this.dir, "photo.png"), 2400, 1600);

            PreparedAsset asset = ImageOptimizer.Prepare(path, optimize: false);

            Assert.IsFalse(asset.Optimized);
            Assert.AreEqual("photo.png", asset.FileName);
            CollectionAssert.AreEqual(File.ReadAllBytes(path), asset.Bytes);
        });

        [TestMethod]
        [DataRow("animation.gif")]
        [DataRow("diagram.svg")]
        [DataRow("spec.pdf")]
        [DataRow("clip.mp4")]
        public void Other_files_are_stored_untouched(string name)
        {
            string path = Path.Combine(this.dir, name);
            File.WriteAllBytes(path, [1, 2, 3, 4]);

            PreparedAsset asset = ImageOptimizer.Prepare(path, optimize: true);

            Assert.IsFalse(asset.Optimized);
            Assert.AreEqual(name, asset.FileName);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, asset.Bytes);
        }

        [TestMethod]
        public void Corrupt_images_are_stored_as_they_are()
        {
            string path = Path.Combine(this.dir, "broken.png");
            File.WriteAllBytes(path, [1, 2, 3, 4]);

            PreparedAsset asset = ImageOptimizer.Prepare(path, optimize: true);

            Assert.IsFalse(asset.Optimized);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, asset.Bytes);
        }
    }
}
