using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CatalogService.Data;
using CatalogService.Models;
using CatalogService.Models.Dtos;

namespace CatalogService.Controllers;

[ApiController]
[Route("api/catalog/books")]
public class BooksController : ControllerBase
{
    private readonly CatalogServiceContext _context;

    public BooksController(CatalogServiceContext context)
    {
        _context = context;
    }

    private static string ComputeStatus(Book book) =>
        book.AvailableCopies > 0 ? "AVAILABLE" : "CHECKED_OUT";

    [HttpGet]
    public async Task<IActionResult> GetBooks(
        [FromQuery] int page = 0,
        [FromQuery] int size = 20,
        [FromQuery] string sortBy = "title",
        [FromQuery] string sortOrder = "asc",
        [FromQuery] string? query = null,
        [FromQuery] string? genre = null,
        [FromQuery] string? isbn = null,
        [FromQuery] bool availableOnly = false)
    {
        var booksQuery = _context.Books.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.ToLower();
            booksQuery = booksQuery.Where(b =>
                b.Title.ToLower().Contains(q) || b.Author.ToLower().Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(genre))
            booksQuery = booksQuery.Where(b => b.Genre == genre);

        if (!string.IsNullOrWhiteSpace(isbn))
            booksQuery = booksQuery.Where(b => b.Isbn == isbn);

        if (availableOnly)
            booksQuery = booksQuery.Where(b => b.AvailableCopies > 0);

        var isDescending = sortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);

        booksQuery = sortBy.ToLower() switch
        {
            "author" => isDescending ? booksQuery.OrderByDescending(b => b.Author) : booksQuery.OrderBy(b => b.Author),
            "publicationyear" => isDescending ? booksQuery.OrderByDescending(b => b.PublicationYear) : booksQuery.OrderBy(b => b.PublicationYear),
            _ => isDescending ? booksQuery.OrderByDescending(b => b.Title) : booksQuery.OrderBy(b => b.Title)
        };

        var totalElements = await booksQuery.CountAsync();
        var totalPages = totalElements == 0 ? 0 : (int)Math.Ceiling(totalElements / (double)size);

        var pageItems = await booksQuery
            .Skip(page * size)
            .Take(size)
            .ToListAsync();

        var content = pageItems.Select(b => new BookListItem
        {
            BookId = b.BookId,
            Isbn = b.Isbn,
            Title = b.Title,
            Author = b.Author,
            Genre = b.Genre,
            PublicationYear = b.PublicationYear,
            Description = b.Description,
            TotalCopies = b.TotalCopies,
            AvailableCopies = b.AvailableCopies,
            Status = ComputeStatus(b)
        }).ToList();

        return Ok(new PaginatedBooksResponse
        {
            Content = content,
            Page = page,
            Size = size,
            TotalElements = totalElements,
            TotalPages = totalPages,
            Last = page >= totalPages - 1
        });
    }

    [HttpGet("{bookId}")]
    public async Task<IActionResult> GetBookById(Guid bookId)
    {
        var book = await _context.Books.FindAsync(bookId);

        if (book is null)
        {
            return NotFound(new ErrorResponse
            {
                Error = "NOT_FOUND",
                Message = $"Book not found with ID: {bookId}",
                Timestamp = DateTime.UtcNow
            });
        }

        return Ok(new BookDetailResponse
        {
            BookId = book.BookId,
            Isbn = book.Isbn,
            Title = book.Title,
            Author = book.Author,
            Genre = book.Genre,
            PublicationYear = book.PublicationYear,
            Description = book.Description,
            Publisher = book.Publisher,
            PageCount = book.PageCount,
            Language = book.Language,
            TotalCopies = book.TotalCopies,
            AvailableCopies = book.AvailableCopies,
            Status = ComputeStatus(book),
            CreatedAt = book.CreatedAt,
            UpdatedAt = book.UpdatedAt
        });
    }

    // internal endpoint called by res service
    [HttpPut("{bookId}/availability")]
    public async Task<IActionResult> UpdateAvailability(Guid bookId, [FromBody] UpdateAvailabilityRequest request)
    {
        var book = await _context.Books.FindAsync(bookId);

        if (book is null)
        {
            return NotFound(new ErrorResponse
            {
                Error = "NOT_FOUND",
                Message = $"Book not found with ID: {bookId}",
                Timestamp = DateTime.UtcNow
            });
        }

        var newAvailable = book.AvailableCopies + request.Delta;

        if (newAvailable < 0 || newAvailable > book.TotalCopies)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "VALIDATION_ERROR",
                Message = $"Resulting availableCopies ({newAvailable}) would be out of valid range (0-{book.TotalCopies})",
                Timestamp = DateTime.UtcNow
            });
        }

        book.AvailableCopies = newAvailable;
        await _context.SaveChangesAsync();

        return Ok(new { bookId = book.BookId, availableCopies = book.AvailableCopies });
    }
}