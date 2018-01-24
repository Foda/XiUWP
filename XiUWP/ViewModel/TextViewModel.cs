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
using Windows.UI.Core;

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

        private Windows.UI.Color _selectColor = Windows.UI.Color.FromArgb(128, 0, 120, 215);

        private int _visibleLineCount = 0;
        private int _firstVisibleLine = 0;
        private int _lastVisibleLine = 0;

        private CursorAnchor _cursorAnchor = new CursorAnchor();
        public CursorAnchor CursorAnchor
        {
            get { return _cursorAnchor; }
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

        private bool _isDraggingMouse = false;
        public bool IsDraggingMouse
        {
            get { return _isDraggingMouse; }
        }

        private List<EditAction> _genericEditActions = new List<EditAction>()
        {
            // Modifier, Key, Action
            new EditAction(VirtualKey.None, VirtualKey.Up, "move_up"),
            new EditAction(VirtualKey.None, VirtualKey.Down, "move_down"),
            new EditAction(VirtualKey.None, VirtualKey.Tab, "insert_tab"),
            new EditAction(VirtualKey.None, VirtualKey.Enter, "insert_newline"),
            new EditAction(VirtualKey.None, VirtualKey.Back, "delete_backward"),
            new EditAction(VirtualKey.Control, VirtualKey.Back, "delete_word_backward"),
            new EditAction(VirtualKey.Control, VirtualKey.Delete, "delete_word_backward"),
            new EditAction(VirtualKey.Control, VirtualKey.Z, "undo"),
            new EditAction(VirtualKey.Control, VirtualKey.Y, "redo"),
            new EditAction(VirtualKey.Control, VirtualKey.A, "select_all"),
        };

        public TextViewModel(CanvasControl rootCanvas, CanvasControl gutterCanvas)
        {
            _rootCanvas = rootCanvas;
            _rootCanvas.Draw += _rootCanvas_Draw;

            _gutterCanvas = gutterCanvas;
            _gutterCanvas.Draw += _gutterCanvas_Draw;

            _textFormat = new CanvasTextFormat();
            _textFormat.FontFamily = "Consolas";
            _textFormat.FontSize = 12;
            _textFormat.WordWrapping = CanvasWordWrapping.NoWrap; //Xi handles this for us

            _xiService = new XIService();
            _xiService.UpdateObservable.Subscribe(update => UpdateTextView(update.Update));
            _xiService.ScrollToObservable.Subscribe(async scrollTo => await ScrollToLine(scrollTo));
            _xiService.StyleObservable.Subscribe(update =>
            {
                Debug.WriteLine(update.ID);
            });

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
            await _xiService.Save();
        }

        public void PointerPressed(Point position, int clickCount = 1)
        {
            var lineAndIndex = GetLineAndCursorIndexFromPos(position);
            var lineIndex = lineAndIndex.Item1;
            var charIndex = lineAndIndex.Item2;

            _xiService.Click(lineIndex, charIndex, 0, clickCount);

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

            if (CursorAnchor.CharacterIndex != charIndex ||
                CursorAnchor.LineIndex != lineIndex)
            {
                _xiService.Drag(lineIndex, charIndex);
            }
        }

        public void PointerReleased(Point position)
        {
            var lineAndIndex = GetLineAndCursorIndexFromPos(position);
            var lineIndex = lineAndIndex.Item1;
            var charIndex = lineAndIndex.Item2;

            if (CursorAnchor.CharacterIndex != charIndex ||
                CursorAnchor.LineIndex != lineIndex)
            {
                _xiService.Drag(lineIndex, charIndex);
            }

            _isDraggingMouse = false;
        }

        /// <summary>
        /// Returns the line and character index from a screen position
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        private Tuple<int, int> GetLineAndCursorIndexFromPos(Point position)
        {
            CanvasTextLayoutRegion hitRegion;
            int cursorIdx = 0;
            int cursorLine = _firstVisibleLine;

            var yOffset = 0;

            // Only check visible lines
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
            var window = Window.Current.CoreWindow;
            var hasCtrlMod = window
                .GetKeyState(VirtualKey.Control)
                .HasFlag(CoreVirtualKeyStates.Down);

            var hasShiftMod = window
                .GetKeyState(VirtualKey.Shift)
                .HasFlag(CoreVirtualKeyStates.Down);

            var desiredGenericAction = _genericEditActions
                .FirstOrDefault(action =>
                {
                    if (action.Key != key)
                        return false;

                    if (hasCtrlMod || hasShiftMod)
                    {
                        return action.Modifier == VirtualKey.None ?
                            false : window.GetKeyState(action.Modifier).HasFlag(CoreVirtualKeyStates.Down);
                    }
                    else
                    {
                        return action.Modifier == VirtualKey.None ?
                            true : window.GetKeyState(action.Modifier).HasFlag(CoreVirtualKeyStates.Down);
                    }
                });

            if (desiredGenericAction != null)
            {
                await _xiService.GenericEdit(desiredGenericAction.Command);
            }
            else
            {
                switch (key)
                {
                    case VirtualKey.Left:
                        if (CursorAnchor.IsAtStartOfLine)
                        {
                            await _xiService.GenericEdit("move_up");
                            await _xiService.GenericEdit("move_to_right_end_of_line");
                        }
                        else
                        {
                            if (hasShiftMod)
                                await _xiService.GenericEdit("move_left_and_modify_selection");
                            else
                                await _xiService.GenericEdit("move_left");
                        }
                        break;
                    case VirtualKey.Right:
                        if (LineUtils.IsNextToPlainLineBreak(_currentLine, CursorAnchor.CharacterIndex, LogicalDirection.Forward))
                        {
                            await _xiService.GenericEdit("move_to_left_end_of_line");
                            await _xiService.GenericEdit("move_down");
                        }
                        else
                        {
                            if (hasShiftMod)
                                await _xiService.GenericEdit("move_right_and_modify_selection");
                            else
                                await _xiService.GenericEdit("move_right");
                        }
                        break;
                    case VirtualKey.S:
                        if (hasCtrlMod)
                        {
                            await Save();
                        }
                        break;
                    default:
                        break;
                }
            }

            _rootCanvas.Invalidate();
        }

        public void UpdateVisibleLineCount()
        {
            if (!_lines.Any())
                return;
            
            var lineCount = (int)(_rootCanvas.ActualHeight / LINE_HEIGHT);
            _visibleLineCount = lineCount + 5;

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
                var lineHeight = LINE_HEIGHT;
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

            var lineHeight = LINE_HEIGHT;// Math.Max(1, _lines[0].Bounds.Height);
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
                                        _currentLine = line.Text;
                                        CursorAnchor.CharacterIndex = line.Cursor[0];
                                        CursorAnchor.LineIndex = newLines.Count - 1;

                                        if (LineUtils.IsNextToPlainLineBreak(_currentLine, CursorAnchor.CharacterIndex, LogicalDirection.Backward))
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
                    MaxScroll = Math.Max(1, ((_lines.Count * LINE_HEIGHT) - ScrollViewportSize));
                    UpdateVisibleLineCount();
                }
            }).ConfigureAwait(false);
        }

        private void _rootCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
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
                if (i == CursorAnchor.LineIndex)
                {
                    CursorAnchor.SetPosition(_lines[i], yOffset);
                }
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

                lineBuilder.AppendLine((i + 1).ToString());
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