using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace FileSearcher.Services
{
    /// <summary>
    /// .docx 文件解析服务（使用 Open XML SDK）
    /// </summary>
    public class DocxParserService
    {
        /// <summary>
        /// 解析 .docx 文件，提取全部文本（正文 + 表格 + 页眉 + 页脚）
        /// </summary>
        public string Parse(string filePath)
        {
            var sb = new StringBuilder();

            using (var document = WordprocessingDocument.Open(filePath, false))
            {
                var mainPart = document.MainDocumentPart;
                var body = mainPart?.Document?.Body;
                if (mainPart == null || body == null) return string.Empty;

                // F1 修复：Elements&lt;Paragraph&gt;() 只遍历 body 的"直接子节点"，
                // 表格（w:tbl）内的段落在 w:tbl/w:tr/w:tc/w:p 路径上，属于更深层级，会被整体漏检；
                // 文本框内嵌段落同理。改用 Descendants 遍历全部层级。
                AppendParagraphs(sb, body);

                // F1 修复：页眉/页脚是独立于 MainDocumentPart 的子部件（HeaderParts/FooterParts），
                // 正文 Body 中根本不包含其内容，必须单独遍历
                foreach (var headerPart in mainPart.HeaderParts)
                {
                    AppendParagraphs(sb, headerPart.Header);
                }

                foreach (var footerPart in mainPart.FooterParts)
                {
                    AppendParagraphs(sb, footerPart.Footer);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 提取指定根节点下所有层级的段落文本
        /// </summary>
        private static void AppendParagraphs(StringBuilder sb, OpenXmlElement root)
        {
            foreach (var paragraph in root.Descendants<Paragraph>())
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
