namespace VanAn.KhachLink.Models;

public class ProductDto
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? Category { get; set; }
    public bool IsActive { get; set; }
    public decimal VatRate { get; set; }
    public string? ImageUrl { get; set; }
}
