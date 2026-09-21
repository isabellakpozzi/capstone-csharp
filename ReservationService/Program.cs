using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using ReservationService.Data;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using ReservationService.Services;
using FluentValidation;
using ReservationService.Filters;
using ReservationService.Validators;
using ReservationService.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddValidatorsFromAssemblyContaining<CreateReservationRequestValidator>();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<ReservationServiceContext>(options =>
        options.UseInMemoryDatabase("ReservationServiceDb"));
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("ReservationDb");
    builder.Services.AddDbContext<ReservationServiceContext>(options =>
        options.UseNpgsql(connectionString));
}

builder.Services.AddScoped<IWaitlistBusinessService, WaitlistBusinessService>();
builder.Services.AddScoped<IReservationBusinessService, ReservationBusinessService>();
builder.Services.AddScoped<IWaitlistBusinessService, WaitlistBusinessService>();
builder.Services.AddHostedService<WaitlistExpiryBackgroundService>();
builder.Services.AddHttpClient<IUserServiceClient, UserServiceClient>(client =>
{
    var baseUrl = builder.Configuration["ServiceUrls:UserService"] ?? "http://localhost:5001";
    client.BaseAddress = new Uri(baseUrl);
});

builder.Services.AddHttpClient<ICatalogServiceClient, CatalogServiceClient>(client =>
{
    var baseUrl = builder.Configuration["ServiceUrls:CatalogService"] ?? "http://localhost:5002";
    client.BaseAddress = new Uri(baseUrl);
});

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter: Bearer {your JWT token}"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ReservationServiceContext>();
    context.Database.Migrate();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
