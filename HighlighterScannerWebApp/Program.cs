using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Tesseract;
using System.Drawing;
using Microsoft.AspNetCore.HttpOverrides;
using HighlighterScannerWebApp;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8
                .GetBytes(builder.Configuration.GetSection("AppSettings:Token").Value!)),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

builder.Services.AddDbContext<ScannerDBContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=scanner.db";
    // Ensure SQLite works on Linux by handling path correctly if needed
    options.UseSqlite(connectionString);
});
builder.Services.AddScoped<ScannerService>();
builder.Services.AddControllers();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

var app = builder.Build();

app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
// Note: HttpsRedirection is often handled by Nginx on Hostinger VPS.
// If you have SSL configured in Nginx, you can leave this out or enable it if Nginx doesn't redirect.
// app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

namespace HighlighterScannerWebApp
{
    public class User
    {
        public int id { get; set; }
        public string email { get; set; } = string.Empty;
        public string passwordHash { get; set; } = string.Empty;
    }

    public class HighlighterColour
    {
        public int id { get; set; }
        public string colourName { get; set; } = string.Empty;
        public string serialNumber { get; set; } = string.Empty;

        //HSV Colour
        public int hueMin { get; set; }
        public int saturationMin { get; set; }
        public int valueMin { get; set; }
        public int hueMax { get; set; }
        public int saturationMax { get; set; }
        public int valueMax { get; set; }
    }

    public class ScannedPageLog
    {
        public int id { get; set; }
        public string fileName { get; set; } = string.Empty;
        public int pageNumber { get; set; }
        public DateTime scannedTime { get; set; }
        public string scannedBy { get; set; } = string.Empty;

        public List<ExtractedLine> ExtractedLines { get; set; } = new();
    }

    public class ExtractedLine
    {
        public int id { get; set; }
        public string lineText { get; set; } = string.Empty;
        public int colourId { get; set; }
        public int pageNumber { get; set; }
    }

    public class ScannerDBContext : DbContext
    {
        public ScannerDBContext(DbContextOptions<ScannerDBContext> options) : base(options)
        {
        }

        public DbSet<HighlighterColour> HighlighterColours { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<ScannedPageLog> ScannedPageLogs { get; set; }
        public DbSet<ExtractedLine> ExtractedLines { get; set; }
    }
    
    
}





// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi


