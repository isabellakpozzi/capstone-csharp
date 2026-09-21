using CatalogService.Controllers;
using CatalogService.Data;
using CatalogService.Models;
using CatalogService.Models.Dtos;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CatalogService.Tests.Controllers;

public class BooksControllerTests
{
    private static CatalogServiceContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CatalogServiceContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CatalogServiceContext(options);
    }

    private static Book MakeBook(string title, string author, string genre, int? year, int total, int available, string isbn) => new()
    {
        Title = title,
        Author = author,
        Genre = genre,
        PublicationYear = year,
        TotalCopies = total,
        AvailableCopies = available,
        Isbn = isbn
    };

    private static async Task SeedBooksAsync(CatalogServiceContext context)
    {
        context.Books.AddRange(
            MakeBook("Clean Code", "Robert C. Martin", "Technology", 2008, 5, 2, "ISBN-1"),
            MakeBook("Refactoring", "Martin Fowler", "Technology", 2018, 3, 0, "ISBN-2"),
            MakeBook("1984", "George Orwell", "Fiction", 1949, 6, 6, "ISBN-3"),
            MakeBook("Animal Farm", "George Orwell", "Fiction", 1945, 4, 0, "ISBN-4"),
            MakeBook("Sapiens", "Yuval Noah Harari", "Non-Fiction", 2011, 3, 1, "ISBN-5")
        );
        await context.SaveChangesAsync();
    }

    // ---------- GetBooks: pagination & defaults ----------

    [Fact]
    public async Task GetBooks_WithNoParameters_UsesDefaultsAndReturnsAllBooksSortedByTitleAscending()
    {
        var context = CreateContext();
        await SeedBooksAsync(context);
        var sut = new BooksController(context);

        var result = await sut.GetBooks(page: 0, size: 20, sortBy: "title", sortOrder: "asc",
            query: null, genre: null, isbn: null, availableOnly: false);

        var response = ((OkObjectResult)result).Value as PaginatedBooksResponse;
        response!.Content.Should().HaveCount(5);
        response.TotalElements.Should().Be(5);
        response.Page.Should().Be(0);
        response.Size.Should().Be(20);
        response.Last.Should().BeTrue();

        response.Content.Select(b => b.Title).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetBooks_WithSmallPageSize_PaginatesCorrectlyAndSetsLastFlag()
    {
        var context = CreateContext();
        await SeedBooksAsync(context);
        var sut = new BooksController(context);

        var page0 = await GetOk(sut, size: 2, page: 0);
        page0.Content.Should().HaveCount(2);
        page0.TotalPages.Should().Be(3); // 5 books / 2 per page = 3 pages
        page0.Last.Should().BeFalse();

        var page2 = await GetOk(sut, size: 2, page: 2);
        page2.Content.Should().HaveCount(1); // last page has the remainder
        page2.Last.Should().BeTrue();
    }

    // ---------- Sorting ----------

    [Fact]
    public async Task GetBooks_SortByAuthorDescending_OrdersCorrectly()
    {
        var context = CreateContext();
        await SeedBooksAsync(context);
        var sut = new BooksController(context);

        var response = await GetOk(sut, sortBy: "author", sortOrder: "desc");

        response.Content.Select(b => b.Author).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task GetBooks_SortByPublicationYearAscending_OrdersCorrectly()
    {
        var context = CreateContext();
        await SeedBooksAsync(context);
        var sut = new BooksController(context);

        var response = await GetOk(sut, sortBy: "publicationYear", sortOrder: "asc");

        response.Content.Select(b => b.PublicationYear).Should().BeInAscendingOrder();
    }

    // ---------- Search ----------

    [Fact]
    public async Task GetBooks_QueryMatchesTitle_ReturnsMatchingBooksOnly()
    {
        var context = CreateContext();
        await SeedBooksAsync(context);
        var sut = new BooksController(context);

        var response = await GetOk(sut, query: "clean");

        response.Content.Should().ContainSingle(b => b.Title == "Clean Code");
    }

    [Fact]
    public async Task GetBooks_QueryMatchesAuthor_ReturnsMatchingBooksOnly()
    {
        var context = CreateContext();
        await SeedBooksAsync(context);
        var sut = new BooksController(context);

        var response = await GetOk(sut, query: "orwell");

        response.Content.Should().HaveCount(2); // 1984 and Animal Farm
        response.Content.Should().OnlyContain(b => b.Author == "George Orwell");
    }

    [Fact]
    public async Task GetBooks_QueryWithNoMatches_ReturnsEmptyContentNotError()
    {
        var context = CreateContext();
        await SeedBooksAsync(context);
        var sut = new BooksController(context);

        var response = await GetOk(sut, query: "nonexistentxyz");

        response.Content.Should().BeEmpty();
        response.TotalElements.Should().Be(0);
    }

    // ---------- Filters ----------

    [Fact]
    public async Task GetBooks_GenreFilter_ReturnsOnlyExactMatches()
    {
        var context = CreateContext();
        await SeedBooksAsync(context);
        var sut = new BooksController(context);

        var response = await GetOk(sut, genre: "Fiction");

        response.Content.Should().HaveCount(2);
        response.Content.Should().OnlyContain(b => b.Genre == "Fiction");
    }

    [Fact]
    public async Task GetBooks_IsbnFilter_ReturnsExactMatch()
    {
        var context = CreateContext();
        await SeedBooksAsync(context);
        var sut = new BooksController(context);

        var response = await GetOk(sut, isbn: "ISBN-3");

        response.Content.Should().ContainSingle(b => b.Isbn == "ISBN-3");
    }

    [Fact]
    public async Task GetBooks_AvailableOnlyFilter_ExcludesZeroAvailabilityBooks()
    {
        var context = CreateContext();
        await SeedBooksAsync(context);
        var sut = new BooksController(context);

        var response = await GetOk(sut, availableOnly: true);

        response.Content.Should().HaveCount(3); // excludes Refactoring and Animal Farm (0 copies)
        response.Content.Should().OnlyContain(b => b.AvailableCopies > 0);
    }

    [Fact]
    public async Task GetBooks_CombinedFilters_AllApplyTogether()
    {
        var context = CreateContext();
        await SeedBooksAsync(context);
        var sut = new BooksController(context);

        var response = await GetOk(sut, genre: "Fiction", availableOnly: true);

        // Of the 2 Fiction books, only "1984" has copies available
        response.Content.Should().ContainSingle(b => b.Title == "1984");
    }

    // ---------- Status calculation ----------

    [Fact]
    public async Task GetBooks_ComputesStatusFromAvailableCopiesNotStoredField()
    {
        var context = CreateContext();
        await SeedBooksAsync(context);
        var sut = new BooksController(context);

        var response = await GetOk(sut);

        response.Content.First(b => b.Title == "Refactoring").Status.Should().Be("CHECKED_OUT");
        response.Content.First(b => b.Title == "Clean Code").Status.Should().Be("AVAILABLE");
    }

    // ---------- GetBookById ----------

    [Fact]
    public async Task GetBookById_WithValidId_ReturnsCompleteDetails()
    {
        var context = CreateContext();
        var book = MakeBook("Clean Code", "Robert C. Martin", "Technology", 2008, 5, 2, "ISBN-1");
        book.Publisher = "Prentice Hall";
        book.PageCount = 464;
        context.Books.Add(book);
        await context.SaveChangesAsync();

        var sut = new BooksController(context);

        var result = await sut.GetBookById(book.BookId);

        var response = ((OkObjectResult)result).Value as BookDetailResponse;
        response!.Title.Should().Be("Clean Code");
        response.Publisher.Should().Be("Prentice Hall");
        response.PageCount.Should().Be(464);
        response.Status.Should().Be("AVAILABLE");
    }

    [Fact]
    public async Task GetBookById_WithNonexistentId_ReturnsNotFound()
    {
        var context = CreateContext();
        var sut = new BooksController(context);

        var result = await sut.GetBookById(Guid.NewGuid());

        result.Should().BeOfType<NotFoundObjectResult>();
        var error = ((NotFoundObjectResult)result).Value as ErrorResponse;
        error!.Error.Should().Be("NOT_FOUND");
    }

    // ---------- UpdateAvailability (internal endpoint) ----------

    [Fact]
    public async Task UpdateAvailability_DecrementWithinBounds_Succeeds()
    {
        var context = CreateContext();
        var book = MakeBook("Clean Code", "A", "Genre", 2020, 5, 3, "ISBN-X");
        context.Books.Add(book);
        await context.SaveChangesAsync();

        var sut = new BooksController(context);

        var result = await sut.UpdateAvailability(book.BookId, new UpdateAvailabilityRequest { Delta = -1 });

        result.Should().BeOfType<OkObjectResult>();
        var updated = await context.Books.FindAsync(book.BookId);
        updated!.AvailableCopies.Should().Be(2);
    }

    [Fact]
    public async Task UpdateAvailability_IncrementBeyondTotalCopies_ReturnsValidationError()
    {
        var context = CreateContext();
        var book = MakeBook("Clean Code", "A", "Genre", 2020, 5, 5, "ISBN-X"); // already at max
        context.Books.Add(book);
        await context.SaveChangesAsync();

        var sut = new BooksController(context);

        var result = await sut.UpdateAvailability(book.BookId, new UpdateAvailabilityRequest { Delta = 1 });

        result.Should().BeOfType<BadRequestObjectResult>();
        var updated = await context.Books.FindAsync(book.BookId);
        updated!.AvailableCopies.Should().Be(5); // unchanged
    }

    [Fact]
    public async Task UpdateAvailability_DecrementBelowZero_ReturnsValidationError()
    {
        var context = CreateContext();
        var book = MakeBook("Clean Code", "A", "Genre", 2020, 5, 0, "ISBN-X"); // already at zero
        context.Books.Add(book);
        await context.SaveChangesAsync();

        var sut = new BooksController(context);

        var result = await sut.UpdateAvailability(book.BookId, new UpdateAvailabilityRequest { Delta = -1 });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateAvailability_NonexistentBook_ReturnsNotFound()
    {
        var context = CreateContext();
        var sut = new BooksController(context);

        var result = await sut.UpdateAvailability(Guid.NewGuid(), new UpdateAvailabilityRequest { Delta = 1 });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ---------- Helper ----------

    private static async Task<PaginatedBooksResponse> GetOk(
        BooksController sut,
        int page = 0, int size = 20, string sortBy = "title", string sortOrder = "asc",
        string? query = null, string? genre = null, string? isbn = null, bool availableOnly = false)
    {
        var result = await sut.GetBooks(page, size, sortBy, sortOrder, query, genre, isbn, availableOnly);
        return (((OkObjectResult)result).Value as PaginatedBooksResponse)!;
    }
}