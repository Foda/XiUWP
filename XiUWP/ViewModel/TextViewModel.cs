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
using System.Diagnostics;
using XiUWP.Model;

namespace XiUWP.ViewModel
{
    public class TextViewModel : ReactiveObject
    {
        private const int LINE_HEIGHT = 16;
        private XIService _xiService;

        private CanvasControl _rootCanvas;
        private CanvasTextFormat _textFormat;
        private List<LineSpan> _lines = new List<LineSpan>();
        private string _currentLine = "";
        private int _cursorIndex = 0;
        private int _cursorLineIndex = 0;

        private Windows.UI.Color _selectColor;

        private float _cursorLeft = 0;
        public float CursorLeft
        {
            get { return _cursorLeft; }
            private set { this.RaiseAndSetIfChanged(ref _cursorLeft, value); }
        }

        private float _cursorTop = 0;
        public float CursorTop
        {
            get { return _cursorTop; }
            private set { this.RaiseAndSetIfChanged(ref _cursorTop, value); }
        }

        private double _scrollViewportSize = 0;
        public double ScrollViewportSize
        {
            get { return _scrollViewportSize; }
            private set { this.RaiseAndSetIfChanged(ref _scrollViewportSize, value); }
        }

        private double _maxScroll = 0;
        public double MaxScroll
        {
            get { return _maxScroll; }
            private set { this.RaiseAndSetIfChanged(ref _maxScroll, value); }
        }

        private double _scrollValue = 0;
        public double ScrollValue
        {
            get { return _scrollValue; }
            set { this.RaiseAndSetIfChanged(ref _scrollValue, value); }
        }

        private int _visibleLineCount = 0;
        private int _firstVisibleLine = 0;
        private int _lastVisibleLine = 0;

        private object LINE_LOCK = new object();

        public TextViewModel(CanvasControl rootCanvas)
        {
            _rootCanvas = rootCanvas;
            _rootCanvas.Draw += _rootCanvas_Draw;

            _textFormat = new CanvasTextFormat();
            _textFormat.FontFamily = "Segoe UI";
            _textFormat.FontSize = 12;
            _textFormat.WordWrapping = CanvasWordWrapping.NoWrap;

            _selectColor = Windows.UI.Color.FromArgb(128, 0, 120, 215);
            
            _xiService = new XIService();
            _xiService.UpdateObservable.Subscribe(update => UpdateTextView(update));
            _xiService.ScrollToObservable.Subscribe(async scrollTo => await ScrollToLine(scrollTo));
            _xiService.StyleObservable.Subscribe(update =>
            {
                Debug.WriteLine(update.ID);
            });

            this.WhenAnyValue(vm => vm.ScrollValue)
                .Subscribe(_ => UpdateScroll());

            Task.Run(_xiService.OpenNewView);
        }

        public async Task Save()
        {
            await _xiService.SaveView();
        }

        public void PointerPressed(Point position)
        {
            var lineAndIndex = GetLineAndCursorIndexFromPos(position);
            var lineIndex = lineAndIndex.Item1;
            var charIndex = lineAndIndex.Item2;

            _xiService.Click(lineIndex, charIndex, 0, 1);

            _rootCanvas.Invalidate();
        }

        public void PointerReleased(Point position)
        {
            var lineAndIndex = GetLineAndCursorIndexFromPos(position);
            var lineIndex = lineAndIndex.Item1;
            var charIndex = lineAndIndex.Item2;

            _xiService.Drag(lineIndex, charIndex);
        }

        private Tuple<int, int> GetLineAndCursorIndexFromPos(Point position)
        {
            CanvasTextLayoutRegion hitRegion;
            int cursorIdx = 0;
            int cursorLine = _firstVisibleLine;

            var yOffset = 0;

            for (int i = _firstVisibleLine; i < _lastVisibleLine; i++)
            {
                var line = _lines[i];
                var offsetBounds = new Rect(0, yOffset,
                    _rootCanvas.ActualWidth,
                    line.Bounds.Height);

                if (offsetBounds.Contains(position))
                {
                    // Try and get the position inside the line of text
                    if (line.TextLayout.HitTest((float)position.X, 1, out hitRegion))
                    {
                        cursorIdx = hitRegion.CharacterIndex;
                    }
                    else
                    {
                        // Position wasn't actually inside the bounds, so set it to the end of the line
                        cursorIdx = _lines[cursorLine].Text.Length;
                    }
                    break;
                }

                cursorLine++;
                yOffset += (int)(line.Bounds.Height);
            }

            return new Tuple<int, int>(cursorLine, cursorIdx);
        }

        public void TextEntered(char character)
        {
            var hasCtrlMod = Window.Current.CoreWindow
                   .GetKeyState(Windows.System.VirtualKey.Control) == Windows.UI.Core.CoreVirtualKeyStates.Down;

            if (character == 'z' && hasCtrlMod)
            {
                _xiService.GenericEdit("undo");
            }
            else if (character == 'y' && hasCtrlMod)
            {
                _xiService.GenericEdit("undo");
            }
            else
            {
                _xiService.Insert(character.ToString());
            }
        }

        public void KeyPressed(VirtualKey key)
        {
            var hasCtrlMod = Window.Current.CoreWindow
                .GetKeyState(Windows.System.VirtualKey.Control) == Windows.UI.Core.CoreVirtualKeyStates.Down;

            var hasShiftMod = Window.Current.CoreWindow
                .GetKeyState(Windows.System.VirtualKey.Shift) == Windows.UI.Core.CoreVirtualKeyStates.Down;
            
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
                    if (_cursorIndex + 1 >= _currentLine.Length)
                    {
                        _xiService.GenericEdit("move_down");
                        _xiService.GenericEdit("move_to_left_end_of_line");
                    }
                    else
                    {
                        _xiService.GenericEdit("move_right");
                    }
                    break;
                default:
                    break;
            }

            _rootCanvas.Invalidate();
        }

        public void UpdateVisibleLineCount()
        {
            if (!_lines.Any())
                return;

            var lineHeight = _lines[0].Bounds.Height;
            var lineCount = (int)(_rootCanvas.ActualHeight / lineHeight);

            _visibleLineCount = lineCount;

            UpdateScroll();
        }

        private async Task ScrollToLine(XiScrollToMsg scrollTo)
        {
            if (!_lines.Any())
                return;

            // Only scroll if the line is not in the visible area
            if (scrollTo.Line < _firstVisibleLine ||
                scrollTo.Line > _lastVisibleLine)
            {
                var lineHeight = 16;
                await DispatcherHelper.ExecuteOnUIThreadAsync(() =>
                {
                    ScrollValue = scrollTo.Line * lineHeight;
                });
            }
        }

        public void UpdateScroll()
        {
            if (!_lines.Any())
                return;

            var lineHeight = 16;// Math.Max(1, _lines[0].Bounds.Height);
            _firstVisibleLine = (int)Math.Max(0, ScrollValue / lineHeight);
            _lastVisibleLine = (int)Math.Min(_firstVisibleLine + _visibleLineCount, _lines.Count - 1);
            
            _xiService.Scroll(_firstVisibleLine, _lastVisibleLine);
            _rootCanvas.Invalidate();
        }

        private async void UpdateTextView(XiUpdateOperation update)
        {
            var oldIdx = 0;
            var newLines = new List<LineSpan>();
            var cursorLine = -1;

            lock (LINE_LOCK)
            {
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
                                    newLines.Add(_lines[i]);
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
                                    newLines.Add(new LineSpan(
                                        line.Text.Trim(),
                                        line.Style));

                                    if (line.Cursor != null)
                                    {
                                        cursorLine = newLines.Count - 1;
                                        _cursorIndex = line.Cursor[0];
                                        _currentLine = line.Text;
                                        _cursorLineIndex = cursorLine;
                                    }
                                }
                            }
                            break;
                        case "invalidate":
                            // The "invalidate" op appends n invalid lines to the new lines array.
                            for (int i = 0; i < op.LinesChangeCount; i++)
                            {
                                newLines.Add(new LineSpan("", new List<int>()));
                            }
                            break;
                        default:
                            break;
                    }
                }
            }

            _lines = newLines;

            if (newLines.Any())
            {
                await DispatcherHelper.ExecuteOnUIThreadAsync(() =>
                {
                    var yOffset = 0;
                    for (int i = 0; i < newLines.Count; i++)
                    {
                        newLines[i].Layout(_rootCanvas, _textFormat,
                            (int)_rootCanvas.ActualWidth,
                            (int)_rootCanvas.ActualHeight);

                        yOffset += (int)(newLines[i].Bounds.Height);
                    }

                    MaxScroll = newLines.Count * newLines[0].Bounds.Height;
                    if (ScrollViewportSize != _rootCanvas.ActualHeight)
                    {
                        ScrollViewportSize = _rootCanvas.ActualHeight;
                        UpdateVisibleLineCount();
                    }
                }).ConfigureAwait(false);
            }

            _rootCanvas.Invalidate();
        }

        private void _rootCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            int yOffset = 0;
            
            lock (LINE_LOCK)
            {
                for (int i = _firstVisibleLine; i < _lastVisibleLine; i++)
                {
                    if (_lines[i].TextLayout == null)
                        continue;

                    args.DrawingSession.DrawTextLayout(_lines[i].TextLayout,
                            new Vector2(0, yOffset), Windows.UI.Colors.Black);

                    // Draw select bounds
                    if (_lines[i].HasSelectBounds)
                    {
                        args.DrawingSession.FillRectangle(
                            (float)_lines[i].SelectBounds.X,
                            yOffset,
                            (float)_lines[i].SelectBounds.Width,
                            (float)_lines[i].SelectBounds.Height,
                            _selectColor);
                    }

                    // Update cursor position
                    if (i == _cursorLineIndex)
                    {
                        var pos = _lines[i].GetCaretPosition(_cursorIndex);
                        CursorLeft = pos.X;
                        CursorTop = pos.Y + yOffset;
                    }

                    yOffset += (int)(_lines[i].Bounds.Height);
                }
            }
        }

        public async Task BoldSelection()
        {
            await WrapSelectionInChars("**");
        }

        public async Task ItalicsSelection()
        {
            await WrapSelectionInChars("_");
        }

        public async Task HeaderCurrentLine(int headerLevel)
        {
            var headerBuilder = new StringBuilder();
            for (int i = 0; i < headerLevel; i++)
            {
                headerBuilder.Append("#");
            }
            headerBuilder.Append(" ");

            var oldCursorIndex = _cursorIndex + headerLevel;

            await _xiService.Click(_cursorLineIndex, 0, 0, 1);
            await _xiService.Insert(headerBuilder.ToString());
            await _xiService.Click(_cursorLineIndex, oldCursorIndex, 0, 1);
        }

        private async Task WrapSelectionInChars(string whatToInsert)
        {
            for (int i = 0; i < _lines.Count; i++)
            {
                if (!_lines[i].HasSelectBounds)
                    continue;

                var startIdx = _lines[i].SelectedStartCharIndex;
                var endIdx = _lines[i].SelectedEndCharIndex + 1;

                await _xiService.Click(i, startIdx, 0, 1);
                await _xiService.Insert(whatToInsert);

                await _xiService.Click(i, endIdx, 0, 1);
                await _xiService.Insert(whatToInsert);
            }
        }
    }
}