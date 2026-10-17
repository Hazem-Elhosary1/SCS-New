using Microsoft.EntityFrameworkCore.Migrations;
using Scs.Plans.Infrastructure.Seed;

#nullable disable

namespace Scs.Plans.Infrastructure.Migrations
{
    /// <summary>
    /// The catalogue's first features (spec 012, FR-022 to FR-024): the 35 of <see cref="FeatureSeed"/>,
    /// each inserted only where its key is absent, every one «قادمة».
    /// </summary>
    public partial class SeedFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var statement in FeatureSeed.Statements())
            {
                migrationBuilder.Sql(statement);
            }
        }

        /// <summary>Removes nothing: an operator may have edited a seeded feature, and plans may hold it.</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
