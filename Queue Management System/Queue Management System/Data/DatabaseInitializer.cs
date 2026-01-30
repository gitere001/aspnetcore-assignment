using Queue_Management_System.Data.Repositories;

namespace Queue_Management_System.Data
{
    public class DatabaseInitializer
    {
        private readonly ServiceRepository _serviceRepo;
        private readonly ServicePointRepository _servicePointRepo;
        private readonly TicketRepository _ticketRepo;
        private readonly UserRepository _userRepo;

        public DatabaseInitializer(
            ServiceRepository serviceRepo,
            ServicePointRepository servicePointRepo,
            TicketRepository ticketRepo,
            UserRepository userRepo)
        {
            _serviceRepo = serviceRepo;
            _servicePointRepo = servicePointRepo;
            _ticketRepo = ticketRepo;
            _userRepo = userRepo;
        }

        public async Task InitializeAsync()
        {
            await _serviceRepo.CreateTable();
            await _servicePointRepo.CreateTable();
            await _ticketRepo.CreateTable();
            await _userRepo.CreateTable();
            Console.WriteLine("✅ Database tables created");
        }
    }
}