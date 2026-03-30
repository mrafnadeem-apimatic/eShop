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
      DECLARE
        migration_record_exists boolean;
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
        ) THEN
          EXECUTE '
            SELECT EXISTS (
              SELECT 1
              FROM public."__EFMigrationsHistory"
              WHERE "MigrationId" = ''20260121120000_AddPaypalOrderIdToOrders''
            )'
          INTO migration_record_exists;

          IF NOT migration_record_exists THEN
            EXECUTE '
              INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
              VALUES (''20260121120000_AddPaypalOrderIdToOrders'', ''10.0.1'')';
          END IF;
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