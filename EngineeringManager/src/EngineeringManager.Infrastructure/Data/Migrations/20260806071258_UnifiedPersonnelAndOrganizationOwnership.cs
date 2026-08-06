using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace EngineeringManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UnifiedPersonnelAndOrganizationOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrganizationUnits_Code",
                table: "OrganizationUnits");

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessPartnerId",
                table: "OrganizationUnits",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAuthorizationScope",
                table: "OrganizationUnits",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LegalEntityId",
                table: "OrganizationUnits",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonId",
                table: "Employees",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonId",
                table: "ConstructionWorkers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "People",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IdentityNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IdentityNumberNormalized = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BankAccountNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BankName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_People", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PersonnelEngagementHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    InternalType = table.Column<int>(type: "int", nullable: true),
                    ExternalType = table.Column<int>(type: "int", nullable: true),
                    LegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrganizationUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CrewBusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PositionTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonnelEngagementHistories", x => x.Id);
                    table.CheckConstraint("CK_PersonnelEngagementHistory_Scope", "(([Scope] = 1 AND [InternalType] IS NOT NULL AND [ExternalType] IS NULL) OR ([Scope] = 2 AND [InternalType] IS NULL AND [ExternalType] IS NOT NULL)) AND NOT ([LegalEntityId] IS NOT NULL AND [BusinessPartnerId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PersonnelEngagementHistories_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonnelEngagementHistories_BusinessPartners_CrewBusinessPartnerId",
                        column: x => x.CrewBusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonnelEngagementHistories_LegalEntities_LegalEntityId",
                        column: x => x.LegalEntityId,
                        principalTable: "LegalEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonnelEngagementHistories_OrganizationUnits_OrganizationUnitId",
                        column: x => x.OrganizationUnitId,
                        principalTable: "OrganizationUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonnelEngagementHistories_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonnelEngagementHistories_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                DECLARE @EmployeePeople TABLE (EmployeeId uniqueidentifier PRIMARY KEY, PersonId uniqueidentifier NOT NULL);
                INSERT INTO @EmployeePeople (EmployeeId, PersonId)
                SELECT [Id], NEWID() FROM [Employees] WHERE [PersonId] IS NULL;

                INSERT INTO [People] ([Id], [PersonNumber], [Name], [IdentityNumber], [IdentityNumberNormalized], [Phone], [BankAccountNumber], [BankName], [Notes], [IsActive], [CreatedAt], [UpdatedAt], [ConcurrencyStamp])
                SELECT map.[PersonId], CONCAT(N'PE-', REPLACE(CONVERT(nvarchar(36), employee.[Id]), N'-', N'')), employee.[Name], employee.[IdentityNumber], NULL,
                       employee.[Phone], employee.[BankAccountNumber], employee.[BankName], employee.[Notes], employee.[IsActive], employee.[CreatedAt], employee.[UpdatedAt], NEWID()
                FROM [Employees] employee
                INNER JOIN @EmployeePeople map ON map.[EmployeeId] = employee.[Id];

                UPDATE employee SET [PersonId] = map.[PersonId]
                FROM [Employees] employee
                INNER JOIN @EmployeePeople map ON map.[EmployeeId] = employee.[Id];

                DECLARE @WorkerPeople TABLE (WorkerId uniqueidentifier PRIMARY KEY, PersonId uniqueidentifier NOT NULL);
                INSERT INTO @WorkerPeople (WorkerId, PersonId)
                SELECT [Id], NEWID() FROM [ConstructionWorkers] WHERE [PersonId] IS NULL;

                INSERT INTO [People] ([Id], [PersonNumber], [Name], [IdentityNumber], [IdentityNumberNormalized], [Phone], [BankAccountNumber], [BankName], [Notes], [IsActive], [CreatedAt], [UpdatedAt], [ConcurrencyStamp])
                SELECT map.[PersonId], CONCAT(N'PW-', REPLACE(CONVERT(nvarchar(36), worker.[Id]), N'-', N'')), worker.[Name], worker.[IdentityNumber], NULL,
                       worker.[Phone], worker.[BankAccountNumber], worker.[BankName], worker.[Notes], worker.[IsActive], worker.[CreatedAt], worker.[UpdatedAt], NEWID()
                FROM [ConstructionWorkers] worker
                INNER JOIN @WorkerPeople map ON map.[WorkerId] = worker.[Id];

                UPDATE worker SET [PersonId] = map.[PersonId]
                FROM [ConstructionWorkers] worker
                INNER JOIN @WorkerPeople map ON map.[WorkerId] = worker.[Id];

                INSERT INTO [PersonnelEngagementHistories] ([Id], [PersonId], [Scope], [InternalType], [ExternalType], [LegalEntityId], [BusinessPartnerId], [OrganizationUnitId], [ProjectId], [CrewBusinessPartnerId], [PositionTitle], [StartDate], [EndDate], [IsPrimary], [Notes], [Reason], [ConcurrencyStamp])
                SELECT NEWID(), employee.[PersonId], 1, employee.[EmployeeType], NULL, history.[LegalEntityId], NULL, history.[DepartmentId], history.[ProjectId], history.[CrewBusinessPartnerId],
                       history.[PositionTitle], history.[StartDate], history.[EndDate], history.[IsPrimary], history.[Notes], N'统一人员迁移回填', NEWID()
                FROM [EmployeeAffiliationHistories] history
                INNER JOIN [Employees] employee ON employee.[Id] = history.[EmployeeId]
                WHERE employee.[PersonId] IS NOT NULL;

                INSERT INTO [PersonnelEngagementHistories] ([Id], [PersonId], [Scope], [InternalType], [ExternalType], [LegalEntityId], [BusinessPartnerId], [OrganizationUnitId], [ProjectId], [CrewBusinessPartnerId], [PositionTitle], [StartDate], [EndDate], [IsPrimary], [Notes], [Reason], [ConcurrencyStamp])
                SELECT NEWID(), employee.[PersonId], 1, employee.[EmployeeType], NULL, employee.[DefaultLegalEntityId], NULL, NULL, NULL, NULL,
                       employee.[PositionTitle], COALESCE(employee.[HireDate], CAST(employee.[CreatedAt] AS date)), employee.[LeaveDate], 1, employee.[Notes], N'统一人员迁移回填', NEWID()
                FROM [Employees] employee
                WHERE employee.[PersonId] IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM [EmployeeAffiliationHistories] history WHERE history.[EmployeeId] = employee.[Id]);

                INSERT INTO [PersonnelEngagementHistories] ([Id], [PersonId], [Scope], [InternalType], [ExternalType], [LegalEntityId], [BusinessPartnerId], [OrganizationUnitId], [ProjectId], [CrewBusinessPartnerId], [PositionTitle], [StartDate], [EndDate], [IsPrimary], [Notes], [Reason], [ConcurrencyStamp])
                SELECT NEWID(), worker.[PersonId], 2, NULL, 1, NULL, membership.[CrewBusinessPartnerId], NULL, NULL, membership.[CrewBusinessPartnerId],
                       worker.[Trade], membership.[StartDate], membership.[EndDate], membership.[IsPrimary], COALESCE(membership.[Notes], worker.[Notes]), N'统一人员迁移回填', NEWID()
                FROM [ConstructionCrewMemberships] membership
                INNER JOIN [ConstructionWorkers] worker ON worker.[Id] = membership.[ConstructionWorkerId]
                WHERE worker.[PersonId] IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationUnits_Code",
                table: "OrganizationUnits",
                column: "Code",
                unique: true,
                filter: "[LegalEntityId] IS NULL AND [BusinessPartnerId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationUnits_BusinessPartnerId_Code",
                table: "OrganizationUnits",
                columns: new[] { "BusinessPartnerId", "Code" },
                unique: true,
                filter: "[BusinessPartnerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationUnits_LegalEntityId_Code",
                table: "OrganizationUnits",
                columns: new[] { "LegalEntityId", "Code" },
                unique: true,
                filter: "[LegalEntityId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrganizationUnits_Owner",
                table: "OrganizationUnits",
                sql: "NOT ([LegalEntityId] IS NOT NULL AND [BusinessPartnerId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_PersonId",
                table: "Employees",
                column: "PersonId",
                unique: true,
                filter: "[PersonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionWorkers_PersonId",
                table: "ConstructionWorkers",
                column: "PersonId",
                unique: true,
                filter: "[PersonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_People_IdentityNumberNormalized",
                table: "People",
                column: "IdentityNumberNormalized",
                unique: true,
                filter: "[IdentityNumberNormalized] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_People_PersonNumber",
                table: "People",
                column: "PersonNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonnelEngagementHistories_BusinessPartnerId",
                table: "PersonnelEngagementHistories",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonnelEngagementHistories_CrewBusinessPartnerId",
                table: "PersonnelEngagementHistories",
                column: "CrewBusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonnelEngagementHistories_LegalEntityId",
                table: "PersonnelEngagementHistories",
                column: "LegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonnelEngagementHistories_OrganizationUnitId",
                table: "PersonnelEngagementHistories",
                column: "OrganizationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonnelEngagementHistories_PersonId_StartDate_IsPrimary",
                table: "PersonnelEngagementHistories",
                columns: new[] { "PersonId", "StartDate", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonnelEngagementHistories_ProjectId",
                table: "PersonnelEngagementHistories",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_ConstructionWorkers_People_PersonId",
                table: "ConstructionWorkers",
                column: "PersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_People_PersonId",
                table: "Employees",
                column: "PersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationUnits_BusinessPartners_BusinessPartnerId",
                table: "OrganizationUnits",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationUnits_LegalEntities_LegalEntityId",
                table: "OrganizationUnits",
                column: "LegalEntityId",
                principalTable: "LegalEntities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConstructionWorkers_People_PersonId",
                table: "ConstructionWorkers");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_People_PersonId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationUnits_BusinessPartners_BusinessPartnerId",
                table: "OrganizationUnits");

            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationUnits_LegalEntities_LegalEntityId",
                table: "OrganizationUnits");

            migrationBuilder.DropTable(
                name: "PersonnelEngagementHistories");

            migrationBuilder.DropTable(
                name: "People");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationUnits_Code",
                table: "OrganizationUnits");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationUnits_BusinessPartnerId_Code",
                table: "OrganizationUnits");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationUnits_LegalEntityId_Code",
                table: "OrganizationUnits");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrganizationUnits_Owner",
                table: "OrganizationUnits");

            migrationBuilder.DropIndex(
                name: "IX_Employees_PersonId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_ConstructionWorkers_PersonId",
                table: "ConstructionWorkers");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerId",
                table: "OrganizationUnits");

            migrationBuilder.DropColumn(
                name: "IsAuthorizationScope",
                table: "OrganizationUnits");

            migrationBuilder.DropColumn(
                name: "LegalEntityId",
                table: "OrganizationUnits");

            migrationBuilder.DropColumn(
                name: "PersonId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PersonId",
                table: "ConstructionWorkers");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationUnits_Code",
                table: "OrganizationUnits",
                column: "Code",
                unique: true);
        }
    }
}
#pragma warning restore CA1861
