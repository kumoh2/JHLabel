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

        // 현재 편집 중인 라벨 디자인 데이터 (디자인 데이터는 mm 단위, DPI 등)
        private LabelModel? currentLabelDesign;

        // EditorInteractionManager 인스턴스
        private EditorInteractionManager _interactionManager;

        public MainPage()
        {
            InitializeComponent();

            // 부모 페이지는 회색, 편집 영역은 흰색 및 중앙 정렬 처리
            this.BackgroundColor = Colors.Gray;
            EditorArea.BackgroundColor = Colors.White;
            EditorArea.HorizontalOptions = LayoutOptions.Center;
            EditorArea.VerticalOptions = LayoutOptions.Center;

            // DB 초기화 및 로드
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "labels.db3");
            System.Diagnostics.Debug.WriteLine($"Database Path: {dbPath}");
            _dbService = new DatabaseService(dbPath);
            LoadLabels();

            // 선택 표시용 Border 초기화
            var selectionIndicator = new Border
            {
                Stroke = Colors.Blue,
                StrokeThickness = 2,
                BackgroundColor = Colors.Transparent,
                InputTransparent = true,
                IsVisible = false
            };
            EditorArea.Children.Add(selectionIndicator);

            // EditorInteractionManager 생성
            _interactionManager = new EditorInteractionManager(EditorArea, selectionIndicator);
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
            // 선택 표시용 Border 재추가
            EditorArea.Children.Add(_interactionManager.SelectionIndicator);
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
                EditorArea.Children.Add(_interactionManager.SelectionIndicator);
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

            var exporter = new LabelExporter(EditorArea.Children.OfType<View>(), _interactionManager.SelectionIndicator, ScreenPixelsToMm);
            string zpl = exporter.GenerateZPL(currentLabelDesign.DPI);
            string pgl = exporter.GeneratePGL(currentLabelDesign.DPI);
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
            const double RefTextWidthMm = 20;
            const double RefTextHeightMm = 10;
            double widthScreen = MmToScreenPixels(RefTextWidthMm);
            double heightScreen = MmToScreenPixels(RefTextHeightMm);

            double xScreen = MmToScreenPixels(defaultX);
            double yScreen = MmToScreenPixels(defaultY);
            var rect = _interactionManager.ClampRect(new Rect(xScreen, yScreen, widthScreen, heightScreen));

            var lbl = new Label { Text = text, BackgroundColor = Colors.White, TextColor = Colors.Black };

            AbsoluteLayout.SetLayoutBounds(lbl, rect);
            AbsoluteLayout.SetLayoutFlags(lbl, AbsoluteLayoutFlags.None);
            lbl.ClassId = "Text:" + text;
            _interactionManager.AddDragAndGesture(lbl);
            EditorArea.Children.Add(lbl);
        }

        // 1D 바코드 추가 (Code128) – 기본 위치 5,20에 동적 크기 (mm 기준)
        private async void OnAddBarcode1DClicked(object sender, EventArgs e)
        {
            string data = await DisplayPromptAsync("Add 1D Barcode", "Enter barcode data:");
            if (string.IsNullOrEmpty(data))
                return;

            double defaultX = 5, defaultY = 20;
            const double RefBarcode1DWidthMm = 30;
            const double RefBarcode1DHeightMm = 10;
            double widthScreen = MmToScreenPixels(RefBarcode1DWidthMm);
            double heightScreen = MmToScreenPixels(RefBarcode1DHeightMm);

            double xScreen = MmToScreenPixels(defaultX);
            double yScreen = MmToScreenPixels(defaultY);
            var rect = _interactionManager.ClampRect(new Rect(xScreen, yScreen, widthScreen, heightScreen));

            var barcodeImageSource = BarcodeGenerator.GenerateBarcodeImage(data, BarcodeFormat.CODE_128, (int)widthScreen, (int)heightScreen);
            var img = new Image
            {
                Source = barcodeImageSource,
                WidthRequest = widthScreen,
                HeightRequest = heightScreen
            };

            AbsoluteLayout.SetLayoutBounds(img, rect);
            AbsoluteLayout.SetLayoutFlags(img, AbsoluteLayoutFlags.None);
            _interactionManager.AddDragAndGesture(img);
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
            const double RefBarcode2DWidthMm = 18; // QR는 정사각형
            double default2DSizeMm = RefBarcode2DWidthMm;

            // QR 코드의 모듈 수 (quiet zone 포함)
            int moduleCount = GetQrModuleCount(data);

            // 원하는 배율 계산: moduleCount * magnification * 25.4 / DPI = default2DSizeMm
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
            var rect = _interactionManager.ClampRect(new Rect(xScreen, yScreen, previewWidth, previewHeight));

            var barcodeImageSource = BarcodeGenerator.GenerateBarcodeImage(data, BarcodeFormat.QR_CODE, (int)previewWidth, (int)previewHeight);
            var img = new Image
            {
                Source = barcodeImageSource,
                WidthRequest = previewWidth,
                HeightRequest = previewHeight
            };

            AbsoluteLayout.SetLayoutBounds(img, rect);
            AbsoluteLayout.SetLayoutFlags(img, AbsoluteLayoutFlags.None);
            _interactionManager.AddDragAndGesture(img);
            // ClassId에 배율 정보 추가 (나중에 ZPL 생성 시 참조)
            img.ClassId = "Barcode2D:" + data + ";" + computedMagnification;
            EditorArea.Children.Add(img);
        }

        // Bring to Front 버튼 클릭 시
        private void OnBringToFrontClicked(object sender, EventArgs e)
        {
            _interactionManager.BringToFront();
        }

        // Send to Back 버튼 클릭 시
        private void OnSendToBackClicked(object sender, EventArgs e)
        {
            _interactionManager.SendToBack();
        }

        // ZXing을 이용하여 QR 코드의 모듈 수(버전 기준)를 반환하는 헬퍼 함수
        private int GetQrModuleCount(string data)
        {
            var writer = new QRCodeWriter();
            var matrix = writer.encode(data, BarcodeFormat.QR_CODE, 0, 0);
            return matrix.Width;
        }

        // 라벨 전환: 선택 해제 및 EditorArea 재구성, ZPL 파싱
        private void LabelListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _interactionManager.ClearSelection();
            if (LabelListView.SelectedItem is LabelModel model)
            {
                currentLabelDesign = model;
                EditorArea.Children.Clear();
                EditorArea.Children.Add(_interactionManager.SelectionIndicator);
                EditorArea.WidthRequest = MmToScreenPixels(currentLabelDesign.PaperWidthMm);
                EditorArea.HeightRequest = MmToScreenPixels(currentLabelDesign.PaperHeightMm);
                // ZplParser 생성 (드래그/제스처 추가를 위해 _interactionManager.AddDragAndGesture 델리게이트 전달)
                var parser = new ZplParser(EditorArea.Width, EditorArea.Height, _interactionManager.AddDragAndGesture);
                var views = parser.Parse(model.ZPL, model.DPI);
                foreach (var view in views)
                {
                    EditorArea.Children.Add(view);
                }
            }
        }
    }
}
