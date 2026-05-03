using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerApi.Data;
using ServerApi.Models;
using System.Security.Claims;

namespace ServerApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShortUrlController : ControllerBase
{
    private readonly AppDbContext _db;

    public ShortUrlController(AppDbContext db)
    {
        _db = db;
    }

    // POST /api/shorturl
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateShortUrl([FromBody] CreateShortUrlRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OriginalUrl) ||
            !Uri.TryCreate(request.OriginalUrl, UriKind.Absolute, out _))
            return BadRequest(new { error = "Invalid URL." });

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int? userId = userIdStr != null ? int.Parse(userIdStr) : null;

        var code = Guid.NewGuid().ToString("N")[..7];
        var shortUrl = new ShortUrl
        {
            Code = code,
            OriginalUrl = request.OriginalUrl,
            CreatedAt = DateTime.UtcNow,
            UserId = userId
        };

        _db.ShortUrls.Add(shortUrl);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            code = shortUrl.Code,
            shortUrl = $"{Request.Scheme}://{Request.Host}/r/{shortUrl.Code}",
            originalUrl = shortUrl.OriginalUrl,
            createdAt = shortUrl.CreatedAt
        });
    }

    // GET /api/shorturl
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int? userId = userIdStr != null ? int.Parse(userIdStr) : null;

        var urls = await _db.ShortUrls
            .Where(u => u.UserId == userId)
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new {
                u.Id,
                u.Code,
                u.OriginalUrl,
                u.CreatedAt,
                shortUrl = $"{Request.Scheme}://{Request.Host}/r/{u.Code}"
            })
            .ToListAsync();

        return Ok(urls);
    }

    // DELETE /api/shorturl/{id}
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int? userId = userIdStr != null ? int.Parse(userIdStr) : null;

        var shortUrl = await _db.ShortUrls
            .FirstOrDefaultAsync(u => u.Id == id && u.UserId == userId);

        if (shortUrl == null) return NotFound();

        _db.ShortUrls.Remove(shortUrl);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}