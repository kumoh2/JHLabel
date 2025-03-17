using Microsoft.Maui.Controls;
using Microsoft.Maui.Layouts;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
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

        // 선택된 뷰 및 선택 표시용 오버레이(Border)와 리사이즈 핸들(BoxView)
        private View? _selectedView;
        private Border _selectionIndicator;
        private BoxView _resizeHandle;

        public MainPage()
        {
            InitializeComponent();
            // SQLite 데이터베이스 초기화
            string dbPath;
#if DEBUG
            dbPath = Path.Combine(Directory.GetCurrentDirectory(), "labels.db3");
#else
            dbPath = Path.Combine(FileSystem.AppDataDirectory, "labels.db3");
#endif
            _dbService = new DatabaseService(dbPath);
            LoadLabels();

            // 선택 표시용 Border 초기화 (Frame 대신 사용)
            _selectionIndicator = new Border
            {
                Stroke = Colors.Blue,
                StrokeThickness = 2,
                BackgroundColor = Colors.Transparent,
                InputTransparent = true,
                IsVisible = false
            };
            EditorArea.Children.Add(_selectionIndicator);

            // 리사이즈 핸들 초기화
            _resizeHandle = new BoxView
            {
                Color = Colors.Red,
                WidthRequest = 20,
                HeightRequest = 20,
                IsVisible = false
            };

            // Pan 제스처를 사용하여 리사이즈 핸들이 드래그되면 선택된 객체의 크기를 조절
            var panResize = new PanGestureRecognizer();
            double initialWidth = 0, initialHeight = 0;
            panResize.PanUpdated += (s, e) =>
            {
                if (_selectedView == null)
                    return;
                if (e.StatusType == GestureStatus.Started)
                {
                    var bounds = AbsoluteLayout.GetLayoutBounds(_selectedView);
                    initialWidth = bounds.Width;
                    initialHeight = bounds.Height;
                }
                else if (e.StatusType == GestureStatus.Running)
                {
                    var bounds = AbsoluteLayout.GetLayoutBounds(_selectedView);
                    double newWidth = initialWidth + e.TotalX;
                    double newHeight = initialHeight + e.TotalY;
                    if (newWidth < 20) newWidth = 20;
                    if (newHeight < 20) newHeight = 20;
                    AbsoluteLayout.SetLayoutBounds(_selectedView, new Rect(bounds.X, bounds.Y, newWidth, newHeight));
                    UpdateSelectionIndicator();
                }
            };
            _resizeHandle.GestureRecognizers.Add(panResize);
            EditorArea.Children.Add(_resizeHandle);
        }

        async void LoadLabels()
        {
            Labels = await _dbService.GetLabelsAsync();
            LabelListView.ItemsSource = Labels;
        }

        // 텍스트 추가 (기본 크기를 지정하여 Measure 문제를 회피)
        private async void OnAddTextClicked(object sender, EventArgs e)
        {
            string text = await DisplayPromptAsync("Add Text", "Enter text:");
            if (string.IsNullOrEmpty(text))
                return;

            var lbl = new Label { Text = text, BackgroundColor = Colors.White, TextColor = Colors.Black };
            // 기본 크기를 명시적으로 설정
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

        // 표(Table) 추가 – Minimum 크기를 지정하여 제스처가 제대로 동작하도록 함
        private async void OnAddTableClicked(object sender, EventArgs e)
        {
            string rowsStr = await DisplayPromptAsync("Add Table", "Enter number of rows:", initialValue: "2");
            string colsStr = await DisplayPromptAsync("Add Table", "Enter number of columns:", initialValue: "2");
            if (int.TryParse(rowsStr, out int rows) && int.TryParse(colsStr, out int cols))
            {
                var tableView = new Views.TableDrawableView
                {
                    Rows = rows,
                    Columns = cols,
                    CellWidth = 150,
                    CellHeight = 100,
                    LineThickness = 3,
                    BackgroundColor = Colors.Transparent,
                    WidthRequest = cols * 150,
                    HeightRequest = rows * 100,
                    MinimumWidthRequest = cols * 150,
                    MinimumHeightRequest = rows * 100
                };
                AbsoluteLayout.SetLayoutBounds(tableView, new Rect(250, 250, cols * 150, rows * 100));
                AbsoluteLayout.SetLayoutFlags(tableView, AbsoluteLayoutFlags.None);
                AddDragAndGesture(tableView);
                tableView.ClassId = $"Table:{rows}:{cols}:150:100";
                EditorArea.Children.Add(tableView);
            }
        }

        private async void OnSaveLabelClicked(object sender, EventArgs e)
        {
            // Save 로직 구현
            string name = await DisplayPromptAsync("Save Label", "Enter label name:");
            if (string.IsNullOrEmpty(name))
                return;
            string zpl = GenerateZPLFromEditor();
            string pgl = GeneratePGLFromEditor();  // PGL 문자열 생성
            var model = new LabelModel { LabelName = name, ZPL = zpl, PGL = pgl };

            int result = await _dbService.SaveLabelAsync(model);

            if (result > 0)
            {
                await DisplayAlert("Saved", "Label saved successfully", "OK");
                LoadLabels();
            }
            else if (result == 0)
            {
                await DisplayAlert("Not Saved", "Label not saved", "OK");
            }
        }

        // 레이어 순서 조정: Bring to Front
        private void OnBringToFrontClicked(object sender, EventArgs e)
        {
            if (_selectedView == null)
            {
                DisplayAlert("Info", "No object selected.", "OK");
                return;
            }
            EditorArea.Children.Remove(_selectedView);
            EditorArea.Children.Add(_selectedView);
            // 선택 표시와 리사이즈 핸들도 최상단에 배치
            EditorArea.Children.Remove(_selectionIndicator);
            EditorArea.Children.Add(_selectionIndicator);
            EditorArea.Children.Remove(_resizeHandle);
            EditorArea.Children.Add(_resizeHandle);
        }

        // 레이어 순서 조정: Send to Back
        private void OnSendToBackClicked(object sender, EventArgs e)
        {
            if (_selectedView == null)
            {
                DisplayAlert("Info", "No object selected.", "OK");
                return;
            }
            EditorArea.Children.Remove(_selectedView);
            EditorArea.Children.Insert(0, _selectedView);
            // 선택 표시와 리사이즈 핸들은 최상단에 유지
            EditorArea.Children.Remove(_selectionIndicator);
            EditorArea.Children.Add(_selectionIndicator);
            EditorArea.Children.Remove(_resizeHandle);
            EditorArea.Children.Add(_resizeHandle);
        }

        // 현재 편집된 내용을 ZPL 문자열로 변환 (선택 표시 및 리사이즈 핸들은 제외)
        private string GenerateZPLFromEditor()
        {
            string zpl = "^XA";
            foreach (var view in EditorArea.Children)
            {
                if (view == _selectionIndicator || view == _resizeHandle)
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
                else if (view is Views.TableDrawableView tableView)
                {
                    if (!string.IsNullOrEmpty(tableView.ClassId))
                    {
                        var parts = tableView.ClassId.Split(':');
                        if (parts.Length == 5)
                        {
                            int rows = int.Parse(parts[1]);
                            int cols = int.Parse(parts[2]);
                            int cellWidth = int.Parse(parts[3]);
                            int cellHeight = int.Parse(parts[4]);
                            int tableWidth = cols * cellWidth;
                            int tableHeight = rows * cellHeight;
                            zpl += $"^FO{x},{y}^GB{tableWidth},{tableHeight},3^FS";
                            for (int i = 1; i < cols; i++)
                            {
                                int lineX = x + i * cellWidth;
                                zpl += $"^FO{lineX},{y}^GB3,{tableHeight},3^FS";
                            }
                            for (int j = 1; j < rows; j++)
                            {
                                int lineY = y + j * cellHeight;
                                zpl += $"^FO{x},{lineY}^GB{tableWidth},3,3^FS";
                            }
                        }
                    }
                }
            }
            zpl += "^XZ";
            return zpl;
        }

        // PGL 명령어 생성 메서드 추가 (프린트로닉스 PGL 언어 형식 예시)
        private string GeneratePGLFromEditor()
        {
            // 시작 및 종료 명령은 프린트로닉스 PGL 언어 규격에 맞게 변경하세요.
            string pgl = "<PGL_START>\n";
            foreach (var view in EditorArea.Children)
            {
                if (view == _selectionIndicator || view == _resizeHandle)
                    continue;

                var bounds = (Rect)((BindableObject)view).GetValue(AbsoluteLayout.LayoutBoundsProperty);
                int x = (int)bounds.X;
                int y = (int)bounds.Y;

                if (view is Label lbl)
                {
                    // 텍스트: TEXT x,y,폰트번호,크기,"텍스트"
                    pgl += $" TEXT {x},{y},0,30,\"{lbl.Text}\";\n";
                }
                else if (view is Image img)
                {
                    if (!string.IsNullOrEmpty(img.ClassId))
                    {
                        if (img.ClassId.StartsWith("Barcode1D:"))
                        {
                            string data = img.ClassId.Substring("Barcode1D:".Length);
                            // 1D 바코드: BARCODE1D x,y, CODE128, 높이,"데이터"
                            pgl += $" BARCODE1D {x},{y},CODE128,80,\"{data}\";\n";
                        }
                        else if (img.ClassId.StartsWith("Barcode2D:"))
                        {
                            string data = img.ClassId.Substring("Barcode2D:".Length);
                            // 2D 바코드 (QR): BARCODE2D x,y,QR,사이즈,"데이터"
                            pgl += $" BARCODE2D {x},{y},QR,150,\"{data}\";\n";
                        }
                    }
                }
                else if (view is Views.TableDrawableView tableView)
                {
                    if (!string.IsNullOrEmpty(tableView.ClassId))
                    {
                        var parts = tableView.ClassId.Split(':');
                        if (parts.Length == 5)
                        {
                            int rows = int.Parse(parts[1]);
                            int cols = int.Parse(parts[2]);
                            int cellWidth = int.Parse(parts[3]);
                            int cellHeight = int.Parse(parts[4]);
                            int tableWidth = cols * cellWidth;
                            int tableHeight = rows * cellHeight;
                            // 표의 외곽선을 그리는 명령어 (예: RECT x,y,width,height,두께)
                            pgl += $" RECT {x},{y},{tableWidth},{tableHeight},3;\n";
                            // 열 구분선 (수직선)
                            for (int i = 1; i < cols; i++)
                            {
                                int lineX = x + i * cellWidth;
                                pgl += $" LINE {lineX},{y} TO {lineX},{y + tableHeight},3;\n";
                            }
                            // 행 구분선 (수평선)
                            for (int j = 1; j < rows; j++)
                            {
                                int lineY = y + j * cellHeight;
                                pgl += $" LINE {x},{lineY} TO {x + tableWidth},{lineY},3;\n";
                            }
                        }
                    }
                }
            }
            pgl += "<PGL_END>";
            return pgl;
        }

        // 모든 추가된 뷰에 대해 드래그 및 선택 제스처 부여 (핀치 제스처 제거)
        private void AddDragAndGesture(View view)
        {
            // 드래그 제스처
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

            // 탭 제스처: 선택
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (s, e) => { SelectView(view); };
            view.GestureRecognizers.Add(tapGesture);
        }

        // 선택 시 호출: 선택된 뷰 저장 및 선택 표시 업데이트
        private void SelectView(View view)
        {
            _selectedView = view;
            UpdateSelectionIndicator();
        }

        // 선택된 객체의 위치/크기에 맞춰 선택 표시와 리사이즈 핸들 업데이트
        private void UpdateSelectionIndicator()
        {
            if (_selectedView == null)
            {
                _selectionIndicator.IsVisible = false;
                _resizeHandle.IsVisible = false;
                return;
            }
            var bounds = AbsoluteLayout.GetLayoutBounds(_selectedView);
            // 만약 width/height가 0 이하이면 Measure를 통해 보정
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                var size = _selectedView.Measure(double.PositiveInfinity, double.PositiveInfinity);
                bounds = new Rect(bounds.X, bounds.Y, size.Width, size.Height);
                AbsoluteLayout.SetLayoutBounds(_selectedView, bounds);
            }
            double margin = 2;
            AbsoluteLayout.SetLayoutBounds(_selectionIndicator, new Rect(bounds.X - margin, bounds.Y - margin, bounds.Width + margin * 2, bounds.Height + margin * 2));
            _selectionIndicator.IsVisible = true;

            double handleSize = 20;
            AbsoluteLayout.SetLayoutBounds(_resizeHandle, new Rect(bounds.X + bounds.Width - handleSize, bounds.Y + bounds.Height - handleSize, handleSize, handleSize));
            _resizeHandle.IsVisible = true;
      
            // _resizeHandle이 다른 요소들보다 항상 위에 보이도록 강제로 최상단으로 올림
            EditorArea.Children.Remove(_resizeHandle);
            EditorArea.Children.Add(_resizeHandle);
        }

        // 라벨 전환 시 선택 해제 및 편집 영역 재구성
        private void LabelListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedView = null;
            _selectionIndicator.IsVisible = false;
            _resizeHandle.IsVisible = false;

            if (LabelListView.SelectedItem is LabelModel model)
            {
                EditorArea.Children.Clear();
                EditorArea.Children.Add(_selectionIndicator);
                EditorArea.Children.Add(_resizeHandle);
                ParseZPLToEditor(model.ZPL);
            }
        }

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

            // 표(Table) 파싱
            var regexTable = new Regex(@"\^FO(\d+),(\d+)\^GB(\d+),(\d+),3\^FS");
            foreach (Match match in regexTable.Matches(zpl))
            {
                if (match.Groups.Count == 5)
                {
                    int x = int.Parse(match.Groups[1].Value);
                    int y = int.Parse(match.Groups[2].Value);
                    int width = int.Parse(match.Groups[3].Value);
                    int height = int.Parse(match.Groups[4].Value);
                    int cols = width / 150;
                    int rows = height / 100;
                    var tableView = new Views.TableDrawableView
                    {
                        Rows = rows,
                        Columns = cols,
                        CellWidth = 150,
                        CellHeight = 100,
                        LineThickness = 3,
                        BackgroundColor = Colors.Transparent,
                        WidthRequest = width,
                        HeightRequest = height,
                        MinimumWidthRequest = width,
                        MinimumHeightRequest = height
                    };
                    AbsoluteLayout.SetLayoutBounds(tableView, new Rect(x, y, width, height));
                    AbsoluteLayout.SetLayoutFlags(tableView, AbsoluteLayoutFlags.None);
                    AddDragAndGesture(tableView);
                    tableView.ClassId = $"Table:{rows}:{cols}:150:100";
                    EditorArea.Children.Add(tableView);
                }
            }
        }
    }
}
