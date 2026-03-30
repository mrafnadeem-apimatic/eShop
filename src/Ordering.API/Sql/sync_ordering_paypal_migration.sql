CREATE TABLE IF NOT EXISTS public."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'ordering'
          AND table_name = 'orders'
    ) THEN
        IF EXISTS (
            SELECT 1
            FROM information_schema.tables
            WHERE table_schema = 'ordering'
              AND table_name = 'buyers'
        )
        AND EXISTS (
            SELECT 1
            FROM information_schema.tables
            WHERE table_schema = 'ordering'
              AND table_name = 'cardtypes'
        )
        AND EXISTS (
            SELECT 1
            FROM information_schema.tables
            WHERE table_schema = 'ordering'
              AND table_name = 'paymentmethods'
        )
        AND EXISTS (
            SELECT 1
            FROM information_schema.tables
            WHERE table_schema = 'ordering'
              AND table_name = 'orderItems'
        )
        AND EXISTS (
            SELECT 1
            FROM information_schema.tables
            WHERE table_schema = 'ordering'
              AND table_name = 'requests'
        ) THEN
            INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            SELECT '20230925222426_Initial', '8.0.0-rc.1.23419.6'
            WHERE NOT EXISTS (
                SELECT 1
                FROM public."__EFMigrationsHistory"
                WHERE "MigrationId" = '20230925222426_Initial'
            );
        END IF;

        IF EXISTS (
            SELECT 1
            FROM information_schema.sequences
            WHERE sequence_schema = 'ordering'
              AND sequence_name = 'orderitemseq'
        ) THEN
            INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            SELECT '20231021004633_FixOrderitemseqSchema', '8.0.0-rc.2.23480.1'
            WHERE NOT EXISTS (
                SELECT 1
                FROM public."__EFMigrationsHistory"
                WHERE "MigrationId" = '20231021004633_FixOrderitemseqSchema'
            );
        END IF;

        IF EXISTS (
            SELECT 1
            FROM information_schema.tables
            WHERE table_schema = 'ordering'
              AND table_name = 'IntegrationEventLog'
        ) THEN
            INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            SELECT '20231026091055_Outbox', '8.0.0-rtm.23512.13'
            WHERE NOT EXISTS (
                SELECT 1
                FROM public."__EFMigrationsHistory"
                WHERE "MigrationId" = '20231026091055_Outbox'
            );
        END IF;

        IF EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'ordering'
              AND table_name = 'orders'
              AND column_name = 'OrderStatus'
        )
        AND NOT EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'ordering'
              AND table_name = 'orders'
              AND column_name = 'OrderStatusId'
        )
        AND NOT EXISTS (
            SELECT 1
            FROM information_schema.tables
            WHERE table_schema = 'ordering'
              AND table_name = 'orderstatus'
        ) THEN
            INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            SELECT '20240106121712_UseEnumForOrderStatus', '8.0.0'
            WHERE NOT EXISTS (
                SELECT 1
                FROM public."__EFMigrationsHistory"
                WHERE "MigrationId" = '20240106121712_UseEnumForOrderStatus'
            );
        END IF;

        IF NOT EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'ordering'
              AND table_name = 'orders'
              AND column_name = 'PaypalOrderId'
        ) THEN
            ALTER TABLE ordering.orders
                ADD COLUMN "PaypalOrderId" text;
        END IF;

        IF EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'ordering'
              AND table_name = 'orders'
              AND column_name = 'PaypalOrderId'
        ) THEN
            INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            SELECT '20260121120000_AddPaypalOrderIdToOrders', '10.0.1'
            WHERE NOT EXISTS (
                SELECT 1
                FROM public."__EFMigrationsHistory"
                WHERE "MigrationId" = '20260121120000_AddPaypalOrderIdToOrders'
            );
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