"""Validate package layout, Unity GUIDs, local documentation links and spec-to-test mappings."""
import json
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT / "Packages/com.kamahir0.my-assets"


def validate():
    errors = []
    manifest = json.loads((PACKAGE / "package.json").read_text())
    if manifest["name"] != PACKAGE.name or manifest["repository"]["directory"] != "Packages/" + PACKAGE.name:
        errors.append("Package identity and repository directory disagree")
    if (ROOT / "README.md").read_bytes() != (PACKAGE / "README.md").read_bytes():
        errors.append("Root and package README must match")
    for assembly in PACKAGE.rglob("*.asmdef"):
        json.loads(assembly.read_text())
    guids = {}
    for directory in [ROOT / "Assets", PACKAGE]:
        for path in directory.rglob("*"):
            if path.name.endswith(".meta"):
                match = re.search(r"^guid: ([0-9a-f]{32})$", path.read_text(), re.M)
                if not match:
                    errors.append(f"Invalid GUID: {path.relative_to(ROOT)}")
                elif match[1] in guids:
                    errors.append(f"Duplicate GUID: {path.relative_to(ROOT)} and {guids[match[1]]}")
                else:
                    guids[match[1]] = path.relative_to(ROOT)
                if not path.with_suffix("").exists():
                    errors.append(f"Orphan metadata: {path.relative_to(ROOT)}")
            elif not any(part.endswith("~") for part in path.relative_to(directory).parts):
                if not Path(str(path) + ".meta").exists():
                    errors.append(f"Missing metadata: {path.relative_to(ROOT)}")
    documents = [ROOT / "AGENTS.md", ROOT / "README.md", *list((ROOT / "docs").rglob("*.md")), *list(PACKAGE.rglob("*.md"))]
    for doc in documents:
        for target in re.findall(r"\]\(([^)]+)\)", doc.read_text()):
            if re.match(r"\w+://", target) or target.startswith("#"):
                continue
            if not (doc.parent / target.split("#")[0]).exists():
                errors.append(f"Broken local link: {doc.relative_to(ROOT)} -> {target}")
    spec = (ROOT / "docs/specs/my-assets.md").read_text()
    ids = re.findall(r"\| (MA-[A-Z]+-\d{3}) \|", spec)
    if len(ids) != len(set(ids)) or not ids:
        errors.append("Specification IDs must be unique and nonempty")
    tests = "\n".join(path.read_text() for path in (PACKAGE / "Tests").rglob("*.cs"))
    for line in spec.splitlines():
        if re.match(r"\| MA-", line):
            for test in line.split("|")[-2].strip().split(" / "):
                if not re.search(r"\b" + re.escape(test) + r"\s*\(", tests):
                    errors.append(f"Specification refers to missing test: {test}")
    if errors:
        print("\n".join(errors), file=sys.stderr)
        return 1
    print(f"Valid package, {len(guids)} unique asset GUIDs, documentation links and {len(ids)} specification requirements.")
    return 0


if __name__ == "__main__":
    sys.exit(validate())
