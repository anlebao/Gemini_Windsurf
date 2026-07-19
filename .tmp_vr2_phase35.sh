#!/bin/bash
# Phase 3.5 VR2: Checkout + Payment webhook → MarkPaidAsync → NATS → PaymentConfirmedSubscriber
set -e

VPS="ubuntu@161.118.212.110"
SSH="ssh -i /c/VibeCoding/CD/SSH/vanan.pem -o StrictHostKeyChecking=no $VPS"

echo "=== VR2: Checkout + Payment webhook ==="

# Step 1: Checkout (create order in PG)
echo "--- Step 1: Checkout ---"
CHECKOUT_RESP=$(docker exec vanan-gateway curl -s -X POST -H 'Content-Type: application/json' \
  -d '{"customerDeviceId":"vr2-phase35","items":[{"productId":"00000000-0000-0000-0000-000000000001","tenantId":"00000000-0000-0000-0000-000000000001","productName":"VR Test Phase 3.5","vatRate":0.10,"quantity":1,"unitPrice":10000}]}' \
  http://localhost:80/api/public/orders/checkout)
echo "Checkout response: $CHECKOUT_RESP"

# Extract first order ID
ORDER_ID=$(echo "$CHECKOUT_RESP" | python3 -c "import json,sys; d=json.load(sys.stdin); print(d['orders'][0]['orderId'])" 2>/dev/null || echo "")
if [ -z "$ORDER_ID" ]; then
  echo "FAIL: Could not extract orderId from checkout response"
  exit 1
fi
echo "Order ID: $ORDER_ID"

# Step 2: Simulate payment webhook (calls MarkPaidAsync with enqueuePaymentConfirmedEvent=true)
echo "--- Step 2: Payment webhook (MarkPaidAsync) ---"
WEBHOOK_RESP=$(docker exec vanan-gateway curl -s -w '\nHTTP:%{http_code}' -X POST -H 'Content-Type: application/json' \
  -d "{\"orderId\":\"$ORDER_ID\",\"tenantId\":\"00000000-0000-0000-0000-000000000001\",\"transactionId\":\"VR2-TXN-$ORDER_ID\",\"amount\":11000,\"currency\":\"VND\"}" \
  http://localhost:80/api/webhooks/payment)
echo "Webhook response: $WEBHOOK_RESP"

# Step 3: Check PG order status (should be Paid)
echo "--- Step 3: Check PG order status ---"
sleep 2
PG_STATUS=$(docker exec postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT \"PaymentStatus\" FROM \"Orders\" WHERE \"Id\"='$ORDER_ID';" 2>/dev/null || echo "QUERY_FAILED")
echo "PG PaymentStatus: $PG_STATUS"

# Step 4: Check Outbox for OrderPaymentConfirmed event
echo "--- Step 4: Check Outbox for OrderPaymentConfirmed event ---"
OUTBOX_EVENTS=$(docker exec postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT \"EventType\",\"Status\",\"RoutingKey\" FROM \"OutboxMessages\" WHERE \"EventData\" LIKE '%$ORDER_ID%' AND \"EventType\"='OrderPaymentConfirmed';" 2>/dev/null || echo "QUERY_FAILED")
echo "Outbox events: $OUTBOX_EVENTS"

# Step 5: Check ShopERP logs for PaymentConfirmedSubscriber
echo "--- Step 5: Check ShopERP PaymentConfirmedSubscriber logs ---"
sleep 3
SHOPERP_LOGS=$(docker logs vanan-shoperp --since 30s 2>&1 | grep -i 'PaymentConfirmed\|accounting entries\|marked as Paid' | tail -5)
echo "ShopERP logs: $SHOPERP_LOGS"

echo "=== VR2 Complete ==="
