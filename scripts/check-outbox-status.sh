#!/bin/bash
sudo docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c 'SELECT count(*) FILTER (WHERE "Status"=0) AS pending, count(*) FILTER (WHERE "Status"=2) AS processed, count(*) FILTER (WHERE "Status"=3) AS failed FROM "OutboxMessages";'
