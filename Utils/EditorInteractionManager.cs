using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Layouts;

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

        // Expose the selection indicator as a public property.
        public Border SelectionIndicator
        {
            get { return _selectionIndicator; }
        }

        // Clears the current selection.
        public void ClearSelection()
        {
            _selectedView = null;
            _selectionIndicator.IsVisible = false;
        }

        /// <summary>
        /// Adds drag and tap gesture recognizers to the view.
        /// </summary>
        public void AddDragAndGesture(View view)
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

        public Rect ClampRect(Rect rect)
        {
            double areaWidth = _editorArea.Width;
            double areaHeight = _editorArea.Height;
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
