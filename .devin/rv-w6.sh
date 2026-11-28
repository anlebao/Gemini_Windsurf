docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -tAc "SELECT count(*) AS mig FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" LIKE '%W6%';"
docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -tAc "SELECT table_name FROM information_schema.tables WHERE table_name LIKE 'Purchase%';"
