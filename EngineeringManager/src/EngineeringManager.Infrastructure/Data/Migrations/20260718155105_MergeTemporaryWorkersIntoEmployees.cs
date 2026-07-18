using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EngineeringManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MergeTemporaryWorkersIntoEmployees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PersonnelMigrationMaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegacyTemporaryWorkerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MigratedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonnelMigrationMaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonnelMigrationMaps_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PersonnelMigrationMaps_EmployeeId",
                table: "PersonnelMigrationMaps",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonnelMigrationMaps_LegacyTemporaryWorkerId",
                table: "PersonnelMigrationMaps",
                column: "LegacyTemporaryWorkerId",
                unique: true);

            migrationBuilder.Sql(
                """
                DECLARE @MergeMap TABLE
                (
                    [LegacyTemporaryWorkerId] uniqueidentifier NOT NULL PRIMARY KEY,
                    [EmployeeId] uniqueidentifier NOT NULL,
                    [IsNewEmployee] bit NOT NULL
                );

                INSERT INTO @MergeMap ([LegacyTemporaryWorkerId], [EmployeeId], [IsNewEmployee])
                SELECT
                    [tw].[Id],
                    COALESCE([tw].[ConvertedEmployeeId], NEWID()),
                    CASE WHEN [tw].[ConvertedEmployeeId] IS NULL THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END
                FROM [TemporaryWorkers] AS [tw];

                IF EXISTS
                (
                    SELECT 1
                    FROM @MergeMap AS [map]
                    INNER JOIN [TemporaryWorkers] AS [tw] ON [tw].[Id] = [map].[LegacyTemporaryWorkerId]
                    LEFT JOIN [Employees] AS [target] ON [target].[Id] = [map].[EmployeeId]
                    INNER JOIN [Employees] AS [other]
                        ON [other].[IdentityNumber] = [tw].[IdentityNumber]
                        AND [other].[Id] <> [map].[EmployeeId]
                    WHERE [tw].[IdentityNumber] IS NOT NULL
                      AND ([map].[IsNewEmployee] = 1 OR [target].[IdentityNumber] IS NULL)
                )
                BEGIN
                    THROW 51001, 'Temporary worker identity conflicts with an existing employee.', 1;
                END;

                IF EXISTS
                (
                    SELECT [pending].[IdentityNumber]
                    FROM
                    (
                        SELECT [map].[EmployeeId], [tw].[IdentityNumber]
                        FROM @MergeMap AS [map]
                        INNER JOIN [TemporaryWorkers] AS [tw] ON [tw].[Id] = [map].[LegacyTemporaryWorkerId]
                        LEFT JOIN [Employees] AS [target] ON [target].[Id] = [map].[EmployeeId]
                        WHERE [tw].[IdentityNumber] IS NOT NULL
                          AND ([map].[IsNewEmployee] = 1 OR [target].[IdentityNumber] IS NULL)
                    ) AS [pending]
                    GROUP BY [pending].[IdentityNumber]
                    HAVING COUNT(DISTINCT [pending].[EmployeeId]) > 1
                )
                BEGIN
                    THROW 51002, 'Duplicate unconverted temporary worker identities were found.', 1;
                END;

                IF EXISTS
                (
                    SELECT 1
                    FROM @MergeMap AS [map]
                    INNER JOIN [TemporaryWorkers] AS [tw] ON [tw].[Id] = [map].[LegacyTemporaryWorkerId]
                    INNER JOIN [Employees] AS [employee]
                        ON [employee].[EmployeeNumber] = N'TMP-' + REPLACE(CONVERT(nvarchar(36), [tw].[Id]), '-', '')
                    WHERE [map].[IsNewEmployee] = 1
                )
                BEGIN
                    THROW 51003, 'Generated temporary employee number conflicts with an existing employee.', 1;
                END;

                IF EXISTS
                (
                    SELECT [projected].[PayrollBatchId], [projected].[RecipientKey]
                    FROM
                    (
                        SELECT
                            [payment].[PayrollBatchId],
                            CASE
                                WHEN [payment].[RecipientType] = 3
                                    THEN N'employee:' + REPLACE(CONVERT(nvarchar(36), [map].[EmployeeId]), '-', '')
                                WHEN [payment].[RecipientType] = 1 AND [payment].[EmployeeId] IS NOT NULL
                                    THEN N'employee:' + REPLACE(CONVERT(nvarchar(36), [payment].[EmployeeId]), '-', '')
                                ELSE [payment].[RecipientKey]
                            END AS [RecipientKey]
                        FROM [PayrollPayments] AS [payment]
                        LEFT JOIN @MergeMap AS [map]
                            ON [map].[LegacyTemporaryWorkerId] = [payment].[TemporaryWorkerId]
                    ) AS [projected]
                    WHERE [projected].[RecipientKey] IS NOT NULL
                    GROUP BY [projected].[PayrollBatchId], [projected].[RecipientKey]
                    HAVING COUNT(*) > 1
                )
                BEGIN
                    THROW 51004, 'Temporary worker merge would duplicate a payroll recipient in one batch.', 1;
                END;

                INSERT INTO [Employees]
                (
                    [Id], [EmployeeNumber], [Name], [EmployeeType], [Phone], [IdentityNumber],
                    [BankAccountNumber], [BankName], [HireDate], [LeaveDate], [PositionTitle],
                    [DefaultLegalEntityId], [DefaultMonthlySalary], [DefaultDailyRate],
                    [DefaultHourlyRate], [DefaultPieceworkRate], [Notes], [IsActive],
                    [CreatedAt], [UpdatedAt], [ConcurrencyStamp]
                )
                SELECT
                    [map].[EmployeeId],
                    N'TMP-' + REPLACE(CONVERT(nvarchar(36), [tw].[Id]), '-', ''),
                    [tw].[Name],
                    CAST(3 AS int),
                    [tw].[Phone],
                    [tw].[IdentityNumber],
                    [tw].[BankAccountNumber],
                    [tw].[BankName],
                    NULL,
                    NULL,
                    [tw].[Trade],
                    NULL,
                    NULL,
                    NULL,
                    NULL,
                    NULL,
                    [tw].[Notes],
                    [tw].[IsActive],
                    [tw].[CreatedAt],
                    [tw].[UpdatedAt],
                    [tw].[ConcurrencyStamp]
                FROM @MergeMap AS [map]
                INNER JOIN [TemporaryWorkers] AS [tw] ON [tw].[Id] = [map].[LegacyTemporaryWorkerId]
                WHERE [map].[IsNewEmployee] = 1;

                ;WITH [ConvertedSource] AS
                (
                    SELECT
                        [map].[EmployeeId],
                        [tw].[IdentityNumber],
                        [tw].[Phone],
                        [tw].[BankAccountNumber],
                        [tw].[BankName],
                        [tw].[Trade],
                        [tw].[Notes],
                        ROW_NUMBER() OVER
                        (
                            PARTITION BY [map].[EmployeeId]
                            ORDER BY [tw].[CreatedAt], [tw].[Id]
                        ) AS [SourceOrder]
                    FROM @MergeMap AS [map]
                    INNER JOIN [TemporaryWorkers] AS [tw] ON [tw].[Id] = [map].[LegacyTemporaryWorkerId]
                    WHERE [map].[IsNewEmployee] = 0
                )
                UPDATE [employee]
                SET
                    [IdentityNumber] = COALESCE([employee].[IdentityNumber], [source].[IdentityNumber]),
                    [Phone] = COALESCE([employee].[Phone], [source].[Phone]),
                    [BankAccountNumber] = COALESCE([employee].[BankAccountNumber], [source].[BankAccountNumber]),
                    [BankName] = COALESCE([employee].[BankName], [source].[BankName]),
                    [PositionTitle] = COALESCE([employee].[PositionTitle], [source].[Trade]),
                    [Notes] = COALESCE([employee].[Notes], [source].[Notes]),
                    [UpdatedAt] = CASE
                        WHEN ([employee].[IdentityNumber] IS NULL AND [source].[IdentityNumber] IS NOT NULL)
                          OR ([employee].[Phone] IS NULL AND [source].[Phone] IS NOT NULL)
                          OR ([employee].[BankAccountNumber] IS NULL AND [source].[BankAccountNumber] IS NOT NULL)
                          OR ([employee].[BankName] IS NULL AND [source].[BankName] IS NOT NULL)
                          OR ([employee].[PositionTitle] IS NULL AND [source].[Trade] IS NOT NULL)
                          OR ([employee].[Notes] IS NULL AND [source].[Notes] IS NOT NULL)
                        THEN SYSDATETIMEOFFSET()
                        ELSE [employee].[UpdatedAt]
                    END,
                    [ConcurrencyStamp] = CASE
                        WHEN ([employee].[IdentityNumber] IS NULL AND [source].[IdentityNumber] IS NOT NULL)
                          OR ([employee].[Phone] IS NULL AND [source].[Phone] IS NOT NULL)
                          OR ([employee].[BankAccountNumber] IS NULL AND [source].[BankAccountNumber] IS NOT NULL)
                          OR ([employee].[BankName] IS NULL AND [source].[BankName] IS NOT NULL)
                          OR ([employee].[PositionTitle] IS NULL AND [source].[Trade] IS NOT NULL)
                          OR ([employee].[Notes] IS NULL AND [source].[Notes] IS NOT NULL)
                        THEN NEWID()
                        ELSE [employee].[ConcurrencyStamp]
                    END
                FROM [Employees] AS [employee]
                INNER JOIN [ConvertedSource] AS [source]
                    ON [source].[EmployeeId] = [employee].[Id]
                    AND [source].[SourceOrder] = 1;

                INSERT INTO [PersonnelMigrationMaps]
                    ([Id], [LegacyTemporaryWorkerId], [EmployeeId], [MigratedAt])
                SELECT NEWID(), [map].[LegacyTemporaryWorkerId], [map].[EmployeeId], SYSDATETIMEOFFSET()
                FROM @MergeMap AS [map];

                INSERT INTO [EmployeeAffiliationHistories]
                (
                    [Id], [EmployeeId], [StartDate], [EndDate], [DepartmentId], [ProjectId],
                    [CrewBusinessPartnerId], [LegalEntityId], [PositionTitle], [IsPrimary], [Notes]
                )
                SELECT
                    NEWID(),
                    [map].[EmployeeId],
                    CONVERT(date, [tw].[CreatedAt]),
                    NULL,
                    NULL,
                    [tw].[DefaultProjectId],
                    NULL,
                    NULL,
                    [tw].[Trade],
                    CAST(1 AS bit),
                    N'由特殊临时人员主档迁移'
                FROM @MergeMap AS [map]
                INNER JOIN [TemporaryWorkers] AS [tw] ON [tw].[Id] = [map].[LegacyTemporaryWorkerId]
                WHERE [tw].[DefaultProjectId] IS NOT NULL
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM [EmployeeAffiliationHistories] AS [history]
                      WHERE [history].[EmployeeId] = [map].[EmployeeId]
                        AND [history].[ProjectId] = [tw].[DefaultProjectId]
                        AND [history].[StartDate] = CONVERT(date, [tw].[CreatedAt])
                  );

                UPDATE [payment]
                SET
                    [RecipientType] = 1,
                    [RecipientKey] = 'employee:' + REPLACE(CONVERT(nvarchar(36), [map].[EmployeeId]), '-', ''),
                    [EmployeeId] = [map].[EmployeeId],
                    [TemporaryWorkerId] = NULL,
                    [PayeeType] = 1
                FROM [PayrollPayments] AS [payment]
                INNER JOIN @MergeMap AS [map]
                    ON [map].[LegacyTemporaryWorkerId] = [payment].[TemporaryWorkerId]
                WHERE [payment].[RecipientType] = 3;

                IF EXISTS
                (
                    SELECT 1
                    FROM [TemporaryWorkers] AS [tw]
                    LEFT JOIN [PersonnelMigrationMaps] AS [map]
                        ON [map].[LegacyTemporaryWorkerId] = [tw].[Id]
                    WHERE [map].[Id] IS NULL
                )
                BEGIN
                    THROW 51005, 'Every temporary worker must have a personnel migration map.', 1;
                END;

                IF EXISTS
                (
                    SELECT 1
                    FROM [PayrollPayments]
                    WHERE [TemporaryWorkerId] IS NOT NULL OR [RecipientType] = 3
                )
                BEGIN
                    THROW 51006, 'Payroll payments still reference temporary workers after migration.', 1;
                END;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_PayrollPayments_TemporaryWorkers_TemporaryWorkerId",
                table: "PayrollPayments");

            migrationBuilder.DropIndex(
                name: "IX_PayrollPayments_TemporaryWorkerId",
                table: "PayrollPayments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollPayments_Recipient",
                table: "PayrollPayments");

            migrationBuilder.DropColumn(
                name: "TemporaryWorkerId",
                table: "PayrollPayments");

            migrationBuilder.DropTable(
                name: "TemporaryWorkers");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollPayments_Recipient",
                table: "PayrollPayments",
                sql: "([RecipientType] = 1 AND [EmployeeId] IS NOT NULL AND [ConstructionWorkerId] IS NULL AND [CrewBusinessPartnerId] IS NULL) OR ([RecipientType] = 2 AND [EmployeeId] IS NULL AND [ConstructionWorkerId] IS NOT NULL AND [CrewBusinessPartnerId] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "The temporary-worker merge is irreversible because the legacy ownership and payroll foreign keys are intentionally removed.");
        }
    }
}
