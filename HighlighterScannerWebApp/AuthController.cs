using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using HighlighterScannerWebApp;

namespace HighlighterScannerWebApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ScannerDBContext _db;
        private readonly IConfiguration _config;

        public AuthController(ScannerDBContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UserDto req) // changed to req
        {
// check if user already there
            if (await _db.Users.AnyAsync(u => u.email == req.Email))
                return BadRequest("User already exists.");

            var u = new User {
                email = req.Email,
                passwordHash = BCrypt.Net.BCrypt.HashPassword(req.Password) // encrypt it!!
            };

            _db.Users.Add(u);
            await _db.SaveChangesAsync();

            return Ok("User registered successfully.");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserDto request)
        {
            // find the user...
            var user = await _db.Users.FirstOrDefaultAsync(u => u.email == request.Email);
            
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.passwordHash)) {
                return Unauthorized("Invalid email or password.");
            }

            var t = CreateToken(user);
            return Ok(new { token = t });
        }

        private string CreateToken(User u)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, u.id.ToString()),
                new Claim(ClaimTypes.Email, u.email)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                _config.GetSection("AppSettings:Token").Value!));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.Now.AddDays(1), // expires in a day
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class UserDto
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
    }
}
