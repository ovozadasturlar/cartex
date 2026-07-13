using Cartex.Domain.Common;
using Cartex.Domain.Enums;

namespace Cartex.Domain.Entities;

/// <summary>
/// Mahsulotning qadog'i: "Qop" = 50 kg, "Quti" = 12 dona. Hajm doim mahsulotning saqlash
/// birligida o'lchanadi (Product.Unit), shuning uchun qadoq mahsulotga biriktiriladi.
/// Kirim qadog'i ta'minotda ("10 qop"), sotuv qadog'i esa rastadagi paket sifatida ishlatiladi.
/// </summary>
public class ProductPack : SoftDeleteEntity
{
    public long ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string Name { get; set; } = null!;
    public decimal Size { get; set; }
    public PackKind Kind { get; set; } = PackKind.Purchase;
    public bool IsDefault { get; set; }
}
