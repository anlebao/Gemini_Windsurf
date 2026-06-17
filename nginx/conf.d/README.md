# Nginx Config — VanAn Tech

## Files

- `vanantech.conf` — Production config (HTTP redirect + HTTPS). **Active sau khi có SSL cert.**

## SSL Bootstrap (chạy 1 lần)

Trước khi nginx có thể dùng HTTPS, cần issue SSL certificate:

```bash
# Trên VPS, sau khi deploy lần đầu:
sudo bash /opt/vanan/scripts/init-ssl.sh
```

**Thứ tự thực hiện:**
1. Thêm DNS A records trên Nhân Hòa (xem bên dưới)
2. Deploy qua CD pipeline bình thường
3. SSH vào VPS, chạy `init-ssl.sh`
4. Sau đó nginx tự restart với HTTPS

## DNS Records cần tạo trên Nhân Hòa

| Subdomain | Type | Value |
|---|---|---|
| `@` | A | `<VPS_IP>` |
| `www` | A | `<VPS_IP>` |
| `diemthuong` | A | `<VPS_IP>` |
| `app` | A | `<VPS_IP>` |
| `api` | A | `<VPS_IP>` |

## Routing

| Domain | → Container |
|---|---|
| `vanantech.io.vn` | nginx static (Dashboard HTML) |
| `diemthuong.vanantech.io.vn` | vanan-khachlink:80 |
| `app.vanantech.io.vn` | vanan-shoperp:80 |
| `api.vanantech.io.vn` | vanan-gateway:80 |
