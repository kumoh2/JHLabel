using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Layouts;
using System.Linq;

namespace JHLabel.Utils
{
    public class EditorInteractionManager
    {
        private readonly AbsoluteLayout _editorArea;
        private readonly Border _selectionIndicator;
        private View? _selectedView;

        public EditorInteractionManager(AbsoluteLayout editorArea, Border selectionIndicator)
        {
            _editorArea = editorArea;
            _selectionIndicator = selectionIndicator;
        }

        public Border SelectionIndicator => _selectionIndicator;

        public void ClearSelection()
        {
            _selectedView = null;
            _selectionIndicator.IsVisible = false;
        }

        public void AddDragAndGesture(View view)
        {
            var panGesture = new PanGestureRecognizer();
            double startX = 0, startY = 0;

            panGesture.PanUpdated += (s, e) =>
            {
                switch (e.StatusType)
                {
                    case GestureStatus.Started:
                        var b = AbsoluteLayout.GetLayoutBounds(view);
                        startX = b.X;
                        startY = b.Y;
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
            tapGesture.Tapped += (s, e) => SelectView(view);
            view.GestureRecognizers.Add(tapGesture);
        }

        public Rect ClampRect(Rect rect)
        {
            double areaWidth = _editorArea.Width;
            double areaHeight = _editorArea.Height;
            double x = rect.X, y = rect.Y, w = rect.Width, h = rect.Height;

            if (x < 0) x = 0;
            if (y < 0) y = 0;
            if (x + w > areaWidth) x = Math.Max(0, areaWidth - w);
            if (y + h > areaHeight) y = Math.Max(0, areaHeight - h);

            return new Rect(x, y, w, h);
        }

        public void SelectView(View view)
        {
            _selectedView = view;
            UpdateSelectionIndicator();
        }

        public void UpdateSelectionIndicator()
        {
            if (_selectedView == null)
            {
                _selectionIndicator.IsVisible = false;
                return;
            }

            var bounds = AbsoluteLayout.GetLayoutBounds(_selectedView);

            // Measure returns a Size in .NET MAUI
            // (no .Request)
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                var measuredSize = _selectedView.Measure(double.PositiveInfinity, double.PositiveInfinity);
                bounds = new Rect(bounds.X, bounds.Y, measuredSize.Width, measuredSize.Height);
                AbsoluteLayout.SetLayoutBounds(_selectedView, bounds);
            }

            double margin = 2;
            AbsoluteLayout.SetLayoutBounds(_selectionIndicator,
                new Rect(bounds.X - margin, bounds.Y - margin,
                         bounds.Width + margin * 2, bounds.Height + margin * 2));
            _selectionIndicator.IsVisible = true;
        }

        public void BringToFront()
        {
            if (_selectedView != null)
            {
                _editorArea.Children.Remove(_selectedView);
                _editorArea.Children.Add(_selectedView);
            }
        }

        public void SendToBack()
        {
            if (_selectedView != null)
            {
                _editorArea.Children.Remove(_selectedView);
                _editorArea.Children.Insert(0, _selectedView);
            }
        }
    }
}
