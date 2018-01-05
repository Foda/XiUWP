using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;

namespace XiUWP.Model
{
    public class LineSpan
    {
        public string Text { get; }
        public IList<int> StyleBlocks { get; }
        public CanvasTextLayout TextLayout { get; private set; }

        public Rect Bounds
        {
            get
            {
                if (TextLayout == null)
                    return Rect.Empty;

                return TextLayout.LayoutBounds;
            }
        }
        public Rect SelectBounds { get; private set; }

        public bool HasSelectBounds { get; private set; }

        public int SelectedStartCharIndex
        {
            get { return HasSelectBounds ? StyleBlocks[0] : 0; }
        }

        public int SelectedEndCharIndex
        {
            get { return HasSelectBounds ? StyleBlocks[0] + StyleBlocks[1] : 0; }
        }

        public LineSpan(string text, IList<int> styleBlocks)
        {
            Text = text;
            StyleBlocks = styleBlocks;
            TextLayout = null;
        }

        public void Layout(
            ICanvasResourceCreator canvasResourceCreator, 
            CanvasTextFormat textFormat,
            int availableWidth, 
            int availableHeight,
            int verticalOffset)
        {
            if (TextLayout != null)
                TextLayout.Dispose();

            TextLayout = new CanvasTextLayout(
                        canvasResourceCreator,
                        Text,
                        textFormat,
                        availableWidth,
                        availableHeight);

            UpdateSelectionBounds(verticalOffset);
        }

        public Vector2 GetCaretPosition(int charIndex)
        {
            if (TextLayout == null)
                return Vector2.Zero;

            return TextLayout.GetCaretPosition(charIndex, false);
        }

        private void UpdateSelectionBounds(int verticalOffset)
        {
            if (StyleBlocks.Count == 3)
            {
                HasSelectBounds = true;

                var startIdx = StyleBlocks[0];
                var endIdx = startIdx + StyleBlocks[1];

                var startPoint = GetCaretPosition(startIdx);
                var endPoint = GetCaretPosition(endIdx);

                SelectBounds = new Rect(
                    startPoint.X, 
                    startPoint.Y + verticalOffset,
                    endPoint.X - startPoint.X,
                    Bounds.Height);
            }
        }
    }
}
