using Microsoft.Maui.Devices;
using Microsoft.Maui.Layouts;
using System.Text.RegularExpressions;
using JHLabel.Models;
using JHLabel.Services;
using JHLabel.Utils;
using ZXing;
using ZXing.QrCode;

namespace JHLabel
{
    public partial class MainPage : ContentPage
    {
        DatabaseService _dbService;
        public List<LabelModel> Labels { get; set; } = new List<LabelModel>();

        // 선택된 뷰 및 선택 표시용 오버레이(Border)
        private View? _selectedView;
        private Border _selectionIndicator;

        // 현재 편집 중인 라벨 디자인 데이터 (디자인 데이터는 mm 단위, DPI 등)
        private LabelModel? currentLabelDesign;

        // 기준 라벨 크기 (참조값: 45×70mm)에서 각 요소의 비율
        private const double RefLabelWidthMm = 45;
        private const double RefLabelHeightMm = 70;
        private const double RefTextWidthMm = 20, RefTextHeightMm = 10;
        private const double RefBarcode1DWidthMm = 30, RefBarcode1DHeightMm = 10;
        private const double RefBarcode2DWidthMm = 18; // QR는 정사각형

        public MainPage()
        {
            InitializeComponent();

            // 부모 페이지는 회색, 편집 영역은 흰색 및 중앙 정렬 처리
            this.BackgroundColor = Colors.Gray;
            EditorArea.BackgroundColor = Colors.White;
            EditorArea.HorizontalOptions = LayoutOptions.Center;
            EditorArea.VerticalOptions = LayoutOptions.Center;

            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "labels.db3");
            System.Diagnostics.Debug.WriteLine($"Database Path: {dbPath}");
            _dbService = new DatabaseService(dbPath);
            LoadLabels();

            // 선택 표시용 Border 초기화
            _selectionIndicator = new Border
            {
                Stroke = Colors.Blue,
                StrokeThickness = 2,
                BackgroundColor = Colors.Transparent,
                InputTransparent = true,
                IsVisible = false
            };
            EditorArea.Children.Add(_selectionIndicator);
        }

        async void LoadLabels()
        {
            Labels = await _dbService.GetLabelsAsync();
            LabelListView.ItemsSource = Labels;
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

        // 라벨 영역 내에서 객체 위치가 벗어나지 않도록 clamp 처리
        private Rect ClampRect(Rect rect)
        {
            double areaWidth = EditorArea.Width;
            double areaHeight = EditorArea.Height;
            double x = rect.X;
            double y = rect.Y;
            double width = rect.Width;
            double height = rect.Height;
            if (x < 0) x = 0;
            if (y < 0) y = 0;
            if (x + width > areaWidth)
                x = Math.Max(0, areaWidth - width);
            if (y + height > areaHeight)
                y = Math.Max(0, areaHeight - height);
            return new Rect(x, y, width, height);
        }
   
        // 새 라벨 생성: 라벨 크기를 EditorArea에 적용 (중앙 배치)
        private async void OnNewLabelClicked(object sender, EventArgs e)
        {
            string labelName = await DisplayPromptAsync("New Label", "Enter label name:");
            if (string.IsNullOrEmpty(labelName))
                return;

            int dpi;
            while (true)
            {
                string dpiStr = await DisplayPromptAsync("New Label", "Enter printer DPI (203, 300, or 600):", initialValue: "203");
                if (int.TryParse(dpiStr, out dpi) && (dpi == 203 || dpi == 300 || dpi == 600))
                    break;
                await DisplayAlert("Invalid Input", "Please enter a valid DPI: 203, 300, or 600.", "OK");
            }
            string widthStr = await DisplayPromptAsync("New Label", "Enter label width in mm:", initialValue: "45");
            if (!double.TryParse(widthStr, out double widthMm))
                widthMm = 45;
            string heightStr = await DisplayPromptAsync("New Label", "Enter label height in mm:", initialValue: "70");
            if (!double.TryParse(heightStr, out double heightMm))
                heightMm = 70;

            currentLabelDesign = new LabelModel
            {
                LabelName = labelName,
                DPI = dpi,
                PaperWidthMm = widthMm,
                PaperHeightMm = heightMm,
                ZPL = string.Empty,
                PGL = string.Empty
            };

            int result = await _dbService.SaveLabelAsync(currentLabelDesign);
            if (result > 0)
            {
                LoadLabels();
            }
            else
            {
                await DisplayAlert("Error", "Failed to create new label", "OK");
                return;
            }

            EditorArea.Children.Clear();
            EditorArea.Children.Add(_selectionIndicator);
            EditorArea.WidthRequest = MmToScreenPixels(currentLabelDesign.PaperWidthMm);
            EditorArea.HeightRequest = MmToScreenPixels(currentLabelDesign.PaperHeightMm);
        }

        private async void OnDeleteLabelClicked(object sender, EventArgs e)
        {
            if (currentLabelDesign == null)
            {
                await DisplayAlert("Info", "No label selected.", "OK");
                return;
            }

            string confirmationName = await DisplayPromptAsync("Confirm Delete", $"To delete '{currentLabelDesign.LabelName}', please type the label name:");
            if (confirmationName != currentLabelDesign.LabelName)
            {
                await DisplayAlert("Error", "Label name does not match. Deletion cancelled.", "OK");
                return;
            }

            int result = await _dbService.DeleteLabelAsync(currentLabelDesign);
            if (result > 0)
            {
                await DisplayAlert("Deleted", "Label deleted successfully", "OK");
                currentLabelDesign = null;
                EditorArea.Children.Clear();
                EditorArea.Children.Add(_selectionIndicator);
                LoadLabels();
            }
            else
            {
                await DisplayAlert("Error", "Failed to delete label.", "OK");
            }
        }

        // Save Label: 현재 편집 중인 라벨 디자인 데이터를 ZPL/PGL 문자열로 변환 후 DB에 저장
        private async void OnSaveLabelClicked(object sender, EventArgs e)
        {
            if (currentLabelDesign == null)
            {
                await DisplayAlert("Error", "No label design available. Create a new label first.", "OK");
                return;
            }

            string zpl = GenerateZPLFromEditor(currentLabelDesign.DPI);
            string pgl = GeneratePGLFromEditor(currentLabelDesign.DPI);
            currentLabelDesign.ZPL = zpl;
            currentLabelDesign.PGL = pgl;

            int result = await _dbService.SaveLabelAsync(currentLabelDesign);
            if (result > 0)
            {
                await DisplayAlert("Saved", "Label saved successfully", "OK");
                LoadLabels();
            }
            else
            {
                await DisplayAlert("Not Saved", "Label not saved", "OK");
            }
        }

        // 텍스트 추가 – 기본 위치 5,5에 동적 크기 (mm 기준)
        private async void OnAddTextClicked(object sender, EventArgs e)
        {
            string text = await DisplayPromptAsync("Add Text", "Enter text:");
            if (string.IsNullOrEmpty(text))
                return;

            double defaultX = 5, defaultY = 5;
            double widthScreen = MmToScreenPixels(RefTextWidthMm);
            double heightScreen = MmToScreenPixels(RefTextHeightMm);

            double xScreen = MmToScreenPixels(defaultX);
            double yScreen = MmToScreenPixels(defaultY);
            var rect = ClampRect(new Rect(xScreen, yScreen, widthScreen, heightScreen));

            var lbl = new Label { Text = text, BackgroundColor = Colors.White, TextColor = Colors.Black };

            AbsoluteLayout.SetLayoutBounds(lbl, rect);
            AbsoluteLayout.SetLayoutFlags(lbl, AbsoluteLayoutFlags.None);
            AddDragAndGesture(lbl);
            lbl.ClassId = "Text:" + text;
            EditorArea.Children.Add(lbl);
        }

        // 1D 바코드 추가 (Code128) – 기본 위치 5,20에 동적 크기 (mm 기준)
        private async void OnAddBarcode1DClicked(object sender, EventArgs e)
        {
            string data = await DisplayPromptAsync("Add 1D Barcode", "Enter barcode data:");
            if (string.IsNullOrEmpty(data))
                return;

            double defaultX = 5, defaultY = 20;
            double widthScreen = MmToScreenPixels(RefBarcode1DWidthMm);
            double heightScreen = MmToScreenPixels(RefBarcode1DHeightMm);

            double xScreen = MmToScreenPixels(defaultX);
            double yScreen = MmToScreenPixels(defaultY);
            var rect = ClampRect(new Rect(xScreen, yScreen, widthScreen, heightScreen));

            var barcodeImageSource = BarcodeGenerator.GenerateBarcodeImage(data, BarcodeFormat.CODE_128, (int)widthScreen, (int)heightScreen);
            var img = new Image
            {
                Source = barcodeImageSource,
                WidthRequest = widthScreen,
                HeightRequest = heightScreen
            };

            AbsoluteLayout.SetLayoutBounds(img, rect);
            AbsoluteLayout.SetLayoutFlags(img, AbsoluteLayoutFlags.None);
            AddDragAndGesture(img);
            img.ClassId = "Barcode1D:" + data;
            EditorArea.Children.Add(img);
        }

        // 2D 바코드 추가 (QR 코드) – 기본 위치 5,35에 동적 크기 (라벨 크기와 DPI 반영)
        private async void OnAddBarcode2DClicked(object sender, EventArgs e)
        {
            string data = await DisplayPromptAsync("Add 2D Barcode", "Enter QR code data:");
            if (string.IsNullOrEmpty(data))
                return;

            double defaultX = 5, defaultY = 35;
            // 기본 QR 코드 크기 (mm 단위)는 라벨 크기에 따라 동적으로 계산
            double default2DSizeMm = RefBarcode2DWidthMm;

            // QR 코드의 모듈 수 (ZXing으로 계산; quiet zone 포함된 값)
            int moduleCount = GetQrModuleCount(data);

            // 원하는 배율을 계산: moduleCount * magnification * 25.4 / DPI = default2DSizeMm
            int computedMagnification = (int)Math.Round((default2DSizeMm * (currentLabelDesign?.DPI ?? 300) / 25.4) / moduleCount);
            if (computedMagnification < 1)
                computedMagnification = 1;

            // 실제 인쇄 시 QR 코드 크기 (mm 단위)
            double actualSizeMm = moduleCount * computedMagnification * 25.4 / (currentLabelDesign?.DPI ?? 300);

            // 화면에 표시할 크기는 mm → 픽셀 변환
            double previewWidth = MmToScreenPixels(actualSizeMm);
            double previewHeight = previewWidth; // 정사각형

            double xScreen = MmToScreenPixels(defaultX);
            double yScreen = MmToScreenPixels(defaultY);
            var rect = ClampRect(new Rect(xScreen, yScreen, previewWidth, previewHeight));

            var barcodeImageSource = BarcodeGenerator.GenerateBarcodeImage(data, BarcodeFormat.QR_CODE, (int)previewWidth, (int)previewHeight);
            var img = new Image
            {
                Source = barcodeImageSource,
                WidthRequest = previewWidth,
                HeightRequest = previewHeight
            };

            AbsoluteLayout.SetLayoutBounds(img, rect);
            AbsoluteLayout.SetLayoutFlags(img, AbsoluteLayoutFlags.None);
            AddDragAndGesture(img);
            // ClassId에 배율 정보 추가하여 나중에 ZPL 생성 시 참고
            img.ClassId = "Barcode2D:" + data + ";" + computedMagnification;
            EditorArea.Children.Add(img);
        }

        // 레이어 순서 조정: Bring to Front / Send to Back
        private void OnBringToFrontClicked(object sender, EventArgs e)
        {
            if (_selectedView == null)
            {
                DisplayAlert("Info", "No object selected.", "OK");
                return;
            }
            EditorArea.Children.Remove(_selectedView);
            EditorArea.Children.Add(_selectedView);
        }
        private void OnSendToBackClicked(object sender, EventArgs e)
        {
            if (_selectedView == null)
            {
                DisplayAlert("Info", "No object selected.", "OK");
                return;
            }
            EditorArea.Children.Remove(_selectedView);
            EditorArea.Children.Insert(0, _selectedView);
        }

        // ZPL 생성: EditorArea의 각 객체 좌표 및 크기를 mm→dot 단위로 변환
        private string GenerateZPLFromEditor(int printerDpi)
        {
            string zpl = "^XA";
            foreach (var view in EditorArea.Children)
            {
                if (view == _selectionIndicator)
                    continue;

                var bounds = (Rect)((BindableObject)view).GetValue(AbsoluteLayout.LayoutBoundsProperty);
                double xMm = ScreenPixelsToMm(bounds.X);
                double yMm = ScreenPixelsToMm(bounds.Y);
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
                            double heightMm = ScreenPixelsToMm(bounds.Height);
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

        // PGL 생성 (GenerateZPL과 유사한 방식)
        private string GeneratePGLFromEditor(int printerDpi)
        {
            string pgl = "<PGL_START>\n";
            foreach (var view in EditorArea.Children)
            {
                if (view == _selectionIndicator)
                    continue;

                var bounds = (Rect)((BindableObject)view).GetValue(AbsoluteLayout.LayoutBoundsProperty);
                double xMm = ScreenPixelsToMm(bounds.X);
                double yMm = ScreenPixelsToMm(bounds.Y);
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
                            double heightMm = ScreenPixelsToMm(bounds.Height);
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

        // ZXing을 이용하여 QR 코드의 모듈 수(버전 기준)를 반환하는 헬퍼 함수
        private int GetQrModuleCount(string data)
        {
            var writer = new QRCodeWriter();
            var matrix = writer.encode(data, BarcodeFormat.QR_CODE, 0, 0);
            return matrix.Width;
        }

        // 드래그 및 선택 제스처 부여 (EditorArea 내에서 객체 이동)
        private void AddDragAndGesture(View view)
        {
            var panGesture = new PanGestureRecognizer();
            double startX = 0, startY = 0;
            panGesture.PanUpdated += (s, e) =>
            {
                switch (e.StatusType)
                {
                    case GestureStatus.Started:
                        var bounds = AbsoluteLayout.GetLayoutBounds(view);
                        startX = bounds.X;
                        startY = bounds.Y;
                        break;
                    case GestureStatus.Running:
                        var current = AbsoluteLayout.GetLayoutBounds(view);
                        double newX = startX + e.TotalX;
                        double newY = startY + e.TotalY;
                        var newRect = new Rect(newX, newY, current.Width, current.Height);
                        newRect = ClampRect(newRect);
                        AbsoluteLayout.SetLayoutBounds(view, newRect);
                        if (_selectedView == view)
                            UpdateSelectionIndicator();
                        break;
                }
            };
            view.GestureRecognizers.Add(panGesture);

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (s, e) => { SelectView(view); };
            view.GestureRecognizers.Add(tapGesture);
        }

        // 객체 선택 시 호출
        private void SelectView(View view)
        {
            _selectedView = view;
            UpdateSelectionIndicator();
        }

        // 선택 표시 업데이트
        private void UpdateSelectionIndicator()
        {
            if (_selectedView == null)
            {
                _selectionIndicator.IsVisible = false;
                return;
            }
            var bounds = AbsoluteLayout.GetLayoutBounds(_selectedView);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                var size = _selectedView.Measure(double.PositiveInfinity, double.PositiveInfinity);
                bounds = new Rect(bounds.X, bounds.Y, size.Width, size.Height);
                AbsoluteLayout.SetLayoutBounds(_selectedView, bounds);
            }
            double margin = 2;
            AbsoluteLayout.SetLayoutBounds(_selectionIndicator, new Rect(bounds.X - margin, bounds.Y - margin, bounds.Width + margin * 2, bounds.Height + margin * 2));
            _selectionIndicator.IsVisible = true;
        }

        // 라벨 전환: 선택 해제 및 EditorArea 재구성, ZPL 문자열 파싱
        private void LabelListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedView = null;
            _selectionIndicator.IsVisible = false;
            if (LabelListView.SelectedItem is LabelModel model)
            {
                currentLabelDesign = model;
                EditorArea.Children.Clear();
                EditorArea.Children.Add(_selectionIndicator);
                EditorArea.WidthRequest = MmToScreenPixels(currentLabelDesign.PaperWidthMm);
                EditorArea.HeightRequest = MmToScreenPixels(currentLabelDesign.PaperHeightMm);
                ParseZPLToEditor(model.ZPL, model.DPI);
            }
        }

        // ZPL 문자열을 EditorArea에 파싱 – 생성 당시의 크기(텍스트, 1D/2D 바코드)로 배치
        private void ParseZPLToEditor(string zpl, int printerDpi)
        {
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
                    var rect = ClampRect(new Rect(xScreen, yScreen, MmToScreenPixels(RefTextWidthMm), MmToScreenPixels(RefTextHeightMm)));
                    var lbl = new Label { Text = content, BackgroundColor = Colors.White, TextColor = Colors.Black };
                    AbsoluteLayout.SetLayoutBounds(lbl, rect);
                    AbsoluteLayout.SetLayoutFlags(lbl, AbsoluteLayoutFlags.None);
                    AddDragAndGesture(lbl);
                    lbl.ClassId = "Text:" + content;
                    EditorArea.Children.Add(lbl);
                }
            }

            // 1D 바코드 파싱
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
                    var rect = ClampRect(new Rect(xScreen, yScreen, MmToScreenPixels(RefBarcode1DWidthMm), MmToScreenPixels(RefBarcode1DHeightMm)));
                    var img = new Image
                    {
                        Source = BarcodeGenerator.GenerateBarcodeImage(data, BarcodeFormat.CODE_128, (int)MmToScreenPixels(RefBarcode1DWidthMm), (int)MmToScreenPixels(RefBarcode1DHeightMm)),
                        WidthRequest = rect.Width,
                        HeightRequest = rect.Height
                    };
                    AbsoluteLayout.SetLayoutBounds(img, rect);
                    AbsoluteLayout.SetLayoutFlags(img, AbsoluteLayoutFlags.None);
                    AddDragAndGesture(img);
                    img.ClassId = "Barcode1D:" + data;
                    EditorArea.Children.Add(img);
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
                    double previewHeight = previewWidth;
                    var rect = ClampRect(new Rect(xScreen, yScreen, previewWidth, previewHeight));

                    var img = new Image
                    {
                        Source = BarcodeGenerator.GenerateBarcodeImage(data, BarcodeFormat.QR_CODE, (int)previewWidth, (int)previewHeight),
                        WidthRequest = previewWidth,
                        HeightRequest = previewHeight
                    };
                    AbsoluteLayout.SetLayoutBounds(img, rect);
                    AbsoluteLayout.SetLayoutFlags(img, AbsoluteLayoutFlags.None);
                    AddDragAndGesture(img);
                    img.ClassId = "Barcode2D:" + data;
                    EditorArea.Children.Add(img);
                }
            }
        }
    }
}
