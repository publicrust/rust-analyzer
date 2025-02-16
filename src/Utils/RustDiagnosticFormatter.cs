using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace RustAnalyzer.Utils
{
    public class RustDiagnosticFormatter
    {
        public class DiagnosticFormatInfo
        {
            public string ErrorCode { get; set; }
            public string ErrorTitle { get; set; }
            public Location Location { get; set; }
            public SourceText SourceText { get; set; }
            public string[] MessageParameters { get; set; }
            public string Note { get; set; }
            public string Help { get; set; }
            public string Example { get; set; }
        }

        public static string FormatDiagnostic(DiagnosticFormatInfo info)
        {
            var lineSpan = info.Location.GetLineSpan();
            var startLinePosition = lineSpan.StartLinePosition;

            var lineText = info.SourceText.Lines[startLinePosition.Line].ToString();
            lineText = TextAlignmentUtils.ExpandTabs(lineText, 4);

            int charColumn = startLinePosition.Character;
            string pointerLine = TextAlignmentUtils.CreatePointerLine(
                lineText,
                charColumn,
                info.Location.SourceSpan.Length,
                4
            );

            var fileName = System.IO.Path.GetFileName(
                info.Location.SourceTree?.FilePath ?? string.Empty
            );

            var messageBuilder = new System.Text.StringBuilder();

            // Заголовок ошибки
            messageBuilder.AppendFormat("error[{0}]: {1}\n", info.ErrorCode, info.ErrorTitle);

            // Информация о расположении
            messageBuilder.AppendFormat(
                "  --> {0}:{1}:{2}\n",
                fileName,
                startLinePosition.Line + 1,
                charColumn + 1
            );

            // Показ кода с указателем на ошибку
            messageBuilder.AppendLine("   |");
            messageBuilder.AppendFormat("{0,4} | {1}\n", startLinePosition.Line + 1, lineText);
            messageBuilder.AppendFormat("   | {0}\n", pointerLine);
            messageBuilder.AppendLine("   |");

            // Дополнительная информация
            if (!string.IsNullOrEmpty(info.Note))
            {
                var formattedNote = FormatMessageWithParameters(info.Note, info.MessageParameters);
                messageBuilder.AppendFormat("   = note: {0}\n", formattedNote);
            }

            if (!string.IsNullOrEmpty(info.Help))
            {
                var formattedHelp = FormatMessageWithParameters(info.Help, info.MessageParameters);
                messageBuilder.AppendFormat("   = help: {0}\n", formattedHelp);
            }

            if (!string.IsNullOrEmpty(info.Example))
            {
                var formattedExample = FormatMessageWithParameters(
                    info.Example,
                    info.MessageParameters
                );
                messageBuilder.AppendLine("   = example:");
                messageBuilder.AppendFormat("           {0}", formattedExample);
            }

            return messageBuilder.ToString();
        }

        private static string FormatMessageWithParameters(string message, string[] parameters)
        {
            if (parameters == null || parameters.Length == 0)
                return message;

            try
            {
                return string.Format(message, parameters);
            }
            catch (System.FormatException)
            {
                return message;
            }
        }
    }
}
