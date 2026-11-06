# Threat Suite — SRS v1.1 §10 (T1–T13) — task_ai_p6

Coverage map: threat → kiểm soát → test local (xUnit, `dotnet test`) → check production (RV script / e2e).
Mọi test trong folder này mang `[Trait("Threat","Tx")]` và tên method prefix `Tx_`.

| # | Threat | Local (file / test) | Production (CP3) |
|---|--------|---------------------|------------------|
| T1 | Cross-tenant leak (SQL + vector) | `T1_CrossTenantTests` (product/chunk/session ×tenant) · `Chat_T1_TenantASeesZeroOfTenantB` · `SessionMessages_CrossTenant_ReturnsEmpty_T1` (ChatApiTests) | rv-script: tenant A hỏi món tenant B → no data; vector path chỉ chạy trên PG thật |
| T2 | Prompt injection user message | `T2_InjectionPayloadTests` — 10 payloads → refused `policy_violation`, 0 LLM, no leak | rv-script: subset 5 payloads prod |
| T3 | Injection trong knowledge doc | `T3_DocInjectionTests` — sanitizer matrix + ingest→approve→chat E2E · `Document_WithInjectionLines_SanitizedFlag_AndStripped` (IngestTests) | — (local-only, sanitizer+retrieval) |
| T4 | Hallucinated product/price | `T4_HallucinationTests` — fake ref strip / unknown product / invented calories · `Validator_*` (ChatUnitTests) | rv-script: hỏi món không có |
| T5 | Confidential leak | `T5_ConfidentialLeakTests` — RecipeQuery confidential / DB-level public chunks / Internal doc · `Document_Confidential_Approved_StillNotInPublicRetrieval` (IngestTests) · `UsageFilter_*` (ChatUnitTests) | — |
| T6 | Tenant spoofing (forge/expired/scope) | `AuthTests` — BadSignature/ExpiredCtx/CallerIdMismatch/TamperedPayload/ShopCaller_TenantOutOfScope | rv-script: forged sig curl → 401 |
| T7 | Key/secret leak | `T7_KeyLeakLogTests` — log scan: raw key, key hash, signing secret, ctx/sig never logged | — |
| T8 | Abuse/chi phí (quota deny + degrade) | `QuotaGateUnitTests` + `MeteringGateApiTests` (Deny→refuse/0 LLM · degrade cache fail→0 LLM) · `KillSwitchApiTests` | rv-script: tenant ví=0 → refuse |
| T9 | External abuse (scope off) | `Chat_ExternalKnowledge_Refused_DefaultOff` (ChatApiTests) · `PolicyEngine_ZeroSourceFlags_Refuses` (ChatUnitTests) | — |
| T10 | Stale knowledge | `T10_KnowledgeFreshnessTests` — đổi giá→giá mới · status source_version/synced_at · stale strip+flag · `Chat_StaleProduct_*`, `Validator_StaleProduct_*` | rv-script: đổi giá → sync → giá mới + `/v1/knowledge/status` drift |
| T11 | Approval bypass | `Chat_BrandStory_OnlyPendingDoc_NoData_T11` · `Chat_RejectedDoc_NeverRetrieved_T11` (ChatApiTests) · `Document_Pending_NotRetrievable_*`, `Document_Reject_NeverRetrievable_*` (IngestTests) | rv-script: ingest doc → pending → chat không dùng |
| T12 | Caller scope escalation | `Gateway_Key_IngestRoute_403` · `Gateway_Key_AdminRoute_403` · `ShopCaller_TenantOutOfScope_403` (AuthTests) | rv-script: gateway key → admin/ingest 403 |
| T13 | SQL/RAG conflict | `Chat_T13_StructuredPriceWinsOverDocClaim` (ChatApiTests) · `Validator_T13_WrongPrice_CorrectedToStructured` (ChatUnitTests) | — |

## Chạy

```bash
dotnet test tests/VanAn.TenantAi.Tests --filter "Threat=T2"   # 1 threat
dotnet test tests/VanAn.TenantAi.Tests                        # toàn bộ
```

## Ghi chú

- InMemory provider không có pgvector — T1 vector path + T10 HNSW chỉ verify được trên production (rv-script).
- Eval NFR-10: `../Eval/evalset.json` (≥30 câu, per-intent, vi-VN viết tay) chạy trong `EvalHarnessTests`.
