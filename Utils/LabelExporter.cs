using SkiaSharp;
using BinaryKits.Zpl.Label.Helpers; // ByteHelper (GF 생성 시 사용)

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
        /// ZPL 문자열 생성
        /// </summary>
        public string GenerateZPL()
        {
            var zpl = "^XA\n";

            foreach (var view in _views)
            {
                if (view == _skipView)
                    continue;

                // 화면상의 Bounds (AbsoluteLayout)
                var bounds = (Rect)((BindableObject)view).GetValue(AbsoluteLayout.LayoutBoundsProperty);

                // mm -> dots 변환 (dots = mm * dpi / 25.4)
                int xDots = MmToDots(_screenPixelsToMm(bounds.X));
                int yDots = MmToDots(_screenPixelsToMm(bounds.Y));
                int wDots = MmToDots(_screenPixelsToMm(bounds.Width));
                int hDots = MmToDots(_screenPixelsToMm(bounds.Height));
                if (wDots < 1) wDots = 1;
                if (hDots < 1) hDots = 1;

                // 텍스트 컨트롤 처리 (ClassId 형식: "Text:실제텍스트|폰트높이|폰트너비")
                if (view is Label lbl && lbl.ClassId != null && lbl.ClassId.StartsWith("Text:"))
                {
                    var classBody = lbl.ClassId.Substring("Text:".Length);
                    var parts = classBody.Split('|');
                    if (parts.Length == 3)
                    {
                        string textPart = parts[0];
                        if (int.TryParse(parts[1], out int fontH) &&
                            int.TryParse(parts[2], out int fontW))
                        {
                            // 텍스트를 이미지로 변환하여 ^GF 명령어 생성
                            string gfCommand = ConvertTextToGF(textPart, fontH, fontW);
                            zpl += $"^FO{xDots},{yDots}{gfCommand}\n";
                        }
                    }
                }
                // Barcode 처리
                else if (view is Microsoft.Maui.Controls.Image img && !string.IsNullOrEmpty(img.ClassId))
                {
                    if (img.ClassId.StartsWith("Barcode1D:"))
                    {
                        // Code128 바코드 처리
                        string data = img.ClassId.Substring("Barcode1D:".Length);
                        int totalModules = BarcodeHelper.GetCode128ModuleCount(data);
                        if (totalModules <= 0) totalModules = 1;
                        int quietZone = 10;
                        int bestModuleWidth = 1;
                        for (int mw = 1; mw <= 10; mw++)
                        {
                            int totalWidth = (totalModules + quietZone) * mw;
                            if (totalWidth <= wDots)
                                bestModuleWidth = mw;
                            else
                                break;
                        }
                        zpl += $"^FO{xDots},{yDots}^BY{bestModuleWidth},2,{hDots}^BCN,{hDots},N,N,N^FD{data}^FS\n";
                    }
                    else if (img.ClassId.StartsWith("Barcode2D:"))
                    {
                        // QR 코드 처리
                        string data = img.ClassId.Substring("Barcode2D:".Length);
                        int moduleCount = BarcodeHelper.GetQrModuleCount(data);
                        if (moduleCount <= 0) moduleCount = 1;
                        int mag = wDots / moduleCount;
                        if (mag < 1) mag = 1;
                        zpl += $"^FO{xDots},{yDots}^BQN,2,{mag}^FDMM,A{data}^FS\n";
                    }
                }
            }

            zpl += "^XZ";
            return zpl;
        }

        /// <summary>
        /// (옵션) PGL 문자열 생성 예시 – 필요 없으면 제거 가능
        /// </summary>
        public string GeneratePGL()
        {
            string pgl = "<PGL_START>\n";
            pgl += "<PGL_END>";
            return pgl;
        }

        private int MmToDots(double mm)
        {
            return (int)Math.Round(mm * _printerDpi / 25.4);
        }

        /// <summary>
        /// SkiaSharp를 사용하여 텍스트를 렌더링하고, 1비트 이미지 데이터로 변환하여 ^GF 명령어 문자열을 반환합니다.
        /// </summary>
        /// <param name="text">렌더링할 텍스트</param>
        /// <param name="fontHeight">폰트 높이 (픽셀)</param>
        /// <param name="fontWidth">폰트 폭 (필요에 따라 조정)</param>
        /// <returns>^GF 명령어 문자열</returns>
        private string ConvertTextToGF(string text, int fontHeight, int fontWidth)
        {
            // SkiaSharp를 사용하여 텍스트 렌더링
            using (var paint = new SKPaint())
            {
                paint.TextSize = fontHeight;
                paint.IsAntialias = true;
                paint.Color = SKColors.Black;
                // 시스템에 설치된 "Open Sans" 폰트를 사용 (필요에 따라 다른 폰트명 사용)
                paint.Typeface = SKTypeface.FromFamilyName("Open Sans");
                
                // 텍스트 폭 측정
                float textWidth = paint.MeasureText(text);
                int width = (int)Math.Ceiling(textWidth) + 2;
                int height = fontHeight + 2; // 간단하게 폰트 높이로 결정

                var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
                using (var surface = SKSurface.Create(info))
                {
                    var canvas = surface.Canvas;
                    canvas.Clear(SKColors.White);
                    // 텍스트 그리기 (y좌표는 폰트 높이로 설정하여 baseline을 맞춤)
                    canvas.DrawText(text, 0, fontHeight, paint);

                    using (var image = surface.Snapshot())
                    using (var bitmap = SKBitmap.FromImage(image))
                    {
                        // 1비트(bitonal) 이미지 데이터로 변환
                        int bytesPerRow = (width + 7) / 8;
                        int totalBytes = bytesPerRow * height;
                        byte[] data = new byte[totalBytes];

                        for (int y = 0; y < height; y++)
                        {
                            int bitIndex = 0;
                            byte currentByte = 0;
                            for (int x = 0; x < width; x++)
                            {
                                SKColor pixel = bitmap.GetPixel(x, y);
                                // 단순 임계값: (R+G+B)/3 < 128이면 검정
                                int avg = (pixel.Red + pixel.Green + pixel.Blue) / 3;
                                bool isBlack = avg < 128;
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
                                int index = y * bytesPerRow + (width / 8);
                                data[index] = currentByte;
                            }
                        }
                        // 16진수 문자열로 변환 (ByteHelper.BytesToHex를 사용)
                        string hexData = ByteHelper.BytesToHex(data);
                        // ^GF 명령어 구성: ^GFA,<totalBytes>,<graphicFieldCount>,<bytesPerRow>,<hexData>
                        string gfCommand = $"^GFA,{totalBytes},{totalBytes},{bytesPerRow},{hexData}";
                        return gfCommand;
                    }
                }
            }
        }
    }
}
