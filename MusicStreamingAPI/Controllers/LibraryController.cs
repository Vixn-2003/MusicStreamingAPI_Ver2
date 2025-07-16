using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicStreamingAPI.Models;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MusicStreamingAPI.DTOs;

namespace MusicStreamingAPI.Controllers
{
    [ApiController]
    [Route("api/library")]
    [Authorize]
    public class LibraryController : ControllerBase
    {
        private readonly MusicStreamingDbContext _context;
        private readonly IWebHostEnvironment _env;

        public LibraryController(MusicStreamingDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        /// <summary>
        /// Get all tracks uploaded by the authenticated user
        /// </summary>
        [HttpGet("my-tracks")]
        public async Task<IActionResult> GetMyTracks()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var tracks = await _context.Sounds.Where(s => s.UploadedBy == userId).ToListAsync();
            return Ok(tracks.Select(s => new SoundDto(s)));
        }
    }

    [ApiController]
    [Route("api/sounds")]
    [Authorize]
    public class SoundsController : ControllerBase
    {
        private readonly MusicStreamingDbContext _context;
        private readonly IWebHostEnvironment _env;

        public SoundsController(MusicStreamingDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        /// <summary>
        /// Add a new track (with file upload)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AddTrack([FromForm] AddTrackRequest request)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            if (request.File == null || request.File.Length == 0) return BadRequest("No file uploaded");
            var ext = Path.GetExtension(request.File.FileName);
            var fileName = $"track_{userId}_{Guid.NewGuid()}{ext}";
            var soundDir = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "sounds");
            if (!Directory.Exists(soundDir)) Directory.CreateDirectory(soundDir);
            var filePath = Path.Combine(soundDir, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await request.File.CopyToAsync(stream);
            }
            var sound = new Sound
            {
                Title = request.Title,
                ArtistName = request.ArtistName,
                AlbumId = request.AlbumId,
                CategoryId = request.CategoryId,
                Duration = request.Duration,
                FileUrl = $"/sounds/{fileName}",
                CoverImageUrl = request.CoverImageUrl,
                Lyrics = request.Lyrics,
                IsPublic = request.IsPublic,
                IsActive = true,
                UploadedBy = userId,
                CreatedAt = DateTime.UtcNow
            };
            _context.Sounds.Add(sound);
            await _context.SaveChangesAsync();
            return Ok(new SoundDto(sound));
        }

        /// <summary>
        /// Update own track
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTrack(int id, [FromBody] UpdateTrackRequest request)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var sound = await _context.Sounds.FindAsync(id);
            if (sound == null) return NotFound();
            if (sound.UploadedBy != userId) return Forbid();
            sound.Title = request.Title ?? sound.Title;
            sound.ArtistName = request.ArtistName ?? sound.ArtistName;
            sound.AlbumId = request.AlbumId ?? sound.AlbumId;
            sound.CategoryId = request.CategoryId ?? sound.CategoryId;
            sound.Duration = request.Duration ?? sound.Duration;
            sound.CoverImageUrl = request.CoverImageUrl ?? sound.CoverImageUrl;
            sound.Lyrics = request.Lyrics ?? sound.Lyrics;
            sound.IsPublic = request.IsPublic ?? sound.IsPublic;
            await _context.SaveChangesAsync();
            return Ok(new SoundDto(sound));
        }

        /// <summary>
        /// Delete own track
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTrack(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var sound = await _context.Sounds.FindAsync(id);
            if (sound == null) return NotFound();
            if (sound.UploadedBy != userId) return Forbid();
            _context.Sounds.Remove(sound);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
   
} 