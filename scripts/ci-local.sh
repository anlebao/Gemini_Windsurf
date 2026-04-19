#!/bin/bash
echo "?? LOCAL CI START"

echo "1? BUILD"
dotnet build || exit 1

echo "2? TEST"
dotnet test || exit 1

echo "3? GUARD"
node windsurf-guard.js --ci || exit 1

echo "?? ALL PASSED"
