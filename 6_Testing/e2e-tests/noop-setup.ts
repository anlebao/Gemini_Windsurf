export default async function noopSetup() {
  // No-op global setup for VPS tests (skip local service health checks)
  console.log('[noop-setup] Skipping local service health checks for VPS test');
}
