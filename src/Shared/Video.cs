using Pgvector;
using System.ComponentModel.DataAnnotations.Schema;

namespace BidEngine.Shared;

public class Video
{
    public Guid Id { get; set; }
    public string Title { get; set; } = String.Empty;
    public string? Description { get; set; }
    // previously I called this c# field as 'VectorizedDescription' so I needed -> [Column("embedding")] , but now I am Changing the name of my C# class to match the database's name. 
    public Vector? Embedding { get; set; }
    public DateTime CreatedAt { get; set; }
}
