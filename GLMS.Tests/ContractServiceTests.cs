using GLMS.Tests.Helpers;
using GLMS.Web.Data;
using GLMS.Web.Models;
using GLMS.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace GLMS.Tests;

public class ContractServiceTests
{
    private static (GlmsDbContext Db, ContractService Service, InMemoryAgreementStorage Storage) Create()
    {
        var db = TestDb.Create();
        var storage = new InMemoryAgreementStorage();
        return (db, new ContractService(db, storage, FixedTimeProvider.Default()), storage);
    }

    // ---------- create / update ----------

    [Fact]
    public async Task CreateAsync_ValidContract_IsSavedWithDatesNormalisedToMidnight()
    {
        var (db, service, _) = Create();
        await using var _db = db;
        var client = await TestDb.AddClientAsync(db);

        var created = await service.CreateAsync(new Contract
        {
            ClientId = client.Id,
            StartDate = new DateTime(2026, 11, 1, 14, 30, 0),
            EndDate = new DateTime(2027, 11, 1, 8, 0, 0),
            Status = ContractStatus.Draft
        });

        var saved = await db.Contracts.SingleAsync(c => c.Id == created.Id);
        Assert.Equal(new DateTime(2026, 11, 1), saved.StartDate);
        Assert.Equal(new DateTime(2027, 11, 1), saved.EndDate);
    }

    [Fact]
    public async Task CreateAsync_UnknownClient_ThrowsBusinessRuleException()
    {
        var (db, service, _) = Create();
        await using var _db = db;

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new Contract { ClientId = 999, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2027, 1, 1) }));
    }

    [Fact]
    public async Task CreateAsync_EndDateNotAfterStartDate_ThrowsAndSavesNothing()
    {
        var (db, service, _) = Create();
        await using var _db = db;
        var client = await TestDb.AddClientAsync(db);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new Contract { ClientId = client.Id, StartDate = new DateTime(2026, 5, 1), EndDate = new DateTime(2026, 5, 1) }));

        Assert.Empty(db.Contracts);
    }

    [Fact]
    public async Task CreateAsync_ActiveContractThatAlreadyEnded_IsRecordedAsExpired()
    {
        var (db, service, _) = Create();
        await using var _db = db;
        var client = await TestDb.AddClientAsync(db);

        var created = await service.CreateAsync(new Contract
        {
            ClientId = client.Id,
            Status = ContractStatus.Active,
            StartDate = new DateTime(2025, 1, 1),
            EndDate = new DateTime(2025, 12, 31)
        });

        Assert.Equal(ContractStatus.Expired, created.Status);
    }

    [Fact]
    public async Task UpdateAsync_ChangesDatesAndServiceLevelButNotStatus()
    {
        var (db, service, _) = Create();
        await using var _db = db;
        var client = await TestDb.AddClientAsync(db);
        var contract = await TestDb.AddContractAsync(db, client, ContractStatus.Active);

        await service.UpdateAsync(new Contract
        {
            Id = contract.Id,
            ClientId = client.Id,
            StartDate = new DateTime(2026, 2, 1),
            EndDate = new DateTime(2028, 2, 1),
            ServiceLevel = ServiceLevel.Express,
            Status = ContractStatus.Expired // must be ignored
        });

        var saved = await db.Contracts.AsNoTracking().SingleAsync(c => c.Id == contract.Id);
        Assert.Equal(new DateTime(2028, 2, 1), saved.EndDate);
        Assert.Equal(ServiceLevel.Express, saved.ServiceLevel);
        Assert.Equal(ContractStatus.Active, saved.Status);
    }

    // ---------- status workflow ----------

    [Fact]
    public async Task ChangeStatusAsync_AllowedTransition_UpdatesStatus()
    {
        var (db, service, _) = Create();
        await using var _db = db;
        var client = await TestDb.AddClientAsync(db);
        var contract = await TestDb.AddContractAsync(db, client, ContractStatus.Draft);

        await service.ChangeStatusAsync(contract.Id, ContractStatus.Active);

        Assert.Equal(ContractStatus.Active, (await db.Contracts.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task ChangeStatusAsync_ExpiredContractCannotBeReactivated()
    {
        var (db, service, _) = Create();
        await using var _db = db;
        var client = await TestDb.AddClientAsync(db);
        var contract = await TestDb.AddContractAsync(db, client, ContractStatus.Expired);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ChangeStatusAsync(contract.Id, ContractStatus.Active));

        Assert.Contains("Expired", ex.Message);
        Assert.Equal(ContractStatus.Expired, (await db.Contracts.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task ChangeStatusAsync_CannotActivateContractWhoseEndDateHasPassed()
    {
        var (db, service, _) = Create();
        await using var _db = db;
        var client = await TestDb.AddClientAsync(db);
        var contract = await TestDb.AddContractAsync(db, client, ContractStatus.Draft, new DateTime(2025, 1, 1), new DateTime(2026, 1, 1));

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ChangeStatusAsync(contract.Id, ContractStatus.Active));
    }

    [Fact]
    public async Task ChangeStatusAsync_UnknownContract_Throws()
    {
        var (db, service, _) = Create();
        await using var _db = db;

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ChangeStatusAsync(404, ContractStatus.Active));
    }

    [Fact]
    public async Task ExpireOverdueContractsAsync_ExpiresActiveAndOnHold_ButNotDraftOrCurrent()
    {
        var (db, service, _) = Create();
        await using var _db = db;
        var client = await TestDb.AddClientAsync(db);
        var pastEnd = new DateTime(2026, 10, 14);   // before the fixed "today" of 2026-10-15
        var activeOverdue = await TestDb.AddContractAsync(db, client, ContractStatus.Active, new DateTime(2025, 1, 1), pastEnd);
        var holdOverdue = await TestDb.AddContractAsync(db, client, ContractStatus.OnHold, new DateTime(2025, 1, 1), pastEnd);
        var draftOverdue = await TestDb.AddContractAsync(db, client, ContractStatus.Draft, new DateTime(2025, 1, 1), pastEnd);
        var endsToday = await TestDb.AddContractAsync(db, client, ContractStatus.Active, new DateTime(2025, 1, 1), new DateTime(2026, 10, 15));
        var future = await TestDb.AddContractAsync(db, client, ContractStatus.Active, new DateTime(2025, 1, 1), new DateTime(2027, 1, 1));

        var changed = await service.ExpireOverdueContractsAsync();

        Assert.Equal(2, changed);
        async Task<ContractStatus> StatusOf(Contract c) => (await db.Contracts.AsNoTracking().SingleAsync(x => x.Id == c.Id)).Status;
        Assert.Equal(ContractStatus.Expired, await StatusOf(activeOverdue));
        Assert.Equal(ContractStatus.Expired, await StatusOf(holdOverdue));
        Assert.Equal(ContractStatus.Draft, await StatusOf(draftOverdue));
        Assert.Equal(ContractStatus.Active, await StatusOf(endsToday));
        Assert.Equal(ContractStatus.Active, await StatusOf(future));
    }

    // ---------- signed agreement upload / download ----------

    [Fact]
    public async Task AttachAgreementAsync_Pdf_IsStoredAndLinkedToTheContract()
    {
        var (db, service, storage) = Create();
        await using var _db = db;
        var client = await TestDb.AddClientAsync(db);
        var contract = await TestDb.AddContractAsync(db, client, ContractStatus.Active);
        using var pdf = PdfSamples.ValidPdfStream();

        await service.AttachAgreementAsync(contract.Id, "Signed Agreement.pdf", pdf.Length, pdf);

        var saved = await db.Contracts.AsNoTracking().SingleAsync();
        Assert.True(saved.HasSignedAgreement);
        Assert.Equal("Signed Agreement.pdf", saved.SignedAgreementOriginalName);
        Assert.NotNull(saved.SignedAgreementUploadedAtUtc);
        Assert.Equal(PdfSamples.ValidPdf(), storage.Files[saved.SignedAgreementStoredName!]);
    }

    [Fact]
    public async Task AttachAgreementAsync_ExeFile_IsRejectedAndNothingIsStored()
    {
        var (db, service, storage) = Create();
        await using var _db = db;
        var client = await TestDb.AddClientAsync(db);
        var contract = await TestDb.AddContractAsync(db, client, ContractStatus.Active);
        using var exe = new MemoryStream(PdfSamples.ExecutableBytes());

        await Assert.ThrowsAsync<InvalidFileException>(() =>
            service.AttachAgreementAsync(contract.Id, "malware.exe", exe.Length, exe));

        Assert.Empty(storage.Files);
        Assert.False((await db.Contracts.AsNoTracking().SingleAsync()).HasSignedAgreement);
    }

    [Fact]
    public async Task AttachAgreementAsync_ReplacingAnAgreement_DeletesThePreviousFile()
    {
        var (db, service, storage) = Create();
        await using var _db = db;
        var client = await TestDb.AddClientAsync(db);
        var contract = await TestDb.AddContractAsync(db, client, ContractStatus.Active);

        using (var first = PdfSamples.ValidPdfStream())
        {
            await service.AttachAgreementAsync(contract.Id, "v1.pdf", first.Length, first);
        }

        var firstStoredName = (await db.Contracts.AsNoTracking().SingleAsync()).SignedAgreementStoredName!;

        using (var second = PdfSamples.ValidPdfStream())
        {
            await service.AttachAgreementAsync(contract.Id, "v2.pdf", second.Length, second);
        }

        var saved = await db.Contracts.AsNoTracking().SingleAsync();
        Assert.Equal("v2.pdf", saved.SignedAgreementOriginalName);
        Assert.NotEqual(firstStoredName, saved.SignedAgreementStoredName);
        Assert.False(storage.Files.ContainsKey(firstStoredName));
        Assert.Single(storage.Files);
    }

    [Fact]
    public async Task AttachAgreementAsync_UnknownContract_Throws()
    {
        var (db, service, storage) = Create();
        await using var _db = db;
        using var pdf = PdfSamples.ValidPdfStream();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AttachAgreementAsync(12345, "a.pdf", pdf.Length, pdf));

        Assert.Empty(storage.Files);
    }

    [Fact]
    public async Task OpenAgreementAsync_AfterUpload_ReturnsTheSameBytesAndOriginalFileName()
    {
        var (db, service, _) = Create();
        await using var _db = db;
        var client = await TestDb.AddClientAsync(db);
        var contract = await TestDb.AddContractAsync(db, client, ContractStatus.Active);
        using (var pdf = PdfSamples.ValidPdfStream())
        {
            await service.AttachAgreementAsync(contract.Id, "My Agreement.pdf", pdf.Length, pdf);
        }

        var download = await service.OpenAgreementAsync(contract.Id);

        Assert.NotNull(download);
        await using var content = download!.Content;
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy);
        Assert.Equal(PdfSamples.ValidPdf(), copy.ToArray());
        Assert.Equal("My Agreement.pdf", download.FileName);
    }

    [Fact]
    public async Task OpenAgreementAsync_NoAgreementOrUnknownContract_ReturnsNull()
    {
        var (db, service, _) = Create();
        await using var _db = db;
        var client = await TestDb.AddClientAsync(db);
        var contract = await TestDb.AddContractAsync(db, client, ContractStatus.Active);

        Assert.Null(await service.OpenAgreementAsync(contract.Id));
        Assert.Null(await service.OpenAgreementAsync(999));
    }
}
