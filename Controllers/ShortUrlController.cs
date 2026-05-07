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
            !Uri.TryCreate(request.OriginalUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return BadRequest(new { error = "Invalid URL. Must start with http:// or https://" });

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int? userId = userIdStr != null ? int.Parse(userIdStr) : null;

        var existing = await _db.ShortUrls
            .FirstOrDefaultAsync(u => u.OriginalUrl == request.OriginalUrl
                && u.UserId == userId
                && u.ExpiresAt > DateTime.UtcNow);

        if (existing != null)
            return Ok(new
            {
                code = existing.Code,
                shortUrl = $"{Request.Scheme}://{Request.Host}/r/{existing.Code}",
                originalUrl = existing.OriginalUrl,
                createdAt = existing.CreatedAt,
                expiresAt = existing.ExpiresAt,
                isNew = false
            });

        var code = Guid.NewGuid().ToString("N")[..7];
        var shortUrl = new ShortUrl
        {
            Code = code,
            OriginalUrl = request.OriginalUrl,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            UserId = userId
        };

        _db.ShortUrls.Add(shortUrl);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            code = shortUrl.Code,
            shortUrl = $"{Request.Scheme}://{Request.Host}/r/{shortUrl.Code}",
            originalUrl = shortUrl.OriginalUrl,
            createdAt = shortUrl.CreatedAt,
            expiresAt = shortUrl.ExpiresAt,
            isNew = true
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
                u.ExpiresAt,
                isExpired = DateTime.UtcNow > u.ExpiresAt,
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


    [HttpPost("extend/{id}")]
    [Authorize]
    public async Task<IActionResult> Extend(int id, [FromBody] ExtendRequest request)
    {
        if (request.Days <= 0 || request.Days % 7 != 0)
            return BadRequest(new { error = "Days must be a multiple of 7." });

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userId = int.Parse(userIdStr!);

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        decimal cost = (request.Days / 7) * 10000m;

        if (user.Balance < cost)
            return BadRequest(new { error = $"Insufficient balance. Need {cost:N0} VND, have {user.Balance:N0} VND." });

        var shortUrl = await _db.ShortUrls
            .FirstOrDefaultAsync(u => u.Id == id && u.UserId == userId);
        if (shortUrl == null) return NotFound();

        var baseDate = shortUrl.ExpiresAt > DateTime.UtcNow ? shortUrl.ExpiresAt : DateTime.UtcNow;
        shortUrl.ExpiresAt = baseDate.AddDays(request.Days);

        user.Balance -= cost;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            expiresAt = shortUrl.ExpiresAt,
            balance = user.Balance
        });
    }
}