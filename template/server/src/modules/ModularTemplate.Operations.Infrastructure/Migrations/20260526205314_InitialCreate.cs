using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModularTemplate.Operations.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "operations");

            migrationBuilder.CreateTable(
                name: "domain_events",
                schema: "operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AggregateType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AggregateId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EventType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EventVersion = table.Column<int>(type: "integer", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_domain_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "operations",
                schema: "operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Pending"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ModuleName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    HandlerName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MessageType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SourceModule = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TargetModule = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CausationId = table.Column<Guid>(type: "uuid", nullable: true),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DispatchedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FailedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_domain_events_AggregateType_AggregateId",
                schema: "operations",
                table: "domain_events",
                columns: new[] { "AggregateType", "AggregateId" });

            migrationBuilder.CreateIndex(
                name: "IX_domain_events_EventType",
                schema: "operations",
                table: "domain_events",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_operations_CreatedAtUtc",
                schema: "operations",
                table: "operations",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_ModuleName_MessageId_HandlerName",
                schema: "operations",
                table: "inbox_messages",
                columns: new[] { "ModuleName", "MessageId", "HandlerName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_ReceivedAtUtc",
                schema: "operations",
                table: "inbox_messages",
                column: "ReceivedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_operations_Status",
                schema: "operations",
                table: "operations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_MessageId",
                schema: "operations",
                table: "outbox_messages",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_MessageType",
                schema: "operations",
                table: "outbox_messages",
                column: "MessageType");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_Status_NextAttemptAtUtc_CreatedAtUtc",
                schema: "operations",
                table: "outbox_messages",
                columns: new[] { "Status", "NextAttemptAtUtc", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "domain_events",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "operations",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "operations");
        }
    }
}
