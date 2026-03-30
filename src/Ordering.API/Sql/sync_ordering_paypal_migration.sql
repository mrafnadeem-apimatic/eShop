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

      DO $$
      BEGIN
        IF EXISTS (
          SELECT 1
          FROM information_schema.tables
          WHERE table_schema = 'ordering'
            AND table_name = 'orders'
        )
        AND EXISTS (
          SELECT 1
          FROM information_schema.tables
          WHERE table_schema = 'public'
            AND table_name = '__EFMigrationsHistory'
        )
        AND NOT EXISTS (
          SELECT 1
          FROM "__EFMigrationsHistory"
          WHERE "MigrationId" = '20260121120000_AddPaypalOrderIdToOrders'
        ) THEN
          INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
          VALUES ('20260121120000_AddPaypalOrderIdToOrders', '10.0.1');
        END IF;
      END
      $$;

SELECT column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_schema = 'ordering'
  AND table_name = 'orders'
  AND column_name = 'PaypalOrderId';

SELECT EXISTS (
    SELECT 1
    FROM information_schema.tables
    WHERE table_schema = 'public'
      AND table_name = '__EFMigrationsHistory'
) AS ef_migration_history_exists;