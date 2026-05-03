using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerApi.Data;

namespace ServerApi.Controllers;

[ApiController]
[Route("r")]
public class RedirectController : ControllerBase
{
    private readonly AppDbContext _db;

    public RedirectController(AppDbContext db)
    {
        _db = db;
    }

    // GET /r/{code}
    [HttpGet("{code}")]
    public async Task<IActionResult> RedirectToOriginal(string code)
    {
        var shortUrl = await _db.ShortUrls
            .FirstOrDefaultAsync(u => u.Code == code);

        if (shortUrl == null)
            return NotFound(new { error = "Short URL not found." });

        return Redirect(shortUrl.OriginalUrl);
    }
}