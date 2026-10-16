using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCourtSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "BookingCreditGrants_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "CourtPriceRules_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "CourtPurchases_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "CourtSaleSettings_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateTable(
                name: "BookingCreditGrants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    MemberId = table.Column<int>(type: "integer", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GrantedByMemberId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingCreditGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingCreditGrants_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookingCreditGrants_Members_GrantedByMemberId",
                        column: x => x.GrantedByMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BookingCreditGrants_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CourtPriceRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    CourtIds = table.Column<int[]>(type: "integer[]", nullable: false),
                    Weekdays = table.Column<int[]>(type: "integer[]", nullable: false),
                    From = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    To = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    PricePerHourCents = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourtPriceRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourtPriceRules_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CourtPurchases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    MemberId = table.Column<int>(type: "integer", nullable: true),
                    BookingId = table.Column<int>(type: "integer", nullable: true),
                    CourtId = table.Column<int>(type: "integer", nullable: false),
                    Interval_To = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Interval_From = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PriceCents = table.Column<int>(type: "integer", nullable: false),
                    CreditUsed = table.Column<bool>(type: "boolean", nullable: false),
                    AmountDueCents = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PaymentMethod = table.Column<int>(type: "integer", nullable: true),
                    StripeCheckoutSessionId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    StripePaymentIntentId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    HoldExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledByMemberId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourtPurchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourtPurchases_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CourtPurchases_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CourtPurchases_Courts_CourtId",
                        column: x => x.CourtId,
                        principalTable: "Courts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CourtPurchases_Members_CancelledByMemberId",
                        column: x => x.CancelledByMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CourtPurchases_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CourtSaleSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    PlayModeId = table.Column<int>(type: "integer", nullable: true),
                    TimeZoneId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MaxBookingMinutes = table.Column<int>(type: "integer", nullable: false),
                    MaxDaysInAdvance = table.Column<int>(type: "integer", nullable: false),
                    MaxUpcomingBookings = table.Column<int>(type: "integer", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourtSaleSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourtSaleSettings_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CourtSaleSettings_PlayModes_PlayModeId",
                        column: x => x.PlayModeId,
                        principalTable: "PlayModes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingCreditGrants_ClubId",
                table: "BookingCreditGrants",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingCreditGrants_GrantedByMemberId",
                table: "BookingCreditGrants",
                column: "GrantedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingCreditGrants_MemberId",
                table: "BookingCreditGrants",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_CourtPriceRules_ClubId",
                table: "CourtPriceRules",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_CourtPurchases_BookingId",
                table: "CourtPurchases",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_CourtPurchases_CancelledByMemberId",
                table: "CourtPurchases",
                column: "CancelledByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_CourtPurchases_ClubId_Status",
                table: "CourtPurchases",
                columns: new[] { "ClubId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CourtPurchases_CourtId",
                table: "CourtPurchases",
                column: "CourtId");

            migrationBuilder.CreateIndex(
                name: "IX_CourtPurchases_Interval_From",
                table: "CourtPurchases",
                column: "Interval_From",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_CourtPurchases_Interval_To",
                table: "CourtPurchases",
                column: "Interval_To",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_CourtPurchases_MemberId",
                table: "CourtPurchases",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_CourtPurchases_StripeCheckoutSessionId",
                table: "CourtPurchases",
                column: "StripeCheckoutSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CourtPurchases_StripePaymentIntentId",
                table: "CourtPurchases",
                column: "StripePaymentIntentId");

            migrationBuilder.CreateIndex(
                name: "IX_CourtSaleSettings_PlayModeId",
                table: "CourtSaleSettings",
                column: "PlayModeId");

            migrationBuilder.CreateIndex(
                name: "UX_CourtSaleSettings_ClubId",
                table: "CourtSaleSettings",
                column: "ClubId",
                unique: true);

            // The bookings left on the guest cards become booking credits on a normal club membership, before the guest cards are dropped
            migrationBuilder.Sql(GuestCardMigrationSql.Sql);

            migrationBuilder.DropTable(
                name: "GuestCards");

            migrationBuilder.DropSequence(
                name: "GuestCards_Id_Sequence");
        }

        /// <summary>Only the schema is reverted: the guest cards come back empty, the credits and created club members are dropped or stay.</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingCreditGrants");

            migrationBuilder.DropTable(
                name: "CourtPriceRules");

            migrationBuilder.DropTable(
                name: "CourtPurchases");

            migrationBuilder.DropTable(
                name: "CourtSaleSettings");

            migrationBuilder.DropSequence(
                name: "BookingCreditGrants_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "CourtPriceRules_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "CourtPurchases_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "CourtSaleSettings_Id_Sequence");

            migrationBuilder.CreateSequence<int>(
                name: "GuestCards_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateTable(
                name: "GuestCards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    ClubId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<Guid>(type: "uuid", nullable: false),
                    GuestMemberId = table.Column<int>(type: "integer", nullable: false),
                    PurchasedBookings = table.Column<int>(type: "integer", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuestCards_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GuestCards_Members_GuestMemberId",
                        column: x => x.GuestMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuestCards_ClubId",
                table: "GuestCards",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestCards_GuestMemberId",
                table: "GuestCards",
                column: "GuestMemberId");
        }
    }
}
