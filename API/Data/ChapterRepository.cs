using API.Entities;
using API.Interfaces.Repositories;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Data
{
    public class ChapterRepository : IChapterRepository
    {
        private readonly DataContext _context;
        private readonly IMapper _mapper;

        public ChapterRepository(DataContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public void Update(Chapter chapter)
        {
            _context.Entry(chapter).State = EntityState.Modified;
        }

        // TODO: Move over Chapter based queries here
    }
}
