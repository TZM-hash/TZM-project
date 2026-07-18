using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;

namespace EngineeringManager.Infrastructure.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<OrganizationUnit> OrganizationUnits => Set<OrganizationUnit>();

    public DbSet<LegalEntity> LegalEntities => Set<LegalEntity>();

    public DbSet<CompanyCategory> CompanyCategories => Set<CompanyCategory>();

    public DbSet<CompanyCertificate> CompanyCertificates => Set<CompanyCertificate>();

    public DbSet<UserOrganizationMembership> UserOrganizationMemberships => Set<UserOrganizationMembership>();

    public DbSet<UserLegalEntityAccess> UserLegalEntityAccesses => Set<UserLegalEntityAccess>();

    public DbSet<UserPermissionOverride> UserPermissionOverrides => Set<UserPermissionOverride>();

    public DbSet<UserDataScope> UserDataScopes => Set<UserDataScope>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    public DbSet<SavedDataView> SavedDataViews => Set<SavedDataView>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectAssignment> ProjectAssignments => Set<ProjectAssignment>();

    public DbSet<ProjectLegalEntity> ProjectLegalEntities => Set<ProjectLegalEntity>();

    public DbSet<ProjectMilestone> ProjectMilestones => Set<ProjectMilestone>();
    public DbSet<ProjectConstructionRecord> ProjectConstructionRecords => Set<ProjectConstructionRecord>();
    public DbSet<ProjectTaxConfiguration> ProjectTaxConfigurations => Set<ProjectTaxConfiguration>();

    public DbSet<Contract> Contracts => Set<Contract>();

    public DbSet<ContractLegalEntityAllocation> ContractLegalEntityAllocations => Set<ContractLegalEntityAllocation>();

    public DbSet<ContractLineItem> ContractLineItems => Set<ContractLineItem>();

    public DbSet<ContractLineItemLegalEntityAllocation> ContractLineItemLegalEntityAllocations => Set<ContractLineItemLegalEntityAllocation>();

    public DbSet<BusinessPartner> BusinessPartners => Set<BusinessPartner>();

    public DbSet<BusinessPartnerRole> BusinessPartnerRoles => Set<BusinessPartnerRole>();

    public DbSet<PartnerContact> PartnerContacts => Set<PartnerContact>();

    public DbSet<ProjectPartner> ProjectPartners => Set<ProjectPartner>();

    public DbSet<StageResult> StageResults => Set<StageResult>();

    public DbSet<StageResultLine> StageResultLines => Set<StageResultLine>();

    public DbSet<Attachment> Attachments => Set<Attachment>();

    public DbSet<FinancialAccount> FinancialAccounts => Set<FinancialAccount>();
    public DbSet<ReceivableEntry> ReceivableEntries => Set<ReceivableEntry>();
    public DbSet<CollectionEntry> CollectionEntries => Set<CollectionEntry>();
    public DbSet<RefundOrReversalEntry> RefundOrReversalEntries => Set<RefundOrReversalEntry>();
    public DbSet<PayableEntry> PayableEntries => Set<PayableEntry>();
    public DbSet<PaymentEntry> PaymentEntries => Set<PaymentEntry>();
    public DbSet<PaymentReversalEntry> PaymentReversalEntries => Set<PaymentReversalEntry>();
    public DbSet<DeductionEntry> DeductionEntries => Set<DeductionEntry>();
    public DbSet<InvoiceEntry> InvoiceEntries => Set<InvoiceEntry>();
    public DbSet<InvoiceReceivableLink> InvoiceReceivableLinks => Set<InvoiceReceivableLink>();
    public DbSet<InvoiceLineItemLink> InvoiceLineItemLinks => Set<InvoiceLineItemLink>();
    public DbSet<AccountTransaction> AccountTransactions => Set<AccountTransaction>();
    public DbSet<AccountTransfer> AccountTransfers => Set<AccountTransfer>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<PersonnelMigrationMap> PersonnelMigrationMaps => Set<PersonnelMigrationMap>();
    public DbSet<BusinessYear> BusinessYears => Set<BusinessYear>();
    public DbSet<EmployeeWageEntry> EmployeeWageEntries => Set<EmployeeWageEntry>();
    public DbSet<EmployeeReceipt> EmployeeReceipts => Set<EmployeeReceipt>();
    public DbSet<EmployeeFinancialAdjustment> EmployeeFinancialAdjustments => Set<EmployeeFinancialAdjustment>();
    public DbSet<EmployeeCertificate> EmployeeCertificates => Set<EmployeeCertificate>();
    public DbSet<EmployeeAffiliationHistory> EmployeeAffiliationHistories => Set<EmployeeAffiliationHistory>();
    public DbSet<PayrollBatch> PayrollBatches => Set<PayrollBatch>();
    public DbSet<PayrollItem> PayrollItems => Set<PayrollItem>();
    public DbSet<PayrollCostAllocation> PayrollCostAllocations => Set<PayrollCostAllocation>();
    public DbSet<PayrollPayment> PayrollPayments => Set<PayrollPayment>();
    public DbSet<PayrollCrewAllocation> PayrollCrewAllocations => Set<PayrollCrewAllocation>();
    public DbSet<ConstructionWorker> ConstructionWorkers => Set<ConstructionWorker>();
    public DbSet<ConstructionCrewMembership> ConstructionCrewMemberships => Set<ConstructionCrewMembership>();
    public DbSet<ExpenseRecord> ExpenseRecords => Set<ExpenseRecord>();
    public DbSet<ExpensePayment> ExpensePayments => Set<ExpensePayment>();
    public DbSet<EmployeeAdvance> EmployeeAdvances => Set<EmployeeAdvance>();
    public DbSet<EmployeeOtherPayment> EmployeeOtherPayments => Set<EmployeeOtherPayment>();
    public DbSet<ExportTemplate> ExportTemplates => Set<ExportTemplate>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportError> ImportErrors => Set<ImportError>();
    public DbSet<BackupTask> BackupTasks => Set<BackupTask>();
    public DbSet<ReminderItem> ReminderItems => Set<ReminderItem>();
    public DbSet<OfflineDraftSync> OfflineDraftSyncs => Set<OfflineDraftSync>();
    public DbSet<OfflineAttachmentSync> OfflineAttachmentSyncs => Set<OfflineAttachmentSync>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<EquipmentLeaseAgreement> EquipmentLeaseAgreements => Set<EquipmentLeaseAgreement>();
    public DbSet<EquipmentProjectUsage> EquipmentProjectUsages => Set<EquipmentProjectUsage>();
    public DbSet<EquipmentWorkPeriod> EquipmentWorkPeriods => Set<EquipmentWorkPeriod>();
    public DbSet<EquipmentSettlement> EquipmentSettlements => Set<EquipmentSettlement>();
    public DbSet<EquipmentSettlementAdjustment> EquipmentSettlementAdjustments => Set<EquipmentSettlementAdjustment>();
    public DbSet<EquipmentAdvancePayment> EquipmentAdvancePayments => Set<EquipmentAdvancePayment>();
    public DbSet<EquipmentOwnershipHistory> EquipmentOwnershipHistories => Set<EquipmentOwnershipHistory>();
    public DbSet<EquipmentMaintenanceRecord> EquipmentMaintenanceRecords => Set<EquipmentMaintenanceRecord>();
    public DbSet<OfflineEquipmentUsageSync> OfflineEquipmentUsageSyncs => Set<OfflineEquipmentUsageSync>();
    public DbSet<OfflineEquipmentAttachmentSync> OfflineEquipmentAttachmentSyncs => Set<OfflineEquipmentAttachmentSync>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.DisplayName)
                .HasMaxLength(100);
        });

        builder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Key).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Value).HasMaxLength(500).IsRequired();
            entity.HasIndex(item => item.Key).IsUnique();
            entity.HasOne(item => item.UpdatedByUser)
                .WithMany()
                .HasForeignKey(item => item.UpdatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<SavedDataView>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.PageKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
            entity.Property(item => item.FilterJson).HasMaxLength(8000).IsRequired();
            entity.Property(item => item.ColumnJson).HasMaxLength(8000).IsRequired();
            entity.Property(item => item.SortKey).HasMaxLength(100);
            entity.HasIndex(item => new { item.UserId, item.PageKey, item.Name }).IsUnique();
            entity.HasIndex(item => new { item.UserId, item.PageKey, item.IsDefault });
            entity.HasOne(item => item.User)
                .WithMany(user => user.SavedDataViews)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<OrganizationUnit>(entity =>
        {
            entity.HasKey(unit => unit.Id);
            entity.Property(unit => unit.Code).HasMaxLength(50).IsRequired();
            entity.Property(unit => unit.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(unit => unit.Code).IsUnique();
            entity.HasOne(unit => unit.Parent)
                .WithMany(unit => unit.Children)
                .HasForeignKey(unit => unit.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<LegalEntity>(entity =>
        {
            entity.HasKey(legalEntity => legalEntity.Id);
            entity.Property(legalEntity => legalEntity.Code).HasMaxLength(50).IsRequired();
            entity.Property(legalEntity => legalEntity.Name).HasMaxLength(200).IsRequired();
            entity.Property(legalEntity => legalEntity.ShortName).HasMaxLength(100).IsRequired();
            entity.Property(legalEntity => legalEntity.UnifiedSocialCreditCode).HasMaxLength(50);
            entity.Property(legalEntity => legalEntity.LegalRepresentative).HasMaxLength(100);
            entity.Property(legalEntity => legalEntity.RegisteredAddress).HasMaxLength(300);
            entity.Property(legalEntity => legalEntity.BusinessAddress).HasMaxLength(300);
            entity.Property(legalEntity => legalEntity.Phone).HasMaxLength(50);
            entity.Property(legalEntity => legalEntity.InvoiceTitle).HasMaxLength(200);
            entity.Property(legalEntity => legalEntity.Notes).HasMaxLength(1000);
            entity.Property(legalEntity => legalEntity.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(legalEntity => legalEntity.Code).IsUnique();
            entity.HasIndex(legalEntity => legalEntity.UnifiedSocialCreditCode).IsUnique();
            entity.HasOne(legalEntity => legalEntity.CompanyCategory)
                .WithMany()
                .HasForeignKey(legalEntity => legalEntity.CompanyCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CompanyCategory>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => item.Code).IsUnique();
            entity.HasIndex(item => new { item.SortOrder, item.Name });
            var seededAt = new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);
            entity.HasData(
                new CompanyCategory { Id = CompanyCategoryDefaults.GeneralTaxpayerCompanyId, Code = "GENERAL_COMPANY", Name = "一般纳税人有限公司", SortOrder = 10, ConcurrencyStamp = Guid.Parse("20000000-0000-0000-0000-000000000001"), CreatedAt = seededAt, UpdatedAt = seededAt },
                new CompanyCategory { Id = CompanyCategoryDefaults.SmallScaleCompanyId, Code = "SMALL_COMPANY", Name = "小规模纳税人有限公司", SortOrder = 20, ConcurrencyStamp = Guid.Parse("20000000-0000-0000-0000-000000000002"), CreatedAt = seededAt, UpdatedAt = seededAt },
                new CompanyCategory { Id = CompanyCategoryDefaults.SmallScaleSoleProprietorId, Code = "SMALL_SOLE", Name = "小规模个体工商户", SortOrder = 30, ConcurrencyStamp = Guid.Parse("20000000-0000-0000-0000-000000000003"), CreatedAt = seededAt, UpdatedAt = seededAt },
                new CompanyCategory { Id = CompanyCategoryDefaults.OtherId, Code = "OTHER", Name = "其他主体", SortOrder = 90, ConcurrencyStamp = Guid.Parse("20000000-0000-0000-0000-000000000004"), CreatedAt = seededAt, UpdatedAt = seededAt });
        });

        builder.Entity<CompanyCertificate>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.CertificateType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.CertificateNumber).HasMaxLength(100);
            entity.Property(item => item.SpecialtyLevelScope).HasMaxLength(500);
            entity.Property(item => item.IssuingAuthority).HasMaxLength(200);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => new { item.LegalEntityId, item.CertificateType, item.CertificateNumber });
            entity.HasIndex(item => new { item.ExpiresOn, item.IsDeleted });
            entity.HasOne(item => item.LegalEntity).WithMany().HasForeignKey(item => item.LegalEntityId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Attachment).WithMany().HasForeignKey(item => item.AttachmentId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<UserOrganizationMembership>(entity =>
        {
            entity.HasKey(membership => membership.Id);
            entity.HasIndex(membership => new { membership.UserId, membership.OrganizationUnitId }).IsUnique();
            entity.HasOne(membership => membership.User)
                .WithMany(user => user.OrganizationMemberships)
                .HasForeignKey(membership => membership.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(membership => membership.OrganizationUnit)
                .WithMany()
                .HasForeignKey(membership => membership.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<UserLegalEntityAccess>(entity =>
        {
            entity.HasKey(access => access.Id);
            entity.HasIndex(access => new { access.UserId, access.LegalEntityId }).IsUnique();
            entity.HasOne(access => access.User)
                .WithMany(user => user.LegalEntityAccesses)
                .HasForeignKey(access => access.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(access => access.LegalEntity)
                .WithMany()
                .HasForeignKey(access => access.LegalEntityId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<UserPermissionOverride>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.PermissionKey).HasMaxLength(150).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(500);
            entity.HasIndex(item => new { item.UserId, item.PermissionKey }).IsUnique();
            entity.HasOne(item => item.User)
                .WithMany(user => user.PermissionOverrides)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserDataScope>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.UserId, item.ScopeType, item.OrganizationUnitId, item.LegalEntityId }).IsUnique();
            entity.HasOne(item => item.User)
                .WithMany(user => user.DataScopes)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Action).HasMaxLength(100).IsRequired();
            entity.Property(item => item.EntityType).HasMaxLength(150).IsRequired();
            entity.Property(item => item.EntityId).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(500);
            entity.Property(item => item.IpAddress).HasMaxLength(64);
            entity.Property(item => item.RequestId).HasMaxLength(100);
            entity.HasIndex(item => item.OccurredAt);
            entity.HasIndex(item => new { item.EntityType, item.EntityId });
        });

        ConfigureProjectModel(builder);
        ConfigureFinanceModel(builder);
        ConfigureEmployeeModel(builder);
        ConfigureEmployeeAnnualLedgerModel(builder);
        ConfigurePayrollModel(builder);
        ConfigureEmployeeLedgerModel(builder);
        ConfigureDataExchangeModel(builder);
        ConfigureOfflineModel(builder);
        ConfigureEquipmentModel(builder);
    }

    private static void ConfigureEquipmentModel(ModelBuilder builder)
    {
        builder.Entity<Equipment>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EquipmentNumber).HasMaxLength(60).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Model).HasMaxLength(100);
            entity.Property(item => item.Category).HasMaxLength(100);
            entity.Property(item => item.PurchaseAmount).HasPrecision(18, 2);
            entity.Property(item => item.InternalDailyRate).HasPrecision(18, 2);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => item.EquipmentNumber).IsUnique();
            entity.HasOne(item => item.OwnerLegalEntity).WithMany().HasForeignKey(item => item.OwnerLegalEntityId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.LessorBusinessPartner).WithMany().HasForeignKey(item => item.LessorBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EquipmentLeaseAgreement>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ContractNumber).HasMaxLength(100);
            entity.Property(item => item.UnitRate).HasPrecision(18, 2);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => new { item.EquipmentId, item.StartDate });
            entity.HasOne(item => item.Equipment).WithMany(item => item.LeaseAgreements).HasForeignKey(item => item.EquipmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.LessorBusinessPartner).WithMany().HasForeignKey(item => item.LessorBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EquipmentProjectUsage>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.UnitRate).HasPrecision(18, 2);
            entity.Property(item => item.SharedUsageReason).HasMaxLength(500);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => new { item.EquipmentId, item.EntryDate, item.ExitDate });
            entity.HasOne(item => item.Equipment).WithMany(item => item.ProjectUsages).HasForeignKey(item => item.EquipmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.LegalEntity).WithMany().HasForeignKey(item => item.LegalEntityId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.LeaseAgreement).WithMany().HasForeignKey(item => item.LeaseAgreementId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EquipmentWorkPeriod>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Notes).HasMaxLength(500);
            entity.HasIndex(item => new { item.UsageId, item.StartDate, item.EndDate });
            entity.HasOne(item => item.Usage).WithMany(item => item.Periods).HasForeignKey(item => item.UsageId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EquipmentSettlement>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.BaseAmount).HasPrecision(18, 2);
            entity.Property(item => item.TotalAmount).HasPrecision(18, 2);
            entity.Property(item => item.OffsetAmount).HasPrecision(18, 2);
            entity.Property(item => item.ModificationReason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.Property(item => item.PreviousSnapshotJson).HasMaxLength(8000);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => item.UsageId).IsUnique();
            entity.HasIndex(item => item.PayableEntryId).IsUnique();
            entity.HasOne(item => item.Usage).WithOne(item => item.Settlement).HasForeignKey<EquipmentSettlement>(item => item.UsageId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.PayableEntry).WithMany().HasForeignKey(item => item.PayableEntryId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EquipmentSettlementAdjustment>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.AdjustmentType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Reason).HasMaxLength(500);
            entity.HasOne(item => item.Settlement).WithMany(item => item.Adjustments).HasForeignKey(item => item.SettlementId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EquipmentAdvancePayment>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Notes).HasMaxLength(500);
            entity.HasOne(item => item.Usage).WithMany(item => item.AdvancePayments).HasForeignKey(item => item.UsageId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.PaymentEntry).WithMany().HasForeignKey(item => item.PaymentEntryId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EquipmentOwnershipHistory>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ExternalRecipientName).HasMaxLength(200);
            entity.Property(item => item.TransferAmount).HasPrecision(18, 2);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.HasOne(item => item.Equipment).WithMany(item => item.OwnershipHistory).HasForeignKey(item => item.EquipmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.FromLegalEntity).WithMany().HasForeignKey(item => item.FromLegalEntityId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ToLegalEntity).WithMany().HasForeignKey(item => item.ToLegalEntityId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EquipmentMaintenanceRecord>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.MaintenanceType).HasMaxLength(100);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Provider).HasMaxLength(200);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.HasIndex(item => new { item.NextDueDate, item.EquipmentId });
            entity.HasOne(item => item.Equipment).WithMany(item => item.MaintenanceRecords).HasForeignKey(item => item.EquipmentId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<OfflineEquipmentUsageSync>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.UserId).HasMaxLength(450).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(30).IsRequired();
            entity.Property(item => item.LastError).HasMaxLength(2000);
            entity.HasIndex(item => new { item.UserId, item.ClientDraftId }).IsUnique();
            entity.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.EquipmentProjectUsage).WithMany().HasForeignKey(item => item.EquipmentProjectUsageId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<OfflineEquipmentAttachmentSync>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.OfflineEquipmentUsageSyncId, item.ClientAttachmentId }).IsUnique();
            entity.HasOne(item => item.UsageSync).WithMany(item => item.Attachments).HasForeignKey(item => item.OfflineEquipmentUsageSyncId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Attachment).WithMany().HasForeignKey(item => item.AttachmentId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureOfflineModel(ModelBuilder builder)
    {
        builder.Entity<OfflineDraftSync>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.UserId).HasMaxLength(450).IsRequired();
            entity.Property(item => item.LastError).HasMaxLength(2000);
            entity.HasIndex(item => new { item.UserId, item.ClientDraftId }).IsUnique();
            entity.HasIndex(item => new { item.Status, item.UpdatedAt });
            entity.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.StageResult).WithMany().HasForeignKey(item => item.StageResultId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<OfflineAttachmentSync>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.OfflineDraftSyncId, item.ClientAttachmentId }).IsUnique();
            entity.HasOne(item => item.DraftSync).WithMany(sync => sync.Attachments).HasForeignKey(item => item.OfflineDraftSyncId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Attachment).WithMany().HasForeignKey(item => item.AttachmentId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureDataExchangeModel(ModelBuilder builder)
    {
        builder.Entity<ExportTemplate>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OwnerUserId).HasMaxLength(450).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(150).IsRequired();
            entity.Property(item => item.SelectedFieldsJson).HasMaxLength(4000).IsRequired();
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => new { item.OwnerUserId, item.Dataset, item.Name }).IsUnique();
            entity.HasIndex(item => new { item.Dataset, item.Scope });
        });
        builder.Entity<ImportBatch>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.CreatedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(item => item.OriginalFileName).HasMaxLength(255).IsRequired();
            entity.Property(item => item.MappingJson).HasMaxLength(4000).IsRequired();
            entity.Property(item => item.OriginalContent).IsRequired();
            entity.HasIndex(item => item.CreatedAt);
        });
        builder.Entity<ImportError>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ColumnName).HasMaxLength(150).IsRequired();
            entity.Property(item => item.Message).HasMaxLength(500).IsRequired();
            entity.Property(item => item.RawValue).HasMaxLength(1000);
            entity.HasOne(item => item.Batch).WithMany(batch => batch.Errors).HasForeignKey(item => item.ImportBatchId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<BackupTask>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.RequestedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(item => item.DatabaseBackupPath).HasMaxLength(1000);
            entity.Property(item => item.AttachmentArchivePath).HasMaxLength(1000);
            entity.Property(item => item.ErrorMessage).HasMaxLength(2000);
            entity.HasIndex(item => item.CreatedAt);
            entity.HasIndex(item => item.Status);
        });
        builder.Entity<ReminderItem>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.DeduplicationKey).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Title).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Message).HasMaxLength(1000).IsRequired();
            entity.Property(item => item.SourceType).HasMaxLength(100);
            entity.Property(item => item.SourceId).HasMaxLength(100);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.HasIndex(item => item.DeduplicationKey).IsUnique();
            entity.HasIndex(item => new { item.Status, item.Severity });
        });
    }

    private static void ConfigureEmployeeLedgerModel(ModelBuilder builder)
    {
        builder.Entity<ExpenseRecord>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Category).HasMaxLength(100).IsRequired();
            entity.Property(item => item.OriginalAmount).HasPrecision(18, 2);
            entity.Property(item => item.AdjustmentAmount).HasPrecision(18, 2);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.ReceiptNumber).HasMaxLength(100);
            entity.Property(item => item.Description).HasMaxLength(1000);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Department).WithMany().HasForeignKey(item => item.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.LegalEntity).WithMany().HasForeignKey(item => item.LegalEntityId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Attachment).WithMany().HasForeignKey(item => item.AttachmentId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ExpensePayment>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Notes).HasMaxLength(500);
            entity.HasOne(item => item.Expense).WithMany(expense => expense.Payments).HasForeignKey(item => item.ExpenseRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Account).WithMany().HasForeignKey(item => item.AccountId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<EmployeeAdvance>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Description).HasMaxLength(500);
            entity.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.LegalEntity).WithMany().HasForeignKey(item => item.LegalEntityId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Account).WithMany().HasForeignKey(item => item.AccountId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<EmployeeOtherPayment>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Description).HasMaxLength(500);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.LegalEntity).WithMany().HasForeignKey(item => item.LegalEntityId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Account).WithMany().HasForeignKey(item => item.AccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.RelatedPayable).WithMany().HasForeignKey(item => item.RelatedPayableId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureEmployeeAnnualLedgerModel(ModelBuilder builder)
    {
        builder.Entity<BusinessYear>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => item.Name).IsUnique();
            entity.HasIndex(item => new { item.StartDate, item.EndDate });
        });
        builder.Entity<EmployeeWageEntry>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Quantity).HasPrecision(18, 4);
            entity.Property(item => item.Unit).HasMaxLength(30);
            entity.Property(item => item.UnitPrice).HasPrecision(18, 4);
            entity.Property(item => item.AutomaticAmount).HasPrecision(18, 2);
            entity.Property(item => item.AdjustmentAmount).HasPrecision(18, 2);
            entity.Property(item => item.FinalAmount).HasPrecision(18, 2);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => new { item.EmployeeId, item.BusinessYearId, item.StartDate });
            entity.HasIndex(item => item.SourcePayrollItemId);
            entity.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.BusinessYear).WithMany().HasForeignKey(item => item.BusinessYearId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.LegalEntity).WithMany().HasForeignKey(item => item.LegalEntityId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.LaborBusinessPartner).WithMany().HasForeignKey(item => item.LaborBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.SourcePayrollItem).WithMany().HasForeignKey(item => item.SourcePayrollItemId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<EmployeeFinancialAdjustment>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Notes).HasMaxLength(1000).IsRequired();
            entity.HasIndex(item => new { item.EmployeeId, item.BusinessYearId, item.AdjustmentDate });
            entity.HasIndex(item => item.ReversalOfId);
            entity.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.BusinessYear).WithMany().HasForeignKey(item => item.BusinessYearId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ReversalOf).WithMany().HasForeignKey(item => item.ReversalOfId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<EmployeeReceipt>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.ActualRecipientName).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => new { item.EmployeeId, item.BusinessYearId, item.ReceiptDate });
            entity.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.BusinessYear).WithMany().HasForeignKey(item => item.BusinessYearId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.PaymentLegalEntity).WithMany().HasForeignKey(item => item.PaymentLegalEntityId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Account).WithMany().HasForeignKey(item => item.AccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.LaborBusinessPartner).WithMany().HasForeignKey(item => item.LaborBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurePayrollModel(ModelBuilder builder)
    {
        builder.Entity<PayrollBatch>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.BatchNumber).HasMaxLength(60).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.StageOrMilestoneName).HasMaxLength(200);
            entity.Property(item => item.ActualAmount).HasPrecision(18, 2);
            entity.Property(item => item.PaymentMethod).HasDefaultValue(PaymentMethod.BankTransfer).HasSentinel((PaymentMethod)0);
            entity.Property(item => item.VoucherNumber).HasMaxLength(100);
            entity.Property(item => item.ReviewedByUserId).HasMaxLength(450);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => item.BatchNumber).IsUnique();
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.LegalEntity).WithMany().HasForeignKey(item => item.LegalEntityId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Account).WithMany().HasForeignKey(item => item.AccountId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<PayrollItem>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Quantity).HasPrecision(18, 4);
            entity.Property(item => item.UnitPrice).HasPrecision(18, 4);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Description).HasMaxLength(500);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasOne(item => item.Batch).WithMany(batch => batch.Items).HasForeignKey(item => item.PayrollBatchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<PayrollCostAllocation>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.HasIndex(item => new { item.PayrollItemId, item.ProjectId, item.LegalEntityId }).IsUnique();
            entity.HasOne(item => item.PayrollItem).WithMany(item => item.CostAllocations).HasForeignKey(item => item.PayrollItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.LegalEntity).WithMany().HasForeignKey(item => item.LegalEntityId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<PayrollPayment>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.RecipientType).HasDefaultValue(PayrollRecipientType.Employee).HasSentinel((PayrollRecipientType)0);
            entity.Property(item => item.PayeeName).HasMaxLength(100).IsRequired();
            entity.Property(item => item.RecipientKey).HasMaxLength(100);
            entity.Property(item => item.RecipientNameSnapshot).HasMaxLength(100);
            entity.Property(item => item.IdentityNumberSnapshot).HasMaxLength(50);
            entity.Property(item => item.PhoneSnapshot).HasMaxLength(50);
            entity.Property(item => item.BankAccountSnapshot).HasMaxLength(100);
            entity.Property(item => item.TradeSnapshot).HasMaxLength(100);
            entity.Property(item => item.CrewNameSnapshot).HasMaxLength(200);
            entity.Property(item => item.Notes).HasMaxLength(500);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => new { item.PayrollBatchId, item.RecipientKey }).IsUnique().HasFilter("[RecipientKey] IS NOT NULL");
            entity.HasOne(item => item.Batch).WithMany(batch => batch.Payments).HasForeignKey(item => item.PayrollBatchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ConstructionWorker).WithMany().HasForeignKey(item => item.ConstructionWorkerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CrewBusinessPartner).WithMany().HasForeignKey(item => item.CrewBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Account).WithMany().HasForeignKey(item => item.AccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.PayeeBusinessPartner).WithMany().HasForeignKey(item => item.PayeeBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_PayrollPayments_Recipient",
                "([RecipientType] = 1 AND [EmployeeId] IS NOT NULL AND [ConstructionWorkerId] IS NULL AND [CrewBusinessPartnerId] IS NULL) OR ([RecipientType] = 2 AND [EmployeeId] IS NULL AND [ConstructionWorkerId] IS NOT NULL AND [CrewBusinessPartnerId] IS NOT NULL)"));
        });
        builder.Entity<ConstructionWorker>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
            entity.Property(item => item.IdentityNumber).HasMaxLength(50);
            entity.Property(item => item.Phone).HasMaxLength(50);
            entity.Property(item => item.BankAccountNumber).HasMaxLength(100);
            entity.Property(item => item.BankName).HasMaxLength(150);
            entity.Property(item => item.Trade).HasMaxLength(100);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => item.IdentityNumber).HasFilter("[IdentityNumber] IS NOT NULL");
        });
        builder.Entity<ConstructionCrewMembership>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => new { item.ConstructionWorkerId, item.CrewBusinessPartnerId, item.StartDate }).IsUnique();
            entity.HasOne(item => item.Worker).WithMany(item => item.Memberships).HasForeignKey(item => item.ConstructionWorkerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CrewBusinessPartner).WithMany().HasForeignKey(item => item.CrewBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<PayrollCrewAllocation>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => new { item.PayrollBatchId, item.CrewBusinessPartnerId }).IsUnique();
            entity.HasOne(item => item.Batch).WithMany(item => item.CrewAllocations).HasForeignKey(item => item.PayrollBatchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CrewBusinessPartner).WithMany().HasForeignKey(item => item.CrewBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Contract).WithMany().HasForeignKey(item => item.ContractId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.PayableEntry).WithMany().HasForeignKey(item => item.PayableEntryId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureEmployeeModel(ModelBuilder builder)
    {
        builder.Entity<Employee>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EmployeeNumber).HasMaxLength(60).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Phone).HasMaxLength(50);
            entity.Property(item => item.IdentityNumber).HasMaxLength(50);
            entity.Property(item => item.BankAccountNumber).HasMaxLength(100);
            entity.Property(item => item.BankName).HasMaxLength(150);
            entity.Property(item => item.PositionTitle).HasMaxLength(100);
            entity.Property(item => item.DefaultMonthlySalary).HasPrecision(18, 2);
            entity.Property(item => item.DefaultDailyRate).HasPrecision(18, 2);
            entity.Property(item => item.DefaultHourlyRate).HasPrecision(18, 2);
            entity.Property(item => item.DefaultPieceworkRate).HasPrecision(18, 4);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => item.EmployeeNumber).IsUnique();
            entity.HasIndex(item => item.IdentityNumber).IsUnique().HasFilter("[IdentityNumber] IS NOT NULL");
            entity.HasOne(item => item.DefaultLegalEntity).WithMany().HasForeignKey(item => item.DefaultLegalEntityId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<PersonnelMigrationMap>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.LegacyTemporaryWorkerId).IsUnique();
            entity.HasIndex(item => item.EmployeeId);
            entity.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<EmployeeAffiliationHistory>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.PositionTitle).HasMaxLength(100);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.HasIndex(item => new { item.EmployeeId, item.StartDate });
            entity.HasOne(item => item.Employee).WithMany(employee => employee.AffiliationHistory).HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Department).WithMany().HasForeignKey(item => item.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CrewBusinessPartner).WithMany().HasForeignKey(item => item.CrewBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.LegalEntity).WithMany().HasForeignKey(item => item.LegalEntityId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<EmployeeCertificate>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.CertificateType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.CertificateNumber).HasMaxLength(100);
            entity.Property(item => item.SpecialtyLevelScope).HasMaxLength(500);
            entity.Property(item => item.IssuingAuthority).HasMaxLength(200);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => new { item.EmployeeId, item.CertificateType, item.CertificateNumber });
            entity.HasIndex(item => new { item.ExpiresOn, item.IsDeleted });
            entity.HasOne(item => item.Employee).WithMany(employee => employee.Certificates).HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Attachment).WithMany().HasForeignKey(item => item.AttachmentId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureFinanceModel(ModelBuilder builder)
    {
        ConfigureFinancialAccount(builder);
        ConfigureReceivables(builder);
        ConfigurePayables(builder);
        ConfigureInvoices(builder);
        ConfigureAccountTransactions(builder);
    }

    private static void ConfigureFinancialAccount(ModelBuilder builder)
    {
        builder.Entity<FinancialAccount>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.AccountName).HasMaxLength(150).IsRequired();
            entity.Property(item => item.AccountNumber).HasMaxLength(100);
            entity.Property(item => item.BankName).HasMaxLength(150);
            entity.Property(item => item.OpeningBalance).HasPrecision(18, 2);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => new { item.LegalEntityId, item.AccountName }).IsUnique();
            entity.HasIndex(item => new { item.LegalEntityId, item.IsDefaultCollection })
                .IsUnique()
                .HasFilter("[IsDefaultCollection] = 1");
            entity.HasIndex(item => new { item.LegalEntityId, item.IsDefaultPayment })
                .IsUnique()
                .HasFilter("[IsDefaultPayment] = 1");
            entity.HasIndex(item => new { item.LegalEntityId, item.IsDefaultInvoice })
                .IsUnique()
                .HasFilter("[IsDefaultInvoice] = 1");
            entity.HasOne(item => item.LegalEntity).WithMany().HasForeignKey(item => item.LegalEntityId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureReceivables(ModelBuilder builder)
    {
        builder.Entity<ReceivableEntry>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Description).HasMaxLength(1000);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            ConfigureProjectContractLegalPartner(entity);
        });
        builder.Entity<CollectionEntry>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            ConfigureProjectContractLegalPartner(entity);
            entity.HasOne(item => item.Receivable).WithMany().HasForeignKey(item => item.ReceivableEntryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Account).WithMany().HasForeignKey(item => item.AccountId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<RefundOrReversalEntry>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.HasOne(item => item.Collection).WithMany().HasForeignKey(item => item.CollectionEntryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Receivable).WithMany().HasForeignKey(item => item.ReceivableEntryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Account).WithMany().HasForeignKey(item => item.AccountId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurePayables(ModelBuilder builder)
    {
        builder.Entity<PayableEntry>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Description).HasMaxLength(1000);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            ConfigureProjectContractLegalPartner(entity);
        });
        builder.Entity<PaymentEntry>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            ConfigureProjectContractLegalPartner(entity);
            entity.HasOne(item => item.Payable).WithMany().HasForeignKey(item => item.PayableEntryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Account).WithMany().HasForeignKey(item => item.AccountId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<PaymentReversalEntry>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.HasOne(item => item.Payment).WithMany().HasForeignKey(item => item.PaymentEntryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Account).WithMany().HasForeignKey(item => item.AccountId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<DeductionEntry>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.HasOne(item => item.Payable).WithMany().HasForeignKey(item => item.PayableEntryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.LegalEntity).WithMany().HasForeignKey(item => item.LegalEntityId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.BusinessPartner).WithMany().HasForeignKey(item => item.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureInvoices(ModelBuilder builder)
    {
        builder.Entity<InvoiceEntry>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.InvoiceNumber).HasMaxLength(100).IsRequired();
            entity.Property(item => item.InvoiceType).HasMaxLength(100);
            entity.Property(item => item.TaxRate).HasPrecision(9, 4);
            entity.Property(item => item.NetAmount).HasPrecision(18, 2);
            entity.Property(item => item.TaxAmount).HasPrecision(18, 2);
            entity.Property(item => item.GrossAmount).HasPrecision(18, 2);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => new { item.LegalEntityId, item.Direction, item.InvoiceNumber }).IsUnique();
            entity.HasOne(item => item.ProjectTaxConfiguration).WithMany().HasForeignKey(item => item.ProjectTaxConfigurationId).OnDelete(DeleteBehavior.Restrict);
            ConfigureProjectContractLegalPartner(entity);
        });
        builder.Entity<InvoiceReceivableLink>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.AllocatedAmount).HasPrecision(18, 2);
            entity.HasIndex(item => new { item.InvoiceEntryId, item.ReceivableEntryId }).IsUnique();
            entity.HasOne(item => item.Invoice).WithMany(invoice => invoice.ReceivableLinks).HasForeignKey(item => item.InvoiceEntryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Receivable).WithMany().HasForeignKey(item => item.ReceivableEntryId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<InvoiceLineItemLink>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.AllocatedAmount).HasPrecision(18, 2);
            entity.HasIndex(item => new { item.InvoiceEntryId, item.ContractLineItemId }).IsUnique();
            entity.HasOne(item => item.Invoice).WithMany(invoice => invoice.LineItemLinks).HasForeignKey(item => item.InvoiceEntryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ContractLineItem).WithMany().HasForeignKey(item => item.ContractLineItemId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAccountTransactions(ModelBuilder builder)
    {
        builder.Entity<AccountTransaction>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Description).HasMaxLength(500);
            entity.HasIndex(item => new { item.SourceType, item.SourceId, item.Direction });
            entity.HasOne(item => item.Account).WithMany().HasForeignKey(item => item.AccountId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<AccountTransfer>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Description).HasMaxLength(500);
            entity.HasOne(item => item.FromAccount).WithMany().HasForeignKey(item => item.FromAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ToAccount).WithMany().HasForeignKey(item => item.ToAccountId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProjectContractLegalPartner<TEntity>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> entity)
        where TEntity : class
    {
        entity.HasOne("Project").WithMany().HasForeignKey("ProjectId").OnDelete(DeleteBehavior.Restrict);
        entity.HasOne("Contract").WithMany().HasForeignKey("ContractId").OnDelete(DeleteBehavior.Restrict);
        entity.HasOne("LegalEntity").WithMany().HasForeignKey("LegalEntityId").OnDelete(DeleteBehavior.Restrict);
        entity.HasOne("BusinessPartner").WithMany().HasForeignKey("BusinessPartnerId").OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureProjectModel(ModelBuilder builder)
    {
        builder.Entity<Project>(entity =>
        {
            entity.HasKey(project => project.Id);
            entity.Property(project => project.ProjectNumber).HasMaxLength(60).IsRequired();
            entity.Property(project => project.Name).HasMaxLength(200).IsRequired();
            entity.Property(project => project.ParentProjectName).HasMaxLength(200);
            entity.Property(project => project.GeneralContractorName).HasMaxLength(200);
            entity.Property(project => project.GeneralContractorContact).HasMaxLength(100);
            entity.Property(project => project.GeneralContractorPhone).HasMaxLength(50);
            entity.Property(project => project.ActualStartDate).HasColumnType("date");
            entity.Property(project => project.ActualCompletionDate).HasColumnType("date");
            entity.Property(project => project.Notes).HasMaxLength(1000);
            entity.Property(project => project.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(project => project.ProjectNumber).IsUnique();
            entity.HasOne(project => project.ResponsibleUser).WithMany().HasForeignKey(project => project.ResponsibleUserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(project => project.Department).WithMany().HasForeignKey(project => project.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(project => project.Branch).WithMany().HasForeignKey(project => project.BranchId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectAssignment>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.HasIndex(item => new { item.ProjectId, item.UserId, item.AssignmentType }).IsUnique();
            entity.HasOne(item => item.Project).WithMany(project => project.Assignments).HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectConstructionRecord>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => new { item.ProjectId, item.RecordType, item.EntryDate });
            entity.HasIndex(item => item.EquipmentId);
            entity.HasIndex(item => item.CrewBusinessPartnerId);
            entity.HasOne(item => item.Project).WithMany(project => project.ConstructionRecords).HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Equipment).WithMany().HasForeignKey(item => item.EquipmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CrewBusinessPartner).WithMany().HasForeignKey(item => item.CrewBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.TransferFromProject).WithMany().HasForeignKey(item => item.TransferFromProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.TransferToProject).WithMany().HasForeignKey(item => item.TransferToProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.PreviousRecord).WithMany().HasForeignKey(item => item.PreviousRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.NextRecord).WithMany().HasForeignKey(item => item.NextRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint("CK_ProjectConstructionRecords_Subject", "([RecordType] = 1 AND [EquipmentId] IS NOT NULL AND [CrewBusinessPartnerId] IS NULL) OR ([RecordType] = 2 AND [EquipmentId] IS NULL AND [CrewBusinessPartnerId] IS NOT NULL)"));
        });

        builder.Entity<ProjectTaxConfiguration>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.TaxRate).HasPrecision(9, 4);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => new { item.ProjectId, item.TaxRate, item.InvoiceType }).IsUnique();
            entity.HasOne(item => item.Project).WithMany(project => project.TaxConfigurations).HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProjectLegalEntity>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.ProjectId, item.LegalEntityId }).IsUnique();
            entity.HasOne(item => item.Project).WithMany(project => project.LegalEntities).HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.LegalEntity).WithMany().HasForeignKey(item => item.LegalEntityId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectMilestone>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(150).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.HasOne(item => item.Project).WithMany(project => project.Milestones).HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Contract>(entity =>
        {
            entity.HasKey(contract => contract.Id);
            entity.Property(contract => contract.ContractNumber).HasMaxLength(80).IsRequired();
            entity.Property(contract => contract.Name).HasMaxLength(200).IsRequired();
            entity.Property(contract => contract.CounterpartyName).HasMaxLength(200);
            entity.Property(contract => contract.TotalAmount).HasPrecision(18, 2);
            entity.Property(contract => contract.Notes).HasMaxLength(1000);
            entity.Property(contract => contract.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(contract => new { contract.ProjectId, contract.ContractNumber }).IsUnique();
            entity.HasOne(contract => contract.Project).WithMany(project => project.Contracts).HasForeignKey(contract => contract.ProjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(contract => contract.BusinessPartner).WithMany().HasForeignKey(contract => contract.BusinessPartnerId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ContractLegalEntityAllocation>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Percentage).HasPrecision(9, 4);
            entity.HasIndex(item => new { item.ContractId, item.LegalEntityId }).IsUnique();
            entity.HasOne(item => item.Contract).WithMany(contract => contract.LegalEntityAllocations).HasForeignKey(item => item.ContractId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.LegalEntity).WithMany().HasForeignKey(item => item.LegalEntityId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ContractLineItem>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(250).IsRequired();
            entity.Property(item => item.Unit).HasMaxLength(30).IsRequired();
            entity.Property(item => item.EstimatedQuantity).HasPrecision(18, 4);
            entity.Property(item => item.EstimatedUnitPrice).HasPrecision(18, 4);
            entity.Property(item => item.SettledQuantity).HasPrecision(18, 4);
            entity.Property(item => item.SettledUnitPrice).HasPrecision(18, 4);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => new { item.ContractId, item.Code }).IsUnique();
            entity.HasOne(item => item.Contract).WithMany(contract => contract.LineItems).HasForeignKey(item => item.ContractId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ContractLineItemLegalEntityAllocation>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Percentage).HasPrecision(9, 4);
            entity.HasIndex(item => new { item.ContractLineItemId, item.LegalEntityId }).IsUnique();
            entity.HasOne(item => item.ContractLineItem).WithMany(line => line.LegalEntityAllocations).HasForeignKey(item => item.ContractLineItemId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.LegalEntity).WithMany().HasForeignKey(item => item.LegalEntityId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<BusinessPartner>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.PartnerNumber).HasMaxLength(60).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ShortName).HasMaxLength(100).IsRequired();
            entity.Property(item => item.UnifiedSocialCreditCode).HasMaxLength(50);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => item.PartnerNumber).IsUnique();
            entity.HasIndex(item => item.UnifiedSocialCreditCode).IsUnique().HasFilter("[UnifiedSocialCreditCode] IS NOT NULL");
        });

        builder.Entity<BusinessPartnerRole>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.TradeCategory).HasMaxLength(100);
            entity.Property(item => item.PricingRule).HasMaxLength(500);
            entity.Property(item => item.SettlementTerms).HasMaxLength(500);
            entity.HasIndex(item => new { item.BusinessPartnerId, item.RoleType }).IsUnique();
            entity.HasOne(item => item.Partner).WithMany(partner => partner.Roles).HasForeignKey(item => item.BusinessPartnerId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PartnerContact>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Phone).HasMaxLength(50);
            entity.Property(item => item.Email).HasMaxLength(150);
            entity.Property(item => item.Address).HasMaxLength(300);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.HasOne(item => item.Partner).WithMany(partner => partner.Contacts).HasForeignKey(item => item.BusinessPartnerId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProjectPartner>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.HasIndex(item => new { item.ProjectId, item.BusinessPartnerId, item.RoleType }).IsUnique();
            entity.HasOne(item => item.Project).WithMany(project => project.Partners).HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Partner).WithMany(partner => partner.ProjectLinks).HasForeignKey(item => item.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Contract).WithMany().HasForeignKey(item => item.ContractId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<StageResult>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Title).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(3000);
            entity.Property(item => item.ConcurrencyStamp).IsConcurrencyToken();
            entity.HasIndex(item => new { item.ProjectId, item.ResultDate });
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Contract).WithMany().HasForeignKey(item => item.ContractId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(item => item.SubmittedByUser).WithMany().HasForeignKey(item => item.SubmittedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<StageResultLine>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.PeriodQuantity).HasPrecision(18, 4);
            entity.Property(item => item.CumulativeQuantity).HasPrecision(18, 4);
            entity.Property(item => item.RemainingQuantity).HasPrecision(18, 4);
            entity.Property(item => item.CompletionPercentage).HasPrecision(9, 4);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.HasIndex(item => new { item.StageResultId, item.ContractLineItemId }).IsUnique();
            entity.HasOne(item => item.StageResult).WithMany(result => result.Lines).HasForeignKey(item => item.StageResultId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.ContractLineItem).WithMany().HasForeignKey(item => item.ContractLineItemId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Attachment>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.StoredName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.OriginalFileName).HasMaxLength(255).IsRequired();
            entity.Property(item => item.ContentType).HasMaxLength(150).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(500);
            entity.HasIndex(item => item.StoredName).IsUnique();
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Contract).WithMany().HasForeignKey(item => item.ContractId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.StageResult).WithMany(result => result.Attachments).HasForeignKey(item => item.StageResultId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.UploadedByUser).WithMany().HasForeignKey(item => item.UploadedByUserId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
