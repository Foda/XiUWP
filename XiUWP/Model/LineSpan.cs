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

        public bool HasSelectBounds
        {
            get { return SelectBounds != Rect.Empty; }
        }

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
            SelectBounds = Rect.Empty;
        }

        public void Layout(
            ICanvasResourceCreator canvasResourceCreator, 
            CanvasTextFormat textFormat,
            int availableWidth, 
            int availableHeight)
        {
            if (TextLayout != null)
                TextLayout.Dispose();

            TextLayout = new CanvasTextLayout(
                        canvasResourceCreator,
                        Text,
                        textFormat,
                        availableWidth,
                        availableHeight);
            
            UpdateSelectionBounds();
        }

        public Vector2 GetCaretPosition(int charIndex)
        {
            if (TextLayout == null)
                return Vector2.Zero;

            return TextLayout.GetCaretPosition(charIndex, false);
        }

        private void UpdateSelectionBounds()
        {
            if (StyleBlocks.Count == 3)
            {
                var startIdx = StyleBlocks[0];
                var endIdx = startIdx + StyleBlocks[1];

                var startPoint = GetCaretPosition(startIdx);
                var endPoint = GetCaretPosition(endIdx);

                SelectBounds = new Rect(
                    startPoint.X, 
                    0,
                    endPoint.X - startPoint.X,
                    Bounds.Height);
            }
        }
    }
}
