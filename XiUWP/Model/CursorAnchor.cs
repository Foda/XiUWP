using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XiUWP.Model
{
    public class CursorAnchor : ReactiveObject
    {
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

        private int _lineIndex = 0;
        public int LineIndex
        {
            get { return _lineIndex; }
            set { this.RaiseAndSetIfChanged(ref _lineIndex, value); }
        }

        private int _characterIndex = 0;
        public int CharacterIndex
        {
            get { return _characterIndex; }
            set { this.RaiseAndSetIfChanged(ref _characterIndex, value); }
        }

        public bool IsAtStartOfLine
        {
            get { return CharacterIndex == 0; }
        }

        public void SetPosition(float left, float top)
        {
            CursorLeft = left;
            CursorTop = top;
        }

        public void SetPosition(LineSpan line, float yOffset)
        {
            var pos = line.GetCaretPosition(this.CharacterIndex);
            SetPosition(pos.X, pos.Y + yOffset);
        }
    }
}
