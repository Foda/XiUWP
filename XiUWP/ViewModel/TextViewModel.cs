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
using XiUWP.Util;
using System.Reactive;

namespace XiUWP.ViewModel
{
    public class TextViewModel : ReactiveObject
    {
        private const int LINE_HEIGHT = 16;
        private XIService _xiService;

        private CanvasControl _rootCanvas;
        private CanvasControl _gutterCanvas;
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

        private bool _showMarkdownPreview;
        public bool ShowMarkdownPreview
        {
            get { return _showMarkdownPreview; }
            private set { this.RaiseAndSetIfChanged(ref _showMarkdownPreview, value); }
        }

        public bool IsDraggingMouse { get { return _isDraggingMouse; } }

        public ReactiveCommand SaveCommand { get; }

        private int _visibleLineCount = 0;
        private int _firstVisibleLine = 0;
        private int _lastVisibleLine = 0;
        private bool _isDraggingMouse = false;

        private StringBuilder _cachedText = new StringBuilder();

        public TextViewModel(CanvasControl rootCanvas, CanvasControl gutterCanvas)
        {
            _rootCanvas = rootCanvas;
            _rootCanvas.Draw += _rootCanvas_Draw;

            _gutterCanvas = gutterCanvas;
            _gutterCanvas.Draw += _gutterCanvas_Draw;

            _textFormat = new CanvasTextFormat();
            _textFormat.FontFamily = "Consolas";
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

            SaveCommand = ReactiveCommand.CreateFromTask(Save);

            this.WhenAnyValue(vm => vm.ScrollValue)
                .Subscribe(_ => UpdateScroll());
        }

        public async Task OpenFile(string file)
        {
            _lines.Clear();
            await _xiService.OpenNewView(file);
        }

        private async Task Save()
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
            _isDraggingMouse = true;
        }

        public void PointerMoved(Point position)
        {
            if (!_isDraggingMouse)
                return;

            var lineAndIndex = GetLineAndCursorIndexFromPos(position);
            var lineIndex = lineAndIndex.Item1;
            var charIndex = lineAndIndex.Item2;

            _xiService.Drag(lineIndex, charIndex);
        }

        public void PointerReleased(Point position)
        {
            var lineAndIndex = GetLineAndCursorIndexFromPos(position);
            var lineIndex = lineAndIndex.Item1;
            var charIndex = lineAndIndex.Item2;

            _xiService.Drag(lineIndex, charIndex);

            _isDraggingMouse = false;
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
                if (line.TextLayout == null)
                    continue;

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
            if (!char.IsControl(character))
            {
                _xiService.Insert(character.ToString());
            }
        }

        public async Task KeyPressed(VirtualKey key)
        {
            var hasCtrlMod = Window.Current.CoreWindow
                .GetKeyState(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

            var hasShiftMod = Window.Current.CoreWindow
                .GetKeyState(Windows.System.VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            
            switch (key)
            {
                case VirtualKey.Tab:
                    await _xiService.GenericEdit("insert_tab");
                    break;
                case VirtualKey.Enter:
                    await _xiService.GenericEdit("insert_newline");
                    break;
                case VirtualKey.Back:
                case VirtualKey.Delete:
                    var deleteMode = hasCtrlMod ? "delete_word_backward" : "delete_backward";
                    await _xiService.GenericEdit(deleteMode);
                    break;
                case VirtualKey.Control:
                    break;
                case VirtualKey.Shift:
                    break;
                case VirtualKey.Up:
                    await _xiService.GenericEdit("move_up");
                    break;
                case VirtualKey.Down:
                    await _xiService.GenericEdit("move_down");
                    break;
                case VirtualKey.Left:
                    if (_cursorIndex == 0)
                    {
                        await _xiService.GenericEdit("move_up");
                        await _xiService.GenericEdit("move_to_right_end_of_line");
                    }
                    else
                    {
                        _xiService.GenericEdit("move_left");
                    }
                    break;
                case VirtualKey.Right:
                    if (LineUtils.IsNextToPlainLineBreak(_currentLine, _cursorIndex, LogicalDirection.Forward))
                    {
                        await _xiService.GenericEdit("move_to_left_end_of_line");
                        await _xiService.GenericEdit("move_down");
                    }
                    else
                    {
                        await _xiService.GenericEdit("move_right");
                    }
                    break;
                case VirtualKey.Z:
                    if (hasCtrlMod)
                    {
                        await _xiService.GenericEdit("undo");
                    }
                    break;
                case VirtualKey.Y:
                    if (hasCtrlMod)
                    {
                        await _xiService.GenericEdit("redo");
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

            var lineHeight = 16;
            var lineCount = (int)(_rootCanvas.ActualHeight / lineHeight);

            _visibleLineCount = lineCount + 10;

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

        private void UpdateScroll()
        {
            if (!_lines.Any())
                return;

            var lineHeight = 16;// Math.Max(1, _lines[0].Bounds.Height);
            _firstVisibleLine = (int)Math.Max(0, ScrollValue / lineHeight);
            _lastVisibleLine = (int)Math.Min(_firstVisibleLine + _visibleLineCount, _lines.Count - 1);

            if (_lastVisibleLine <= _firstVisibleLine)
                return;

            _xiService.Scroll(_firstVisibleLine, _lastVisibleLine);
            _rootCanvas.Invalidate();
            _gutterCanvas.Invalidate();
        }

        private async void UpdateTextView(XiUpdateOperation update)
        {
            var oldIdx = 0;
            var newLines = new List<LineSpan>();

            await DispatcherHelper.ExecuteOnUIThreadAsync(() =>
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
                                    var newLine = new LineSpan(
                                        line.Text.Trim(),
                                        line.Style);

                                    newLine.Layout(_rootCanvas, _textFormat,
                                        (int)_rootCanvas.ActualWidth,
                                        (int)_rootCanvas.ActualHeight);

                                    newLines.Add(newLine);

                                    if (line.Cursor != null)
                                    {
                                        _cursorIndex = line.Cursor[0];
                                        _currentLine = line.Text;
                                        _cursorLineIndex = newLines.Count - 1;

                                        if (LineUtils.IsNextToPlainLineBreak(_currentLine, _cursorIndex, LogicalDirection.Backward))
                                        {
                                            _xiService.GenericEdit("move_left");
                                        }
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

                _lines = newLines;

                if (_lines.Any())
                {
                    ScrollViewportSize = _rootCanvas.ActualHeight;
                    MaxScroll = Math.Max(1, ((_lines.Count * 16) - ScrollViewportSize));
                    UpdateVisibleLineCount();
                }
            }).ConfigureAwait(false);
        }

        private void _rootCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            _cachedText.Clear();

            int yOffset = 0;
          
            for (int i = _firstVisibleLine; i < Math.Min(_lastVisibleLine, _lines.Count); i++)
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
                _cachedText.AppendLine(_lines[i].Text);
                yOffset += (int)(_lines[i].Bounds.Height);
            }
        }

        private void _gutterCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var lineBuilder = new StringBuilder();

            for (int i = _firstVisibleLine; i < Math.Min(_lastVisibleLine, _lines.Count); i++)
            {
                if (_lines[i].TextLayout == null)
                    continue;

                lineBuilder.AppendLine(i.ToString());
            }

            var textLayout = new CanvasTextLayout(_gutterCanvas,
                lineBuilder.ToString(), _textFormat, 
                (float)_gutterCanvas.ActualWidth, 
                (float)_gutterCanvas.ActualHeight);

            args.DrawingSession.DrawTextLayout(textLayout,
                        new Vector2(0, 0), Windows.UI.Colors.SlateGray);
        }
    }
}