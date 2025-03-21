using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks; // ← Task/async 사용할 때 필요
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Layouts;
using JHLabel.Models;
using JHLabel.Services;
using JHLabel.Utils;
using ZXing;

namespace JHLabel
{
    public partial class MainPage : ContentPage
    {
        DatabaseService _dbService;
        public List<LabelModel> Labels { get; set; } = new List<LabelModel>();

        private LabelModel? currentLabelDesign;
        private EditorInteractionManager _interactionManager;

        public MainPage()
        {
            InitializeComponent();

            this.BackgroundColor = Colors.Gray;
            EditorArea.BackgroundColor = Colors.White;
            EditorArea.HorizontalOptions = LayoutOptions.Center;
            EditorArea.VerticalOptions = LayoutOptions.Center;

            // DB
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "labels.db3");
            _dbService = new DatabaseService(dbPath);

            // 비동기 로드
            _ = LoadLabels();

            // 선택 표시용 Border
            var selectionIndicator = new Border
            {
                Stroke = Colors.Blue,
                StrokeThickness = 2,
                BackgroundColor = Colors.Transparent,
                InputTransparent = true,
                IsVisible = false
            };
            EditorArea.Children.Add(selectionIndicator);

            _interactionManager = new EditorInteractionManager(EditorArea, selectionIndicator);
        }

        private async Task LoadLabels()
        {
            Labels = await _dbService.GetLabelsAsync();
            LabelListView.ItemsSource = Labels;
        }

        // mm→화면픽셀
        private double MmToScreenPx(double mm)
        {
            double screenDpi = DeviceDisplay.MainDisplayInfo.Density * 160.0;
            double pxPerMm = screenDpi / 25.4;
            return mm * pxPerMm;
        }

        // 화면픽셀→mm
        private double ScreenPxToMm(double px)
        {
            double screenDpi = DeviceDisplay.MainDisplayInfo.Density * 160.0;
            double pxPerMm = screenDpi / 25.4;
            return px / pxPerMm;
        }

        // 도트→화면픽셀 (int→double)
        private double DotsToScreenPx(int dots)
        {
            if (currentLabelDesign == null) return dots;
            // dots -> mm -> px
            double mm = dots * 25.4 / currentLabelDesign.DPI;
            return MmToScreenPx(mm);
        }

        // 화면픽셀→도트
        private int ScreenPxToDots(double px)
        {
            if (currentLabelDesign == null) return (int)px;
            double mm = ScreenPxToMm(px);
            int dots = (int)Math.Round(mm * currentLabelDesign.DPI / 25.4);
            return dots;
        }

        private async void OnNewLabelClicked(object sender, EventArgs e)
        {
            string labelName = await DisplayPromptAsync("New Label", "Enter label name:");
            if (string.IsNullOrEmpty(labelName)) return;

            int dpi = 203;
            while (true)
            {
                string dpiStr = await DisplayPromptAsync("New Label", "DPI (203/300/600):", initialValue: "203");
                if (int.TryParse(dpiStr, out dpi) && (dpi == 203 || dpi == 300 || dpi == 600))
                    break;
                await DisplayAlert("Invalid", "올바른 DPI(203/300/600)을 입력하세요.", "OK");
            }

            string wStr = await DisplayPromptAsync("New Label", "width(mm):", initialValue: "50");
            double wMm = double.TryParse(wStr, out double wVal) ? wVal : 50;
            string hStr = await DisplayPromptAsync("New Label", "height(mm):", initialValue: "30");
            double hMm = double.TryParse(hStr, out double hVal) ? hVal : 30;

            currentLabelDesign = new LabelModel
            {
                LabelName = labelName,
                DPI = dpi,
                PaperWidthMm = wMm,
                PaperHeightMm = hMm
            };

            int result = await _dbService.SaveLabelAsync(currentLabelDesign);
            if (result > 0)
            {
                await LoadLabels();
                LabelListView.SelectedItem = Labels.FirstOrDefault(x => x.Id == currentLabelDesign.Id);
            }

            EditorArea.Children.Clear();
            EditorArea.Children.Add(_interactionManager.SelectionIndicator);

            EditorArea.WidthRequest = MmToScreenPx(currentLabelDesign.PaperWidthMm);
            EditorArea.HeightRequest = MmToScreenPx(currentLabelDesign.PaperHeightMm);
        }

        private async void OnDeleteLabelClicked(object sender, EventArgs e)
        {
            if (currentLabelDesign == null)
            {
                await DisplayAlert("Info", "No label selected.", "OK");
                return;
            }

            string confirm = await DisplayPromptAsync("Confirm Delete", 
                $"Type label name '{currentLabelDesign.LabelName}' to delete:");
            if (confirm != currentLabelDesign.LabelName)
            {
                await DisplayAlert("Cancelled", "Label name mismatch.", "OK");
                return;
            }

            int result = await _dbService.DeleteLabelAsync(currentLabelDesign);
            if (result > 0)
            {
                await DisplayAlert("Deleted", "Label deleted.", "OK");
                currentLabelDesign = null;
                EditorArea.Children.Clear();
                EditorArea.Children.Add(_interactionManager.SelectionIndicator);
                await LoadLabels();
            }
        }

        private async void OnSaveLabelClicked(object sender, EventArgs e)
        {
            if (currentLabelDesign == null)
            {
                await DisplayAlert("Error", "No label design available.", "OK");
                return;
            }

            // LabelExporter에 IEnum<View>를 넘겨주어야 함
            var allViews = EditorArea.Children.OfType<View>();
            var exporter = new LabelExporter(allViews, _interactionManager.SelectionIndicator,
                ScreenPxToMm, currentLabelDesign.DPI);

            string zpl = exporter.GenerateZPL();
            string pgl = exporter.GeneratePGL();

            currentLabelDesign.ZPL = zpl;
            currentLabelDesign.PGL = pgl;

            int result = await _dbService.SaveLabelAsync(currentLabelDesign);
            if (result > 0)
                await DisplayAlert("Saved", "Label saved.", "OK");
            else
                await DisplayAlert("Error", "Save failed.", "OK");

            // 갱신
            await LoadLabels();
        }

        private async void OnAddTextClicked(object sender, EventArgs e)
        {
            if (currentLabelDesign == null) return;

            // 문자열 입력
            string text = await DisplayPromptAsync("Add Text", "Enter text:");
            if (string.IsNullOrEmpty(text)) return;

            // 폰트 사이즈 (도트 단위) 입력
            int fontDots = 40; 
            string inputSize = await DisplayPromptAsync("Font Size (dots)", "Default 40", initialValue: "40");
            if (!int.TryParse(inputSize, out fontDots)) fontDots = 40;

            // 텍스트 길이에 따라 대략적인 Bounding Box 추정 (가로=문자수*폰트, 세로=폰트)
            int length = text.Length;
            int totalWidthDots = length * fontDots;
            int totalHeightDots = fontDots;

            // 도트→화면 픽셀 변환
            double wPx = DotsToScreenPx(totalWidthDots);
            double hPx = DotsToScreenPx(totalHeightDots);

            // 기본 위치 (10도트,10도트)
            double xPx = DotsToScreenPx(10);
            double yPx = DotsToScreenPx(10);
            var rect = _interactionManager.ClampRect(new Rect(xPx, yPx, wPx, hPx));

            // 화면용 Label
            var lbl = new Label
            {
                Text = text,
                BackgroundColor = Colors.White,
                TextColor = Colors.Black,
                FontSize = DotsToScreenPx(fontDots) // 화면에서도 비슷한 크기로 보이게
            };
            AbsoluteLayout.SetLayoutBounds(lbl, rect);
            AbsoluteLayout.SetLayoutFlags(lbl, AbsoluteLayoutFlags.None);

            // ClassId: "Text:{문자열}|{폰트사이즈}"
            lbl.ClassId = $"Text:{text}|{fontDots}";

            _interactionManager.AddDragAndGesture(lbl);
            EditorArea.Children.Add(lbl);
        }
        private async void OnAddBarcode1DClicked(object sender, EventArgs e)
        {
            if (currentLabelDesign == null) return;

            string data = await DisplayPromptAsync("Add 1D Barcode", "Enter barcode data:");
            if (string.IsNullOrEmpty(data)) return;

            // Code128 모듈 계산
            int totalModules = BarcodeHelper.GetCode128ModuleCount(data);
            if (totalModules < 1) totalModules = 1;

            int quietZone = 10;
            int mw = 2; // 모듈폭 2도트
            int heightDots = 50; // 바코드 높이
            int totalWidthDots = (totalModules + quietZone) * mw;

            double wPx = DotsToScreenPx(totalWidthDots);
            double hPx = DotsToScreenPx(heightDots);

            double xPx = DotsToScreenPx(10);
            double yPx = DotsToScreenPx(60);

            var rect = _interactionManager.ClampRect(new Rect(xPx, yPx, wPx, hPx));

            var source = BarcodeHelper.GenerateExactBarcodeImage(data, BarcodeFormat.CODE_128,
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
            _interactionManager.AddDragAndGesture(img);
            EditorArea.Children.Add(img);
        }

        private async void OnAddBarcode2DClicked(object sender, EventArgs e)
        {
            if (currentLabelDesign == null) return;

            string data = await DisplayPromptAsync("Add 2D Barcode", "Enter QR data:");
            if (string.IsNullOrEmpty(data)) return;

            int moduleCount = BarcodeHelper.GetQrModuleCount(data);
            if (moduleCount < 1) moduleCount = 1;

            int mag = 4; // 확대배수
            int totalDots = moduleCount * mag;

            double sizePx = DotsToScreenPx(totalDots);

            double xPx = DotsToScreenPx(10);
            double yPx = DotsToScreenPx(120);

            var rect = _interactionManager.ClampRect(new Rect(xPx, yPx, sizePx, sizePx));

            var source = BarcodeHelper.GenerateExactBarcodeImage(data, BarcodeFormat.QR_CODE,
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
            _interactionManager.AddDragAndGesture(img);
            EditorArea.Children.Add(img);
        }

        private void OnBringToFrontClicked(object sender, EventArgs e)
        {
            _interactionManager.BringToFront();
        }

        private void OnSendToBackClicked(object sender, EventArgs e)
        {
            _interactionManager.SendToBack();
        }

        private void LabelListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _interactionManager.ClearSelection();

            if (LabelListView.SelectedItem is LabelModel model)
            {
                currentLabelDesign = model;
                EditorArea.Children.Clear();
                EditorArea.Children.Add(_interactionManager.SelectionIndicator);

                EditorArea.WidthRequest = MmToScreenPx(currentLabelDesign.PaperWidthMm);
                EditorArea.HeightRequest = MmToScreenPx(currentLabelDesign.PaperHeightMm);

                // ZPL 파서 생성자에서 Func<int,double>로 dots→screenPx 받음
                var parser = new ZplParser(
                    EditorArea.Width,
                    EditorArea.Height,
                    _interactionManager.AddDragAndGesture,
                    DotsToScreenPx
                );

                var views = parser.Parse(model.ZPL ?? "");
                foreach (var v in views)
                    EditorArea.Children.Add(v);
            }
        }
    }
}
