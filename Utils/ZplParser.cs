using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Layouts;

namespace JHLabel.Utils
{
    public class ZplParser
    {
        private readonly double _editorWidth;
        private readonly double _editorHeight;
        private readonly Action<View>? _addGesture;
        private readonly Func<int, double> _dotsToScreenPx; // int->double

        public ZplParser(double editorWidth, double editorHeight,
                         Action<View>? addGesture,
                         Func<int,double> dotsToScreenPx)
        {
            _editorWidth = editorWidth;
            _editorHeight = editorHeight;
            _addGesture = addGesture;
            _dotsToScreenPx = dotsToScreenPx;
        }

        public List<View> Parse(string zpl)
        {
            var views = new List<View>();

            // 텍스트: ^FOx,y ^A0N,h,w ^FD...^FS
            var regexText = new Regex(@"\^FO(\d+),(\d+)\^A0N,(\d+),(\d+)\^FD([^\\^]+)\^FS");
            foreach (Match match in regexText.Matches(zpl))
            {
                if (match.Groups.Count == 6)
                {
                    int xDots = int.Parse(match.Groups[1].Value);
                    int yDots = int.Parse(match.Groups[2].Value);
                    int fontH = int.Parse(match.Groups[3].Value);
                    int fontW = int.Parse(match.Groups[4].Value);
                    string text = match.Groups[5].Value;

                    double xPx = _dotsToScreenPx(xDots);
                    double yPx = _dotsToScreenPx(yDots);

                    // 단순히 fontH, fontW를 그대로 bounding box로 사용
                    double wPx = _dotsToScreenPx(fontW);
                    double hPx = _dotsToScreenPx(fontH);

                    var rect = ClampRect(new Rect(xPx, yPx, wPx, hPx));

                    var lbl = new Label
                    {
                        Text = text,
                        BackgroundColor = Colors.White,
                        TextColor = Colors.Black
                    };
                    AbsoluteLayout.SetLayoutBounds(lbl, rect);
                    AbsoluteLayout.SetLayoutFlags(lbl, AbsoluteLayoutFlags.None);
                    lbl.ClassId = $"Text:{text}";
                    _addGesture?.Invoke(lbl);
                    views.Add(lbl);
                }
            }

            // 1D 바코드: ^FOx,y ... ^BCN,h ... ^FDdata^FS
            var regexBarcode1D = new Regex(@"\^FO(\d+),(\d+).*?\^BCN,(\d+),.*?\^FD([^\\^]+)\^FS",
                RegexOptions.Singleline);
            foreach (Match match in regexBarcode1D.Matches(zpl))
            {
                if (match.Groups.Count == 5)
                {
                    int xDots = int.Parse(match.Groups[1].Value);
                    int yDots = int.Parse(match.Groups[2].Value);
                    int heightDots = int.Parse(match.Groups[3].Value);
                    string data = match.Groups[4].Value;

                    double xPx = _dotsToScreenPx(xDots);
                    double yPx = _dotsToScreenPx(yDots);
                    double hPx = _dotsToScreenPx(heightDots);

                    // 폭: Code128 모듈 수 계산 + ^BY 파싱까지 해야 정확하지만, 간단 예시로 임의 추정
                    int totalModules = BarcodeHelper.GetCode128ModuleCount(data);
                    if (totalModules <= 0) totalModules = 1;
                    int quietZone = 10;
                    int assumedModuleWidth = 2; // 임의
                    int totalWidthDots = (totalModules + quietZone) * assumedModuleWidth;
                    double wPx = _dotsToScreenPx(totalWidthDots);

                    var rect = ClampRect(new Rect(xPx, yPx, wPx, hPx));

                    var source = BarcodeHelper.GenerateExactBarcodeImage(
                        data, ZXing.BarcodeFormat.CODE_128,
                        (int)Math.Round(wPx), (int)Math.Round(hPx));

                    var img = new Image
                    {
                        Source = source,
                        WidthRequest = wPx,
                        HeightRequest = hPx
                    };
                    AbsoluteLayout.SetLayoutBounds(img, rect);
                    AbsoluteLayout.SetLayoutFlags(img, AbsoluteLayoutFlags.None);
                    img.ClassId = $"Barcode1D:{data}";
                    _addGesture?.Invoke(img);
                    views.Add(img);
                }
            }

            // 2D 바코드 (QR): ^FOx,y ^BQN,2,m ^FDMM,A(data)^FS
            var regexBarcode2D = new Regex(@"\^FO(\d+),(\d+)\^BQN,2,(\d+)\^FDMM,A([^\\^]+)\^FS");
            foreach (Match match in regexBarcode2D.Matches(zpl))
            {
                if (match.Groups.Count == 5)
                {
                    int xDots = int.Parse(match.Groups[1].Value);
                    int yDots = int.Parse(match.Groups[2].Value);
                    int mag = int.Parse(match.Groups[3].Value);
                    string data = match.Groups[4].Value;

                    double xPx = _dotsToScreenPx(xDots);
                    double yPx = _dotsToScreenPx(yDots);

                    int moduleCount = BarcodeHelper.GetQrModuleCount(data);
                    if (moduleCount <= 0) moduleCount = 1;
                    int totalDots = moduleCount * mag;

                    double sizePx = _dotsToScreenPx(totalDots);

                    var rect = ClampRect(new Rect(xPx, yPx, sizePx, sizePx));

                    var source = BarcodeHelper.GenerateExactBarcodeImage(
                        data, ZXing.BarcodeFormat.QR_CODE,
                        (int)Math.Round(sizePx), (int)Math.Round(sizePx));

                    var img = new Image
                    {
                        Source = source,
                        WidthRequest = sizePx,
                        HeightRequest = sizePx
                    };
                    AbsoluteLayout.SetLayoutBounds(img, rect);
                    AbsoluteLayout.SetLayoutFlags(img, AbsoluteLayoutFlags.None);
                    img.ClassId = $"Barcode2D:{data}";
                    _addGesture?.Invoke(img);
                    views.Add(img);
                }
            }

            return views;
        }

        private Rect ClampRect(Rect rect)
        {
            double x = rect.X, y = rect.Y, w = rect.Width, h = rect.Height;
            if (x < 0) x = 0;
            if (y < 0) y = 0;
            if (x + w > _editorWidth) x = Math.Max(0, _editorWidth - w);
            if (y + h > _editorHeight) y = Math.Max(0, _editorHeight - h);
            return new Rect(x, y, w, h);
        }
    }
}
