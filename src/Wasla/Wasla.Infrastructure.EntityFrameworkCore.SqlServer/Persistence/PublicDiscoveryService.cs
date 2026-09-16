using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Doctors;
using Wasla.Application.Features.PublicDiscovery;
using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.Practices;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class PublicDiscoveryService(
    WaslaDbContext dbContext,
    IDateTimeProvider clock,
    IPracticeReservationOccupancyReader occupancyReader) : IPublicDiscoveryService
{
    public async Task<PagedResponse<PublicDoctorSearchItemResponse>> SearchDoctorsAsync(
        SearchPublicDoctorsQuery request,
        CancellationToken cancellationToken)
    {
        var query = EligibleDoctorQuery();
        var searchText = request.SearchText?.Trim();
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var normalizedArabic = DoctorNameNormalizer.NormalizeArabic(searchText);
            var normalizedEnglish = DoctorNameNormalizer.NormalizeEnglish(searchText);
            if (normalizedArabic.Length == 0)
            {
                query = query.Where(doctor => doctor.NormalizedNameEn != null &&
                    doctor.NormalizedNameEn.Contains(normalizedEnglish));
            }
            else
            {
                query = query.Where(doctor =>
                    doctor.NormalizedNameAr.Contains(normalizedArabic) ||
                    doctor.NormalizedNameEn != null && doctor.NormalizedNameEn.Contains(normalizedEnglish));
            }
        }

        if (request.SpecializationId.HasValue)
        {
            var specializationId = request.SpecializationId.Value;
            query = query.Where(doctor =>
                dbContext.DoctorSpecializations.Any(item =>
                    item.DoctorId == doctor.Id && item.MedicalSpecializationId == specializationId) &&
                dbContext.MedicalSpecializations.Any(item => item.Id == specializationId && item.IsActive));
        }

        if (request.GovernorateId.HasValue || request.CityId.HasValue || request.AreaId.HasValue)
        {
            query = query.Where(doctor => dbContext.DoctorPractices.Any(practice =>
                practice.DoctorId == doctor.Id && practice.IsActive &&
                (!request.GovernorateId.HasValue || practice.GovernorateId == request.GovernorateId.Value) &&
                (!request.CityId.HasValue || practice.CityId == request.CityId.Value) &&
                (!request.AreaId.HasValue || practice.AreaId == request.AreaId.Value)));
        }

        var totalCount = await query.LongCountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return new PagedResponse<PublicDoctorSearchItemResponse>(
                [], 0, request.PageNumber, request.PageSize);
        }

        var nowUtc = EnsureUtc(clock.UtcNow);
        var rankedQuery = query.Select(doctor => new
        {
            DoctorId = doctor.Id,
            PopularityScore = dbContext.PublicDoctorSearchRanks
                .Where(rank => rank.DoctorId == doctor.Id)
                .Select(rank => (long?)rank.PopularityScore)
                .FirstOrDefault() ?? 0L,
            NextAvailableSlotUtc = dbContext.PublicPracticeAvailabilitySlots
                .Where(slot =>
                    slot.DoctorId == doctor.Id &&
                    slot.IsAvailable &&
                    slot.VisibleFromUtc <= nowUtc &&
                    slot.SlotStartUtc > nowUtc)
                .Where(slot => dbContext.DoctorPractices.Any(practice =>
                    practice.Id == slot.DoctorPracticeId && practice.IsActive))
                .Where(slot => dbContext.DoctorPracticeConfigurations.Any(configuration =>
                    configuration.DoctorPracticeId == slot.DoctorPracticeId &&
                    configuration.AllowOnlineBooking))
                .Where(slot => dbContext.DoctorPracticeSegmentVisitTypePrices.Any(price =>
                    price.DoctorPracticeId == slot.DoctorPracticeId &&
                    dbContext.DoctorPracticeSegments.Any(segment =>
                        segment.Id == price.SegmentId &&
                        segment.DoctorPracticeId == price.DoctorPracticeId &&
                        segment.IsDefault &&
                        segment.IsActive) &&
                    dbContext.DoctorPracticeVisitTypes.Any(visitType =>
                        visitType.Id == price.VisitTypeId &&
                        visitType.DoctorPracticeId == price.DoctorPracticeId &&
                        visitType.Type == DoctorPracticeVisitTypeCode.NewConsultation &&
                        visitType.IsActive)))
                .Select(slot => (DateTime?)slot.SlotStartUtc)
                .Min()
        });

        var pageDoctorIds = await rankedQuery
            .OrderByDescending(item => item.PopularityScore)
            .ThenBy(item => item.NextAvailableSlotUtc == null)
            .ThenBy(item => item.NextAvailableSlotUtc)
            .ThenBy(item => item.DoctorId)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(item => item.DoctorId)
            .ToArrayAsync(cancellationToken);
        if (pageDoctorIds.Length == 0)
        {
            return new PagedResponse<PublicDoctorSearchItemResponse>(
                [], totalCount, request.PageNumber, request.PageSize);
        }

        var catalog = await LoadCatalogAsync(pageDoctorIds, cancellationToken);
        var occupancy = await ReadOccupancyAsync(catalog.Practices.Select(item => item.Id), cancellationToken);
        var items = pageDoctorIds.Select(doctorId =>
        {
            var doctor = catalog.Doctors[doctorId];
            var practices = BuildPractices(catalog, doctorId, request, occupancy);
            return new PublicDoctorSearchItemResponse(
                doctor.Id,
                doctor.ProfileImageMediaKey is null ? null : DoctorProfileImageUrl(doctor.Id),
                doctor.NameAr,
                doctor.NameEn,
                catalog.Specializations.GetValueOrDefault(doctorId, []).ToArray(),
                practices.OrderByDescending(practice => practice.MatchesLocation)
                    .ThenBy(practice => practice.Response.NameAr)
                    .ThenBy(practice => practice.Response.PracticeId)
                    .Select(practice => practice.Response)
                    .ToArray());
        })
        .ToArray();

        return new PagedResponse<PublicDoctorSearchItemResponse>(
            items, totalCount, request.PageNumber, request.PageSize);
    }

    public async Task<PublicDoctorDetailsResponse?> GetDoctorAsync(
        Guid doctorId,
        CancellationToken cancellationToken)
    {
        if (!await EligibleDoctorQuery().AnyAsync(item => item.Id == doctorId, cancellationToken))
        {
            return null;
        }

        var catalog = await LoadCatalogAsync([doctorId], cancellationToken);
        var occupancy = await ReadOccupancyAsync(catalog.Practices.Select(item => item.Id), cancellationToken);
        var doctor = catalog.Doctors[doctorId];
        var practices = BuildPractices(catalog, doctorId, null, occupancy)
            .OrderBy(item => item.Response.NameAr)
            .ThenBy(item => item.Response.PracticeId)
            .Select(item => item.Response)
            .ToArray();
        return new PublicDoctorDetailsResponse(
            doctor.Id,
            doctor.ProfileImageMediaKey is null ? null : DoctorProfileImageUrl(doctor.Id),
            doctor.NameAr,
            doctor.NameEn,
            catalog.Specializations.GetValueOrDefault(doctorId, []).ToArray(),
            doctor.Bio,
            catalog.Qualifications.GetValueOrDefault(doctorId, []).ToArray(),
            practices);
    }

    public async Task<IReadOnlyList<PublicSpecializationResponse>> ListSpecializationsAsync(
        CancellationToken cancellationToken)
        => await dbContext.MedicalSpecializations.AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.NameAr)
            .Select(item => new PublicSpecializationResponse(item.Id, item.NameAr, item.NameEn))
            .ToListAsync(cancellationToken);

    public async Task<Result<IReadOnlyList<PublicAvailableDateResponse>>> GetAvailableDatesAsync(
        Guid practiceId,
        CancellationToken cancellationToken)
    {
        var snapshot = await LoadPracticeAsync(practiceId, cancellationToken);
        if (snapshot is null)
        {
            return Result<IReadOnlyList<PublicAvailableDateResponse>>.Fail(
                PublicDiscoveryErrors.PracticeNotFound);
        }

        if (!HasBaseBookingRequirements(snapshot))
        {
            return Result<IReadOnlyList<PublicAvailableDateResponse>>.Ok([]);
        }

        var (today, throughDate, _) = LocalWindow(snapshot.Configuration!.TimeZoneId);
        var occupancy = await occupancyReader.ReadAsync(
            [practiceId], today, throughDate, cancellationToken);
        var dates = Enumerable.Range(0, PublicDiscoveryPolicy.PublicBookingHorizonDays)
            .Select(offset => today.AddDays(offset))
            .Where(date => AvailableSlots(snapshot, date, occupancy).Length > 0)
            .Select(date => new PublicAvailableDateResponse(date, true))
            .ToArray();
        return Result<IReadOnlyList<PublicAvailableDateResponse>>.Ok(dates);
    }

    public async Task<Result<IReadOnlyList<PublicAvailableSlotResponse>>> GetAvailableSlotsAsync(
        Guid practiceId,
        DateOnly requestedDate,
        CancellationToken cancellationToken)
    {
        var snapshot = await LoadPracticeAsync(practiceId, cancellationToken);
        if (snapshot is null)
        {
            return Result<IReadOnlyList<PublicAvailableSlotResponse>>.Fail(
                PublicDiscoveryErrors.PracticeNotFound);
        }

        if (!HasBaseBookingRequirements(snapshot))
        {
            return Result<IReadOnlyList<PublicAvailableSlotResponse>>.Fail(
                PublicDiscoveryErrors.PracticeNotBookable);
        }

        var window = LocalWindow(snapshot.Configuration!.TimeZoneId);
        if (requestedDate < window.Today || requestedDate > window.ThroughDate)
        {
            return Result<IReadOnlyList<PublicAvailableSlotResponse>>.Fail(
                PublicDiscoveryErrors.DateOutsideHorizon);
        }

        var occupancy = await occupancyReader.ReadAsync(
            [practiceId], requestedDate, requestedDate, cancellationToken);
        return Result<IReadOnlyList<PublicAvailableSlotResponse>>.Ok(
            AvailableSlots(snapshot, requestedDate, occupancy)
                .Select(time => new PublicAvailableSlotResponse(requestedDate, time))
                .ToArray());
    }

    public async Task<Result<PublicBookingOptionsResponse>> GetBookingOptionsAsync(
        Guid practiceId,
        DateOnly requestedDate,
        TimeOnly requestedTime,
        CancellationToken cancellationToken)
    {
        var snapshot = await LoadPracticeAsync(practiceId, cancellationToken);
        if (snapshot is null)
        {
            return Result<PublicBookingOptionsResponse>.Fail(PublicDiscoveryErrors.PracticeNotFound);
        }

        if (!HasBaseBookingRequirements(snapshot))
        {
            return Result<PublicBookingOptionsResponse>.Fail(PublicDiscoveryErrors.PracticeNotBookable);
        }

        var window = LocalWindow(snapshot.Configuration!.TimeZoneId);
        if (requestedDate < window.Today || requestedDate > window.ThroughDate)
        {
            return Result<PublicBookingOptionsResponse>.Fail(PublicDiscoveryErrors.DateOutsideHorizon);
        }

        var occupancy = await occupancyReader.ReadAsync(
            [practiceId], requestedDate, requestedDate, cancellationToken);
        if (!AvailableSlots(snapshot, requestedDate, occupancy).Contains(requestedTime))
        {
            return Result<PublicBookingOptionsResponse>.Fail(PublicDiscoveryErrors.SlotUnavailable);
        }

        var dayOccupancy = occupancy.GetValueOrDefault((practiceId, requestedDate), PracticeOccupancySnapshot.Empty);
        var slotCount = DoctorPracticeAvailabilityCalculator.CalculateAvailableSlotStarts(
            requestedDate, snapshot.Periods, snapshot.Exceptions).Count;
        var effectiveCapacity = snapshot.Configuration.EffectiveDailyCapacity(slotCount);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(snapshot.Configuration.TimeZoneId);
        var now = new DateTimeOffset(EnsureUtc(clock.UtcNow));
        var effectivePeriods = DoctorPracticeAvailabilityCalculator.GetEffectiveWorkingPeriods(
            requestedDate, snapshot.Periods, snapshot.Exceptions);
        var activeSegments = snapshot.Segments.Where(item => item.IsActive).ToArray();
        var protectedCapacity = activeSegments
            .Where(item => !item.IsDefault)
            .Sum(item => item.ProtectedCapacity(
                dayOccupancy.SegmentReservationCounts.GetValueOrDefault(item.Id),
                requestedDate,
                effectivePeriods,
                now,
                timeZone));
        var generalRemaining = Math.Max(
            0, effectiveCapacity - dayOccupancy.TotalReservations - protectedCapacity);

        var visitTypes = snapshot.VisitTypes
            .Where(item => item.IsActive && item.Type != DoctorPracticeVisitTypeCode.FollowUp)
            .OrderBy(item => item.Type)
            .Select(visitType => new PublicBookingVisitTypeResponse(
                visitType.Id,
                visitType.NameAr,
                visitType.NameEn,
                activeSegments
                    .Where(segment => SegmentCanBook(
                        segment,
                        dayOccupancy,
                        requestedDate,
                        effectivePeriods,
                        now,
                        timeZone,
                        generalRemaining))
                    .Select(segment => new
                    {
                        Segment = segment,
                        Price = snapshot.Prices.SingleOrDefault(price =>
                            price.SegmentId == segment.Id && price.VisitTypeId == visitType.Id)
                    })
                    .Where(item => item.Price is not null)
                    .OrderByDescending(item => item.Segment.IsDefault)
                    .ThenByDescending(item => item.Segment.Priority)
                    .ThenBy(item => item.Segment.NameAr)
                    .Select(item => new PublicBookingSegmentResponse(
                        item.Segment.Id,
                        item.Segment.NameAr,
                        item.Segment.NameEn,
                        item.Price!.Price,
                        item.Segment.IsDefault))
                    .ToArray()))
            .Where(item => item.Segments.Count > 0)
            .ToArray();

        return Result<PublicBookingOptionsResponse>.Ok(new PublicBookingOptionsResponse(
            practiceId, requestedDate, requestedTime, visitTypes));
    }

    public Task<string?> GetDoctorProfileImageKeyAsync(
        Guid doctorId,
        CancellationToken cancellationToken)
        => EligibleDoctorQuery()
            .Where(item => item.Id == doctorId)
            .Select(item => item.ProfileImageMediaKey)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<string?> GetPracticeLogoKeyAsync(
        Guid practiceId,
        CancellationToken cancellationToken)
        => (from practice in dbContext.DoctorPractices.AsNoTracking()
            join doctor in dbContext.Doctors.AsNoTracking() on practice.DoctorId equals doctor.Id
            join user in dbContext.ApplicationUsers.AsNoTracking() on doctor.ApplicationUserId equals user.Id
            join branding in dbContext.DoctorPracticeBrandings.AsNoTracking()
                on practice.Id equals branding.DoctorPracticeId
            where practice.Id == practiceId && practice.IsActive &&
                  doctor.ApprovalStatus == DoctorApprovalStatus.Approved && user.IsActive
            select branding.LogoMediaKey).SingleOrDefaultAsync(cancellationToken);

    private IQueryable<Doctor> EligibleDoctorQuery()
        => from doctor in dbContext.Doctors.AsNoTracking()
           join user in dbContext.ApplicationUsers.AsNoTracking()
               on doctor.ApplicationUserId equals user.Id
           where doctor.ApprovalStatus == DoctorApprovalStatus.Approved && user.IsActive &&
                 dbContext.DoctorPractices.Any(practice =>
                     practice.DoctorId == doctor.Id && practice.IsActive)
           select doctor;

    private async Task<PublicCatalog> LoadCatalogAsync(
        IReadOnlyCollection<Guid> doctorIds,
        CancellationToken cancellationToken)
    {
        var doctors = await dbContext.Doctors.AsNoTracking()
            .Where(item => doctorIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var practices = await (from practice in dbContext.DoctorPractices.AsNoTracking()
            join governorate in dbContext.Governorates.AsNoTracking()
                on practice.GovernorateId equals governorate.Id
            join city in dbContext.Cities.AsNoTracking() on practice.CityId equals city.Id
            join area in dbContext.Areas.AsNoTracking() on practice.AreaId equals area.Id
            where doctorIds.Contains(practice.DoctorId) && practice.IsActive
            select new PracticeRecord(
                practice.Id,
                practice.DoctorId,
                practice.NameAr,
                practice.NameEn,
                practice.GovernorateId,
                governorate.NameAr,
                governorate.NameEn,
                practice.CityId,
                city.NameAr,
                city.NameEn,
                practice.AreaId,
                area.NameAr,
                area.NameEn,
                practice.DetailedAddress,
                practice.Latitude,
                practice.Longitude)).ToListAsync(cancellationToken);
        var practiceIds = practices.Select(item => item.Id).ToArray();
        var configurations = await dbContext.DoctorPracticeConfigurations.AsNoTracking()
            .Where(item => practiceIds.Contains(item.DoctorPracticeId))
            .ToDictionaryAsync(item => item.DoctorPracticeId, cancellationToken);
        var brandings = await dbContext.DoctorPracticeBrandings.AsNoTracking()
            .Where(item => practiceIds.Contains(item.DoctorPracticeId))
            .ToDictionaryAsync(item => item.DoctorPracticeId, cancellationToken);
        var periods = await dbContext.DoctorPracticeSchedulePeriods.AsNoTracking()
            .Where(item => practiceIds.Contains(item.DoctorPracticeId))
            .ToListAsync(cancellationToken);
        var exceptions = await dbContext.DoctorPracticeScheduleExceptions.AsNoTracking()
            .Where(item => practiceIds.Contains(item.DoctorPracticeId))
            .ToListAsync(cancellationToken);
        var segments = await dbContext.DoctorPracticeSegments.AsNoTracking()
            .Where(item => practiceIds.Contains(item.DoctorPracticeId))
            .ToListAsync(cancellationToken);
        var visitTypes = await dbContext.DoctorPracticeVisitTypes.AsNoTracking()
            .Where(item => practiceIds.Contains(item.DoctorPracticeId))
            .ToListAsync(cancellationToken);
        var prices = await dbContext.DoctorPracticeSegmentVisitTypePrices.AsNoTracking()
            .Where(item => practiceIds.Contains(item.DoctorPracticeId))
            .ToListAsync(cancellationToken);
        var specializations = await (from selected in dbContext.DoctorSpecializations.AsNoTracking()
            join specialization in dbContext.MedicalSpecializations.AsNoTracking()
                on selected.MedicalSpecializationId equals specialization.Id
            where doctorIds.Contains(selected.DoctorId) && specialization.IsActive
            orderby selected.IsPrimary descending, specialization.SortOrder, specialization.NameAr
            select new
            {
                selected.DoctorId,
                Response = new PublicDoctorSpecializationResponse(
                    specialization.Id,
                    specialization.NameAr,
                    specialization.NameEn,
                    selected.IsPrimary)
            }).ToListAsync(cancellationToken);
        var qualifications = await dbContext.DoctorQualifications.AsNoTracking()
            .Where(item => doctorIds.Contains(item.DoctorId))
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.Id)
            .Select(item => new
            {
                item.DoctorId,
                Response = new PublicDoctorQualificationResponse(
                    item.Id, item.NameAr, item.NameEn, item.DisplayOrder)
            }).ToListAsync(cancellationToken);

        return new PublicCatalog(
            doctors,
            practices,
            configurations,
            brandings,
            periods.ToLookup(item => item.DoctorPracticeId),
            exceptions.ToLookup(item => item.DoctorPracticeId),
            segments.ToLookup(item => item.DoctorPracticeId),
            visitTypes.ToLookup(item => item.DoctorPracticeId),
            prices.ToLookup(item => item.DoctorPracticeId),
            specializations.GroupBy(item => item.DoctorId)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<PublicDoctorSpecializationResponse>)group.Select(item => item.Response).ToArray()),
            qualifications.GroupBy(item => item.DoctorId)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<PublicDoctorQualificationResponse>)group.Select(item => item.Response).ToArray()));
    }

    private async Task<PracticeSnapshot?> LoadPracticeAsync(
        Guid practiceId,
        CancellationToken cancellationToken)
    {
        var eligible = await (from practice in dbContext.DoctorPractices.AsNoTracking()
            join doctor in dbContext.Doctors.AsNoTracking() on practice.DoctorId equals doctor.Id
            join user in dbContext.ApplicationUsers.AsNoTracking() on doctor.ApplicationUserId equals user.Id
            where practice.Id == practiceId && practice.IsActive &&
                  doctor.ApprovalStatus == DoctorApprovalStatus.Approved && user.IsActive
            select practice.Id).AnyAsync(cancellationToken);
        if (!eligible)
        {
            return null;
        }

        var configuration = await dbContext.DoctorPracticeConfigurations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.DoctorPracticeId == practiceId, cancellationToken);
        var periods = await dbContext.DoctorPracticeSchedulePeriods.AsNoTracking()
            .Where(item => item.DoctorPracticeId == practiceId).ToListAsync(cancellationToken);
        var exceptions = await dbContext.DoctorPracticeScheduleExceptions.AsNoTracking()
            .Where(item => item.DoctorPracticeId == practiceId).ToListAsync(cancellationToken);
        var segments = await dbContext.DoctorPracticeSegments.AsNoTracking()
            .Where(item => item.DoctorPracticeId == practiceId).ToListAsync(cancellationToken);
        var visitTypes = await dbContext.DoctorPracticeVisitTypes.AsNoTracking()
            .Where(item => item.DoctorPracticeId == practiceId).ToListAsync(cancellationToken);
        var prices = await dbContext.DoctorPracticeSegmentVisitTypePrices.AsNoTracking()
            .Where(item => item.DoctorPracticeId == practiceId).ToListAsync(cancellationToken);
        return new PracticeSnapshot(
            practiceId, configuration, periods, exceptions, segments, visitTypes, prices);
    }

    private BuiltPractice[] BuildPractices(
        PublicCatalog catalog,
        Guid doctorId,
        SearchPublicDoctorsQuery? request,
        IReadOnlyDictionary<(Guid PracticeId, DateOnly Date), PracticeOccupancySnapshot> occupancy)
        => catalog.Practices.Where(item => item.DoctorId == doctorId).Select(practice =>
        {
            catalog.Configurations.TryGetValue(practice.Id, out var configuration);
            var snapshot = new PracticeSnapshot(
                practice.Id,
                configuration,
                catalog.Periods[practice.Id].ToArray(),
                catalog.Exceptions[practice.Id].ToArray(),
                catalog.Segments[practice.Id].ToArray(),
                catalog.VisitTypes[practice.Id].ToArray(),
                catalog.Prices[practice.Id].ToArray());
            var next = NextAvailable(snapshot, occupancy);
            var price = PublicSearchPrice(snapshot);
            catalog.Brandings.TryGetValue(practice.Id, out var branding);
            var response = new PublicPracticeResponse(
                practice.Id,
                practice.NameAr,
                practice.NameEn,
                branding?.LogoMediaKey is null ? null : PracticeLogoUrl(practice.Id),
                new PublicLocationResponse(practice.GovernorateId, practice.GovernorateNameAr, practice.GovernorateNameEn),
                new PublicLocationResponse(practice.CityId, practice.CityNameAr, practice.CityNameEn),
                new PublicLocationResponse(practice.AreaId, practice.AreaNameAr, practice.AreaNameEn),
                practice.Address,
                practice.Latitude,
                practice.Longitude,
                price,
                next?.Date,
                next?.Time,
                next?.IsToday == true,
                next is not null,
                configuration?.AllowOnlineBooking == true,
                BookingDisabledReason(snapshot, next));
            var matches = request is null ||
                (!request.GovernorateId.HasValue || practice.GovernorateId == request.GovernorateId.Value) &&
                (!request.CityId.HasValue || practice.CityId == request.CityId.Value) &&
                (!request.AreaId.HasValue || practice.AreaId == request.AreaId.Value);
            return new BuiltPractice(response, next?.Utc, matches);
        }).ToArray();

    private NextSlot? NextAvailable(
        PracticeSnapshot snapshot,
        IReadOnlyDictionary<(Guid PracticeId, DateOnly Date), PracticeOccupancySnapshot> occupancy)
    {
        if (!HasBaseBookingRequirements(snapshot))
        {
            return null;
        }

        var (today, _, timeZone) = LocalWindow(snapshot.Configuration!.TimeZoneId);
        for (var offset = 0; offset < PublicDiscoveryPolicy.PublicBookingHorizonDays; offset++)
        {
            var date = today.AddDays(offset);
            var slots = AvailableSlots(snapshot, date, occupancy);
            if (slots.Length == 0)
            {
                continue;
            }

            var time = slots[0];
            var localDateTime = date.ToDateTime(time, DateTimeKind.Unspecified);
            return new NextSlot(
                date,
                time,
                date == today,
                new DateTimeOffset(localDateTime, timeZone.GetUtcOffset(localDateTime)).ToUniversalTime());
        }

        return null;
    }

    private TimeOnly[] AvailableSlots(
        PracticeSnapshot snapshot,
        DateOnly date,
        IReadOnlyDictionary<(Guid PracticeId, DateOnly Date), PracticeOccupancySnapshot> occupancy)
    {
        if (!HasBaseBookingRequirements(snapshot))
        {
            return [];
        }

        var (today, throughDate, timeZone) = LocalWindow(snapshot.Configuration!.TimeZoneId);
        if (date < today || date > throughDate)
        {
            return [];
        }

        var generated = DoctorPracticeAvailabilityCalculator.CalculateAvailableSlotStarts(
            date, snapshot.Periods, snapshot.Exceptions);
        var dayOccupancy = occupancy.GetValueOrDefault(
            (snapshot.PracticeId, date), PracticeOccupancySnapshot.Empty);
        var remainingDailyCapacity = Math.Max(
            0,
            snapshot.Configuration.EffectiveDailyCapacity(generated.Count) - dayOccupancy.TotalReservations);
        if (remainingDailyCapacity == 0)
        {
            return [];
        }

        var localNow = TimeZoneInfo.ConvertTime(new DateTimeOffset(EnsureUtc(clock.UtcNow)), timeZone);
        var localTime = TimeOnly.FromDateTime(localNow.DateTime);
        return generated
            .Where(time => date != today || time > localTime)
            .Where(time => !dayOccupancy.OccupiedSlots.Contains(time))
            .ToArray();
    }

    private static bool HasBaseBookingRequirements(PracticeSnapshot snapshot)
        => snapshot.Configuration?.AllowOnlineBooking == true &&
           PublicSearchPrice(snapshot).HasValue;

    private string? BookingDisabledReason(PracticeSnapshot snapshot, NextSlot? next)
    {
        if (next is not null)
        {
            return null;
        }

        if (snapshot.Configuration?.AllowOnlineBooking != true)
        {
            return "OnlineBookingDisabled";
        }

        if (!PublicSearchPrice(snapshot).HasValue)
        {
            return "NoPricing";
        }

        var (today, _, _) = LocalWindow(snapshot.Configuration.TimeZoneId);
        var hasSchedule = Enumerable.Range(0, PublicDiscoveryPolicy.PublicBookingHorizonDays)
            .Select(offset => today.AddDays(offset))
            .Any(date => DoctorPracticeAvailabilityCalculator.CalculateAvailableSlotStarts(
                date,
                snapshot.Periods,
                snapshot.Exceptions).Count > 0);
        return hasSchedule ? "NoAvailableCapacity" : "NoAvailableSchedule";
    }

    private static decimal? PublicSearchPrice(PracticeSnapshot snapshot)
    {
        var normal = snapshot.Segments.SingleOrDefault(item => item.IsDefault && item.IsActive);
        var consultation = snapshot.VisitTypes.SingleOrDefault(item =>
            item.Type == DoctorPracticeVisitTypeCode.NewConsultation && item.IsActive);
        if (normal is null || consultation is null)
        {
            return null;
        }

        return snapshot.Prices.SingleOrDefault(item =>
            item.SegmentId == normal.Id && item.VisitTypeId == consultation.Id)?.Price;
    }

    private static bool SegmentCanBook(
        DoctorPracticeSegment segment,
        PracticeOccupancySnapshot occupancy,
        DateOnly date,
        IReadOnlyCollection<PracticeWorkingPeriod> effectivePeriods,
        DateTimeOffset now,
        TimeZoneInfo timeZone,
        int generalRemaining)
    {
        if (segment.ReservedDailyQuota.HasValue)
        {
            var consumed = occupancy.SegmentReservationCounts.GetValueOrDefault(segment.Id);
            if (segment.ProtectedCapacity(consumed, date, effectivePeriods, now, timeZone) > 0)
            {
                return true;
            }
        }

        return generalRemaining > 0;
    }

    private async Task<IReadOnlyDictionary<(Guid PracticeId, DateOnly Date), PracticeOccupancySnapshot>> ReadOccupancyAsync(
        IEnumerable<Guid> practiceIds,
        CancellationToken cancellationToken)
    {
        var ids = practiceIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<(Guid, DateOnly), PracticeOccupancySnapshot>();
        }

        var utcToday = DateOnly.FromDateTime(EnsureUtc(clock.UtcNow));
        return await occupancyReader.ReadAsync(
            ids,
            utcToday.AddDays(-1),
            utcToday.AddDays(PublicDiscoveryPolicy.PublicBookingHorizonDays + 1),
            cancellationToken);
    }

    private (DateOnly Today, DateOnly ThroughDate, TimeZoneInfo TimeZone) LocalWindow(string timeZoneId)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(new DateTimeOffset(EnsureUtc(clock.UtcNow)), timeZone);
        var today = DateOnly.FromDateTime(localNow.DateTime);
        return (today, today.AddDays(PublicDiscoveryPolicy.PublicBookingHorizonDays - 1), timeZone);
    }

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();

    private static string DoctorProfileImageUrl(Guid doctorId)
        => $"/api/v1/public/doctors/{doctorId:D}/profile-image";

    private static string PracticeLogoUrl(Guid practiceId)
        => $"/api/v1/public/practices/{practiceId:D}/logo";

    private sealed record PracticeRecord(
        Guid Id,
        Guid DoctorId,
        string NameAr,
        string? NameEn,
        int GovernorateId,
        string GovernorateNameAr,
        string? GovernorateNameEn,
        int CityId,
        string CityNameAr,
        string? CityNameEn,
        int AreaId,
        string AreaNameAr,
        string? AreaNameEn,
        string Address,
        decimal Latitude,
        decimal Longitude);

    private sealed record PracticeSnapshot(
        Guid PracticeId,
        DoctorPracticeConfiguration? Configuration,
        IReadOnlyList<DoctorPracticeSchedulePeriod> Periods,
        IReadOnlyList<DoctorPracticeScheduleException> Exceptions,
        IReadOnlyList<DoctorPracticeSegment> Segments,
        IReadOnlyList<DoctorPracticeVisitType> VisitTypes,
        IReadOnlyList<DoctorPracticeSegmentVisitTypePrice> Prices);

    private sealed record PublicCatalog(
        IReadOnlyDictionary<Guid, Doctor> Doctors,
        IReadOnlyList<PracticeRecord> Practices,
        IReadOnlyDictionary<Guid, DoctorPracticeConfiguration> Configurations,
        IReadOnlyDictionary<Guid, DoctorPracticeBranding> Brandings,
        ILookup<Guid, DoctorPracticeSchedulePeriod> Periods,
        ILookup<Guid, DoctorPracticeScheduleException> Exceptions,
        ILookup<Guid, DoctorPracticeSegment> Segments,
        ILookup<Guid, DoctorPracticeVisitType> VisitTypes,
        ILookup<Guid, DoctorPracticeSegmentVisitTypePrice> Prices,
        IReadOnlyDictionary<Guid, IReadOnlyList<PublicDoctorSpecializationResponse>> Specializations,
        IReadOnlyDictionary<Guid, IReadOnlyList<PublicDoctorQualificationResponse>> Qualifications);

    private sealed record BuiltPractice(
        PublicPracticeResponse Response,
        DateTimeOffset? NextAvailableUtc,
        bool MatchesLocation);

    private sealed record NextSlot(DateOnly Date, TimeOnly Time, bool IsToday, DateTimeOffset Utc);
}
