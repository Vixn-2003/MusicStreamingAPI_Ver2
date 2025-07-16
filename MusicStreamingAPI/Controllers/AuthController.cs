using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MusicStreamingAPI.Models;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MusicStreamingAPI.DTOs;

namespace MusicStreamingAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly MusicStreamingDbContext _context;
        private readonly IConfiguration _config;

        public AuthController(MusicStreamingDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        /// <summary>
        /// Login and get JWT token
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username || u.Email == request.Username);
            if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
                return Unauthorized(new { message = "Invalid credentials" });
            if (user.IsActive == false)
                return Unauthorized(new { message = "User is banned or inactive" });
            var token = GenerateJwtToken(user);
            return Ok(new { token, userId = user.UserId, roles = user.IsAdmin == true ? new[] { "Admin" } : new[] { "User" } });
        }

        /// <summary>
        /// Register a new user
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
                return BadRequest(new { message = "Username already exists" });
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                return BadRequest(new { message = "Email already exists" });
            var user = new User
            {
                Username = request.Username,
                Email = request.Email,
                PasswordHash = HashPassword(request.Password),
                FirstName = request.FirstName,
                LastName = request.LastName,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                IsAdmin = false
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            var token = GenerateJwtToken(user);
            return Ok(new { token, userId = user.UserId, roles = new[] { "User" } });
        }

        // Helper: Hash password
        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        // Helper: Verify password
        private bool VerifyPassword(string password, string hash)
        {
            return HashPassword(password) == hash;
        }

        // Helper: Generate JWT token
        private string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
                new Claim(ClaimTypes.Role, user.IsAdmin == true ? "Admin" : "User")
            };
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"] ?? "supersecretkey"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"] ?? "MusicStreamingAPI",
                audience: _config["Jwt:Audience"] ?? "MusicStreamingAPI",
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds
            );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

   
} 