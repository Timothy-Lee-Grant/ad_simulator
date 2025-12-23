using System.Threading.Tasks;
using BidEngine.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector;

namespace BidEngine.Controllers
{
    [ApiController]
    [Route("api/ads")]
    public class AdsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public AdsController(AppDbContext db)
        {
            _db = db;
        }

        public record SemanticSearchRequest(float[] Embedding, int K = 5);

        [HttpPost("semantic-search")]
        public async Task<IActionResult> SemanticSearch([FromBody] SemanticSearchRequest req)
        {
            if (req?.Embedding == null || req.Embedding.Length == 0) return BadRequest("missing embedding");

            // Limit k to a reasonable range
            var k = System.Math.Clamp(req.K, 1, 50);

            // Use a raw SQL query to leverage the pgvector operator (<=>) for distance
            // Select full rows so EF can materialize Ad entities
            var sql = $"SELECT * FROM ads ORDER BY embedding <=> @q LIMIT {k}";

            var param = new NpgsqlParameter("q", new Vector(req.Embedding));

            var results = await _db.Ads
                .FromSqlRaw(sql, param)
                .AsNoTracking()
                .ToListAsync();

            var response = results.Select(a => new {
                a.Id,
                a.Title,
                a.Description,
                a.ImageUrl,
                a.RedirectUrl
            });

            return Ok(response);
        }

        [HttpGet("similar-to-video")]
        public async Task<IActionResult> SimilarToVideo([FromQuery] string title, [FromQuery] int k = 3)
        {
            if (string.IsNullOrWhiteSpace(title)) return BadRequest("missing title");

            // Use a simple ILIKE match against the video title to find a seed embedding
            var slug = title.Replace('-', ' ');
            var video = await _db.Videos.FirstOrDefaultAsync(v => EF.Functions.ILike(v.Title, $"%{slug}%"));
            if (video == null || video.Embedding == null) return NotFound();

            var param = new NpgsqlParameter("q", video.Embedding);
            var sql = $"SELECT * FROM ads ORDER BY embedding <=> @q LIMIT {System.Math.Clamp(k,1,50)}";

            var results = await _db.Ads.FromSqlRaw(sql, param).AsNoTracking().ToListAsync();

            var response = results.Select(a => new {
                a.Id,
                a.Title,
                a.Description,
                a.ImageUrl,
                a.RedirectUrl
            });

            return Ok(response);
        }
    }
}
