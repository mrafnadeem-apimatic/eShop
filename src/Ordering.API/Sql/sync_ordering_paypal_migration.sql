DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'ordering'
          AND table_name = 'orders'
    )
    AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'ordering'
          AND table_name = 'orders'
          AND column_name = 'PaypalOrderId'
    ) THEN
        ALTER TABLE ordering.orders
            ADD COLUMN "PaypalOrderId" text;
    END IF;
END
$$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260121120000_AddPaypalOrderIdToOrders', '10.0.1'
WHERE NOT EXISTS (
    SELECT 1
    FROM "__EFMigrationsHistory"
    WHERE "MigrationId" = '20260121120000_AddPaypalOrderIdToOrders'
);

SELECT column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_schema = 'ordering'
  AND table_name = 'orders'
  AND column_name = 'PaypalOrderId';

SELECT "MigrationId", "ProductVersion"
FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20260121120000_AddPaypalOrderIdToOrders';