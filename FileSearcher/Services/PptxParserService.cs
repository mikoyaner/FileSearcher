using System.Text;
using DocumentFormat.OpenXml;
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
        /// 解析 .pptx 文件，提取全部文本（形状 + 表格 + 组合形状 + 备注 + SmartArt）
        /// </summary>
        public string Parse(string filePath)
        {
            var sb = new StringBuilder();

            using (var presentation = PresentationDocument.Open(filePath, false))
            {
                var presentationPart = presentation.PresentationPart;
                if (presentationPart == null) return string.Empty;

                // F2 修复：改按 SlideIdList 顺序遍历 —— 此前按 SlideParts（关系表顺序）枚举，
                // [幻灯片 N] 编号可能与实际放映顺序不一致
                var slideIds = presentationPart.Presentation.SlideIdList?.ChildElements.OfType<SlideId>();
                if (slideIds == null) return sb.ToString();

                int slideNumber = 1;
                foreach (var slideId in slideIds)
                {
                    sb.AppendLine($"[幻灯片 {slideNumber}]");
                    slideNumber++;

                    var slidePart = (SlidePart)presentationPart.GetPartById(slideId.RelationshipId!);

                    // F2 修复：Descendants&lt;A.Text&gt; 提取 DrawingML 全部文本节点（a:t）——
                    // 此前只读 Shape.TextBody，表格（GraphicFrame/a:tbl）与组合形状中的文本漏检
                    AppendAllDrawingText(sb, slidePart.Slide);

                    // F2 修复：备注页文本 —— 此前完全不读取（备注是独立 Part）
                    var notesSlide = slidePart.NotesSlidePart?.NotesSlide;
                    if (notesSlide != null)
                    {
                        AppendAllDrawingText(sb, notesSlide);
                    }

                    // F2 修复：SmartArt 图形文本存储在独立的 DiagramDataPart 中，页面 XML 不包含它
                    foreach (var diagramDataPart in slidePart.DiagramDataParts)
                    {
                        AppendAllDrawingText(sb, diagramDataPart.DataModelRoot);
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 提取指定根节点下所有 DrawingML 文本节点（a:t）
        /// </summary>
        private static void AppendAllDrawingText(StringBuilder sb, OpenXmlElement? root)
        {
            if (root == null) return;

            foreach (var text in root.Descendants<A.Text>())
            {
                if (!string.IsNullOrWhiteSpace(text.Text))
                {
                    sb.AppendLine(text.Text);
                }
            }
        }
    }
}
