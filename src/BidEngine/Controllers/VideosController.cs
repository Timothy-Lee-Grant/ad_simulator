using BidEngine.Data;
using BidEngine.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BidEngine.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VideosController : ControllerBase
{
    private readonly AppDbContext _db;

    public VideosController(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/videos?limit=3
    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetVideos([FromQuery] int limit = 3)
    {
        var videos = await _db.Videos
            .OrderByDescending(v => v.CreatedAt)
            .Take(limit)
            .Select(v => new { v.Id, v.Title, v.Description })
            .ToListAsync();

        return Ok(videos);
    }

    // GET /api/videos/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<object>> GetVideo(Guid id)
    {
        var video = await _db.Videos
            .Where(v => v.Id == id)
            .Select(v => new { v.Id, v.Title, v.Description })
            .SingleOrDefaultAsync();

        if (video == null) return NotFound();
        return Ok(video);
    }
}
