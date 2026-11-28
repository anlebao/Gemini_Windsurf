SELECT column_name FROM information_schema.columns WHERE table_name = 'OutboxMessages' ORDER BY ordinal_position;
SELECT * FROM "OutboxMessages" WHERE "EventType" = 'OrderCreated' ORDER BY "CreatedAt" DESC LIMIT 3;
