# Baseline evidence

- Baseline HEAD: feaa8c73f4f2eaed46d38e24e161893fae6909a3 (main). No later commits at task start; worktree clean.
- Branch: codex/family-self-service-historical-documents.
- SDK: .NET 10.0.401; runtime 10.0.12; Windows x64.
- Initial restore failed with MSB4126 because inherited Platform=MCD is not a solution configuration.
- dotnet restore Wasla.sln -p:Platform="Any CPU": passed.
- dotnet build Wasla.sln --no-restore -p:Platform="Any CPU": passed, 0 warnings, 0 errors.
- Baseline solution tests started with --no-build --no-restore and TRX output. Unit: 432 passed; architecture: 26 passed. Integration results are recorded separately once completed. SQL cases skip unless explicitly configured; a skip is not SQL verification.
- baseline-api-inventory.json captures 714 controller route/method attributes; this is a source inventory, not a generated OpenAPI claim.
- baseline-permissions.json captures every pre-task name/index/GUID from the compiled baseline Domain assembly.
- credential-review.json identifies configuration key locations without values. Active status was not probed. Rotation, history review and secure deployment overrides remain external P0 release gates.
- No production/staging database or credentials were queried. No migration or seeding was run against a live environment.
