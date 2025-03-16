using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using JHLabel.Models;
using JHLabel.Services;
using JHLabel.Views;
using ZXing.Net.Maui; // ZXing.Net.MAUI 패키지 필요

namespace JHLabel
{
    public partial class MainPage : ContentPage
    {
        DatabaseService _dbService;
        public List<LabelModel> Labels { get; set; }
        public MainPage()
        {
            InitializeComponent();
            // SQLite 데이터베이스 초기화 (AppDataDirectory에 labels.db3 생성)
            string dbPath = System.IO.Path.Combine(FileSystem.AppDataDirectory, "labels.db3");
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
            string barcodeData = await DisplayPromptAsync("Add 1D Barcode", "Enter barcode data:");
            if (string.IsNullOrEmpty(barcodeData))
                return;

            var barcodeView = new BarcodeGeneratorView
            {
                BarcodeValue = barcodeData,
                BarcodeFormat = ZXing.Net.Maui.BarcodeFormat.Code128,
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Start,
                WidthRequest = 200,
                HeightRequest = 80,
                BackgroundColor = Colors.White
            };
            AbsoluteLayout.SetLayoutBounds(barcodeView, new Rect(150, 150, barcodeView.WidthRequest, barcodeView.HeightRequest));
            AbsoluteLayout.SetLayoutFlags(barcodeView, AbsoluteLayoutFlags.None);
            AddDragGesture(barcodeView);
            barcodeView.ClassId = "Barcode1D:" + barcodeData;
            EditorArea.Children.Add(barcodeView);
        }

        // 2D 바코드 추가 (QR 코드)
        private async void OnAddBarcode2DClicked(object sender, EventArgs e)
        {
            string barcodeData = await DisplayPromptAsync("Add 2D Barcode", "Enter QR code data:");
            if (string.IsNullOrEmpty(barcodeData))
                return;

            var barcodeView = new BarcodeGeneratorView
            {
                BarcodeValue = barcodeData,
                BarcodeFormat = ZXing.Net.Maui.BarcodeFormat.QR_CODE,
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Start,
                WidthRequest = 150,
                HeightRequest = 150,
                BackgroundColor = Colors.White
            };
            AbsoluteLayout.SetLayoutBounds(barcodeView, new Rect(200, 200, barcodeView.WidthRequest, barcodeView.HeightRequest));
            AbsoluteLayout.SetLayoutFlags(barcodeView, AbsoluteLayoutFlags.None);
            AddDragGesture(barcodeView);
            barcodeView.ClassId = "Barcode2D:" + barcodeData;
            EditorArea.Children.Add(barcodeView);
        }

        // 표(Table) 추가
        private async void OnAddTableClicked(object sender, EventArgs e)
        {
            string rowsStr = await DisplayPromptAsync("Add Table", "Enter number of rows:", initialValue: "2");
            string colsStr = await DisplayPromptAsync("Add Table", "Enter number of columns:", initialValue: "2");
            if (int.TryParse(rowsStr, out int rows) && int.TryParse(colsStr, out int cols))
            {
                var tableView = new TableDrawableView
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

        // 현재 편집된 내용을 ZPL로 변환하여 DB에 저장
        private async void OnSaveLabelClicked(object sender, EventArgs e)
        {
            string labelName = await DisplayPromptAsync("Save Label", "Enter label name:");
            if (string.IsNullOrEmpty(labelName))
                return;
            string zpl = GenerateZPLFromEditor();
            var model = new LabelModel { LabelName = labelName, ZPL = zpl };
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
                var bounds = AbsoluteLayout.GetLayoutBounds(view);
                int x = (int)bounds.X;
                int y = (int)bounds.Y;

                if (view is Label lbl)
                {
                    zpl += $"^FO{x},{y}^A0N,30,30^FD{lbl.Text}^FS";
                }
                else if (view is BarcodeGeneratorView barcodeView)
                {
                    string id = barcodeView.ClassId ?? "";
                    if (id.StartsWith("Barcode1D:"))
                    {
                        string data = id.Substring("Barcode1D:".Length);
                        zpl += $"^FO{x},{y}^BCN,80,Y,N,N^FD{data}^FS";
                    }
                    else if (id.StartsWith("Barcode2D:"))
                    {
                        string data = id.Substring("Barcode2D:".Length);
                        zpl += $"^FO{x},{y}^BQN,2,5^FDMM,A{data}^FS";
                    }
                }
                else if (view is TableDrawableView tableView)
                {
                    string id = tableView.ClassId ?? "";
                    var parts = id.Split(':');
                    if (parts.Length == 5)
                    {
                        int rows = int.Parse(parts[1]);
                        int cols = int.Parse(parts[2]);
                        int cellWidth = int.Parse(parts[3]);
                        int cellHeight = int.Parse(parts[4]);
                        int tableWidth = cols * cellWidth;
                        int tableHeight = rows * cellHeight;
                        // 외곽 사각형
                        zpl += $"^FO{x},{y}^GB{tableWidth},{tableHeight},3^FS";
                        // 수직선
                        for (int i = 1; i < cols; i++)
                        {
                            int lineX = x + i * cellWidth;
                            zpl += $"^FO{lineX},{y}^GB3,{tableHeight},3^FS";
                        }
                        // 수평선
                        for (int j = 1; j < rows; j++)
                        {
                            int lineY = y + j * cellHeight;
                            zpl += $"^FO{x},{lineY}^GB{tableWidth},3,3^FS";
                        }
                    }
                }
            }
            zpl += "^XZ";
            return zpl;
        }

        // 저장된 라벨을 선택하면 ZPL을 파싱하여 UI 요소로 재구성 (각 요소별 정규식 파서)
        private void LabelListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LabelListView.SelectedItem is LabelModel model)
            {
                EditorArea.Children.Clear();
                ParseZPLToEditor(model.ZPL);
            }
        }

        // 텍스트, 1D/2D 바코드, 표를 간단히 파싱하여 재생성합니다.
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
                    var barcodeView = new BarcodeGeneratorView
                    {
                        BarcodeValue = data,
                        BarcodeFormat = ZXing.Net.Maui.BarcodeFormat.Code128,
                        WidthRequest = 200,
                        HeightRequest = 80,
                        BackgroundColor = Colors.White
                    };
                    AbsoluteLayout.SetLayoutBounds(barcodeView, new Rect(x, y, barcodeView.WidthRequest, barcodeView.HeightRequest));
                    AbsoluteLayout.SetLayoutFlags(barcodeView, AbsoluteLayoutFlags.None);
                    AddDragGesture(barcodeView);
                    barcodeView.ClassId = "Barcode1D:" + data;
                    EditorArea.Children.Add(barcodeView);
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
                    var barcodeView = new BarcodeGeneratorView
                    {
                        BarcodeValue = data,
                        BarcodeFormat = ZXing.Net.Maui.BarcodeFormat.QR_CODE,
                        WidthRequest = 150,
                        HeightRequest = 150,
                        BackgroundColor = Colors.White
                    };
                    AbsoluteLayout.SetLayoutBounds(barcodeView, new Rect(x, y, barcodeView.WidthRequest, barcodeView.HeightRequest));
                    AbsoluteLayout.SetLayoutFlags(barcodeView, AbsoluteLayoutFlags.None);
                    AddDragGesture(barcodeView);
                    barcodeView.ClassId = "Barcode2D:" + data;
                    EditorArea.Children.Add(barcodeView);
                }
            }

            // 표(Table) 파싱 (외곽 사각형만 인식, 내부 그리드는 기본 셀 크기를 가정)
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
                    var tableView = new TableDrawableView
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

        // 모든 추가된 뷰에 대해 드래그(팬) 제스처를 부여하는 헬퍼 메서드
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
    }
}
