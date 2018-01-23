using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XiUWP.Model;

namespace XiUWP.Util
{
    public class LineUtils
    {
        public static bool IsNextToPlainLineBreak(string line, int cursorIdx, LogicalDirection direction)
        {
            if (cursorIdx >= line.Length || line.Length == 0)
                return false;

            if (direction == LogicalDirection.Backward)
            {
                if (cursorIdx > 0)
                    return IsCharUnicodeNewLine(line[cursorIdx - 1]);
                else if (cursorIdx == 0)
                    return false;
            }
            else if (direction == LogicalDirection.Forward && cursorIdx < line.Length - 1)
            {
                return IsCharUnicodeNewLine(line[cursorIdx]);
            }

            return false;
        }

        internal static Char[] NextLineCharacters = new char[] { '\n', '\r', '\v', '\f', '\u0085' /*NEL*/, '\u2028' /*LS*/, '\u2029' /*PS*/ };

        // Returns true if a specified char matches the Unicode definition of "newline".
        internal static bool IsCharUnicodeNewLine(char ch)
        {
            return Array.IndexOf(NextLineCharacters, ch) > -1;
        }
    }
}
