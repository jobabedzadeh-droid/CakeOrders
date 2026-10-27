namespace CakeOrders.Domain.Entities;

/// <summary>
/// A person who can be selected as a referrer when an order is created.
/// </summary>
public sealed class Referrer
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}