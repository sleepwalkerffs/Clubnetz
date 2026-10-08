using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AtpClubMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add IsAtpClub column to Clubs table (defaults to false for existing clubs)
            migrationBuilder.AddColumn<bool>(
                name: "IsAtpClub",
                table: "Clubs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Step 2: Create DummyAtpClub and move IsAtpPlayer members to it.
            // Only execute if there are any ClubMembers marked as IsAtpPlayer.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    dummy_club_id integer;
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Members" WHERE "IsAtpPlayer" = true AND "MemberType" = 0) THEN
                        -- Get the next ID from the HiLo sequence
                        SELECT nextval('"Clubs_Id_Sequence"') INTO dummy_club_id;

                        -- Insert the DummyAtpClub with all required columns
                        INSERT INTO "Clubs" (
                            "Id",
                            "Name",
                            "ConcurrentAllowedBookings",
                            "IsAtpClub",
                            "BookingGracePeriodInMinutes",
                            "OpeningHours_From",
                            "OpeningHours_To",
                            "PrimeTimeHours_From",
                            "PrimeTimeHours_To",
                            "Metadata_Created"
                        ) VALUES (
                            dummy_club_id,
                            'DummyAtpClub',
                            15,
                            true,
                            NULL,
                            '08:00:00',
                            '21:00:00',
                            '17:00:00',
                            '20:00:00',
                            NOW()
                        );

                        -- Move all ClubMembers that are currently marked as IsAtpPlayer to the DummyAtpClub
                        UPDATE "Members"
                        SET "ClubId" = dummy_club_id
                        WHERE "IsAtpPlayer" = true AND "MemberType" = 0;
                    END IF;
                END $$;
                """);

            // Step 3: Drop the IsAtpPlayer column (no longer needed)
            migrationBuilder.DropColumn(
                name: "IsAtpPlayer",
                table: "Members");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Step 1: Re-add IsAtpPlayer column
            migrationBuilder.AddColumn<bool>(
                name: "IsAtpPlayer",
                table: "Members",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Step 2: Mark members in DummyAtpClub as IsAtpPlayer and move them back
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    dummy_club_id integer;
                BEGIN
                    SELECT "Id" INTO dummy_club_id FROM "Clubs" WHERE "Name" = 'DummyAtpClub' AND "IsAtpClub" = true LIMIT 1;

                    IF dummy_club_id IS NOT NULL THEN
                        -- Mark members in the DummyAtpClub as IsAtpPlayer
                        UPDATE "Members"
                        SET "IsAtpPlayer" = true
                        WHERE "ClubId" = dummy_club_id AND "MemberType" = 0;

                        -- Delete the DummyAtpClub (members will need manual reassignment)
                        DELETE FROM "Clubs" WHERE "Id" = dummy_club_id;
                    END IF;
                END $$;
                """);

            // Step 3: Drop IsAtpClub column
            migrationBuilder.DropColumn(
                name: "IsAtpClub",
                table: "Clubs");
        }
    }
}
