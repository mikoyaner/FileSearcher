using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using A = DocumentFormat.OpenXml.Drawing;

namespace FileSearcher.Services
{
    /// <summary>
    /// .pptx 文件解析服务（使用 Open XML SDK）
    /// </summary>
    public class PptxParserService
    {
        /// <summary>
        /// 解析 .pptx 文件，提取所有幻灯片文本框内容
        /// </summary>
        public string Parse(string filePath)
        {
            var sb = new StringBuilder();

            using (var presentation = PresentationDocument.Open(filePath, false))
            {
                var presentationPart = presentation.PresentationPart;
                if (presentationPart == null) return string.Empty;

                var slideParts = presentationPart.SlideParts;
                int slideNumber = 1;

                foreach (var slidePart in slideParts)
                {
                    sb.AppendLine($"[幻灯片 {slideNumber}]");

                    var slide = slidePart.Slide;
                    if (slide != null)
                    {
                        // 提取所有文本框内容
                        foreach (var shape in slide.Descendants<Shape>())
                        {
                            var textBody = shape.TextBody;
                            if (textBody != null)
                            {
                                foreach (var paragraph in textBody.Elements<A.Paragraph>())
                                {
                                    var text = paragraph.InnerText;
                                    if (!string.IsNullOrWhiteSpace(text))
                                    {
                                        sb.AppendLine(text);
                                    }
                                }
                            }
                        }
                    }

                    slideNumber++;
                }
            }

            return sb.ToString();
        }
    }
}
