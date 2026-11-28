#!/bin/bash
# W2 — tìm compose dir của gateway (labels)
echo "== compose labels =="
docker inspect vanan-gateway-1 --format '{{index .Config.Labels "com.docker.compose.project"}} | {{index .Config.Labels "com.docker.compose.project.working_dir"}} | {{index .Config.Labels "com.docker.compose.project.config_files"}}' 2>/dev/null
echo "== find compose files =="
find / -maxdepth 4 -name 'docker-compose*.yml' -not -path '*/node_modules/*' 2>/dev/null | head -8
echo "== done =="