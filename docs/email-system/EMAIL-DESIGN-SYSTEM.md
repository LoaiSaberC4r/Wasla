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
| Event Hero | Event symbol, localized heading and optional English subtitle, semantic tone |
| Details Card | Label/value rows and optional status |
| Action Button | Anchor linked to an approved HTTPS destination |
| OTP | Individual digit cells in logical LTR order, preserving leading zeros |
| Security Alert | OTP, activation, and credential guidance |
| Information Box | Context, privacy guidance, next steps, and permitted review messages |
| Status | Status text with a semantic color |
| Progress Stepper | Three stages for doctor registration review |
| Feature and Steps List | Patient onboarding and doctor setup |
| Language Section | Explicit Arabic RTL and English LTR regions with bilingual divider |

Bilingual rendering uses one header, footer, and event hero. OTPs and action destinations appear once in each representation. Names use the existing Arabic value when an English name is unavailable. Missing optional details are omitted. Appointment times are displayed as stored local date and time. Doctor event and subscription timestamps are labeled UTC. Plain text includes the same content, status, and available action links.
