using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "ClubPaymentAccounts_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "MembershipOrderMembers_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "MembershipOrders_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "MembershipPlans_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "StripeWebhookEvents_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.AddColumn<int>(
                name: "MembershipOrderId",
                table: "MemberSeasons",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClubPaymentAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    StripeAccountId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ChargesEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    PayoutsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    DetailsSubmitted = table.Column<bool>(type: "boolean", nullable: false),
                    ConnectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubPaymentAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubPaymentAccounts_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MembershipPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    SeasonId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PriceCents = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    MaxAdults = table.Column<int>(type: "integer", nullable: false),
                    IncludesChildren = table.Column<bool>(type: "boolean", nullable: false),
                    BirthYearFrom = table.Column<int>(type: "integer", nullable: true),
                    BirthYearTo = table.Column<int>(type: "integer", nullable: true),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    NewMembersOnly = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresEnrolledFamilyAdult = table.Column<bool>(type: "boolean", nullable: false),
                    GrantsSeasonEnrollment = table.Column<bool>(type: "boolean", nullable: false),
                    AvailableFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    AvailableUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    IsSelfService = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MembershipPlans_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MembershipPlans_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StripeWebhookEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    EventId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StripeAccountId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ObjectId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StripeWebhookEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MembershipOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    SeasonId = table.Column<int>(type: "integer", nullable: false),
                    MembershipPlanId = table.Column<int>(type: "integer", nullable: false),
                    PlanName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AmountCents = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PayerMemberId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PaymentMethod = table.Column<int>(type: "integer", nullable: true),
                    StripeCheckoutSessionId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    StripePaymentIntentId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MarkedPaidByMemberId = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MembershipOrders_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MembershipOrders_Members_MarkedPaidByMemberId",
                        column: x => x.MarkedPaidByMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MembershipOrders_Members_PayerMemberId",
                        column: x => x.PayerMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MembershipOrders_MembershipPlans_MembershipPlanId",
                        column: x => x.MembershipPlanId,
                        principalTable: "MembershipPlans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MembershipOrders_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MembershipOrderMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    MembershipOrderId = table.Column<int>(type: "integer", nullable: false),
                    MemberId = table.Column<int>(type: "integer", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipOrderMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MembershipOrderMembers_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MembershipOrderMembers_MembershipOrders_MembershipOrderId",
                        column: x => x.MembershipOrderId,
                        principalTable: "MembershipOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberSeasons_MembershipOrderId",
                table: "MemberSeasons",
                column: "MembershipOrderId");

            migrationBuilder.CreateIndex(
                name: "UX_ClubPaymentAccounts_ClubId",
                table: "ClubPaymentAccounts",
                column: "ClubId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ClubPaymentAccounts_StripeAccountId",
                table: "ClubPaymentAccounts",
                column: "StripeAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MembershipOrderMembers_MemberId",
                table: "MembershipOrderMembers",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "UX_MembershipOrderMembers_MembershipOrderId_MemberId",
                table: "MembershipOrderMembers",
                columns: new[] { "MembershipOrderId", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MembershipOrders_ClubId_SeasonId_Status",
                table: "MembershipOrders",
                columns: new[] { "ClubId", "SeasonId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MembershipOrders_MarkedPaidByMemberId",
                table: "MembershipOrders",
                column: "MarkedPaidByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipOrders_MembershipPlanId",
                table: "MembershipOrders",
                column: "MembershipPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipOrders_PayerMemberId",
                table: "MembershipOrders",
                column: "PayerMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipOrders_SeasonId",
                table: "MembershipOrders",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipOrders_StripePaymentIntentId",
                table: "MembershipOrders",
                column: "StripePaymentIntentId");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipPlans_SeasonId",
                table: "MembershipPlans",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "UX_MembershipPlans_ClubId_SeasonId_Name",
                table: "MembershipPlans",
                columns: new[] { "ClubId", "SeasonId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_StripeWebhookEvents_EventId",
                table: "StripeWebhookEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MemberSeasons_MembershipOrders_MembershipOrderId",
                table: "MemberSeasons",
                column: "MembershipOrderId",
                principalTable: "MembershipOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MemberSeasons_MembershipOrders_MembershipOrderId",
                table: "MemberSeasons");

            migrationBuilder.DropTable(
                name: "ClubPaymentAccounts");

            migrationBuilder.DropTable(
                name: "MembershipOrderMembers");

            migrationBuilder.DropTable(
                name: "StripeWebhookEvents");

            migrationBuilder.DropTable(
                name: "MembershipOrders");

            migrationBuilder.DropTable(
                name: "MembershipPlans");

            migrationBuilder.DropIndex(
                name: "IX_MemberSeasons_MembershipOrderId",
                table: "MemberSeasons");

            migrationBuilder.DropColumn(
                name: "MembershipOrderId",
                table: "MemberSeasons");

            migrationBuilder.DropSequence(
                name: "ClubPaymentAccounts_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "MembershipOrderMembers_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "MembershipOrders_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "MembershipPlans_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "StripeWebhookEvents_Id_Sequence");
        }
    }
}
