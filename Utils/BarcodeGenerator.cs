using System;
using System.IO;
using SkiaSharp;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.QrCode.Internal;

namespace JHLabel.Utils
{
    public static class BarcodeHelper
    {
        /// <summary>
        /// ZXing을 이용하여 Code128 바코드 전체 모듈(Bar) 수를 계산합니다.
        /// Quiet Zone(보통 10 modules) 포함 여부는 별도 처리해야 합니다.
        /// </summary>
        public static int GetCode128ModuleCount(string data)
        {
            var writer = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    // width=0, height=0으로 설정해놓고 RAW 바코드만 인코딩 시도
                    Width = 0,
                    Height = 0,
                    Margin = 0,
                    PureBarcode = true
                }
            };
            var pixelData = writer.Write(data);
            // pixelData.Width == 전체 바코드 너비(도트 단위) -> 여기서 각 도트=1 모듈
            // 하지만 실제 ZXing은 1 bar = 1 pixel 처리
            // => 곧 pixelData.Width가 "모듈 수"와 동일
            return pixelData.Width;
        }

        /// <summary>
        /// ZXing을 이용하여 QR 코드 모듈 수를 계산합니다.
        /// </summary>
        public static int GetQrModuleCount(string data)
        {
            var writer = new QRCodeWriter();
            var matrix = writer.encode(data, BarcodeFormat.QR_CODE, 0, 0);
            return matrix.Width; // 모듈 수
        }

        /// <summary>
        /// WYSIWYG 용으로 바코드를 정확히 width x height 도트 크기에 맞춰 그리는 ImageSource 생성
        /// (너비/높이를 강제로 맞추어 '늘리거나 줄여' 렌더링)
        /// </summary>
        public static ImageSource GenerateExactBarcodeImage(string data, BarcodeFormat format, int totalWidthDots, int totalHeightDots)
        {
            // ZXing 기본 옵션
            var writer = new BarcodeWriterPixelData
            {
                Format = format,
                Options = new EncodingOptions
                {
                    Width = totalWidthDots,
                    Height = totalHeightDots,
                    Margin = 0,        // quiet zone 0
                    PureBarcode = true // 텍스트 제거
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
