using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Layouts;
using ZXing;
using ZXing.QrCode;

namespace JHLabel.Utils
{
    public class ZplParser
    {
        // 참조용 상수 (필요에 따라 수정)
        private const double RefTextWidthMm = 20;
        private const double RefTextHeightMm = 10;
        private const double RefBarcode1DWidthMm = 30;
        private const double RefBarcode1DHeightMm = 10;
        private const double RefBarcode2DWidthMm = 18; // QR는 정사각형

        private readonly double _editorWidth;
        private readonly double _editorHeight;
        private readonly Action<View>? _addGesture;

        public ZplParser(double editorWidth, double editorHeight, Action<View>? addGesture = null)
        {
            _editorWidth = editorWidth;
            _editorHeight = editorHeight;
            _addGesture = addGesture;
        }

        // mm → 화면 픽셀 변환 (미리보기용)
        private double MmToScreenPixels(double mm)
        {
            var displayInfo = DeviceDisplay.MainDisplayInfo;
            double screenDpi = displayInfo.Density * 160; // 1 DIU = 160 DPI 기준
            double pixelsPerMm = screenDpi / 25.4;
            return mm * pixelsPerMm;
        }

        // 화면 픽셀 → mm 변환
        private double ScreenPixelsToMm(double pixels)
        {
            var displayInfo = DeviceDisplay.MainDisplayInfo;
            double screenDpi = displayInfo.Density * 160;
            double pixelsPerMm = screenDpi / 25.4;
            return pixels / pixelsPerMm;
        }

        // 에디터 영역 내에서 객체 위치가 벗어나지 않도록 clamp 처리
        private Rect ClampRect(Rect rect)
        {
            double x = rect.X;
            double y = rect.Y;
            double width = rect.Width;
            double height = rect.Height;
            if (x < 0) x = 0;
            if (y < 0) y = 0;
            if (x + width > _editorWidth)
                x = Math.Max(0, _editorWidth - width);
            if (y + height > _editorHeight)
                y = Math.Max(0, _editorHeight - height);
            return new Rect(x, y, width, height);
        }

        /// <summary>
        /// ZPL 문자열을 파싱하여 EditorArea에 추가할 View 목록을 반환합니다.
        /// </summary>
        /// <param name="zpl">ZPL 문자열</param>
        /// <param name="printerDpi">프린터 DPI</param>
        /// <returns>파싱된 View 목록</returns>
        public List<View> Parse(string zpl, int printerDpi)
        {
            var views = new List<View>();

            // 텍스트 파싱
            var regexText = new Regex(@"\^FO(\d+),(\d+)\^A0N,30,30\^FD([^\\^]+)\^FS");
            foreach (Match match in regexText.Matches(zpl))
            {
                if (match.Groups.Count == 4)
                {
                    int xDots = int.Parse(match.Groups[1].Value);
                    int yDots = int.Parse(match.Groups[2].Value);
                    string content = match.Groups[3].Value;
                    double xMm = xDots * 25.4 / printerDpi;
                    double yMm = yDots * 25.4 / printerDpi;
                    if (xMm < 0) xMm = 0;
                    if (yMm < 0) yMm = 0;
                    double xScreen = MmToScreenPixels(xMm);
                    double yScreen = MmToScreenPixels(yMm);
                    double textWidth = MmToScreenPixels(RefTextWidthMm);
                    double textHeight = MmToScreenPixels(RefTextHeightMm);
                    var rect = ClampRect(new Rect(xScreen, yScreen, textWidth, textHeight));

                    var lbl = new Label { Text = content, BackgroundColor = Colors.White, TextColor = Colors.Black };
                    AbsoluteLayout.SetLayoutBounds(lbl, rect);
                    AbsoluteLayout.SetLayoutFlags(lbl, AbsoluteLayoutFlags.None);
                    lbl.ClassId = "Text:" + content;
                    _addGesture?.Invoke(lbl);
                    views.Add(lbl);
                }
            }

            // 1D 바코드 파싱 (Code128)
            var regexBarcode1D = new Regex(@"\^FO(\d+),(\d+)\^BCN,(\d+),N,N,N\^FD([^\\^]+)\^FS");
            foreach (Match match in regexBarcode1D.Matches(zpl))
            {
                if (match.Groups.Count == 5)
                {
                    int xDots = int.Parse(match.Groups[1].Value);
                    int yDots = int.Parse(match.Groups[2].Value);
                    string data = match.Groups[4].Value;
                    double xMm = xDots * 25.4 / printerDpi;
                    double yMm = yDots * 25.4 / printerDpi;
                    if (xMm < 0) xMm = 0;
                    if (yMm < 0) yMm = 0;
                    double xScreen = MmToScreenPixels(xMm);
                    double yScreen = MmToScreenPixels(yMm);
                    double barcodeWidth = MmToScreenPixels(RefBarcode1DWidthMm);
                    double barcodeHeight = MmToScreenPixels(RefBarcode1DHeightMm);
                    var rect = ClampRect(new Rect(xScreen, yScreen, barcodeWidth, barcodeHeight));

                    var imageSource = BarcodeGenerator.GenerateBarcodeImage(data, BarcodeFormat.CODE_128, (int)barcodeWidth, (int)barcodeHeight);
                    var img = new Image { Source = imageSource, WidthRequest = barcodeWidth, HeightRequest = barcodeHeight };
                    AbsoluteLayout.SetLayoutBounds(img, rect);
                    AbsoluteLayout.SetLayoutFlags(img, AbsoluteLayoutFlags.None);
                    img.ClassId = "Barcode1D:" + data;
                    _addGesture?.Invoke(img);
                    views.Add(img);
                }
            }

            // 2D 바코드 파싱 (QR 코드)
            var regexBarcode2D = new Regex(@"\^FO(\d+),(\d+)\^BQN,2,(\d+)\^FDMM,A([^\\^]+)\^FS");
            foreach (Match match in regexBarcode2D.Matches(zpl))
            {
                if (match.Groups.Count == 5)
                {
                    int xDots = int.Parse(match.Groups[1].Value);
                    int yDots = int.Parse(match.Groups[2].Value);
                    string data = match.Groups[4].Value;
                    double xMm = xDots * 25.4 / printerDpi;
                    double yMm = yDots * 25.4 / printerDpi;
                    if (xMm < 0) xMm = 0;
                    if (yMm < 0) yMm = 0;
                    double xScreen = MmToScreenPixels(xMm);
                    double yScreen = MmToScreenPixels(yMm);
                    double previewWidth = MmToScreenPixels(RefBarcode2DWidthMm);
                    double previewHeight = previewWidth; // 정사각형
                    var rect = ClampRect(new Rect(xScreen, yScreen, previewWidth, previewHeight));

                    var imageSource = BarcodeGenerator.GenerateBarcodeImage(data, BarcodeFormat.QR_CODE, (int)previewWidth, (int)previewHeight);
                    var img = new Image { Source = imageSource, WidthRequest = previewWidth, HeightRequest = previewHeight };
                    AbsoluteLayout.SetLayoutBounds(img, rect);
                    AbsoluteLayout.SetLayoutFlags(img, AbsoluteLayoutFlags.None);
                    img.ClassId = "Barcode2D:" + data;
                    _addGesture?.Invoke(img);
                    views.Add(img);
                }
            }

            return views;
        }
    }
}
