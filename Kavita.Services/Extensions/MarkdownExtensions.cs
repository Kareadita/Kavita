using Markdig;

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

    public static MarkdownPipelineBuilder UseKavitaPlus(this MarkdownPipelineBuilder pipeline)
    {
        return pipeline.UsePipeTables()
            .UseFootnotes()
            .UseMathematics()
            .UseGenericAttributes(); // Always last!
    }
}
