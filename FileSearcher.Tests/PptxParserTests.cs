using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using A = DocumentFormat.OpenXml.Drawing;
using FileSearcher.Services;
using Xunit;

namespace FileSearcher.Tests
{
    /// <summary>
    /// pptx 解析回归测试：表格文本、备注文本、放映顺序
    /// </summary>
    public class PptxParserTests : IDisposable
    {
        private readonly SearchService _service = new();
        private readonly string _dir = TestHelpers.CreateTempDirectory();

        [Fact]
        public void Pptx_TableText_Found()
        {
            // F2 回归：表格（GraphicFrame/a:tbl）不在 Shape.TextBody 路径上，
            // 修复前整张表格的文本漏检
            string path = Path.Combine(_dir, "table.pptx");
            CreatePptx(path);

            Assert.NotEmpty(_service.SearchInFile(path, "表格内关键词"));
        }

        [Fact]
        public void Pptx_NotesText_Found()
        {
            // F2 回归：备注页是独立 Part，修复前完全不读取
            string path = Path.Combine(_dir, "notes.pptx");
            CreatePptx(path);

            Assert.NotEmpty(_service.SearchInFile(path, "备注关键词"));
        }

        [Fact]
        public void Pptx_ShapeText_Found()
        {
            // 普通形状文本作为基线，确保重写后原有能力不回退
            string path = Path.Combine(_dir, "shape.pptx");
            CreatePptx(path);

            Assert.NotEmpty(_service.SearchInFile(path, "第一页普通文本"));
            Assert.NotEmpty(_service.SearchInFile(path, "第二页文本"));
        }

        [Fact]
        public void Pptx_SlideNumber_FollowsSlideIdListOrder()
        {
            // F2 回归：幻灯片按 SlideIdList（放映顺序）遍历 ——
            // 同一次搜索中，第 1 页文本的匹配序号必须先于第 2 页
            string path = Path.Combine(_dir, "order.pptx");
            CreatePptx(path);

            var results = _service.SearchInFile(path, "文本");

            Assert.Equal(2, results.Count);
            Assert.Contains("第一页", results[0].Context);
            Assert.Contains("第二页", results[1].Context);
        }

        /// <summary>
        /// 构造两页 pptx：第 1 页含普通形状 + 表格 + 备注，第 2 页含普通形状
        /// </summary>
        private static void CreatePptx(string path)
        {
            using var document = PresentationDocument.Create(path, PresentationDocumentType.Presentation);
            // PresentationDocument.Create 不会自动创建 PresentationPart，需显式添加
            var presentationPart = document.AddPresentationPart();

            // 按 SlideIdList 登记两页的顺序（rId1 → 第 1 页，rId2 → 第 2 页）
            presentationPart.Presentation = new Presentation(
                new SlideIdList(
                    new SlideId { Id = 256U, RelationshipId = "rId1" },
                    new SlideId { Id = 257U, RelationshipId = "rId2" }));

            // 第 1 页：普通形状 + 表格
            var slide1 = presentationPart.AddNewPart<SlidePart>("rId1");
            slide1.Slide = new Slide(
                new CommonSlideData(
                    new ShapeTree(
                        new Shape(
                            new TextBody(
                                new A.BodyProperties(),
                                new A.Paragraph(new A.Run(new A.Text("第一页普通文本"))))),
                        new GraphicFrame(
                            new A.Graphic(
                                new A.GraphicData(
                                    new A.Table(
                                        new A.TableProperties(),
                                        new A.TableGrid(),
                                        new A.TableRow(
                                            new A.TableCell(
                                                new A.TextBody(
                                                    new A.BodyProperties(),
                                                    new A.Paragraph(new A.Run(new A.Text("表格内关键词"))))))))
                                { Uri = "http://schemas.openxmlformats.org/drawingml/2006/table" })))));

            // 第 1 页备注
            var notes1 = slide1.AddNewPart<NotesSlidePart>();
            notes1.NotesSlide = new NotesSlide(
                new CommonSlideData(
                    new ShapeTree(
                        new Shape(
                            new TextBody(
                                new A.BodyProperties(),
                                new A.Paragraph(new A.Run(new A.Text("备注关键词，仅演讲者可见"))))))));

            // 第 2 页：普通形状
            var slide2 = presentationPart.AddNewPart<SlidePart>("rId2");
            slide2.Slide = new Slide(
                new CommonSlideData(
                    new ShapeTree(
                        new Shape(
                            new TextBody(
                                new A.BodyProperties(),
                                new A.Paragraph(new A.Run(new A.Text("第二页文本"))))))));
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { /* 清理失败不影响测试结果 */ }
        }
    }
}
