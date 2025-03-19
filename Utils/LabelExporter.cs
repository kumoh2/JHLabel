using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Layouts;
using ZXing;
using ZXing.QrCode;

namespace JHLabel.Utils
{
    public class LabelExporter
    {
        // 참조 상수 (필요시 수정)
        private const double RefTextWidthMm = 20;
        private const double RefTextHeightMm = 10;
        private const double RefBarcode1DWidthMm = 30;
        private const double RefBarcode1DHeightMm = 10;
        private const double RefBarcode2DWidthMm = 18; // QR는 정사각형

        private readonly IEnumerable<View> _views;
        private readonly View _skipView;
        private readonly Func<double, double> _screenPixelsToMm;

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="views">에디터 영역의 모든 View 컬렉션 (예: EditorArea.Children)</param>
        /// <param name="skipView">ZPL/PGL 생성 시 스킵할 View (예: 선택 표시용 Border)</param>
        /// <param name="screenPixelsToMm">화면 픽셀을 mm로 변환하는 함수</param>
        public LabelExporter(IEnumerable<View> views, View skipView, Func<double, double> screenPixelsToMm)
        {
            _views = views;
            _skipView = skipView;
            _screenPixelsToMm = screenPixelsToMm;
        }

        /// <summary>
        /// 주어진 프린터 DPI를 기반으로 ZPL 문자열을 생성합니다.
        /// </summary>
        public string GenerateZPL(int printerDpi)
        {
            string zpl = "^XA";
            foreach (var view in _views)
            {
                if (view == _skipView)
                    continue;

                var bounds = (Rect)((BindableObject)view).GetValue(AbsoluteLayout.LayoutBoundsProperty);
                double xMm = _screenPixelsToMm(bounds.X);
                double yMm = _screenPixelsToMm(bounds.Y);
                if (xMm < 0) xMm = 0;
                if (yMm < 0) yMm = 0;
                int xDots = (int)Math.Round(xMm * printerDpi / 25.4);
                int yDots = (int)Math.Round(yMm * printerDpi / 25.4);

                if (view is Label lbl)
                {
                    zpl += $"^FO{xDots},{yDots}^A0N,30,30^FD{lbl.Text}^FS";
                }
                else if (view is Image img)
                {
                    if (!string.IsNullOrEmpty(img.ClassId))
                    {
                        if (img.ClassId.StartsWith("Barcode1D:"))
                        {
                            string data = img.ClassId.Substring("Barcode1D:".Length);
                            double heightMm = _screenPixelsToMm(bounds.Height);
                            int heightDots = (int)Math.Round(heightMm * printerDpi / 25.4);
                            if (heightDots <= 0)
                                heightDots = 80; // fallback
                            zpl += $"^FO{xDots},{yDots}^BCN,{heightDots},N,N,N^FD{data}^FS";
                        }
                        else if (img.ClassId.StartsWith("Barcode2D:"))
                        {
                            // ClassId 형식: "Barcode2D:{data};{computedMagnification}"
                            string[] parts = img.ClassId.Split(';');
                            string data = parts[0].Substring("Barcode2D:".Length);
                            int magnification = 1;
                            if (parts.Length > 1 && int.TryParse(parts[1], out int mag))
                                magnification = mag;
                            zpl += $"^FO{xDots},{yDots}^BQN,2,{magnification}^FDMM,A{data}^FS";
                        }
                    }
                }
            }
            zpl += "^XZ";
            return zpl;
        }

        /// <summary>
        /// 주어진 프린터 DPI를 기반으로 PGL 문자열을 생성합니다.
        /// </summary>
        public string GeneratePGL(int printerDpi)
        {
            string pgl = "<PGL_START>\n";
            foreach (var view in _views)
            {
                if (view == _skipView)
                    continue;

                var bounds = (Rect)((BindableObject)view).GetValue(AbsoluteLayout.LayoutBoundsProperty);
                double xMm = _screenPixelsToMm(bounds.X);
                double yMm = _screenPixelsToMm(bounds.Y);
                if (xMm < 0) xMm = 0;
                if (yMm < 0) yMm = 0;
                int xDots = (int)Math.Round(xMm * printerDpi / 25.4);
                int yDots = (int)Math.Round(yMm * printerDpi / 25.4);

                if (view is Label lbl)
                {
                    pgl += $" TEXT {xDots},{yDots},0,30,\"{lbl.Text}\";\n";
                }
                else if (view is Image img)
                {
                    if (!string.IsNullOrEmpty(img.ClassId))
                    {
                        if (img.ClassId.StartsWith("Barcode1D:"))
                        {
                            string data = img.ClassId.Substring("Barcode1D:".Length);
                            double heightMm = _screenPixelsToMm(bounds.Height);
                            int heightDots = (int)Math.Round(heightMm * printerDpi / 25.4);
                            if (heightDots <= 0)
                                heightDots = 80;
                            pgl += $" BARCODE1D {xDots},{yDots},CODE128,{heightDots},\"{data}\",NOHR;\n";
                        }
                        else if (img.ClassId.StartsWith("Barcode2D:"))
                        {
                            string[] parts = img.ClassId.Split(';');
                            string data = parts[0].Substring("Barcode2D:".Length);
                            int magnification = 1;
                            if (parts.Length > 1 && int.TryParse(parts[1], out int mag))
                                magnification = mag;
                            int moduleCount = GetQrModuleCount(data);
                            int printedWidthDots = moduleCount * magnification;
                            pgl += $" BARCODE2D {xDots},{yDots},QR,{printedWidthDots},\"{data}\";\n";
                        }
                    }
                }
            }
            pgl += "<PGL_END>";
            return pgl;
        }

        /// <summary>
        /// ZXing을 이용하여 QR 코드 모듈 수를 계산합니다.
        /// </summary>
        private int GetQrModuleCount(string data)
        {
            var writer = new QRCodeWriter();
            var matrix = writer.encode(data, BarcodeFormat.QR_CODE, 0, 0);
            return matrix.Width;
        }
    }
}
