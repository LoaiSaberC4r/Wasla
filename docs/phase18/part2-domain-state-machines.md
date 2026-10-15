# State and liveness

Ticket business states remain Waiting, Called, InProgress, NoShow, Completed, Cancelled. Reservation states remain Active, Cancelled, NoShow, Expired, ConvertedToTicket. Archival is a separate physical location and adds no business status.

Observed operational day → versioned Open → verified end reached → cancel Waiting/Called and expire Active Reservations in bounded transactions → finalize NoShow liveness → Closed, immutable. InProgress is untouched and can complete after closure; terminal siblings need not wait for it. A pre-close legitimate schedule/timezone edit revises evidence and audit. Missing/invalid effective evidence is held, never replaced with arbitrary midnight or today's historical schedule.

Ticket Completed/Cancelled/closed NoShow + integrity validation → atomic move. Linked ConvertedToTicket Reservation, all original histories/attempts, registry location/sequence, successful counts/checkpoints and new safe audit commit together. Failure → rollback and deduplicated integrity metadata. Standalone Cancelled/Expired/NoShow Reservation moves only without any lifetime Ticket link. Active and InProgress never move.

The old filtered unique `UX_Tickets_OpenPatientPractice` included every NoShow indefinitely. That conflicts with a closed NoShow remaining NoShow while a returning Patient may enter a new visit, particularly with archival paused. `IsOperationallyOpen` and `OperationalFinalizedAtUtc` are liveness metadata: Waiting/Called/InProgress occupy the lease; same-day restorable NoShow occupies it; verified closed NoShow releases it. Business status, NoShow timestamp, number, Payment and old histories do not change. A new immutable metadata event records finalization; no cancellation/refund is synthesized. The unique index now enforces this stored lease. Aggregate restore/cancel and application closure guards prevent reopening/reclassifying a finalized NoShow.

No retention clock or disposal transition exists. Archived operational capabilities are false. Independent Refund/clinical permissions and workflows remain separate.
