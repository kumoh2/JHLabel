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
        private readonly IEnumerable<View> _views;
        private readonly View _skipView;
        private readonly Func<double, double> _screenPixelsToMm;
        private readonly int _printerDpi;

        public LabelExporter(IEnumerable<View> views, View skipView,
            Func<double, double> screenPixelsToMm, int printerDpi)
        {
            _views = views;
            _skipView = skipView;
            _screenPixelsToMm = screenPixelsToMm;
            _printerDpi = printerDpi;
        }

        /// <summary>
        /// ZPL 생성
        /// </summary>
        public string GenerateZPL()
        {
            var zpl = "^XA\n";

            foreach (var view in _views)
            {
                if (view == _skipView) 
                    continue;

                // 화면상 Bounds
                var bounds = (Rect)((BindableObject)view).GetValue(AbsoluteLayout.LayoutBoundsProperty);

                // 위치/크기를 '도트'로 변환
                int xDots = MmToDots(_screenPixelsToMm(bounds.X));
                int yDots = MmToDots(_screenPixelsToMm(bounds.Y));
                int wDots = MmToDots(_screenPixelsToMm(bounds.Width));
                int hDots = MmToDots(_screenPixelsToMm(bounds.Height));
                if (wDots < 1) wDots = 1;
                if (hDots < 1) hDots = 1;

                if (view is Label lbl)
                {
                    // 폰트 크기: 높이를 hDots로 하고, 너비는 hDots/2로 간단히 잡음(2:1 비)
                    // (원한다면 hDots, wDots에 맞게 더 복잡한 로직도 가능)
                    int fontH = hDots;
                    int fontW = fontH / 2;
                    zpl += $"^FO{xDots},{yDots}^A0N,{fontH},{fontW}^FD{lbl.Text}^FS\n";
                }
                else if (view is Image img && !string.IsNullOrEmpty(img.ClassId))
                {
                    if (img.ClassId.StartsWith("Barcode1D:"))
                    {
                        // Code128
                        string data = img.ClassId.Substring("Barcode1D:".Length);

                        // (1) 바코드 높이 = hDots
                        // (2) 폭은 moduleWidth로 조절
                        //     - Code128 전체 모듈 수 = ZXing 이용
                        int totalModules = BarcodeHelper.GetCode128ModuleCount(data);
                        if (totalModules <= 0) totalModules = 1;

                        // quiet zone 10 모듈 가정
                        int neededDots = totalModules; // 본문 바코드
                        int quietZone = 10; // 양옆 모듈
                        // bounding box 폭 내에서 최대로 들어갈 수 있는 moduleWidth 찾기
                        int bestModuleWidth = 1;
                        for (int mw = 1; mw <= 10; mw++)
                        {
                            int totalWidth = (totalModules + quietZone) * mw;
                            if (totalWidth <= wDots)
                                bestModuleWidth = mw;
                            else
                                break;
                        }

                        // ^BY {narrowBarWidth}, {wideBarRatio=2}, {height}
                        zpl += $"^FO{xDots},{yDots}^BY{bestModuleWidth},2,{hDots}^BCN,{hDots},N,N,N^FD{data}^FS\n";
                    }
                    else if (img.ClassId.StartsWith("Barcode2D:"))
                    {
                        // QR
                        string data = img.ClassId.Substring("Barcode2D:".Length);

                        int moduleCount = BarcodeHelper.GetQrModuleCount(data);
                        if (moduleCount <= 0) moduleCount = 1;

                        // bounding box 폭을 모두 사용하려면
                        // magnification = wDots / moduleCount
                        // (너무 크면 모듈이 사각형 밖으로 나갈 수 있으니, 최소값 적용)
                        int mag = wDots / moduleCount;
                        if (mag < 1) mag = 1;

                        // ^BQN,2,magnification
                        zpl += $"^FO{xDots},{yDots}^BQN,2,{mag}^FDMM,A{data}^FS\n";
                    }
                }
            }

            zpl += "^XZ";
            return zpl;
        }

        /// <summary>
        /// (옵션) PGL 생성 예시 – 필요 없다면 제거
        /// </summary>
        public string GeneratePGL()
        {
            string pgl = "<PGL_START>\n";

            // 이하 로직은 GenerateZPL과 유사하게 각 객체별로 변환
            // 필요에 맞게 수정

            pgl += "<PGL_END>";
            return pgl;
        }

        private int MmToDots(double mm)
        {
            // mm -> dots
            // dots = mm * dpi / 25.4
            return (int)Math.Round(mm * _printerDpi / 25.4);
        }
    }
}
