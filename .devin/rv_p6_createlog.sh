#!/bin/bash
docker logs vanan-gateway-1 --since 10m 2>&1 | grep -B3 -A 25 "Operation is not valid" | tail -40
echo "=== or InvalidOperation stack ==="
docker logs vanan-gateway-1 --since 10m 2>&1 | grep -B2 -A 20 "BookingService.CreateBookingAsync" | tail -30
echo DONE
