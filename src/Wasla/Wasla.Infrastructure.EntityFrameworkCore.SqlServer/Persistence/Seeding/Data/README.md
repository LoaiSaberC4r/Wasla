# Egypt location seed data

## Purpose

These UTF-8 JSON files are the frozen, runtime-independent seed catalog for
Wasla's three-level Egypt location hierarchy. They contain names and
relationships only; coordinates, polygons, postal codes, streets, and buildings
are intentionally excluded.

## Source and version

- Dataset: [Egypt Administrative Divisions Dataset](https://github.com/open-admin-data/egypt-administrative-divisions)
- Publisher: Open Admin Data
- Dataset release: `2026.06` (`2026-06-01`)
- Frozen source commit: `c6fb904e0c58cb6ab0421fe03e06d5f0924575eb`
- License: CC BY 4.0
- Upstream counts: 27 governorates, 365 districts, and 5,716 shiyakhas

The application never downloads this data at runtime. The three JSON files are
embedded in `Wasla.Infrastructure.EntityFrameworkCore.SqlServer.dll` and loaded with
`System.Text.Json`.

## Normalization

The source hierarchy is mapped as follows:

| Source level | Wasla level | Included units |
| --- | --- | --- |
| Governorate | `Governorate` | Egyptian governorates |
| District | `City` | Markaz, Qism, and City units |
| Shiyakha | `Area` | Villages, sheyakhas, and sub-district units |

Fourteen source district records named `Zemam Out` (`EG0100`, `EG0200`,
`EG1300`, `EG1400`, `EG1800`, `EG2100`, `EG2200`, `EG2300`, `EG2400`,
`EG2500`, `EG2600`, `EG2700`, `EG2800`, and `EG2900`) are boundary
placeholders, have no shiyakha children, and are not Markaz/Qism/City units.
They are therefore excluded. All 5,716 source shiyakhas are retained.

Where a Qism and Markaz under the same governorate shared the same upstream
English label, the explicit suffix `(Qism)` or `(Markaz)` was added to both
English names. This affects 36 city records and prevents ambiguous approved
names without changing the Arabic administrative names. No name is generated
or translated at runtime.

The normalized checked-in counts are:

| File | Records |
| --- | ---: |
| `egypt-governorates.v1.json` | 27 |
| `egypt-cities.v1.json` | 351 |
| `egypt-areas.v1.json` | 5,716 |

## Stable identifiers and display order

Source records are ordered by their numeric Open Admin Data code before IDs are
assigned. The resulting IDs and display orders are stored explicitly in JSON;
the application does not calculate them from collection enumeration.

- Governorate IDs are `1` through `27` in source-code order.
- City IDs are `GovernorateId * 1000 + LocalCitySequence`.
- Area IDs are `CityId * 10000 + LocalAreaSequence`.
- Local sequences and `DisplayOrder` start at 1 within each parent and follow
  source-code order.
- The highest v1 Area ID is `270080001`, within the signed 32-bit integer range.

Once any version is deployed to production, its IDs must never be renumbered or
reused. New records must receive new local sequences; removed or renamed source
records require an explicit compatibility decision rather than reordering
existing records.

## Reviewing future updates

Updates require a new reviewed dataset version. Reviewers must pin and record
the upstream release and commit, retain CC BY attribution, compare additions,
removals, moves, and spelling changes, and explicitly approve every parent or
name change. Existing IDs must be carried forward unchanged. The catalog and
seeding tests must pass with updated asserted counts before deployment. Never
replace these files from an unpinned live API response.
