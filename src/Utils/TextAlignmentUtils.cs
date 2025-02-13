using System;
using System.Text;

namespace RustAnalyzer.Utils
{
    public static class TextAlignmentUtils
    {
        /// <summary>
        /// Computes the visual column of the start of a member name, accounting for tabs.
        /// </summary>
        /// <param name="line">The source line of code.</param>
        /// <param name="charColumn">The character position in the line (0-based).</param>
        /// <param name="tabSize">The size of a tab in spaces (default is 4).</param>
        /// <returns>The visual column in "spaces".</returns>
        public static int ComputeVisualColumn(string line, int charColumn, int tabSize = 4)
        {
            if (line == null)
                throw new ArgumentNullException(nameof(line));
            if (charColumn < 0 || charColumn > line.Length)
                throw new ArgumentOutOfRangeException(
                    nameof(charColumn),
                    "Character column is out of line bounds."
                );
            if (tabSize <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(tabSize),
                    "Tab size must be greater than zero."
                );

            int visualColumn = 0;

            for (int i = 0; i < charColumn; i++)
            {
                if (line[i] == '\t')
                {
                    // Calculate remaining spaces to the next tab stop
                    int spacesToAdd = tabSize - (visualColumn % tabSize);
                    visualColumn += spacesToAdd;
                }
                else
                {
                    visualColumn++;
                }
            }

            return visualColumn;
        }

        /// <summary>
        /// Creates a pointer line with '^' characters under the specified member name in the code line.
        /// </summary>
        /// <param name="line">The source line of code.</param>
        /// <param name="charColumn">The starting character position (0-based).</param>
        /// <param name="length">The length of the member name.</param>
        /// <param name="tabSize">The size of a tab in spaces (default is 4).</param>
        /// <returns>A string with spaces and '^' characters highlighting the member.</returns>
        public static string CreatePointerLine(
            string line,
            int charColumn,
            int length,
            int tabSize = 4
        )
        {
            if (line == null)
                throw new ArgumentNullException(nameof(line));
            if (charColumn < 0 || charColumn > line.Length)
                throw new ArgumentOutOfRangeException(
                    nameof(charColumn),
                    "Character column is out of line bounds."
                );
            if (length <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(length),
                    "Length must be greater than zero."
                );
            if (tabSize <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(tabSize),
                    "Tab size must be greater than zero."
                );

            // Compute the visual column where the pointer starts
            int visualColumn = ComputeVisualColumn(line, charColumn, tabSize);

            // Generate the pointer line with '^' characters
            return new string(' ', visualColumn) + new string('^', length);
        }

        /// <summary>
        /// Expands tabs in the line to spaces based on the specified tab size.
        /// </summary>
        /// <param name="line">The source line of code.</param>
        /// <param name="tabSize">The size of a tab in spaces (default is 4).</param>
        /// <returns>A string where all tabs are replaced with spaces.</returns>
        public static string ExpandTabs(string line, int tabSize = 4)
        {
            if (line == null)
                throw new ArgumentNullException(nameof(line));
            if (tabSize <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(tabSize),
                    "Tab size must be greater than zero."
                );

            var result = new StringBuilder();
            int column = 0;

            foreach (char c in line)
            {
                if (c == '\t')
                {
                    // Add spaces to align to the next tab stop
                    int spacesToAdd = tabSize - (column % tabSize);
                    result.Append(' ', spacesToAdd);
                    column += spacesToAdd;
                }
                else
                {
                    result.Append(c);
                    column++;
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Converts a visual column index back to the character index in the original line.
        /// </summary>
        /// <param name="line">The source line of code.</param>
        /// <param name="visualColumn">The target visual column.</param>
        /// <param name="tabSize">The size of a tab in spaces (default is 4).</param>
        /// <returns>The character index in the line (0-based).</returns>
        public static int ComputeCharColumn(string line, int visualColumn, int tabSize = 4)
        {
            if (line == null)
                throw new ArgumentNullException(nameof(line));
            if (visualColumn < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(visualColumn),
                    "Visual column must be non-negative."
                );
            if (tabSize <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(tabSize),
                    "Tab size must be greater than zero."
                );

            int currentVisualColumn = 0;

            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '\t')
                {
                    // Calculate the next tab stop
                    int spacesToAdd = tabSize - (currentVisualColumn % tabSize);
                    currentVisualColumn += spacesToAdd;
                }
                else
                {
                    currentVisualColumn++;
                }

                if (currentVisualColumn > visualColumn)
                    return i;
            }

            return line.Length; // Return the end of the line if visual column exceeds the line length
        }
    }
}
