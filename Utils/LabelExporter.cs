using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Layouts;
using Microsoft.Maui.Devices;
using ZXing;
using ZXing.QrCode;
using SkiaSharp;
using BinaryKits.Zpl.Label.Helpers; // ByteHelper

namespace JHLabel.Utils
{
    public class LabelExporter
    {
        private readonly IEnumerable<View> _views;
        private readonly View _skipView;
        private readonly Func<double, double> _screenPixelsToMm;
        private readonly int _printerDpi;

        public LabelExporter(
            IEnumerable<View> views, 
            View skipView,
            Func<double, double> screenPixelsToMm, 
            int printerDpi)
        {
            _views = views;
            _skipView = skipView;
            _screenPixelsToMm = screenPixelsToMm;
            _printerDpi = printerDpi;
        }

        public string GenerateZPL()
        {
            var zpl = "^XA\n";

            foreach (var view in _views)
            {
                if (view == _skipView) 
                    continue;

                var bounds = (Rect)((BindableObject)view).GetValue(AbsoluteLayout.LayoutBoundsProperty);
                int xDots = MmToDots(_screenPixelsToMm(bounds.X));
                int yDots = MmToDots(_screenPixelsToMm(bounds.Y));
                int wDots = MmToDots(_screenPixelsToMm(bounds.Width));
                int hDots = MmToDots(_screenPixelsToMm(bounds.Height));
                if (wDots < 1) wDots = 1;
                if (hDots < 1) hDots = 1;

                // Text:문자열|폰트사이즈
                if (view is Label lbl && lbl.ClassId != null && lbl.ClassId.StartsWith("Text:"))
                {
                    var classBody = lbl.ClassId.Substring("Text:".Length);
                    var parts = classBody.Split('|');
                    if (parts.Length == 2)
                    {
                        string textPart = parts[0];
                        if (int.TryParse(parts[1], out int fontDots))
                        {
                            string gfCommand = ConvertTextToGF(textPart, fontDots);
                            zpl += $"^FO{xDots},{yDots}{gfCommand}\n";
                        }
                    }
                }
                else if (view is Image img && !string.IsNullOrEmpty(img.ClassId))
                {
                    // 바코드 로직은 기존과 동일
                    // ...
                }
            }

            zpl += "^XZ";
            return zpl;
        }

        public string GeneratePGL()
        {
            // 필요 시 구현
            return "<PGL_START>\n<PGL_END>";
        }

        private int MmToDots(double mm)
        {
            return (int)Math.Round(mm * _printerDpi / 25.4);
        }

        /// <summary>
        /// 도트 -> 화면픽셀 (DeviceDisplay.MainDisplayInfo.Density 이용)
        /// </summary>
        private float DotsToScreenPx(int dots)
        {
            double density = DeviceDisplay.MainDisplayInfo.Density;
            return (float)(dots / density);
        }

        /// <summary>
        /// 텍스트를 내부적으로 2배 해상도로 그린 뒤 1/2로 축소하여 1비트 이미지로 변환 (간단한 해상도 개선)
        /// 텍스트 하단 잘림 방지를 위해 leading과 여유 픽셀(+4)도 포함
        /// </summary>
        private string ConvertTextToGF(string text, int fontDots)
        {
            // 내부 렌더링 스케일팩터
            float scaleFactor = 2f;
            
            // 스크린 픽셀로 변환한 뒤 scaleFactor를 곱
            float baseFontSize = DotsToScreenPx(fontDots);
            float scaledFontSize = baseFontSize * scaleFactor;

            using var paint = new SKPaint
            {
                TextSize = scaledFontSize,
                IsAntialias = true,
                Color = SKColors.Black,
                Typeface = SKTypeface.FromFamilyName("Open Sans")
            };

            paint.GetFontMetrics(out SKFontMetrics metrics);
            // 실제 텍스트 폭 계산
            float textWidth = paint.MeasureText(text);

            // h = (Descent - Ascent + Leading) + 마진
            float fullHeight = (metrics.Descent - metrics.Ascent) + metrics.Leading + 4; 
            int width = (int)Math.Ceiling(textWidth) + 4;
            int height = (int)Math.Ceiling(fullHeight);

            // 내부 캔버스(2배)
            var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);

            // baseline에 맞춰 그리기
            float baselineY = -metrics.Ascent + 2; // +2 여유
            canvas.DrawText(text, 2, baselineY, paint);

            // 큰 사이즈(bitmap)에서 Snapshot
            using var bigImage = surface.Snapshot();
            using var bigBitmap = SKBitmap.FromImage(bigImage);

            // (선택) 1/2로 축소
            // 최종 도트 크기: width/scaleFactor, height/scaleFactor
            int finalWidth = (int)Math.Ceiling(width / scaleFactor);
            int finalHeight = (int)Math.Ceiling(height / scaleFactor);

            // 리샘플(축소)하여 smallBitmap 생성
            using var smallBitmap = bigBitmap.Resize(
                new SKImageInfo(finalWidth, finalHeight), 
                SKFilterQuality.High
            );

            // 1비트(bitonal)로 변환
            int bytesPerRow = (finalWidth + 7) / 8;
            int totalBytes = bytesPerRow * finalHeight;
            byte[] data = new byte[totalBytes];

            for (int y = 0; y < finalHeight; y++)
            {
                int bitIndex = 0;
                byte currentByte = 0;
                for (int x = 0; x < finalWidth; x++)
                {
                    SKColor pixel = smallBitmap.GetPixel(x, y);
                    int avg = (pixel.Red + pixel.Green + pixel.Blue) / 3;
                    bool isBlack = avg < 128;  // 간단 임계값
                    if (isBlack)
                    {
                        currentByte |= (byte)(1 << (7 - bitIndex));
                    }
                    bitIndex++;
                    if (bitIndex == 8)
                    {
                        int index = y * bytesPerRow + (x / 8);
                        data[index] = currentByte;
                        currentByte = 0;
                        bitIndex = 0;
                    }
                }
                if (bitIndex > 0)
                {
                    int index = y * bytesPerRow + (finalWidth / 8);
                    data[index] = currentByte;
                }
            }

            string hexData = ByteHelper.BytesToHex(data);
            return $"^GFA,{totalBytes},{totalBytes},{bytesPerRow},{hexData}";
        }
    }
}
