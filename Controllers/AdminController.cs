using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerApi.Data;
using ServerApi.Models;

namespace ServerApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    public AdminController(AppDbContext db) { _db = db; }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _db.Users
            .Select(u => new { u.Id, u.Username, u.Role, u.Balance, u.CreatedAt })
            .ToListAsync();
        return Ok(users);
    }

    [HttpPut("users/{id}/role")]
    public async Task<IActionResult> SetRole(int id, [FromBody] SetRoleRequest request)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        user.Role = request.Role;
        await _db.SaveChangesAsync();
        return Ok(new { user.Id, user.Username, user.Role });
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        // Xóa tất cả URL của user
        var urls = _db.ShortUrls.Where(u => u.UserId == id);
        _db.ShortUrls.RemoveRange(urls);
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("urls")]
    public async Task<IActionResult> GetAllUrls()
    {
        var urls = await _db.ShortUrls
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new {
                u.Id,
                u.Code,
                u.OriginalUrl,
                u.CreatedAt,
                u.ExpiresAt,
                u.UserId,
                isExpired = DateTime.UtcNow > u.ExpiresAt,
                shortUrl = $"{Request.Scheme}://{Request.Host}/r/{u.Code}"
            })
            .ToListAsync();
        return Ok(urls);
    }

    [HttpDelete("urls/{id}")]
    public async Task<IActionResult> DeleteUrl(int id)
    {
        var url = await _db.ShortUrls.FindAsync(id);
        if (url == null) return NotFound();
        _db.ShortUrls.Remove(url);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var totalUsers = await _db.Users.CountAsync();
        var totalUrls = await _db.ShortUrls.CountAsync();
        var activeUrls = await _db.ShortUrls.CountAsync(u => u.ExpiresAt > DateTime.UtcNow);
        var expiredUrls = totalUrls - activeUrls;
        return Ok(new { totalUsers, totalUrls, activeUrls, expiredUrls });
    }
}