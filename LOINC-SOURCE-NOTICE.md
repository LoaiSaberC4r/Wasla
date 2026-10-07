# Diagnostic terminology source notice

Wasla can import runtime reference catalogs from an official offline LOINC release, initially version 2.83. This repository contains synthetic test packages, rather than a copied terminology dataset. Importing reference data requires a MedicalCatalogManager account and an explicit staged review and apply action.

LOINC® is a registered trademark of Regenstrief Institute, Inc. LOINC terminology is copyrighted by Regenstrief Institute, Inc. and the LOINC Committee. Wasla does not own this terminology. Use and redistribution of imported source content remain subject to the [LOINC license](https://loinc.org/kb/license).

Radiology reference content comes from the [LOINC/RSNA Radiology Playbook](https://loinc.org/kb/radiology), developed in collaboration with the Radiological Society of North America. Imported RadLex content is subject to the applicable RSNA terms identified in the LOINC license. The [Playbook guide](https://loinc.org/kb/radiology/loinc-rsna-radiology-playbook-user-guide) and [LOINC Part linkage documentation](https://loinc.org/kb/enriched-linkages-between-loinc-terms-and-loinc-parts) describe its attributes.

The catalog retains the source code, source version, official English terminology, any supplied official Arabic terminology, canonical source columns (including external copyright notices where present), and original Radiology Part rows. Source content remains distinguishable from Wasla display names, aliases and internal reference notes. A local Arabic label is never presented as an official translation. Missing official Arabic stays null.

No startup job downloads LOINC. No source terminology is seeded into migrations. Each upload records its file name, SHA-256, source version, reviewing actor, immutable staged rows and apply/discard outcome. Applications redistributing imported terminology must carry the source notices and license link with that distribution, including the unmodified required notice in section 10 of the [official license](https://loinc.org/license). Online deployments must include that notice in their license or terms of use.
