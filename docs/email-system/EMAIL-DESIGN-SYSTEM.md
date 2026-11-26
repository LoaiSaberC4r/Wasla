# Email design system

The shared HTML layout uses a centered white, fluid-width presentation table capped at 640 px, a 24 px content inset, 16 px component spacing, Tahoma/Arial, and inline styles. Outlook desktop receives a fixed-width conditional wrapper. Headers use a 4 px primary separator and footers have a 3 px primary separator.

| Purpose | Default |
|---|---|
| Primary, light, surface, border | `#087F75`, `#E8F5F2`, `#F0F8F6`, `#CCE7DF` |
| Heading, text, muted | `#172B36`, `#334155`, `#64748B` |
| Outer, main, secondary, border | `#EDF2F4`, `#FFFFFF`, `#F8FAFC`, `#E2E8F0` |
| Success text and surface | `#166534`, `#DCFCE7` |
| Warning accent, text, surface | `#B45309`, `#92400E`, `#FFFBEB` |
| Danger text and surface | `#B91C1C`, `#FFF1F2` |
| Information text and surface | `#1E40AF`, `#EFF6FF` |

| Component | Use |
|---|---|
| Header | Optional logo, localized Wasla branding and tagline |
| Footer | Allowed destinations, disclaimer, and current year from the application clock |
| Event Hero | 36 px outline icon in a 72 px semantic circle, localized heading and optional English subtitle |
| Details Card | Label/value rows and optional status |
| Action Button | Single anchor linked to an approved HTTPS destination, placed by template |
| OTP | Individual digit cells in logical LTR order, preserving leading zeros |
| Security Alert | OTP, activation, and credential guidance |
| Information Box | Context, privacy guidance, next steps, and permitted review messages |
| Status | Status text with a semantic color |
| Progress Stepper | Connected registration stages with primary completed, amber current, and gray pending indicators and text |
| Feature and Steps List | Directional feature rows with 22 px icons, localized titles and descriptions, and optional doctor setup numbering |
| Language Section | Explicit Arabic RTL and English LTR regions with bilingual divider |

Bilingual rendering uses one header, footer, and event hero. The action appears once in the Arabic section with an Arabic primary label and English secondary label. Single-language messages place it at the same point in their own section. Plain text uses the same order, including the single activation token or link and a final shared footer.

The transparent 96 px PNGs are rasterized from the Lucide 0.468.0 outline icons in the approved semantic tones. The icon identifiers come from the catalog and resolve to the bundled assets only. The package's ISC and Feather MIT attribution ships alongside the files. [Lucide license](https://lucide.dev/license)

Reservation dates and 12-hour times use the stored local appointment value. Doctor and subscription dates convert from UTC using the configured IANA or Windows timezone and daylight-saving rules. Missing or invalid email timezone configuration displays UTC explicitly. Remaining time is calculated from the original UTC instants and uses localized singular and plural labels.
