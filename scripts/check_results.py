"""Fail on an absent/empty test verdict as well as a test failure."""
import argparse
from pathlib import Path
import sys
import xml.etree.ElementTree as ET


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("reports", nargs="+")
    args = parser.parse_args()
    reports = []
    for value in args.reports:
        path = Path(value)
        if path.is_dir():
            reports.extend(p for p in sorted(path.rglob("*.xml")) if ET.parse(p).getroot().tag in ("test-run", "testsuites", "testsuite"))
        else:
            reports.append(path)
    if not reports:
        print("No test reports were found.", file=sys.stderr)
        return 1
    for report in reports:
        root = ET.parse(report).getroot()
        cases = list(root.iter("test-case")) + list(root.iter("testcase"))
        failed = [case for case in cases if (case.tag == "test-case" and case.get("result") != "Passed") or
                  case.find("failure") is not None or case.find("error") is not None or case.find("skipped") is not None]
        if not cases or failed or (root.tag == "test-run" and root.get("result") != "Passed"):
            print(f"{report}: invalid verdict; cases={len(cases)}, failed={len(failed)}", file=sys.stderr)
            return 1
        print(f"{report}: {len(cases)} tests, no failures")
    return 0


if __name__ == "__main__":
    sys.exit(main())
