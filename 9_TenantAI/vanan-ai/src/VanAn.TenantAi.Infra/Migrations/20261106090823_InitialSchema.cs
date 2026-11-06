using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace VanAn.TenantAi.Infra.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "ai_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    config_version = table.Column<int>(type: "integer", nullable: false),
                    intent_scopes_json = table.Column<string>(type: "jsonb", nullable: false),
                    external_allow_json = table.Column<string>(type: "jsonb", nullable: false),
                    refused_topics_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_policies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "caller_keys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    caller_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    key_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_admin = table.Column<bool>(type: "boolean", nullable: false),
                    scopes_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    tenant_scope_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_caller_keys", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "conversation_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    message_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    intent = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    product_refs_json = table.Column<string>(type: "jsonb", nullable: true),
                    source_ids_json = table.Column<string>(type: "jsonb", nullable: true),
                    confidence = table.Column<double>(type: "double precision", nullable: true),
                    refused = table.Column<bool>(type: "boolean", nullable: false),
                    refuse_reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    degraded = table.Column<bool>(type: "boolean", nullable: false),
                    prompt_tokens = table.Column<int>(type: "integer", nullable: true),
                    completion_tokens = table.Column<int>(type: "integer", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversation_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "conversation_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    meta_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_active_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversation_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ingest_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    item_count = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ingest_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "intent_routes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    intent = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    allowed_source_flags = table.Column<long>(type: "bigint", nullable: false),
                    requires_facts = table.Column<bool>(type: "boolean", nullable: false),
                    refuse_template = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_intent_routes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    classification = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    approval_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    approved_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    approved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_ref = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_knowledge_documents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_usage_permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    intent = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    classification = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    permission = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_knowledge_usage_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "product_knowledge",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    calories = table.Column<int>(type: "integer", nullable: true),
                    ingredients_json = table.Column<string>(type: "jsonb", nullable: false),
                    allergens_json = table.Column<string>(type: "jsonb", nullable: false),
                    available = table.Column<bool>(type: "boolean", nullable: false),
                    category_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    source_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    synced_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_knowledge", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_ai_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ai_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    persona_json = table.Column<string>(type: "jsonb", nullable: false),
                    business_profile_json = table.Column<string>(type: "jsonb", nullable: false),
                    published_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    published_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_ai_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_chunks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chunk_index = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(768)", nullable: true),
                    token_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_knowledge_chunks", x => x.id);
                    table.ForeignKey(
                        name: "fk_knowledge_chunks_knowledge_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "knowledge_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_policies_tenant_id_config_version",
                table: "ai_policies",
                columns: new[] { "tenant_id", "config_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_caller_keys_caller_id",
                table: "caller_keys",
                column: "caller_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_conversation_messages_expires_at_utc",
                table: "conversation_messages",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_conversation_messages_tenant_id_session_id_created_at_utc",
                table: "conversation_messages",
                columns: new[] { "tenant_id", "session_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_conversation_messages_tenant_id_session_id_message_id",
                table: "conversation_messages",
                columns: new[] { "tenant_id", "session_id", "message_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_conversation_sessions_tenant_id_session_id",
                table: "conversation_sessions",
                columns: new[] { "tenant_id", "session_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ingest_logs_tenant_id_kind_idempotency_key",
                table: "ingest_logs",
                columns: new[] { "tenant_id", "kind", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_intent_routes_intent",
                table: "intent_routes",
                column: "intent",
                unique: true,
                filter: "tenant_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_intent_routes_tenant_id_intent",
                table: "intent_routes",
                columns: new[] { "tenant_id", "intent" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_chunks_document_id_chunk_index",
                table: "knowledge_chunks",
                columns: new[] { "document_id", "chunk_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_chunks_embedding",
                table: "knowledge_chunks",
                column: "embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_chunks_tenant_id",
                table: "knowledge_chunks",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_documents_tenant_id_approval_status",
                table: "knowledge_documents",
                columns: new[] { "tenant_id", "approval_status" });

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_documents_tenant_id_type",
                table: "knowledge_documents",
                columns: new[] { "tenant_id", "type" });

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_usage_permissions_tenant_id_intent_channel",
                table: "knowledge_usage_permissions",
                columns: new[] { "tenant_id", "intent", "channel" });

            migrationBuilder.CreateIndex(
                name: "ix_product_knowledge_tenant_id",
                table: "product_knowledge",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_knowledge_tenant_id_available",
                table: "product_knowledge",
                columns: new[] { "tenant_id", "available" });

            migrationBuilder.CreateIndex(
                name: "ix_product_knowledge_tenant_id_product_id",
                table: "product_knowledge",
                columns: new[] { "tenant_id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_ai_configs_tenant_id_status",
                table: "tenant_ai_configs",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_ai_configs_tenant_id_version",
                table: "tenant_ai_configs",
                columns: new[] { "tenant_id", "version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_policies");

            migrationBuilder.DropTable(
                name: "caller_keys");

            migrationBuilder.DropTable(
                name: "conversation_messages");

            migrationBuilder.DropTable(
                name: "conversation_sessions");

            migrationBuilder.DropTable(
                name: "ingest_logs");

            migrationBuilder.DropTable(
                name: "intent_routes");

            migrationBuilder.DropTable(
                name: "knowledge_chunks");

            migrationBuilder.DropTable(
                name: "knowledge_usage_permissions");

            migrationBuilder.DropTable(
                name: "product_knowledge");

            migrationBuilder.DropTable(
                name: "tenant_ai_configs");

            migrationBuilder.DropTable(
                name: "knowledge_documents");
        }
    }
}
