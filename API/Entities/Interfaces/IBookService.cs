using API.Parser;

namespace API.Entities.Interfaces
{
    public interface IBookService
    {
        int GetNumberOfPages(string filePath);

        ParserInfo ParseInfo(string filePath);
    }
}