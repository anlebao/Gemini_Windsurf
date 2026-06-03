# ADR-001: SQLite + NATS Offline First

## Status

**Approved** (2026-06-01)

## Context

ShopERP cần hoạt động ở môi trường có kết nối mạng không ổn định (quán cà phê, nhà hàng ở vùng sâu). Các yêu cầu:

- **Offline capability**: Các station (order, kitchen) phải hoạt động khi mất mạng
- **Event-driven**: Các thay đổi cần được broadcast real-time đến các station khác
- **Data sync**: Dữ liệu local cần đồng bộ lên cloud khi có mạng
- **Conflict resolution**: Xử lý conflicts khi nhiều station cùng thay đổi

## Decision

Sử dụng **SQLite local + NATS message bus + PostgreSQL cloud** architecture.

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   Station   │     │   Station   │     │   Station   │
│  (SQLite)   │     │  (SQLite)   │     │  (SQLite)   │
└──────┬──────┘     └──────┬──────┘     └──────┬──────┘
       │                   │                   │
       └───────────────────┼───────────────────┘
                           │
                    ┌──────▼──────┐
                    │    NATS     │
                    │  (Message   │
                    │   Broker)   │
                    └──────┬──────┘
                           │
                    ┌──────▼──────┐
                    │  PostgreSQL │
                    │   (Cloud)   │
                    └─────────────┘
```

### Data Flow

1. **Local write**: Mọi thay đổi ghi vào SQLite local trước
2. **Outbox pattern**: Events được ghi vào Outbox table trong SQLite
3. **NATS publish**: Background worker đọc Outbox, publish lên NATS
4. **Sync to cloud**: NATS consumer ghi vào PostgreSQL
5. **Real-time broadcast**: Other stations subscribe NATS, update SQLite local

## Consequences

### Positive

- [x] **Offline operation**: Station hoạt động 100% khi mất mạng
- [x] **Real-time sync**: Events broadcast ngay lập tức qua NATS
- [x] **Resilient**: SQLite local + retry mechanism, không mất data
- [x] **Scalable**: NATS cluster có thể scale horizontally
- [x] **Simple deployment**: Không cần complex distributed transaction

### Negative

- [ ] **Eventual consistency**: Data giữa các station có thể khác nhau vài giây
- [ ] **Conflict complexity**: Cần logic resolve conflicts (last-write-wins hoặc custom)
- [ ] **Operational overhead**: Cần monitor 3 hệ thống (SQLite, NATS, PostgreSQL)
- [ ] **Testing complexity**: Cần test cả online và offline scenarios

### Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| NATS downtime | High | SQLite local hoạt động, retry queue |
| Conflict storms | Medium | Implement versioning + timestamps |
| Data loss | Critical | Outbox pattern, at-least-once delivery |
| Sync lag | Medium | Monitor + alert if lag > 30s |

## Alternatives Considered

| Option | Pros | Cons | Decision |
|--------|------|------|----------|
| Direct PostgreSQL | Simple, consistent | Requires internet, latency cao | Rejected |
| Firebase Realtime | Managed, real-time | Vendor lock-in, cost | Rejected |
| PouchDB/CouchDB | Built-in sync | Learning curve, less control | Rejected |
| SQLite + NATS | Full control, scalable | Complex, cần build | **Selected** |

## Implementation

- [x] CoreHub với SQLite + EF Core
- [x] Outbox pattern implementation
- [x] NATS integration (publish/subscribe)
- [x] PostgreSQL sync consumer
- [ ] Conflict resolution UI
- [ ] Offline indicator in UI

## References

- `docs/Implement/NATS_SQLite_Deployment_Guide.md`
- `docs/Implement/Outbox_Pattern_Implementation_Guide.md`
- `.windsurf/skills/nats-sqlite-deployment-validation.md`
- `docs/Implement/SQLite_Concurrency_Architecture.md`

## Related

- ADR-002: Multi-tenancy cần được preserve qua tất cả layers
- ADR-003: Accounting entries append-only phù hợp với event-driven

## Notes

- **Proposed by**: AI Assistant
- **Approved by**: User (implicit via roadmap approval)
- **Date**: 2026-06-01
- **Review cycle**: 6 months
