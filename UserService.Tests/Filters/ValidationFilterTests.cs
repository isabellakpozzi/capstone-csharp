using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Moq;
using UserService.Filters;
using Xunit;

namespace UserService.Tests.Filters;

public class ValidationFilterTests
{
    public class SampleDto
    {
        public string Name { get; set; } = string.Empty;
    }

    private static ActionExecutingContext CreateActionContext(object argument)
    {
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(
            httpContext,
            new Microsoft.AspNetCore.Routing.RouteData(),
            new ActionDescriptor());

        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?> { ["dto"] = argument },
            controller: new object());
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenValidationFails_ShortCircuitsWith400()
    {
        var validator = new Mock<IValidator<SampleDto>>();
        validator
            .As<IValidator>()
            .Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[] { new ValidationFailure("Name", "Name is required") }));

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(s => s.GetService(typeof(IValidator<SampleDto>)))
            .Returns(validator.Object);

        var filter = new ValidationFilter(serviceProvider.Object);
        var context = CreateActionContext(new SampleDto());
        var nextCalled = false;

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.Should().BeFalse();
        context.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenValidationPasses_CallsNext()
    {
        var validator = new Mock<IValidator<SampleDto>>();
        validator
            .As<IValidator>()
            .Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult()); // no errors

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(s => s.GetService(typeof(IValidator<SampleDto>)))
            .Returns(validator.Object);

        var filter = new ValidationFilter(serviceProvider.Object);
        var context = CreateActionContext(new SampleDto());
        var nextCalled = false;

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.Should().BeTrue();
        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenNoValidatorRegisteredForType_CallsNextWithoutError()
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(s => s.GetService(It.IsAny<Type>()))
            .Returns((object?)null);

        var filter = new ValidationFilter(serviceProvider.Object);
        var context = CreateActionContext(new SampleDto());
        var nextCalled = false;

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.Should().BeTrue();
    }
}