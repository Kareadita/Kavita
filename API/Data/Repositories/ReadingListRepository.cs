using API.Interfaces;
using API.Interfaces.Repositories;
using AutoMapper;

namespace API.Data.Repositories
{
    public class ReadingListRepository : IReadingListRepository
    {
        private readonly DataContext _context;
        private readonly IMapper _mapper;

        public ReadingListRepository(DataContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }




    }
}
