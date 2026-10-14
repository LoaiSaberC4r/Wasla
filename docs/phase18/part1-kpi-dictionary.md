# Phase 18 KPI dictionary

96 metric definitions generated from `KpiDefinitionRegistry`. The calculator and query source adapters implement these definitions. Counts are not interchangeable with events, cohorts or ratios. Source absence uses null and explicit status. Platform patient/clinical groups below five and complementary slices are suppressed.

Operational dates use each source Practice time-zone snapshot; cash reports use the explicit report zone. AsOfUtc constrains recorded knowledge separately from effective business dates. Previous-period percentage deltas are percentage points; relative deltas are a distinct field. Recompute combined ratios from numerators and denominators. Time capacity and configured daily patient limits remain separate units; unbounded headcount is not fabricated as minutes.

## BookingsCreated

| Attribute | Definition |
|---|---|
| NameAr | الحجوزات المنشأة |
| NameEn | Bookings created |
| Purpose | Bookings created |
| Sources | Reservation.CreatedOnUtc |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Creation date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Distinct ReservationId created in window. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## AppointmentsScheduled

| Attribute | Definition |
|---|---|
| NameAr | المواعيد المجدولة |
| NameEn | Appointments scheduled |
| Purpose | Appointments scheduled |
| Sources | ReservationHistory schedule snapshots |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Effective scheduled local date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Distinct reservations scheduled in window at AsOfUtc. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## ReservationStatusDistribution

| Attribute | Definition |
|---|---|
| NameAr | حالات الحجوزات |
| NameEn | Reservation status distribution |
| Purpose | Reservation status distribution |
| Sources | ReservationHistory |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Scheduled cohort |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Distinct reservations per effective status. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## CancellationsByEvent

| Attribute | Definition |
|---|---|
| NameAr | أحداث الإلغاء |
| NameEn | Cancellation events |
| Purpose | Cancellation events |
| Sources | ReservationHistory.Cancelled |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Event date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Cancellation events in window, with recorded initiator and reason. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## CancellationRateScheduledCohort

| Attribute | Definition |
|---|---|
| NameAr | نسبة إلغاء المواعيد |
| NameEn | Scheduled cancellation rate |
| Purpose | Scheduled cancellation rate |
| Sources | ReservationHistory |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Scheduled cohort |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Cancelled reservations in scheduled cohort. |
| Denominator | All reservations in scheduled cohort. |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 2 eligible successes /4 eligible records =50%; an empty denominator is unavailable. |
| Unit | percent |

## ReservationNoShowRate

| Attribute | Definition |
|---|---|
| NameAr | نسبة عدم حضور الحجز |
| NameEn | Reservation no-show rate |
| Purpose | Reservation no-show rate |
| Sources | ReservationHistory; recorded grace snapshot |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Matured scheduled cohort |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Distinct effective NoShow reservations. |
| Denominator | Scheduled reservations whose grace expired, excluding pre-appointment cancellations. |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Historical grace must be recorded; otherwise NotMeasured. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 2 eligible successes /4 eligible records =50%; an empty denominator is unavailable. |
| Unit | percent |

## TicketNoShowRate

| Attribute | Definition |
|---|---|
| NameAr | نسبة عدم حضور التذكرة |
| NameEn | Ticket no-show rate |
| Purpose | Ticket no-show rate |
| Sources | TicketHistory |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Matured check-in cohort |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Distinct tickets in effective NoShow state. |
| Denominator | Matured checked-in tickets. |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 2 eligible successes /4 eligible records =50%; an empty denominator is unavailable. |
| Unit | percent |

## RescheduleEvents

| Attribute | Definition |
|---|---|
| NameAr | أحداث إعادة الجدولة |
| NameEn | Reschedule events |
| Purpose | Reschedule events |
| Sources | ReservationHistory.Rescheduled |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Event date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Recorded reschedule events. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## RescheduledReservations

| Attribute | Definition |
|---|---|
| NameAr | الحجوزات المعاد جدولتها |
| NameEn | Rescheduled reservations |
| Purpose | Rescheduled reservations |
| Sources | ReservationHistory.Rescheduled |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Event date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Distinct affected ReservationId. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## ReservationToTicketConversion

| Attribute | Definition |
|---|---|
| NameAr | تحويل الحجز إلى تذكرة |
| NameEn | Reservation-to-ticket conversion |
| Purpose | Reservation-to-ticket conversion |
| Sources | ReservationHistory; Ticket.ReservationId |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Matured scheduled cohort |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Distinct eligible reservations linked to checked-in tickets. |
| Denominator | Matured reservations excluding cancellations before appointment. |
| Exclusions | Walk-ins; future appointments; pre-slot cancellations; records after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 2 eligible successes /4 eligible records =50%; an empty denominator is unavailable. |
| Unit | percent |

## BookingLeadTimeMinutes

| Attribute | Definition |
|---|---|
| NameAr | وقت الحجز المسبق |
| NameEn | Original booking lead time |
| Purpose | Original booking lead time |
| Sources | ReservationHistory.Created schedule |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Creation cohort |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Sum of valid original scheduled start minus CreatedOnUtc minutes. |
| Denominator | Valid original creation snapshots. |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 60 total valid interval minutes /4 valid intervals =15 minutes; an empty denominator is unavailable. |
| Unit | minutes |

## PunctualArrival

| Attribute | Definition |
|---|---|
| NameAr | الوصول في الموعد |
| NameEn | Punctual arrival |
| Purpose | Punctual arrival |
| Sources | ReservationHistory; Ticket.CheckInTimeUtc |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Check-in cohort |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Checked-in bookings no more than 5 minutes after last valid schedule before check-in. |
| Denominator | Checked-in bookings with trustworthy pre-check-in schedule. |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 2 eligible successes /4 eligible records =50%; an empty denominator is unavailable. |
| Unit | percent |

## CheckIns

| Attribute | Definition |
|---|---|
| NameAr | الحضور |
| NameEn | Check-ins |
| Purpose | Check-ins |
| Sources | Ticket.CheckInTimeUtc |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Check-in local date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Distinct TicketId. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## WalkIns

| Attribute | Definition |
|---|---|
| NameAr | الحضور دون حجز |
| NameEn | Walk-ins |
| Purpose | Walk-ins |
| Sources | Ticket.Source |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Check-in local date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Walk-in TicketId. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## WalkInShare

| Attribute | Definition |
|---|---|
| NameAr | نسبة الحضور دون حجز |
| NameEn | Walk-in share |
| Purpose | Walk-in share |
| Sources | Ticket.Source |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Check-in local date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Walk-in tickets. |
| Denominator | All checked-in tickets. |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 2 eligible successes /4 eligible records =50%; an empty denominator is unavailable. |
| Unit | percent |

## Waiting

| Attribute | Definition |
|---|---|
| NameAr | المنتظرون |
| NameEn | Waiting |
| Purpose | Waiting |
| Sources | TicketHistory |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Current state at AsOfUtc |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Tickets in Waiting. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## Called

| Attribute | Definition |
|---|---|
| NameAr | المستدعون |
| NameEn | Called |
| Purpose | Called |
| Sources | TicketHistory |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Current state at AsOfUtc |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Tickets in Called. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## InProgress

| Attribute | Definition |
|---|---|
| NameAr | الخدمة الجارية |
| NameEn | In progress |
| Purpose | In progress |
| Sources | TicketHistory |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Current state at AsOfUtc |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Tickets in InProgress. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## TicketsCompleted

| Attribute | Definition |
|---|---|
| NameAr | التذاكر المكتملة |
| NameEn | Operational completions |
| Purpose | Operational completions |
| Sources | TicketHistory.Completed |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Completion event date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Distinct completed operational TicketId; not clinical encounter completion. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## TicketsCancelled

| Attribute | Definition |
|---|---|
| NameAr | التذاكر الملغاة |
| NameEn | Cancelled tickets |
| Purpose | Cancelled tickets |
| Sources | TicketHistory |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Check-in cohort |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Effective Cancelled TicketId. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## TicketsNoShow

| Attribute | Definition |
|---|---|
| NameAr | تذاكر عدم الحضور |
| NameEn | No-show tickets |
| Purpose | No-show tickets |
| Sources | TicketHistory |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Check-in cohort |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Effective NoShow TicketId. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## WaitToFirstCall

| Attribute | Definition |
|---|---|
| NameAr | الانتظار لأول نداء |
| NameEn | Wait to first call |
| Purpose | Wait to first call |
| Sources | TicketHistory; TicketCallAttempt |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Check-in cohort |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Sum of first valid call minus check-in minutes. |
| Denominator | Tickets with valid non-negative call intervals. |
| Exclusions | Missing or invalid call timestamps; show excluded count. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 60 total valid interval minutes /4 valid intervals =15 minutes; an empty denominator is unavailable. |
| Unit | minutes |

## WaitToServiceStart

| Attribute | Definition |
|---|---|
| NameAr | الانتظار لبدء الخدمة |
| NameEn | Wait to service start |
| Purpose | Wait to service start |
| Sources | TicketHistory.InProgress |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Check-in cohort |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Sum of first valid InProgress minus check-in minutes. |
| Denominator | Tickets with valid start timestamps. |
| Exclusions | Missing/invalid start timestamps. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 60 total valid interval minutes /4 valid intervals =15 minutes; an empty denominator is unavailable. |
| Unit | minutes |

## CallToStart

| Attribute | Definition |
|---|---|
| NameAr | النداء إلى بدء الخدمة |
| NameEn | Call to service start |
| Purpose | Call to service start |
| Sources | TicketHistory |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Check-in cohort |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Sum of valid start minus immediately preceding effective call minutes. |
| Denominator | Valid call/start pairs. |
| Exclusions | Missing or invalid call/start pairs. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 60 total valid interval minutes /4 valid intervals =15 minutes; an empty denominator is unavailable. |
| Unit | minutes |

## ServiceDuration

| Attribute | Definition |
|---|---|
| NameAr | مدة الخدمة التشغيلية |
| NameEn | Operational service duration |
| Purpose | Operational service duration |
| Sources | TicketHistory.InProgress/Completed |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Completion cohort |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Sum of completion minus preceding InProgress start per valid service cycle. |
| Denominator | Valid operational service cycles. |
| Exclusions | Missing, reversed, invalid timestamps; no clinical duration inference. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 60 total valid interval minutes /4 valid intervals =15 minutes; an empty denominator is unavailable. |
| Unit | minutes |

## CallAttempts

| Attribute | Definition |
|---|---|
| NameAr | محاولات النداء |
| NameEn | Call attempts |
| Purpose | Call attempts |
| Sources | TicketHistory.Called/Recalled/ManualCalled |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Event date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Recorded call events. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## Recalls

| Attribute | Definition |
|---|---|
| NameAr | إعادة النداء |
| NameEn | Recalls |
| Purpose | Recalls |
| Sources | TicketHistory.Recalled |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Event date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Recorded recall events. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## OperationalExceptions

| Attribute | Definition |
|---|---|
| NameAr | الاستثناءات التشغيلية |
| NameEn | Operational exceptions |
| Purpose | Operational exceptions |
| Sources | TicketHistory force/manual/no-response/day-ended |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Event date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Recorded exceptional operational events. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## ReceptionActivity

| Attribute | Definition |
|---|---|
| NameAr | نشاط الاستقبال |
| NameEn | Reception activity |
| Purpose | Reception activity |
| Sources | ReservationHistory; TicketHistory; Reception.ApplicationUserId |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Event date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Recorded operational events by Reception actors. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## CapacityMinutes

| Attribute | Definition |
|---|---|
| NameAr | الدقائق المتاحة |
| NameEn | Effective capacity minutes |
| Purpose | Effective capacity minutes |
| Sources | Immutable schedule snapshots |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Scheduled local date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Union of effective availability intervals after exceptions. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | No historical schedule snapshot: NotMeasured. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | Availability08:00-09:00 and08:30-09:30 has90 union minutes, not120. |
| Unit | minutes |

## UnusedCapacityMinutes

| Attribute | Definition |
|---|---|
| NameAr | دقائق المواعيد غير المستخدمة |
| NameEn | Unused booking capacity minutes |
| Purpose | Unused booking capacity minutes |
| Sources | Immutable schedule snapshots; reservation interval snapshots |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Scheduled local date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Effective available minutes minus union of booked minutes intersecting availability. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Historical effective schedules required; time capacity is separate from headcount constraints. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 90 available union minutes -30 booked union minutes =60 unused minutes. |
| Unit | minutes |

## MaximumDailyPatientCapacity

| Attribute | Definition |
|---|---|
| NameAr | حد المرضى اليومي المجمع |
| NameEn | Configured daily patient capacity |
| Purpose | Configured daily patient capacity |
| Sources | Immutable MaximumDailyPatients policy snapshots |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Working local days |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Sum of configured daily patient limits for active working days. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Unbounded limits or intraday policy changes are NotMeasured; never translate headcount into invented minutes. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | Two working days with recorded limits30 each contribute60 patient places; an unbounded limit remains unmeasured. |
| Unit | count |

## BookedUtilization

| Attribute | Definition |
|---|---|
| NameAr | استغلال المواعيد |
| NameEn | Booked utilization |
| Purpose | Booked utilization |
| Sources | Immutable schedule snapshots; booking intervals |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Scheduled local date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Union of booked intervals intersecting availability. |
| Denominator | Effective available minutes. |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Historical effective schedule snapshots required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 2 eligible successes /4 eligible records =50%; an empty denominator is unavailable. |
| Unit | percent |

## ActualServiceUtilization

| Attribute | Definition |
|---|---|
| NameAr | استغلال الخدمة |
| NameEn | Actual service utilization |
| Purpose | Actual service utilization |
| Sources | Immutable schedule snapshots; TicketHistory |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Scheduled local date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Union of valid service intervals intersecting availability. |
| Denominator | Effective available minutes. |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Historical effective schedule snapshots required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 2 eligible successes /4 eligible records =50%; an empty denominator is unavailable. |
| Unit | percent |

## SegmentQuotaUtilization

| Attribute | Definition |
|---|---|
| NameAr | استغلال حصة الشريحة |
| NameEn | Segment quota utilization |
| Purpose | Segment quota utilization |
| Sources | Historical quota snapshots; recorded consumption |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Scheduled local date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Recorded quota consumption. |
| Denominator | Effective historical quota. |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Absent historical quota: NotMeasured. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 2 eligible successes /4 eligible records =50%; an empty denominator is unavailable. |
| Unit | percent |

## UniqueDoctorPatients

| Attribute | Definition |
|---|---|
| NameAr | المرضى المميزون |
| NameEn | Distinct Doctor patients |
| Purpose | Distinct Doctor patients |
| Sources | Ticket.PatientId |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Check-in cohort |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Distinct global PatientId in Doctor scope. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## NewToDoctorPatients

| Attribute | Definition |
|---|---|
| NameAr | مرضى جدد للطبيب |
| NameEn | New-to-Doctor patients |
| Purpose | New-to-Doctor patients |
| Sources | Ticket history for owning Doctor |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Check-in cohort |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Patients whose earliest known Doctor check-in is in window. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## ReturningDoctorPatients

| Attribute | Definition |
|---|---|
| NameAr | المرضى العائدون |
| NameEn | Returning Doctor patients |
| Purpose | Returning Doctor patients |
| Sources | Ticket history for owning Doctor |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Check-in cohort |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Patients in selected period with an earlier known check-in with same Doctor. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## PatientRetention90d

| Attribute | Definition |
|---|---|
| NameAr | الاحتفاظ بالمرضى ٩٠ يومًا |
| NameEn | 90-day patient retention |
| Purpose | 90-day patient retention |
| Sources | Ticket.PatientId |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | 90 days preceding selected period |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Patients from prior 90-day checked-in cohort returning in selected period. |
| Denominator | Distinct patients in prior 90-day cohort. |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 2 eligible successes /4 eligible records =50%; an empty denominator is unavailable. |
| Unit | percent |

## FollowUpActivity

| Attribute | Definition |
|---|---|
| NameAr | زيارات المتابعة |
| NameEn | Follow-up activity |
| Purpose | Follow-up activity |
| Sources | Ticket.VisitTypeCodeSnapshot |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Check-in cohort |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | FollowUp TicketId; never a Segment. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## ClinicalWorkflowCompleted

| Attribute | Definition |
|---|---|
| NameAr | السجلات السريرية المكتملة |
| NameEn | Clinical workflow completion |
| Purpose | Clinical workflow completion |
| Sources | MedicalEncounter.CompletedAtUtc |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Clinical completion date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Completed permitted clinical records; independent of Ticket.Completed. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## GrossCollected

| Attribute | Definition |
|---|---|
| NameAr | المتحصلات الإجمالية |
| NameEn | Gross collected |
| Purpose | Gross collected |
| Sources | Committed Payment |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Financial effective date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Sum of committed patient payments. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | EGP300.00 + EGP200.00 collections = EGP500.00 gross. |
| Unit | EGP |

## Refunds

| Attribute | Definition |
|---|---|
| NameAr | المبالغ المستردة |
| NameEn | Refunds |
| Purpose | Refunds |
| Sources | Committed Refund |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Financial effective date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Sum of committed patient refunds. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | EGP50.00 + EGP25.00 refunds = EGP75.00. |
| Unit | EGP |

## NetCollected

| Attribute | Definition |
|---|---|
| NameAr | صافي المتحصلات |
| NameEn | Net collected |
| Purpose | Net collected |
| Sources | Committed Payment/Refund |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Financial effective date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | GrossCollected - Refunds. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | EGP500.00 gross - EGP75.00 refunds = EGP425.00 net. |
| Unit | EGP |

## PaidOperatingExpenses

| Attribute | Definition |
|---|---|
| NameAr | المصروفات التشغيلية المدفوعة |
| NameEn | Paid operating expenses |
| Purpose | Paid operating expenses |
| Sources | Operating settlement/reversal allocation snapshots |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Effective paid/reversal business date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Full salary and expense settlements minus compensating reversal movements. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | EGP500.00 settlements - EGP100.00 reversals = EGP400.00 outgoing cash. |
| Unit | EGP |

## OutstandingDueInPeriod

| Attribute | Definition |
|---|---|
| NameAr | الالتزامات المستحقة بالفترة |
| NameEn | Outstanding due in period |
| Purpose | Outstanding due in period |
| Sources | Obligation revisions; settlement/reversal history |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Due business date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Unsettled obligations due within period as known at AsOfUtc. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | Eligible immutable allocated liabilities EGP200.00 + EGP100.00 = EGP300.00; each metric applies its own due-date scope. |
| Unit | EGP |

## AllOutstandingAsOf

| Attribute | Definition |
|---|---|
| NameAr | كل الالتزامات غير المسددة |
| NameEn | All outstanding as of |
| Purpose | All outstanding as of |
| Sources | Obligation revisions; settlement/reversal history |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | AsOfUtc |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | All existing outstanding obligations, independent of reporting month. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | Eligible immutable allocated liabilities EGP200.00 + EGP100.00 = EGP300.00; each metric applies its own due-date scope. |
| Unit | EGP |

## OverdueAsOf

| Attribute | Definition |
|---|---|
| NameAr | المستحقات المتأخرة |
| NameEn | Overdue as of |
| Purpose | Overdue as of |
| Sources | Obligation revisions; settlement/reversal history |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Due business date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Outstanding obligations whose due date precedes local AsOf date. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | Eligible immutable allocated liabilities EGP200.00 + EGP100.00 = EGP300.00; each metric applies its own due-date scope. |
| Unit | EGP |

## NetOperatingCashResult

| Attribute | Definition |
|---|---|
| NameAr | نتيجة النقد التشغيلي |
| NameEn | Net operating cash result |
| Purpose | Net operating cash result |
| Sources | Patient collections; operating settlements/reversals |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Financial effective date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | NetCollected - PaidOperatingExpenses; not accounting profit/bank balance. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | EGP425.00 net collections - EGP200.00 outgoing cash = EGP225.00. |
| Unit | EGP |

## ProjectedResultForPeriod

| Attribute | Definition |
|---|---|
| NameAr | النتيجة التشغيلية المتوقعة |
| NameEn | Projected operating result |
| Purpose | Projected operating result |
| Sources | Collections; operating obligations/movements |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Financial effective/due dates |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | NetCollected - PaidOperatingExpenses - OutstandingDueInPeriod. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | EGP225.00 net operating cash - EGP100.00 unpaid due-in-period = EGP125.00. |
| Unit | EGP |

## PlatformActiveDoctor30d

| Attribute | Definition |
|---|---|
| NameAr | الأطباء النشطون ٣٠ يومًا |
| NameEn | Active Doctors in 30 days |
| Purpose | Active Doctors in 30 days |
| Sources | DoctorStatusHistory; Reservation; Ticket |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Trailing 30 days at AsOfUtc |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Approved Doctors with qualifying booking creation or check-in. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## PlatformActivePatient30d

| Attribute | Definition |
|---|---|
| NameAr | المرضى النشطون ٣٠ يومًا |
| NameEn | Active patients in 30 days |
| Purpose | Active patients in 30 days |
| Sources | Reservation; Ticket |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Trailing 30 days at AsOfUtc |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Distinct patients with qualifying booking or check-in. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## FeatureAdoption

| Attribute | Definition |
|---|---|
| NameAr | استخدام الميزة |
| NameEn | Feature adoption |
| Purpose | Feature adoption |
| Sources | Recorded feature transactions |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Activity window |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Distinct source-backed eligible feature users. |
| Denominator | Eligible actor cohort. |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Unrecorded page views/searches: NotInstrumented. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 2 eligible successes /4 eligible records =50%; an empty denominator is unavailable. |
| Unit | percent |

## RegisteredDoctors

| Attribute | Definition |
|---|---|
| NameAr | الأطباء المسجلون |
| NameEn | Registered Doctors |
| Purpose | Registered Doctors |
| Sources | Doctor.CreatedOnUtc |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | At AsOfUtc |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Distinct registered Doctors. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## PendingDoctors

| Attribute | Definition |
|---|---|
| NameAr | الأطباء قيد المراجعة |
| NameEn | Pending Doctors |
| Purpose | Pending Doctors |
| Sources | DoctorStatusHistory |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | At AsOfUtc |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Doctors pending approval. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## ApprovedDoctors

| Attribute | Definition |
|---|---|
| NameAr | الأطباء المعتمدون |
| NameEn | Approved Doctors |
| Purpose | Approved Doctors |
| Sources | DoctorStatusHistory |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | At AsOfUtc |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Doctors approved; does not imply bookable. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## RejectedDoctors

| Attribute | Definition |
|---|---|
| NameAr | الأطباء المرفوضون |
| NameEn | Rejected Doctors |
| Purpose | Rejected Doctors |
| Sources | DoctorStatusHistory |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | At AsOfUtc |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Doctors rejected. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## SuspendedDoctors

| Attribute | Definition |
|---|---|
| NameAr | الأطباء الموقوفون |
| NameEn | Suspended Doctors |
| Purpose | Suspended Doctors |
| Sources | DoctorStatusHistory |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | At AsOfUtc |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Doctors suspended. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## ActivePractices

| Attribute | Definition |
|---|---|
| NameAr | العيادات النشطة |
| NameEn | Active practices |
| Purpose | Active practices |
| Sources | Practice current lifecycle |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Current state |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Active practices; separate from availability. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Historical lifecycle unavailable: NotMeasured. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## RegisteredPatients

| Attribute | Definition |
|---|---|
| NameAr | المرضى المسجلون |
| NameEn | Registered patients |
| Purpose | Registered patients |
| Sources | Patient.CreatedOnUtc |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | At AsOfUtc |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Distinct global patients. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## ReceptionAccounts

| Attribute | Definition |
|---|---|
| NameAr | حسابات الاستقبال |
| NameEn | Reception accounts |
| Purpose | Reception accounts |
| Sources | Reception.CreatedOnUtc |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | At AsOfUtc |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Distinct Reception accounts. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## ApprovalWaitMinutes

| Attribute | Definition |
|---|---|
| NameAr | الانتظار للموافقة |
| NameEn | Approval waiting time |
| Purpose | Approval waiting time |
| Sources | Doctor.CreatedOnUtc; DoctorStatusHistory |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Registration cohort |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Sum of registration-to-first-approval duration. |
| Denominator | Doctors with trustworthy approval timestamps. |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 60 total valid interval minutes /4 valid intervals =15 minutes; an empty denominator is unavailable. |
| Unit | minutes |

## DoctorAcquisition

| Attribute | Definition |
|---|---|
| NameAr | اكتساب الأطباء |
| NameEn | Doctor acquisition |
| Purpose | Doctor acquisition |
| Sources | Doctor.CreatedOnUtc |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Creation date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Distinct newly registered Doctors. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## PatientAcquisition

| Attribute | Definition |
|---|---|
| NameAr | اكتساب المرضى |
| NameEn | Patient acquisition |
| Purpose | Patient acquisition |
| Sources | Patient.CreatedOnUtc |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Creation date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Distinct newly registered patients. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## DoctorRetention

| Attribute | Definition |
|---|---|
| NameAr | استمرار الأطباء |
| NameEn | Doctor activity retention |
| Purpose | Doctor activity retention |
| Sources | Reservation/Ticket activity |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Previous/current activity windows |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Previously active Doctors also active now. |
| Denominator | Distinct Doctors active in preceding equal window. |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 2 eligible successes /4 eligible records =50%; an empty denominator is unavailable. |
| Unit | percent |

## PatientRetention

| Attribute | Definition |
|---|---|
| NameAr | استمرار المرضى |
| NameEn | Patient activity retention |
| Purpose | Patient activity retention |
| Sources | Reservation/Ticket activity |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Previous/current activity windows |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Previously active patients also active now. |
| Denominator | Distinct patients active in preceding equal window. |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 2 eligible successes /4 eligible records =50%; an empty denominator is unavailable. |
| Unit | percent |

## OpenHandovers

| Attribute | Definition |
|---|---|
| NameAr | التسليمات المفتوحة |
| NameEn | Open handovers |
| Purpose | Open handovers |
| Sources | HandoverHistory |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | At AsOfUtc |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Nonclosed Practice-scoped handovers. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## HandoversCreated

| Attribute | Definition |
|---|---|
| NameAr | التسليمات المنشأة |
| NameEn | Handovers created |
| Purpose | Handovers created |
| Sources | HandoverHistory.Created |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Recorded event date in report zone |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Recorded creation events in permitted Practices. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## HandoverAcknowledgements

| Attribute | Definition |
|---|---|
| NameAr | إقرارات استلام التسليم |
| NameEn | Handover acknowledgements |
| Purpose | Handover acknowledgements |
| Sources | HandoverHistory.Acknowledged |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Recorded event date in report zone |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Recorded acknowledgement events. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## HandoversClosed

| Attribute | Definition |
|---|---|
| NameAr | التسليمات المغلقة |
| NameEn | Handovers closed |
| Purpose | Handovers closed |
| Sources | HandoverHistory.Closed/DoctorDirectClosure |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Recorded event date in report zone |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Recorded terminal closure events. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## OpenInvestigations

| Attribute | Definition |
|---|---|
| NameAr | التحقيقات المفتوحة |
| NameEn | Open investigations |
| Purpose | Open investigations |
| Sources | InvestigationHistory |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | At AsOfUtc |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Nonclosed administrative cases. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## InvestigationResolutions

| Attribute | Definition |
|---|---|
| NameAr | التحقيقات المحلولة |
| NameEn | Investigation resolutions |
| Purpose | Investigation resolutions |
| Sources | InvestigationHistory.Resolve |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Event date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Recorded resolution transitions. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## CurrentBookableDoctors

| Attribute | Definition |
|---|---|
| NameAr | أطباء لديهم مواعيد متاحة مسجلة |
| NameEn | Doctors with recorded bookable slots |
| Purpose | Doctors with recorded bookable slots |
| Sources | Current public availability projection + current configuration |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Current observed projection |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Distinct approved active Doctors with visible available future slots and active normal consultation pricing. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## PublicEligibleDoctors

| Attribute | Definition |
|---|---|
| NameAr | أطباء مؤهلون للظهور |
| NameEn | Public eligible Doctors |
| Purpose | Public eligible Doctors |
| Sources | Doctor + active account + active practice |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Current source |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Distinct approved Doctors with active account and at least one active practice. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## PendingApprovalAgeMinutes

| Attribute | Definition |
|---|---|
| NameAr | عمر انتظار الاعتماد |
| NameEn | Pending approval age |
| Purpose | Pending approval age |
| Sources | Doctor.CreatedOnUtc + current pending status |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Current pending cohort |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Sum current registration-to-now minutes |
| Denominator | Pending Doctors with trustworthy creation timestamps |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 60 total valid interval minutes /4 valid intervals =15 minutes; an empty denominator is unavailable. |
| Unit | minutes |

## VerificationCompleteDoctors

| Attribute | Definition |
|---|---|
| NameAr | مكتملو مستندات التحقق |
| NameEn | Doctors with verification documents |
| Purpose | Doctors with verification documents |
| Sources | Doctor required document-presence metadata |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Current metadata |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Doctors with front/back identity and syndicate front document keys; contents are excluded. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## DoctorSuspensions

| Attribute | Definition |
|---|---|
| NameAr | إيقاف الأطباء |
| NameEn | Doctor suspensions |
| Purpose | Doctor suspensions |
| Sources | DoctorStatusHistory |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Status event date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Recorded transitions to Suspended. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## DoctorReactivations

| Attribute | Definition |
|---|---|
| NameAr | إعادة تفعيل الأطباء |
| NameEn | Doctor reactivations |
| Purpose | Doctor reactivations |
| Sources | DoctorStatusHistory |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Status event date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Recorded Suspended to Approved transitions. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## SpecializationApprovals

| Attribute | Definition |
|---|---|
| NameAr | اعتماد التخصصات |
| NameEn | Specialization approvals |
| Purpose | Specialization approvals |
| Sources | DoctorSpecializationRequestHistory |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Decision event date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Recorded specialization approval events. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## SpecializationReviewMinutes

| Attribute | Definition |
|---|---|
| NameAr | زمن مراجعة التخصص |
| NameEn | Specialization review time |
| Purpose | Specialization review time |
| Sources | Specialization submission/review timestamps |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Reviewed decision cohort |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Sum valid submission-to-review minutes |
| Denominator | Requests with trustworthy reviewed/submitted timestamps |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 60 total valid interval minutes /4 valid intervals =15 minutes; an empty denominator is unavailable. |
| Unit | minutes |

## ActiveUserAccounts

| Attribute | Definition |
|---|---|
| NameAr | الحسابات النشطة |
| NameEn | Active user accounts |
| Purpose | Active user accounts |
| Sources | ApplicationUser.IsActive |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Current state |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Distinct active accounts. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## InactiveUserAccounts

| Attribute | Definition |
|---|---|
| NameAr | الحسابات غير النشطة |
| NameEn | Inactive user accounts |
| Purpose | Inactive user accounts |
| Sources | ApplicationUser.IsActive |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Current state |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Distinct inactive accounts. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## FirstLoginAccounts

| Attribute | Definition |
|---|---|
| NameAr | حسابات الدخول الأول |
| NameEn | First-login accounts |
| Purpose | First-login accounts |
| Sources | ApplicationUser.IsFirstLogin |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Current state |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Distinct accounts requiring first-login completion. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## RoleAssignments

| Attribute | Definition |
|---|---|
| NameAr | إسناد الأدوار |
| NameEn | Role assignments |
| Purpose | Role assignments |
| Sources | UserRole |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Current governance |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Distinct role assignment rows. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## PermissionGrants

| Attribute | Definition |
|---|---|
| NameAr | منح الصلاحيات |
| NameEn | Permission grants |
| Purpose | Permission grants |
| Sources | RolePermission |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Current governance |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Distinct role permission grants. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## AssignedPracticePermissionGrants

| Attribute | Definition |
|---|---|
| NameAr | الصلاحيات المفوضة للعيادات |
| NameEn | Delegated practice permission grants |
| Purpose | Delegated practice permission grants |
| Sources | Active Reception assignment permissions |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Current governance |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Delegated permission rows on active assignments. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## SuperAdminAccounts

| Attribute | Definition |
|---|---|
| NameAr | حسابات الإدارة |
| NameEn | SuperAdmin accounts |
| Purpose | SuperAdmin accounts |
| Sources | SuperAdmin.IsDeleted |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Current governance |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Nondeleted SuperAdmin identities; founder policy remains on existing commands. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## PasswordResetChallenges

| Attribute | Definition |
|---|---|
| NameAr | طلبات إعادة تعيين كلمة المرور |
| NameEn | Password reset challenges |
| Purpose | Password reset challenges |
| Sources | PasswordResetChallenge.CreatedOnUtc |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Creation event date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Recorded challenge creations; no OTP, hash or token metadata. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## PasswordResetConsumptions

| Attribute | Definition |
|---|---|
| NameAr | إتمام إعادة تعيين كلمة المرور |
| NameEn | Password reset completions |
| Purpose | Password reset completions |
| Sources | PasswordResetChallenge.ConsumedOnUtc |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Completion event date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Recorded reset consumption events. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## SecurityLoginEvents

| Attribute | Definition |
|---|---|
| NameAr | أحداث تسجيل الدخول |
| NameEn | Login security events |
| Purpose | Login security events |
| Sources | No persisted login-event store |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Activity date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | NotInstrumented; no inferred authentication activity. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## PageViewAdoption

| Attribute | Definition |
|---|---|
| NameAr | استخدام الصفحات |
| NameEn | Page view adoption |
| Purpose | Page view adoption |
| Sources | No persisted page-view telemetry |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Activity date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | NotInstrumented; no inferred page views. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## SearchAdoption

| Attribute | Definition |
|---|---|
| NameAr | استخدام البحث |
| NameEn | Search adoption |
| Purpose | Search adoption |
| Sources | No persisted search telemetry |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Activity date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | NotInstrumented; no inferred search requests. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## PracticeCoverage

| Attribute | Definition |
|---|---|
| NameAr | تغطية العيادات |
| NameEn | Practice coverage |
| Purpose | Practice coverage |
| Sources | DoctorPractice geography keys |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Current state |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Distinct Practices in geographic group. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## SpecializationCoverage

| Attribute | Definition |
|---|---|
| NameAr | تغطية التخصصات |
| NameEn | Specialization coverage |
| Purpose | Specialization coverage |
| Sources | Approved DoctorSpecialization selections |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Current state |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Distinct approved Doctors associated with specialization. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## FeatureBookingDoctors

| Attribute | Definition |
|---|---|
| NameAr | أطباء يستخدمون الحجز |
| NameEn | Doctors using booking |
| Purpose | Doctors using booking |
| Sources | Reservation.CreatedOnUtc |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Activity date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Distinct Doctors with recorded created bookings. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |

## FeatureQueueDoctors

| Attribute | Definition |
|---|---|
| NameAr | أطباء يستخدمون الطابور |
| NameEn | Doctors using queue |
| Purpose | Doctors using queue |
| Sources | Ticket.CheckInTimeUtc |
| ActorScope | Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only. |
| DateBasis | Activity date |
| EligibleStates | Reconstructed state at AsOfUtc; recorded transactions only. |
| Numerator | Distinct Doctors with recorded check-ins. |
| Denominator | Not applicable |
| Exclusions | Records recorded after AsOfUtc. |
| TimeZoneBasis | Practice-local labeled dates for operations; explicit report zone for financial movements. |
| Completeness | Persisted authoritative source records required. |
| AsOfBehavior | Ignore events/movements recorded after AsOfUtc; do not infer missing history. |
| Example | 3 distinct eligible source IDs contribute3; an empty complete cohort is0. |
| Unit | count |
