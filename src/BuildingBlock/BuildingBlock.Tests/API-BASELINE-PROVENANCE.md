# Frozen main public API compatibility reference

The five `PublicAPI.Shipped.txt` files were captured independently from the clean
pre-change `main` revision `17c9e0b96af87de0143ca4ac4978bc970ca6f9dd`, built in Release
with .NET SDK 10.0.401 during the task's initial baseline run. They were not
generated from the Phase 5 branch's assemblies. Each file records the original
assembly SHA256. The earlier source export omitted its API baseline files; these
are newly established references to the authoritative starting revision, not a
claim to have recovered missing historical files.

Capture used `PublicApiSignatures.Read(assembly)` against those five independently
built assemblies. The source library implementations are unchanged from main.
The current compatibility test compares every frozen exported type and declared
public constructor, method/operator, property, field and event with the current
assembly. It retains the removed-API-fragment assertions and empty Unshipped rule.
This strengthens the earlier test, which checked exported type presence only.
Signatures are sorted ordinally and do not depend on file paths or user locale.

To reproduce, archive the specified Git revision into a separate directory,
build its BuildingBlock test project in Release, and load its library assemblies
in a separate process using that project's dependency resolver. Run the formatter
against those assemblies, retaining their hashes and revision in each file header.
Do not regenerate from the code being tested to conceal an API change. Future API
changes require an explicit reviewed baseline update. These managed signatures do
not replace binary/platform compatibility testing or nullable-annotation analysis.

The five missing-file failures passed after this independent capture. No test was
removed or skipped. Exact commands/counts and original failures are retained in
the Phase 5 closure report and machine-readable evidence.
