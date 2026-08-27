using Markdig;
using Markdig.Renderers;

namespace Kavita.Services.Extensions;


public static class MarkdownExtensions
{
    public static MarkdownPipelineBuilder UseGithub(this MarkdownPipelineBuilder pipeline)
    {
        return pipeline.UsePipeTables()
            .UseFootnotes()
            .UseMathematics()
            .UseGenericAttributes(); // Always last!
    }

    /**
     * Handles the Mangabaka summary markdown -> Html. Works differently than other html conversions
     * <example>This should write                     -> This should write</example>
     * <example>This is **bold**, *italic*, [link](x) -> This is <strong>bold</strong>, <em>italic</em>, <a href="x">link</a></example>
     * <example>Line one<br /> Line two with **bold** -> Line one<br /> Line two with <strong>bold</strong></example>
     * <example>R&D &lt; 5                               -> R&amp;D &lt; 5</example>
     */
    public static MarkdownPipelineBuilder UseKavitaPlus(this MarkdownPipelineBuilder pipeline)
    {
        pipeline.UsePipeTables().UseFootnotes().UseMathematics().UseGenericAttributes(); // Always last!
        pipeline.Extensions.Add(new NoParagraphExtension());
        return pipeline;
    }

    private sealed class NoParagraphExtension : IMarkdownExtension
    {
        public void Setup(MarkdownPipelineBuilder pipeline) { }
        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
            if (renderer is HtmlRenderer html) html.ImplicitParagraph = true;
        }
    }
}
