using Microsoft.EntityFrameworkCore;
using CatalogService.Models;

namespace CatalogService.Data;

public static class CatalogDataSeeder
{
    public static async Task SeedAsync(CatalogServiceContext context)
    {
        if (await context.Books.AnyAsync())
            return; 

        var books = new List<Book>
        {
            new Book
            {
                Isbn = "978-0-13-468599-1",
                Title = "Clean Code",
                Author = "Robert C. Martin",
                Genre = "Technology",
                PublicationYear = 2008,
                Description = "A handbook of agile software craftsmanship",
                Publisher = "Prentice Hall",
                PageCount = 464,
                Language = "English",
                TotalCopies = 5,
                AvailableCopies = 5
            },
            new Book
            {
                Isbn = "978-0-13-475759-9",
                Title = "Refactoring",
                Author = "Martin Fowler",
                Genre = "Technology",
                PublicationYear = 2018,
                Description = "Improving the design of existing code",
                Publisher = "Addison-Wesley",
                PageCount = 448,
                Language = "English",
                TotalCopies = 3,
                AvailableCopies = 0 // seeded as fully checked out to test unavailable flows
            },
            new Book
            {
                Isbn = "978-0-596-00712-6",
                Title = "Head First Design Patterns",
                Author = "Eric Freeman",
                Genre = "Technology",
                PublicationYear = 2004,
                Description = "A brain-friendly guide to design patterns",
                Publisher = "O'Reilly Media",
                PageCount = 694,
                Language = "English",
                TotalCopies = 4,
                AvailableCopies = 4
            },
            new Book
            {
                Isbn = "978-0-452-28423-4",
                Title = "1984",
                Author = "George Orwell",
                Genre = "Fiction",
                PublicationYear = 1949,
                Description = "A dystopian social science fiction novel",
                Publisher = "Secker & Warburg",
                PageCount = 328,
                Language = "English",
                TotalCopies = 6,
                AvailableCopies = 2
            },
            new Book
            {
                Isbn = "978-0-06-231609-7",
                Title = "Sapiens",
                Author = "Yuval Noah Harari",
                Genre = "Non-Fiction",
                PublicationYear = 2011,
                Description = "A brief history of humankind",
                Publisher = "Harper",
                PageCount = 443,
                Language = "English",
                TotalCopies = 3,
                AvailableCopies = 1
            }
        };

        await context.Books.AddRangeAsync(books);
        await context.SaveChangesAsync();
    }
}