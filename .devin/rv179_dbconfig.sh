#!/bin/bash
echo "---ENV---"
docker exec vanan-shoperp-1 env | grep -iE 'SQLITE|CONNECTION|DB_' | sed 's/PASSWORD=[^ ]*/PASSWORD=***/'
echo "---CONFIG---"
docker exec vanan-shoperp-1 sh -c 'grep -riE "sqlite|DefaultConnection|Data Source" /app/appsettings*.json 2>/dev/null | head -10'
echo "---ALL DB FILES---"
docker exec vanan-shoperp-1 sh -c 'find / -name "*.db" -type f 2>/dev/null | head -20'
echo "---DOCKER MOUNTS---"
docker inspect vanan-shoperp-1 --format '{{range .Mounts}}{{.Source}} -> {{.Destination}}{{"\n"}}{{end}}'
