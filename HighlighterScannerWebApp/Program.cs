using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.HttpOverrides;
using HighlighterScannerWebApp;

// alright let's get this thing started
var builder = WebApplication.CreateBuilder(args);
// removed openapi cause we're downgrading to 8.0 lol

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts => // shortened to opts cause why not
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8
                .GetBytes(builder.Configuration.GetSection("AppSettings:Token").Value!)),
            ValidateIssuer = false, // too lazy to check issuer
            ValidateAudience = false
        };
    });

builder.Services.AddDbContext<ScannerDBContext>(options =>
{
    // if the connection string is missing just use the local db
    var conn = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=scanner.db";
    options.UseSqlite(conn);
});
builder.Services.AddScoped<ScannerService>();
builder.Services.AddControllers();

builder.Services.Configure<ForwardedHeadersOptions>(o => {
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // clear these so nginx actually works on a vps...
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

// make sure the database actually exists... 
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ScannerDBContext>();
    db.Database.EnsureCreated();
}

app.UseForwardedHeaders();

// no more mapopenapi here... bye bye


app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run(); // and we're live!!

namespace HighlighterScannerWebApp
{
    // some data classes... probably should be in their own files but oh well
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
        public int hue { get; set; }
        public int saturation { get; set; }
        public int value { get; set; }
        public string addedBy { get; set; } = string.Empty;
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








