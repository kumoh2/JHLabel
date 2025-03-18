using Microsoft.Maui.Layouts;
using System.Text.RegularExpressions;
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

        // 선택된 뷰 및 선택 표시용 오버레이(Border)
        private View? _selectedView;
        private Border _selectionIndicator;

        // 현재 편집 중인 라벨 디자인 데이터 (디자인 데이터는 mm 단위, DPI 등)
        private LabelModel? currentLabelDesign;

        public MainPage()
        {
            InitializeComponent();

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

        // 새 라벨 생성: 사용자에게 라벨 이름, DPI, 용지 폭, 높이(mm)를 입력받아 새 디자인 데이터 생성
        private async void OnNewLabelClicked(object sender, EventArgs e)
        {
            string labelName = await DisplayPromptAsync("New Label", "Enter label name:");
            if (string.IsNullOrEmpty(labelName))
                return;
            string dpiStr = await DisplayPromptAsync("New Label", "Enter printer DPI (e.g., 203,300,600):", initialValue: "203");
            if (!int.TryParse(dpiStr, out int dpi))
                dpi = 203;
            string widthStr = await DisplayPromptAsync("New Label", "Enter label width in mm:", initialValue: "45");
            if (!double.TryParse(widthStr, out double widthMm))
                widthMm = 45;
            string heightStr = await DisplayPromptAsync("New Label", "Enter label height in mm:", initialValue: "70");
            if (!double.TryParse(heightStr, out double heightMm))
                heightMm = 70;

            // 새로운 라벨 디자인 데이터 생성 (ZPL/PGL은 빈 문자열로 초기화)
            currentLabelDesign = new LabelModel
            {
                LabelName = labelName,
                DPI = dpi,
                PaperWidthMm = widthMm,
                PaperHeightMm = heightMm,
                ZPL = string.Empty,
                PGL = string.Empty
            };

            // 새 라벨을 DB에 저장 후 라벨 리스트 UI 갱신
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

            // EditorArea 초기화
            EditorArea.Children.Clear();
            EditorArea.Children.Add(_selectionIndicator);
        }

        private async void OnDeleteLabelClicked(object sender, EventArgs e)
        {
            if (currentLabelDesign == null)
            {
                await DisplayAlert("Info", "No label selected.", "OK");
                return;
            }
            
            // 삭제 확인: 사용자가 라벨 이름을 정확하게 입력하도록 함
            string confirmationName = await DisplayPromptAsync("Confirm Delete", 
                $"To delete '{currentLabelDesign.LabelName}', please type the label name:");
            
            if (confirmationName != currentLabelDesign.LabelName)
            {
                await DisplayAlert("Error", "Label name does not match. Deletion cancelled.", "OK");
                return;
            }
            
            // DB에서 삭제
            int result = await _dbService.DeleteLabelAsync(currentLabelDesign);
            if (result > 0)
            {
                await DisplayAlert("Deleted", "Label deleted successfully", "OK");
                // 선택된 라벨을 해제하고 에디터 영역 초기화
                currentLabelDesign = null;
                EditorArea.Children.Clear();
                EditorArea.Children.Add(_selectionIndicator);
                // 라벨 리스트 UI 갱신
                LoadLabels();
            }
            else
            {
                await DisplayAlert("Error", "Failed to delete label.", "OK");
            }
        }

        // Save Label: 현재 편집 중인 라벨 디자인 데이터를 ZPL/PGL 문자열로 변환하고 DB에 저장(업데이트)
        private async void OnSaveLabelClicked(object sender, EventArgs e)
        {
            if (currentLabelDesign == null)
            {
                await DisplayAlert("Error", "No label design available. Create a new label first.", "OK");
                return;
            }

            // DPI와 용지 크기를 기반으로 출력 문자열 생성
            string zpl = GenerateZPLFromEditor(currentLabelDesign.DPI, currentLabelDesign.PaperWidthMm, currentLabelDesign.PaperHeightMm);
            string pgl = GeneratePGLFromEditor(currentLabelDesign.DPI, currentLabelDesign.PaperWidthMm, currentLabelDesign.PaperHeightMm);
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

        private async void OnAddTextClicked(object sender, EventArgs e)
        {
            string text = await DisplayPromptAsync("Add Text", "Enter text:");
            if (string.IsNullOrEmpty(text))
                return;

            var lbl = new Label { Text = text, BackgroundColor = Colors.White, TextColor = Colors.Black };
            // 기본 크기를 mm 단위로 설정 (예: 100mm 위치, 100x30mm 크기)
            AbsoluteLayout.SetLayoutBounds(lbl, new Rect(100, 100, 100, 30));
            AbsoluteLayout.SetLayoutFlags(lbl, AbsoluteLayoutFlags.None);
            AddDragAndGesture(lbl);
            lbl.ClassId = "Text:" + text;
            EditorArea.Children.Add(lbl);
        }

        // 1D 바코드 추가 (Code128)
        private async void OnAddBarcode1DClicked(object sender, EventArgs e)
        {
            string data = await DisplayPromptAsync("Add 1D Barcode", "Enter barcode data:");
            if (string.IsNullOrEmpty(data))
                return;
            var barcodeImageSource = BarcodeGenerator.GenerateBarcodeImage(data, BarcodeFormat.CODE_128, 200, 80);
            var img = new Image { Source = barcodeImageSource, WidthRequest = 200, HeightRequest = 80 };
            AbsoluteLayout.SetLayoutBounds(img, new Rect(150, 150, 200, 80));
            AbsoluteLayout.SetLayoutFlags(img, AbsoluteLayoutFlags.None);
            AddDragAndGesture(img);
            img.ClassId = "Barcode1D:" + data;
            EditorArea.Children.Add(img);
        }

        // 2D 바코드 추가 (QR 코드)
        private async void OnAddBarcode2DClicked(object sender, EventArgs e)
        {
            string data = await DisplayPromptAsync("Add 2D Barcode", "Enter QR code data:");
            if (string.IsNullOrEmpty(data))
                return;
            var barcodeImageSource = BarcodeGenerator.GenerateBarcodeImage(data, BarcodeFormat.QR_CODE, 150, 150);
            var img = new Image { Source = barcodeImageSource, WidthRequest = 150, HeightRequest = 150 };
            AbsoluteLayout.SetLayoutBounds(img, new Rect(200, 200, 150, 150));
            AbsoluteLayout.SetLayoutFlags(img, AbsoluteLayoutFlags.None);
            AddDragAndGesture(img);
            img.ClassId = "Barcode2D:" + data;
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

        // ZPL 문자열 생성: 디자인 데이터는 mm 단위로 저장되어 있으며, 출력 시에만 DPI 변환 적용
        private string GenerateZPLFromEditor(int dpi, double paperWidthMm, double paperHeightMm)
        {
            string zpl = "^XA";
            foreach (var view in EditorArea.Children)
            {
                if (view == _selectionIndicator)
                    continue;

                var bounds = (Rect)((BindableObject)view).GetValue(AbsoluteLayout.LayoutBoundsProperty);
                int x = (int)bounds.X;
                int y = (int)bounds.Y;
                if (view is Label lbl)
                {
                    zpl += $"^FO{x},{y}^A0N,30,30^FD{lbl.Text}^FS";
                }
                else if (view is Image img)
                {
                    if (!string.IsNullOrEmpty(img.ClassId))
                    {
                        if (img.ClassId.StartsWith("Barcode1D:"))
                        {
                            string data = img.ClassId.Substring("Barcode1D:".Length);
                            zpl += $"^FO{x},{y}^BCN,80,Y,N,N^FD{data}^FS";
                        }
                        else if (img.ClassId.StartsWith("Barcode2D:"))
                        {
                            string data = img.ClassId.Substring("Barcode2D:".Length);
                            zpl += $"^FO{x},{y}^BQN,2,5^FDMM,A{data}^FS";
                        }
                    }
                }
            }
            zpl += "^XZ";
            return zpl;
        }

        // PGL 문자열 생성: 동일한 방식으로 DPI 및 용지 크기 적용
        private string GeneratePGLFromEditor(int dpi, double paperWidthMm, double paperHeightMm)
        {
            string pgl = "<PGL_START>\n";
            foreach (var view in EditorArea.Children)
            {
                if (view == _selectionIndicator)
                    continue;
                var bounds = (Rect)((BindableObject)view).GetValue(AbsoluteLayout.LayoutBoundsProperty);
                int x = (int)bounds.X;
                int y = (int)bounds.Y;

                if (view is Label lbl)
                {
                    pgl += $" TEXT {x},{y},0,30,\"{lbl.Text}\";\n";
                }
                else if (view is Image img)
                {
                    if (!string.IsNullOrEmpty(img.ClassId))
                    {
                        if (img.ClassId.StartsWith("Barcode1D:"))
                        {
                            string data = img.ClassId.Substring("Barcode1D:".Length);
                            pgl += $" BARCODE1D {x},{y},CODE128,80,\"{data}\";\n";
                        }
                        else if (img.ClassId.StartsWith("Barcode2D:"))
                        {
                            string data = img.ClassId.Substring("Barcode2D:".Length);
                            pgl += $" BARCODE2D {x},{y},QR,150,\"{data}\";\n";
                        }
                    }
                }
            }
            pgl += "<PGL_END>";
            return pgl;
        }

        // 드래그 및 선택 제스처 부여
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
                        AbsoluteLayout.SetLayoutBounds(view, new Rect(newX, newY, current.Width, current.Height));
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

        // 개체 선택 시 호출
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

        // 라벨 전환 시: 선택 해제 및 편집 영역 재구성 (ZPL 파싱)
        private void LabelListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedView = null;
            _selectionIndicator.IsVisible = false;
            if (LabelListView.SelectedItem is LabelModel model)
            {
                // 선택된 라벨을 현재 편집 중인 라벨로 지정
                currentLabelDesign = model;

                EditorArea.Children.Clear();
                EditorArea.Children.Add(_selectionIndicator);
                // 로드 시 ParseZPLToEditor 호출 (필요에 따라 구현)
                ParseZPLToEditor(model.ZPL);
            }
        }

        // ZPL 문자열을 EditorArea에 파싱 (예시; 필요에 따라 구현)
        private void ParseZPLToEditor(string zpl)
        {
            // 텍스트 파싱
            var regexText = new Regex(@"\^FO(\d+),(\d+)\^A0N,30,30\^FD([^\\^]+)\^FS");
            foreach (Match match in regexText.Matches(zpl))
            {
                if (match.Groups.Count == 4)
                {
                    int x = int.Parse(match.Groups[1].Value);
                    int y = int.Parse(match.Groups[2].Value);
                    string content = match.Groups[3].Value;
                    var lbl = new Label { Text = content, BackgroundColor = Colors.White, TextColor = Colors.Black };
                    AbsoluteLayout.SetLayoutBounds(lbl, new Rect(x, y, 100, 30));
                    AbsoluteLayout.SetLayoutFlags(lbl, AbsoluteLayoutFlags.None);
                    AddDragAndGesture(lbl);
                    lbl.ClassId = "Text:" + content;
                    EditorArea.Children.Add(lbl);
                }
            }

            // 1D 바코드 파싱
            var regexBarcode1D = new Regex(@"\^FO(\d+),(\d+)\^BCN,80,Y,N,N\^FD([^\\^]+)\^FS");
            foreach (Match match in regexBarcode1D.Matches(zpl))
            {
                if (match.Groups.Count == 4)
                {
                    int x = int.Parse(match.Groups[1].Value);
                    int y = int.Parse(match.Groups[2].Value);
                    string data = match.Groups[3].Value;
                    var img = new Image { Source = BarcodeGenerator.GenerateBarcodeImage(data, BarcodeFormat.CODE_128, 200, 80), WidthRequest = 200, HeightRequest = 80 };
                    AbsoluteLayout.SetLayoutBounds(img, new Rect(x, y, 200, 80));
                    AbsoluteLayout.SetLayoutFlags(img, AbsoluteLayoutFlags.None);
                    AddDragAndGesture(img);
                    img.ClassId = "Barcode1D:" + data;
                    EditorArea.Children.Add(img);
                }
            }

            // 2D 바코드 파싱
            var regexBarcode2D = new Regex(@"\^FO(\d+),(\d+)\^BQN,2,5\^FDMM,A([^\\^]+)\^FS");
            foreach (Match match in regexBarcode2D.Matches(zpl))
            {
                if (match.Groups.Count == 4)
                {
                    int x = int.Parse(match.Groups[1].Value);
                    int y = int.Parse(match.Groups[2].Value);
                    string data = match.Groups[3].Value;
                    var img = new Image { Source = BarcodeGenerator.GenerateBarcodeImage(data, BarcodeFormat.QR_CODE, 150, 150), WidthRequest = 150, HeightRequest = 150 };
                    AbsoluteLayout.SetLayoutBounds(img, new Rect(x, y, 150, 150));
                    AbsoluteLayout.SetLayoutFlags(img, AbsoluteLayoutFlags.None);
                    AddDragAndGesture(img);
                    img.ClassId = "Barcode2D:" + data;
                    EditorArea.Children.Add(img);
                }
            }
        }
    }
}
