using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Globalization;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using BuildingBlock.Application.Time;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wasla.Application.Features.PublicDiscovery;
using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.Practices;
using Wasla.Domain.ReferenceData;
using Wasla.Domain.Security;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

namespace Wasla.Tests.Integration;

public sealed class PublicDiscoveryApiTests
{
    private static readonly DateTime NowUtc = new(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid CardiologyId = Guid.Parse("71000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Search_enforces_publication_filters_multi_practice_price_and_pagination()
    {
        await using var factory = await PublicDiscoveryApiFactory.CreateAsync();
        using var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        var allResponse = await client.GetAsync(
            "/api/v1/public/doctors?pageNumber=1&pageSize=20", cancellationToken);
        var allBody = await allResponse.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(allResponse.IsSuccessStatusCode, allBody);
        var all = JsonDocument.Parse(allBody).RootElement;
        Assert.Equal(2, all.GetProperty("totalCount").GetInt64());
        Assert.Equal(2, all.GetProperty("items").GetArrayLength());
        Assert.Equal(
            [
                factory.DiscoverableDoctorId,
                Guid.Parse("72000000-0000-0000-0000-000000000002")
            ],
            all.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty("doctorId").GetGuid()).Order().ToArray());

        var normalizedSearch = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/public/doctors?searchText={Uri.EscapeDataString("أَحْمَد")}", cancellationToken);
        var doctor = Assert.Single(normalizedSearch.GetProperty("items").EnumerateArray());
        Assert.Equal(factory.DiscoverableDoctorId, doctor.GetProperty("doctorId").GetGuid());
        var practices = doctor.GetProperty("practices");
        Assert.Equal(3, practices.GetArrayLength());
        Assert.Equal(factory.MatchingPracticeId, practices[0].GetProperty("practiceId").GetGuid());
        Assert.DoesNotContain(
            practices.EnumerateArray(),
            item => item.GetProperty("practiceId").GetGuid() == factory.InactivePracticeId);
        Assert.Equal(500m, practices[0].GetProperty("publicSearchPrice").GetDecimal());
        Assert.Equal("2026-09-15", practices[0].GetProperty("nextAvailableSlotDate").GetString());
        Assert.Equal("15:10:00", practices[0].GetProperty("nextAvailableSlotTime").GetString());
        Assert.True(practices[0].GetProperty("isToday").GetBoolean());
        var nonBookable = Assert.Single(
            practices.EnumerateArray(),
            item => item.GetProperty("practiceId").GetGuid() == factory.NonBookablePracticeId);
        Assert.Equal(JsonValueKind.Null, nonBookable.GetProperty("publicSearchPrice").ValueKind);
        Assert.Equal(JsonValueKind.Null, nonBookable.GetProperty("nextAvailableSlotDate").ValueKind);
        Assert.False(nonBookable.GetProperty("isBookable").GetBoolean());
        Assert.False(nonBookable.GetProperty("onlineBookingEnabled").GetBoolean());
        Assert.Equal(
            "OnlineBookingDisabled",
            nonBookable.GetProperty("bookingDisabledReason").GetString());
        Assert.True(practices[0].GetProperty("onlineBookingEnabled").GetBoolean());
        Assert.Equal(JsonValueKind.Null, practices[0].GetProperty("bookingDisabledReason").ValueKind);

        var combined = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/public/doctors?searchText={Uri.EscapeDataString("احمد")}" +
            $"&specializationId={CardiologyId:D}&governorateId=1&cityId=11&areaId=111",
            cancellationToken);
        Assert.Single(combined.GetProperty("items").EnumerateArray());

        var noFalseTaaMarbutaMatch = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/public/doctors?searchText={Uri.EscapeDataString("احمد منه")}", cancellationToken);
        Assert.Empty(noFalseTaaMarbutaMatch.GetProperty("items").EnumerateArray());

        var page = await client.GetFromJsonAsync<JsonElement>(
            "/api/v1/public/doctors?pageNumber=2&pageSize=1", cancellationToken);
        Assert.Equal(2, page.GetProperty("totalCount").GetInt64());
        Assert.Single(page.GetProperty("items").EnumerateArray());

        var specializationOnly = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/public/doctors?specializationId={CardiologyId:D}", cancellationToken);
        Assert.Single(specializationOnly.GetProperty("items").EnumerateArray());
        var governorateOnly = await client.GetFromJsonAsync<JsonElement>(
            "/api/v1/public/doctors?governorateId=1", cancellationToken);
        Assert.Single(governorateOnly.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Details_and_availability_expose_only_public_data_and_valid_booking_options()
    {
        await using var factory = await PublicDiscoveryApiFactory.CreateAsync();
        using var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        var detailsResponse = await client.GetAsync(
            $"/api/v1/public/doctors/{factory.DiscoverableDoctorId:D}", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        var rawDetails = await detailsResponse.Content.ReadAsStringAsync(cancellationToken);
        Assert.DoesNotContain("nationalId", rawDetails, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("personalId", rawDetails, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rowVersion", rawDetails, StringComparison.OrdinalIgnoreCase);
        var details = JsonDocument.Parse(rawDetails).RootElement;
        Assert.Equal("استشاري قلب", details.GetProperty("bio").GetString());
        Assert.Equal(
            [1, 2],
            details.GetProperty("qualifications").EnumerateArray()
                .Select(item => item.GetProperty("displayOrder").GetInt32()).ToArray());

        var date = new DateOnly(2026, 9, 15);
        var dates = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/public/practices/{factory.MatchingPracticeId:D}/available-dates",
            cancellationToken);
        Assert.NotEmpty(dates.EnumerateArray());
        Assert.All(dates.EnumerateArray(), item =>
        {
            var availableDate = DateOnly.Parse(
                item.GetProperty("date").GetString()!, CultureInfo.InvariantCulture);
            Assert.InRange(availableDate, date, date.AddDays(29));
            Assert.True(item.GetProperty("isAvailable").GetBoolean());
        });

        var slots = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/public/practices/{factory.MatchingPracticeId:D}/available-slots?date={date:yyyy-MM-dd}",
            cancellationToken);
        Assert.Equal(
            [new TimeOnly(15, 10), new TimeOnly(15, 30)],
            slots.EnumerateArray().Select(slot => TimeOnly.Parse(
                slot.GetProperty("time").GetString()!, CultureInfo.InvariantCulture)).ToArray());

        var options = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/public/practices/{factory.MatchingPracticeId:D}/booking-options" +
            $"?date={date:yyyy-MM-dd}&time=15:10:00", cancellationToken);
        var visitType = Assert.Single(options.GetProperty("visitTypes").EnumerateArray());
        Assert.Equal("كشف جديد", visitType.GetProperty("nameAr").GetString());
        Assert.Equal(2, visitType.GetProperty("segments").GetArrayLength());
        Assert.Contains(visitType.GetProperty("segments").EnumerateArray(),
            segment => segment.GetProperty("price").GetDecimal() == 100m);
        Assert.DoesNotContain("متابعة", options.ToString(), StringComparison.Ordinal);

        var outsideHorizon = await client.GetAsync(
            $"/api/v1/public/practices/{factory.MatchingPracticeId:D}/available-slots?date={date.AddDays(30):yyyy-MM-dd}",
            cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, outsideHorizon.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(
            $"/api/v1/public/practices/{factory.InactivePracticeId:D}/available-slots?date={date:yyyy-MM-dd}",
            cancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync(
            $"/api/v1/public/practices/{factory.NonBookablePracticeId:D}/available-slots?date={date:yyyy-MM-dd}",
            cancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(
            "/api/v1/public/reservations", new { }, cancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Maximum_daily_patients_closes_availability_when_occupancy_reaches_capacity()
    {
        await using var factory = await PublicDiscoveryApiFactory.CreateAsync(atCapacity: true);
        using var client = factory.CreateClient();
        var date = new DateOnly(2026, 9, 15);

        var slots = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/public/practices/{factory.MatchingPracticeId:D}/available-slots?date={date:yyyy-MM-dd}",
            TestContext.Current.CancellationToken);

        Assert.Empty(slots.EnumerateArray());
    }

    [Fact]
    public async Task Search_ranks_globally_by_popularity_then_next_slot_before_paging()
    {
        var popularDoctorId = Guid.Parse("72000000-0000-0000-0000-000000000002");
        await using var factory = await PublicDiscoveryApiFactory.CreateAsync(
            popularityScores: new Dictionary<Guid, long> { [popularDoctorId] = 7 });
        using var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        var firstPage = await client.GetFromJsonAsync<JsonElement>(
            "/api/v1/public/doctors?pageNumber=1&pageSize=1", cancellationToken);
        var secondPage = await client.GetFromJsonAsync<JsonElement>(
            "/api/v1/public/doctors?pageNumber=2&pageSize=1", cancellationToken);

        Assert.Equal(
            popularDoctorId,
            Assert.Single(firstPage.GetProperty("items").EnumerateArray())
                .GetProperty("doctorId").GetGuid());
        Assert.Equal(
            factory.DiscoverableDoctorId,
            Assert.Single(secondPage.GetProperty("items").EnumerateArray())
                .GetProperty("doctorId").GetGuid());
    }

    [Fact]
    public async Task Search_with_one_thousand_candidates_has_constant_queries_and_page_scoped_hydration()
    {
        const int candidateCount = 1000;
        const int pageSize = 20;
        await using var factory = await PublicDiscoveryApiFactory.CreateAsync(
            eligibleDoctorCount: candidateCount);
        using var client = factory.CreateClient();
        factory.ResetSearchDiagnostics();
        var stopwatch = Stopwatch.StartNew();

        var response = await client.GetAsync(
            $"/api/v1/public/doctors?pageNumber=1&pageSize={pageSize}",
            TestContext.Current.CancellationToken);
        stopwatch.Stop();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, body);
        var payload = JsonDocument.Parse(body).RootElement;

        Assert.Equal(candidateCount, payload.GetProperty("totalCount").GetInt64());
        Assert.Equal(pageSize, payload.GetProperty("items").GetArrayLength());
        Assert.Equal(13, factory.ExecutedSelectCount);
        var hydratedPracticeCount = payload.GetProperty("items").EnumerateArray()
            .Sum(item => item.GetProperty("practices").GetArrayLength());
        Assert.Equal(hydratedPracticeCount, factory.LastOccupancyPracticeIds.Count);
        Assert.InRange(hydratedPracticeCount, pageSize, pageSize + 2);
        Assert.Contains(factory.ExecutedSql, sql =>
            sql.Contains("COUNT", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(factory.ExecutedSql, sql =>
            sql.Contains("LIMIT", StringComparison.OrdinalIgnoreCase) &&
            sql.Contains("OFFSET", StringComparison.OrdinalIgnoreCase));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30),
            $"The instrumented request took {stopwatch.Elapsed}.");
    }

    private sealed class PublicDiscoveryApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        private readonly int eligibleDoctorCount;
        private readonly QueryCountingInterceptor queryCounter = new();
        private readonly RecordingOccupancyReader occupancyReader;
        private readonly IReadOnlyDictionary<Guid, long> popularityScores;

        private PublicDiscoveryApiFactory(
            bool atCapacity,
            int eligibleDoctorCount,
            IReadOnlyDictionary<Guid, long>? popularityScores)
        {
            this.eligibleDoctorCount = eligibleDoctorCount;
            this.popularityScores = popularityScores ?? new Dictionary<Guid, long>();
            occupancyReader = new RecordingOccupancyReader(
                atCapacity ? MatchingPracticeId : null);
        }

        public Guid DiscoverableDoctorId { get; } = Guid.Parse("72000000-0000-0000-0000-000000000001");
        public Guid MatchingPracticeId { get; } = Guid.Parse("73000000-0000-0000-0000-000000000001");
        public Guid NonBookablePracticeId { get; } = Guid.Parse("73000000-0000-0000-0000-000000000003");
        public Guid InactivePracticeId { get; } = Guid.Parse("73000000-0000-0000-0000-000000000004");
        public int ExecutedSelectCount => queryCounter.ExecutedSql.Length;
        public string[] ExecutedSql => queryCounter.ExecutedSql;
        public IReadOnlyCollection<Guid> LastOccupancyPracticeIds => occupancyReader.LastPracticeIds;

        public static async Task<PublicDiscoveryApiFactory> CreateAsync(
            bool atCapacity = false,
            int eligibleDoctorCount = 2,
            IReadOnlyDictionary<Guid, long>? popularityScores = null)
        {
            var factory = new PublicDiscoveryApiFactory(
                atCapacity,
                eligibleDoctorCount,
                popularityScores);
            await factory.connection.OpenAsync(TestContext.Current.CancellationToken);
            _ = factory.CreateClient();
            await factory.InitializeAsync();
            return factory;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:SigningKey"] = "test-jwt-signing-key-with-at-least-thirty-two-characters",
                    ["PasswordReset:HmacSecret"] = "test-password-reset-hmac-secret-at-least-32-chars",
                    ["MediaStorage:RootPath"] = Path.Combine(Path.GetTempPath(), $"wasla-public-{Guid.NewGuid():N}"),
                    ["DatabaseInitialization:ApplyMigrationsOnStartup"] = "false",
                    ["DatabaseInitialization:ApplySeedingOnStartup"] = "false",
                    ["PublicDiscoveryProjection:Enabled"] = "false",
                    ["EmailOutbox:Enabled"] = "false"
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<WaslaDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<WaslaDbContext>>();
                services.RemoveAll<WaslaDbContext>();
                services.RemoveAll<IDateTimeProvider>();
                services.AddSingleton<IDateTimeProvider>(new FixedClock(NowUtc));
                services.RemoveAll<IPracticeReservationOccupancyReader>();
                services.AddSingleton<IPracticeReservationOccupancyReader>(occupancyReader);
                services.RemoveAll<IPublicDoctorPopularityReader>();
                services.AddSingleton<IPublicDoctorPopularityReader>(
                    new FixedPopularityReader(popularityScores));
                services.AddDbContext<WaslaDbContext>(options => options
                    .UseSqlite(connection)
                    .AddInterceptors(queryCounter));
            });
        }

        public void ResetSearchDiagnostics()
        {
            queryCounter.Reset();
            occupancyReader.Reset();
        }

        private async Task InitializeAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
            await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            var actorId = Guid.Parse("70000000-0000-0000-0000-000000000001");
            var actor = ApplicationUser.Create(
                actorId, "public-seed", "public-seed@example.test", null, "hash",
                UserType.SuperAdmin, false, NowUtc).Value;
            db.ApplicationUsers.Add(actor);
            db.Governorates.AddRange(
                Governorate.Create(1, "القاهرة", "Cairo", 1).Value,
                Governorate.Create(2, "المنوفية", "Monufia", 2).Value);
            db.Cities.AddRange(
                City.Create(11, 1, "مدينة نصر", "Nasr City", 1).Value,
                City.Create(21, 2, "شبين الكوم", "Shebin El Kom", 1).Value);
            db.Areas.AddRange(
                Area.Create(111, 11, "الحي السابع", "Seventh District", 1).Value,
                Area.Create(211, 21, "وسط البلد", "Downtown", 1).Value);
            db.MedicalSpecializations.Add(MedicalSpecialization.Create(
                CardiologyId, "قلب وأوعية دموية", "Cardiology", null, null, 1, actorId).Value);

            AddDiscoverableDoctor(db, actorId);
            AddSimpleDoctor(db, actorId, Guid.Parse("72000000-0000-0000-0000-000000000002"),
                "سارة علي", DoctorApprovalStatus.Approved, hasActivePractice: true, areaTwo: true);
            AddSimpleDoctor(db, actorId, Guid.Parse("72000000-0000-0000-0000-000000000003"),
                "طبيب معلق", DoctorApprovalStatus.Pending, hasActivePractice: true, areaTwo: false);
            AddSimpleDoctor(db, actorId, Guid.Parse("72000000-0000-0000-0000-000000000004"),
                "طبيب مرفوض", DoctorApprovalStatus.Rejected, hasActivePractice: true, areaTwo: false);
            AddSimpleDoctor(db, actorId, Guid.Parse("72000000-0000-0000-0000-000000000005"),
                "طبيب موقوف", DoctorApprovalStatus.Suspended, hasActivePractice: true, areaTwo: false);
            AddSimpleDoctor(db, actorId, Guid.Parse("72000000-0000-0000-0000-000000000006"),
                "طبيب بلا عيادة", DoctorApprovalStatus.Approved, hasActivePractice: false, areaTwo: false);
            AddSimpleDoctor(db, actorId, Guid.Parse("72000000-0000-0000-0000-000000000007"),
                "طبيب حسابه غير نشط", DoctorApprovalStatus.Approved, hasActivePractice: true,
                areaTwo: false, activeUser: false);
            for (var index = 3; index <= eligibleDoctorCount; index++)
            {
                AddSimpleDoctor(
                    db,
                    actorId,
                    Guid.Parse($"76000000-0000-0000-0000-{index:D12}"),
                    $"طبيب أداء {index}",
                    DoctorApprovalStatus.Approved,
                    hasActivePractice: true,
                    areaTwo: true);
            }

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await scope.ServiceProvider
                .GetRequiredService<IPublicDiscoveryRankingProjectionRefresher>()
                .RefreshAllAsync(TestContext.Current.CancellationToken);
            ResetSearchDiagnostics();
        }

        private void AddDiscoverableDoctor(WaslaDbContext db, Guid actorId)
        {
            var userId = Guid.Parse("74000000-0000-0000-0000-000000000001");
            var user = CreateUser(userId, "discoverable");
            var doctor = CreateDoctor(DiscoverableDoctorId, userId, "أحمد منة");
            Assert.True(doctor.Approve("29001011234567", actorId, NowUtc).IsSuccess);
            Assert.True(doctor.UpdateBio("استشاري قلب", actorId).IsSuccess);
            db.ApplicationUsers.Add(user);
            db.Doctors.Add(doctor);
            db.DoctorSpecializations.Add(new DoctorSpecialization(
                Guid.NewGuid(), doctor.Id, CardiologyId, true, actorId, NowUtc));
            db.DoctorQualifications.AddRange(
                DoctorQualification.Create(Guid.NewGuid(), doctor.Id, "دكتوراه", null, 2, actorId).Value,
                DoctorQualification.Create(Guid.NewGuid(), doctor.Id, "زمالة", "Fellowship", 1, actorId).Value);

            AddPracticeGraph(db, doctor.Id, actorId, MatchingPracticeId,
                "عيادة القاهرة", 1, 11, 111, active: true, bookable: true,
                DayOfWeek.Tuesday, new TimeOnly(15, 10), new TimeOnly(15, 50), maximumDailyPatients: 1);
            AddPracticeGraph(db, doctor.Id, actorId,
                Guid.Parse("73000000-0000-0000-0000-000000000002"),
                "عيادة شبين", 2, 21, 211, active: true, bookable: true,
                DayOfWeek.Wednesday, new TimeOnly(10, 0), new TimeOnly(11, 0), maximumDailyPatients: null);
            AddPracticeGraph(db, doctor.Id, actorId, NonBookablePracticeId,
                "عيادة غير قابلة للحجز", 2, 21, 211, active: true, bookable: false,
                DayOfWeek.Thursday, new TimeOnly(10, 0), new TimeOnly(11, 0), maximumDailyPatients: null);
            AddPracticeGraph(db, doctor.Id, actorId, InactivePracticeId,
                "عيادة غير نشطة", 1, 11, 111, active: false, bookable: true,
                DayOfWeek.Friday, new TimeOnly(10, 0), new TimeOnly(11, 0), maximumDailyPatients: null);
        }

        private static void AddSimpleDoctor(
            WaslaDbContext db,
            Guid actorId,
            Guid doctorId,
            string nameAr,
            DoctorApprovalStatus status,
            bool hasActivePractice,
            bool areaTwo,
            bool activeUser = true)
        {
            var userId = Guid.NewGuid();
            var user = CreateUser(userId, doctorId.ToString("N"));
            if (!activeUser)
            {
                user.Deactivate();
            }
            var doctor = CreateDoctor(doctorId, userId, nameAr);
            if (status is DoctorApprovalStatus.Approved or DoctorApprovalStatus.Suspended)
            {
                Assert.True(doctor.Approve(doctorId.ToString("N"), actorId, NowUtc).IsSuccess);
            }
            else if (status == DoctorApprovalStatus.Rejected)
            {
                Assert.True(doctor.Reject("documents", actorId, NowUtc).IsSuccess);
            }

            if (status == DoctorApprovalStatus.Suspended)
            {
                Assert.True(doctor.Suspend("review", actorId, NowUtc).IsSuccess);
            }

            db.ApplicationUsers.Add(user);
            db.Doctors.Add(doctor);
            AddPracticeGraph(db, doctor.Id, actorId, Guid.NewGuid(), "عيادة اختبار",
                areaTwo ? 2 : 1, areaTwo ? 21 : 11, areaTwo ? 211 : 111,
                hasActivePractice, bookable: true, DayOfWeek.Wednesday,
                new TimeOnly(12, 0), new TimeOnly(13, 0), maximumDailyPatients: null);
        }

        private static ApplicationUser CreateUser(Guid userId, string suffix)
            => ApplicationUser.Create(
                userId, $"doctor-{suffix}", $"doctor-{suffix}@example.test", null, "hash",
                UserType.Doctor, false, NowUtc).Value;

        private static Doctor CreateDoctor(Guid doctorId, Guid userId, string nameAr)
            => Doctor.Create(
                doctorId, userId, nameAr, "Test Doctor", new DateOnly(1985, 1, 1),
                Gender.Male, null, "front", "back", "syndicate", null,
                DateOnly.FromDateTime(NowUtc)).Value;

        private static void AddPracticeGraph(
            WaslaDbContext db,
            Guid doctorId,
            Guid actorId,
            Guid practiceId,
            string nameAr,
            int governorateId,
            int cityId,
            int areaId,
            bool active,
            bool bookable,
            DayOfWeek day,
            TimeOnly start,
            TimeOnly end,
            int? maximumDailyPatients)
        {
            var practice = DoctorPractice.Create(
                practiceId, doctorId, nameAr, null, governorateId, cityId, areaId,
                "15 شارع الاختبار", 30m, 31m, actorId).Value;
            var configuration = DoctorPracticeConfiguration.CreateDefault(
                Guid.NewGuid(), practiceId, actorId).Value;
            Assert.True(configuration.Update(
                bookable, true, 20, 15, 120, maximumDailyPatients, 3, "Africa/Cairo", actorId).IsSuccess);
            var branding = DoctorPracticeBranding.CreateDefault(Guid.NewGuid(), practiceId, actorId).Value;
            Assert.True(branding.ReplaceLogo($"logos/{practiceId:N}.png", actorId).IsSuccess);
            if (active)
            {
                Assert.True(practice.Activate(true, true, true, actorId).IsSuccess);
            }

            var normal = DoctorPracticeSegment.CreateDefault(Guid.NewGuid(), practiceId, actorId).Value;
            var vip = DoctorPracticeSegment.Create(
                Guid.NewGuid(), practiceId, "مميز", "VIP", 10, null, null, false, actorId).Value;
            var consultation = DoctorPracticeVisitType.CreateDefault(
                Guid.NewGuid(), practiceId, DoctorPracticeVisitTypeCode.NewConsultation, actorId).Value;
            var followUp = DoctorPracticeVisitType.CreateDefault(
                Guid.NewGuid(), practiceId, DoctorPracticeVisitTypeCode.FollowUp, actorId).Value;
            db.DoctorPractices.Add(practice);
            db.DoctorPracticeConfigurations.Add(configuration);
            db.DoctorPracticeBrandings.Add(branding);
            db.DoctorPracticeSchedulePeriods.Add(DoctorPracticeSchedulePeriod.Create(
                Guid.NewGuid(), practiceId, day, start, end, 20, actorId).Value);
            db.DoctorPracticeSegments.AddRange(normal, vip);
            db.DoctorPracticeVisitTypes.AddRange(consultation, followUp);
            if (bookable)
            {
                db.DoctorPracticeSegmentVisitTypePrices.AddRange(
                    DoctorPracticeSegmentVisitTypePrice.Create(
                        Guid.NewGuid(), practiceId, normal.Id, consultation.Id, 500m, actorId).Value,
                    DoctorPracticeSegmentVisitTypePrice.Create(
                        Guid.NewGuid(), practiceId, vip.Id, consultation.Id, 100m, actorId).Value,
                    DoctorPracticeSegmentVisitTypePrice.Create(
                        Guid.NewGuid(), practiceId, normal.Id, followUp.Id, 250m, actorId).Value);
            }
        }

        private sealed class FixedClock(DateTime utcNow) : IDateTimeProvider
        {
            public DateTime UtcNow { get; } = utcNow;
        }

        private sealed class RecordingOccupancyReader(Guid? atCapacityPracticeId)
            : IPracticeReservationOccupancyReader
        {
            private Guid[] lastPracticeIds = [];

            public IReadOnlyCollection<Guid> LastPracticeIds => lastPracticeIds;

            public Task<IReadOnlyDictionary<(Guid PracticeId, DateOnly Date), PracticeOccupancySnapshot>> ReadAsync(
                IReadOnlyCollection<Guid> practiceIds,
                DateOnly fromDate,
                DateOnly throughDate,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lastPracticeIds = practiceIds.Distinct().ToArray();
                var date = new DateOnly(2026, 9, 15);
                IReadOnlyDictionary<(Guid, DateOnly), PracticeOccupancySnapshot> result =
                    atCapacityPracticeId.HasValue &&
                    practiceIds.Contains(atCapacityPracticeId.Value) &&
                    date >= fromDate && date <= throughDate
                        ? new Dictionary<(Guid, DateOnly), PracticeOccupancySnapshot>
                        {
                            [(atCapacityPracticeId.Value, date)] = new(
                                new HashSet<TimeOnly>(), 1, new Dictionary<Guid, int>())
                        }
                        : new Dictionary<(Guid, DateOnly), PracticeOccupancySnapshot>();
                return Task.FromResult(result);
            }

            public void Reset() => lastPracticeIds = [];
        }

        private sealed class FixedPopularityReader(IReadOnlyDictionary<Guid, long> scores)
            : IPublicDoctorPopularityReader
        {
            public Task<IReadOnlyDictionary<Guid, long>> ReadSuccessfulReservationCountsAsync(
                IReadOnlyCollection<Guid> doctorIds,
                DateTime sinceUtc,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult<IReadOnlyDictionary<Guid, long>>(
                    doctorIds.Distinct().ToDictionary(id => id, id => scores.GetValueOrDefault(id)));
            }
        }

        private sealed class QueryCountingInterceptor : DbCommandInterceptor
        {
            private readonly ConcurrentQueue<string> executedSql = new();

            public string[] ExecutedSql => executedSql.ToArray();

            public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<DbDataReader> result,
                CancellationToken cancellationToken = default)
            {
                if (command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                {
                    executedSql.Enqueue(command.CommandText);
                }

                return base.ReaderExecutingAsync(
                    command, eventData, result, cancellationToken);
            }

            public void Reset()
            {
                while (executedSql.TryDequeue(out _))
                {
                }
            }
        }
    }
}
