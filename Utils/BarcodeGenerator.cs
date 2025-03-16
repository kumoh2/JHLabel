using System.IO;
using SkiaSharp;
using ZXing;
using ZXing.Common;
using Microsoft.Maui.Controls;

namespace JHLabel.Utils
{
    public static class BarcodeGenerator
    {
        public static ImageSource GenerateBarcodeImage(string data, BarcodeFormat format, int width, int height)
        {
            var writer = new BarcodeWriterPixelData
            {
                Format = format,
                Options = new EncodingOptions
                {
                    Width = width,
                    Height = height,
                    Margin = 0
                }
            };

            var pixelData = writer.Write(data);

            using (var bitmap = new SKBitmap(pixelData.Width, pixelData.Height, SKColorType.Bgra8888, SKAlphaType.Premul))
            {
                System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bitmap.GetPixels(), pixelData.Pixels.Length);

                using (var image = SKImage.FromBitmap(bitmap))
                using (var dataStream = image.Encode(SKEncodedImageFormat.Png, 100))
                {
                    var bytes = dataStream.ToArray();
                    return ImageSource.FromStream(() => new MemoryStream(bytes));
                }
            }
        }
    }
}
