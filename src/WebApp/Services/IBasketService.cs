namespace eShop.WebApp.Services;

public interface IBasketService
{
    Task<IReadOnlyCollection<BasketQuantity>> GetBasketAsync();
    Task DeleteBasketAsync();
    Task UpdateBasketAsync(IReadOnlyCollection<BasketQuantity> basket);
}
