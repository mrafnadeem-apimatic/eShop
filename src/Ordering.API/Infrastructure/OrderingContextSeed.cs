namespace eShop.Ordering.API.Infrastructure;

using eShop.Ordering.Domain.AggregatesModel.BuyerAggregate;

public class OrderingContextSeed: IDbSeeder<OrderingContext>
{
    public async Task SeedAsync(OrderingContext context)
    {
        // Ensure schema is up to date for recently added properties (e.g. PaypalOrderId)
        // This is a safety net in case the database was created before the latest EF migrations.
        await context.Database.ExecuteSqlRawAsync("""
            ALTER TABLE ordering.orders
            ADD COLUMN IF NOT EXISTS "PaypalOrderId" text NULL;
            """);

        if (!context.CardTypes.Any())
        {
            context.CardTypes.AddRange(GetPredefinedCardTypes());

            await context.SaveChangesAsync();
        }

        await context.SaveChangesAsync();
    }

    private static IEnumerable<CardType> GetPredefinedCardTypes()
    {
        yield return new CardType { Id = 1, Name = "Amex" };
        yield return new CardType { Id = 2, Name = "Visa" };
        yield return new CardType { Id = 3, Name = "MasterCard" };
    }
}
