using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace JHLabel.Views
{
    public partial class TableDrawableView : ContentView
    {
        // 표의 행, 열, 셀 크기, 선 두께 등 프로퍼티
        public int Rows { get; set; } = 2;
        public int Columns { get; set; } = 2;
        public float CellWidth { get; set; } = 150;
        public float CellHeight { get; set; } = 100;
        public float LineThickness { get; set; } = 3;

        public TableDrawableView()
        {
            InitializeComponent();
            graphicsView.Drawable = new TableDrawable(this);
        }
    }

    // 실제 그리기 로직: 외곽 사각형과 내부 수직/수평선을 그립니다.
    public class TableDrawable : IDrawable
    {
        private readonly TableDrawableView _view;
        public TableDrawable(TableDrawableView view)
        {
            _view = view;
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.StrokeColor = Colors.Black;
            canvas.StrokeSize = _view.LineThickness;

            float totalWidth = _view.Columns * _view.CellWidth;
            float totalHeight = _view.Rows * _view.CellHeight;
            // 외곽 사각형
            canvas.DrawRectangle(0, 0, totalWidth, totalHeight);

            // 수직선 그리기
            for (int i = 1; i < _view.Columns; i++)
            {
                float x = i * _view.CellWidth;
                canvas.DrawLine(x, 0, x, totalHeight);
            }
            // 수평선 그리기
            for (int j = 1; j < _view.Rows; j++)
            {
                float y = j * _view.CellHeight;
                canvas.DrawLine(0, y, totalWidth, y);
            }
        }
    }
}
