using Microsoft.Maui.Controls;
using Microsoft.Maui.Layouts;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using JHLabel.Models;
using JHLabel.Services;
using JHLabel.Utils;
using ZXing; // ZXing.Net (무료 라이브러리)

namespace JHLabel
{
    public partial class MainPage : ContentPage
    {
        DatabaseService _dbService;
        public List<LabelModel> Labels { get; set; } = new List<LabelModel>();

        public MainPage()
        {
            InitializeComponent();
            // SQLite 데이터베이스 초기화 (AppDataDirectory에 labels.db3 생성)
            
            string dbPath;
            #if DEBUG
                // 개발용: 현재 작업 디렉토리(예: bin/Debug/net10.0-windows...)에 저장
                dbPath = Path.Combine(Directory.GetCurrentDirectory(), "labels.db3");
            #else
                // 릴리즈 시에는 앱 전용 저장소 사용
                dbPath = Path.Combine(FileSystem.AppDataDirectory, "labels.db3");
            #endif
            _dbService = new DatabaseService(dbPath);
            LoadLabels();
        }

        async void LoadLabels()
        {
            Labels = await _dbService.GetLabelsAsync();
            LabelListView.ItemsSource = Labels;
        }

        // 텍스트 추가
        private async void OnAddTextClicked(object sender, EventArgs e)
        {
            string text = await DisplayPromptAsync("Add Text", "Enter text:");
            if (string.IsNullOrEmpty(text))
                return;

            var lbl = new Label { Text = text, BackgroundColor = Colors.White, TextColor = Colors.Black };
            AbsoluteLayout.SetLayoutBounds(lbl, new Rect(100, 100, -1, -1));
            AbsoluteLayout.SetLayoutFlags(lbl, AbsoluteLayoutFlags.None);
            AddDragGesture(lbl);
            lbl.ClassId = "Text:" + text;
            EditorArea.Children.Add(lbl);
        }

        // 1D 바코드 추가 (Code128)
        private async void OnAddBarcode1DClicked(object sender, EventArgs e)
        {
            string data = await DisplayPromptAsync("Add 1D Barcode", "Enter barcode data:");
            if (string.IsNullOrEmpty(data))
                return;
            // Barcode 생성
            var barcodeImageSource = BarcodeGenerator.GenerateBarcodeImage(data, BarcodeFormat.CODE_128, 200, 80);
            var img = new Image { Source = barcodeImageSource, WidthRequest = 200, HeightRequest = 80 };
            AbsoluteLayout.SetLayoutBounds(img, new Rect(150, 150, 200, 80));
            AbsoluteLayout.SetLayoutFlags(img, AbsoluteLayoutFlags.None);
            AddDragGesture(img);
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
            AddDragGesture(img);
            img.ClassId = "Barcode2D:" + data;
            EditorArea.Children.Add(img);
        }

        // 표(Table) 추가
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
                    HeightRequest = rows * 100
                };
                AbsoluteLayout.SetLayoutBounds(tableView, new Rect(250, 250, tableView.WidthRequest, tableView.HeightRequest));
                AbsoluteLayout.SetLayoutFlags(tableView, AbsoluteLayoutFlags.None);
                AddDragGesture(tableView);
                tableView.ClassId = $"Table:{rows}:{cols}:150:100";
                EditorArea.Children.Add(tableView);
            }
        }

        // 현재 편집된 내용을 ZPL 문자열로 변환하여 DB에 저장
        private async void OnSaveLabelClicked(object sender, EventArgs e)
        {
            string name = await DisplayPromptAsync("Save Label", "Enter label name:");
            if (string.IsNullOrEmpty(name))
                return;
            string zpl = GenerateZPLFromEditor();
            var model = new LabelModel { LabelName = name, ZPL = zpl };
            await _dbService.SaveLabelAsync(model);
            await DisplayAlert("Saved", "Label saved successfully", "OK");
            LoadLabels();
        }

        // 편집 영역의 각 요소를 ZPL 명령어로 변환 (텍스트, 1D/2D 바코드, 표)
        private string GenerateZPLFromEditor()
        {
            string zpl = "^XA";
            foreach (var view in EditorArea.Children)
            {
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

        // 모든 추가된 뷰에 대해 드래그(팬) 제스처 부여
        private void AddDragGesture(View view)
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
                        break;
                }
            };
            view.GestureRecognizers.Add(panGesture);
        }

        private void LabelListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LabelListView.SelectedItem is LabelModel model)
            {
                EditorArea.Children.Clear();
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
                    AbsoluteLayout.SetLayoutBounds(lbl, new Rect(x, y, -1, -1));
                    AbsoluteLayout.SetLayoutFlags(lbl, AbsoluteLayoutFlags.None);
                    AddDragGesture(lbl);
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
                    AddDragGesture(img);
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
                    AddDragGesture(img);
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
                        HeightRequest = height
                    };
                    AbsoluteLayout.SetLayoutBounds(tableView, new Rect(x, y, width, height));
                    AbsoluteLayout.SetLayoutFlags(tableView, AbsoluteLayoutFlags.None);
                    AddDragGesture(tableView);
                    tableView.ClassId = $"Table:{rows}:{cols}:150:100";
                    EditorArea.Children.Add(tableView);
                }
            }
        }
    }
}
