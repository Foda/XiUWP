using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.Toolkit.Uwp.Helpers;
using Newtonsoft.Json;
using ReactiveUI;
using Windows.Foundation;
using Windows.System;
using XiUWP.Service;
using Windows.UI.Xaml;

namespace XiUWP.ViewModel
{
    public class TextViewModel : ReactiveObject
    {
        private const int LINE_HEIGHT = 16;
        private XIService _xiService;

        private CanvasControl _rootCanvas;
        private CanvasTextFormat _textFormat;
        private List<CanvasTextLayout> _lineLayouts = new List<CanvasTextLayout>();
        private List<string> _oldLines = new List<string>();
        private Vector2 _cursorPosition;
        private int _cursorIndex = 0;

        private object LINE_LOCK = new object();

        public TextViewModel(CanvasControl rootCanvas)
        {
            _rootCanvas = rootCanvas;
            _rootCanvas.Draw += _rootCanvas_Draw;

            _textFormat = new CanvasTextFormat();
            _textFormat.FontFamily = "Segoe UI";
            _textFormat.FontSize = 12;

            _xiService = new XIService();
            _xiService.UpdateObservable.Subscribe(update => UpdateTextView(update));

            Task.Run(_xiService.OpenNewView);
        }

        public async Task Save()
        {
            await _xiService.SaveView();
        }

        public void PointerPressed(Point position)
        {
            CanvasTextLayoutRegion hitRegion;
            var idx = 0;
            var yOffset = 0;

            foreach (var line in _lineLayouts)
            {
                var offsetBounds = new Rect(0, yOffset,
                    _rootCanvas.ActualWidth,
                    line.LayoutBoundsIncludingTrailingWhitespace.Height);

                if (offsetBounds.Contains(position))
                {
                    var posYOffset = position.Y - yOffset;
                    if (line.HitTest((float)position.X, (float)posYOffset, out hitRegion))
                    {
                        _cursorPosition = line.GetCaretPosition(hitRegion.CharacterIndex, false);
                        _cursorPosition.Y = _cursorPosition.Y + yOffset;

                        _xiService.Click(idx, hitRegion.CharacterIndex, 0, 1);
                        break;
                    }
                }

                idx++;
                yOffset += (int)(line.LayoutBoundsIncludingTrailingWhitespace.Height);
            }

            _rootCanvas.Invalidate();
        }

        public void TextEntered(string character)
        {
            _xiService.Insert(character);
        }

        public void KeyPressed(VirtualKey key)
        {
            var hasCtrlMod = Window.Current.CoreWindow
                .GetKeyState(Windows.System.VirtualKey.Control) == Windows.UI.Core.CoreVirtualKeyStates.Down;

            var hasShiftMod = Window.Current.CoreWindow
                .GetKeyState(Windows.System.VirtualKey.Shift) == Windows.UI.Core.CoreVirtualKeyStates.Down;
            
            var character = "";
            switch (key)
            {
                case VirtualKey.Tab:
                    _xiService.GenericEdit("insert_tab");
                    break;
                case VirtualKey.Enter:
                    _xiService.GenericEdit("insert_newline");
                    break;
                case VirtualKey.Back:
                case VirtualKey.Delete:
                    var deleteMode = hasCtrlMod ? "delete_word_backward" : "delete_backward";
                    _xiService.GenericEdit(deleteMode);
                    break;
                case VirtualKey.Control:
                    break;
                case VirtualKey.Shift:
                    break;
                case VirtualKey.Up:
                    _xiService.GenericEdit("move_up");
                    break;
                case VirtualKey.Down:
                    _xiService.GenericEdit("move_down");
                    break;
                case VirtualKey.Left:
                    _xiService.GenericEdit("move_left");
                    break;
                case VirtualKey.Right:
                    _xiService.GenericEdit("move_right");
                    break;
                case VirtualKey.Z:
                    if (hasCtrlMod)
                    {
                        _xiService.GenericEdit("undo");
                    }
                    else
                    {
                        character = hasShiftMod ? key.ToString().ToUpper() : key.ToString().ToLower();
                        _xiService.Insert(character);
                    }
                    break;
                case VirtualKey.Y:
                    if (hasCtrlMod)
                    {
                        _xiService.GenericEdit("redo");
                    }
                    else
                    {
                        character = hasShiftMod ? key.ToString().ToUpper() : key.ToString().ToLower();
                        _xiService.Insert(character);
                    }
                    break;
                default:
                    break;
            }

            _rootCanvas.Invalidate();
        }

        private async void UpdateTextView(XiUpdateOperation update)
        {
            var oldIdx = 0;
            var newLines = new List<string>();
            var cursorLine = -1;
            var cursorIdx = 0;
            
            foreach (var op in update.Operations)
            {
                switch (op.Operation)
                {
                    case "skip":
                        // The "skip" op increments old_ix by n.
                        oldIdx += op.LinesChangeCount;
                        break;
                    case "copy":
                        // The "copy" op appends the n lines [old_ix: old_ix + n] 
                        // to the new lines array, and increments old_ix by n.
                        {
                            for (int i = oldIdx; i < oldIdx + op.LinesChangeCount; i++)
                            {
                                newLines.Add(_oldLines[i]);
                            }
                            oldIdx += op.LinesChangeCount;
                            break;
                        }
                    case "update":
                        // The "update" op updates the cursor and / or style of n existing lines.
                        // As in "ins", n must equal lines.length.It also increments old_ix by n.
                        oldIdx += op.LinesChangeCount;
                        break;
                    case "ins":
                        {
                            // The "ins" op appends new lines, specified by the "lines"
                            // parameter, specified in more detail below. For this op, 
                            // n must equal lines.length(alternative: make n optional in this case). 
                            // It does not update old_ix.
                            foreach (var line in op.Lines)
                            {
                                newLines.Add(line.Text);

                                if (line.Cursor != null)
                                {
                                    cursorLine = newLines.Count - 1;
                                    cursorIdx = line.Cursor[0];
                                }
                            }
                        }
                        break;
                    case "invalidate":
                        // The "invalidate" op appends n invalid lines to the new lines array.
                        break;
                    default:
                        break;
                }
            }

            await DispatcherHelper.ExecuteOnUIThreadAsync(() =>
            {
                _lineLayouts.Clear();
                var yOffset = 0;
                for (int i = 0; i < newLines.Count; i++)
                {
                    var textLayout = new CanvasTextLayout(
                        _rootCanvas,
                        newLines[i],
                        _textFormat,
                        (int)_rootCanvas.ActualWidth,
                        (int)_rootCanvas.ActualHeight);

                    _lineLayouts.Add(textLayout);

                    if (i == cursorLine)
                    {
                        _cursorPosition = _lineLayouts[cursorLine].GetCaretPosition(cursorIdx, false);
                        _cursorPosition.Y = _cursorPosition.Y + yOffset;
                    }

                    yOffset += (int)(textLayout.LayoutBoundsIncludingTrailingWhitespace.Height);
                }
            });

            _rootCanvas.Invalidate();
            _oldLines = newLines;
        }

        private void _rootCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            //args.DrawingSession.TextAntialiasing = CanvasTextAntialiasing.ClearType;
            int yOffset = 0;

            lock (LINE_LOCK)
            {
                foreach (var line in _lineLayouts)
                {
                    args.DrawingSession.DrawTextLayout(line,
                            new Vector2(0, yOffset), Windows.UI.Colors.Black);

                    yOffset += Math.Max(16, (int)(line.LayoutBoundsIncludingTrailingWhitespace.Height));
                }
            }

            args.DrawingSession.DrawLine(_cursorPosition, _cursorPosition + new Vector2(0, 12),
                Windows.UI.Colors.Red, 2);
        }
    }
}
