"""Record executed TRX results and the exact checkout for CI evidence."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

parser = argparse.ArgumentParser()
parser.add_argument("--results-root", default="TestResults")
parser.add_argument("--output", default="TestResults/provenance.json")
args = parser.parse_args()
root = Path(args.results_root)
ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
runs = []
for path in sorted(root.rglob("*.trx")):
    document = ET.parse(path)
    summary = document.find("t:ResultSummary", ns)
    counters = summary.find("t:Counters", ns)
    skipped = []
    for result in document.findall("t:Results/t:UnitTestResult", ns):
        if result.get("outcome") == "NotExecuted":
            skipped.append(result.get("testName"))
    runs.append({"file": str(path), "outcome": summary.get("outcome"), "counters": {k: int(v) for k, v in counters.attrib.items()},
                 "skippedTests": skipped, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
evidence = {"commit": subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip(),
            "branch": os.environ.get("GITHUB_HEAD_REF") or subprocess.check_output(["git", "branch", "--show-current"], text=True).strip(), "runs": runs}
output = Path(args.output)
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps(evidence, indent=2) + "\n", encoding="utf-8")
template = Path("docs/family-self-service/TEST-EVIDENCE.json")
if template.exists():
    complete = json.loads(template.read_text(encoding="utf-8-sig"))
    complete["finalCheckoutVerification"] = evidence
    (output.parent / "TEST-EVIDENCE.json").write_text(json.dumps(complete, indent=2) + "\n", encoding="utf-8")
print(f"Recorded {len(runs)} TRX runs for {evidence['commit']}.")
