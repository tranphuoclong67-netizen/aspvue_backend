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
			.Select(u => new { u.Id, u.Username, u.Role, u.CreatedAt })
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
				u.UserId,
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
}