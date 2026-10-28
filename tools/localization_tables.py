"""Check language packs, or edit every language side by side in a UTF-8 CSV.

python tools/localization_tables.py check
python tools/localization_tables.py export Temp/translations.csv
python tools/localization_tables.py import Temp/translations.csv
"""
import argparse
import csv
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PACKS = ROOT / "Assets/Resources/Localization"


def read_tables():
    languages = json.loads((PACKS / "languages.json").read_text(encoding="utf-8"))["languages"]
    tables, files = {}, {}
    for locale in languages:
        code = locale["code"]
        tables[code] = {}
        for path in sorted((PACKS / code / "Strings").rglob("*.json")):
            for entry in json.loads(path.read_text(encoding="utf-8"))["entries"]:
                key = entry["key"]
                if key in tables[code]:
                    raise ValueError(f"Duplicate key: {code}/{key}")
                tables[code][key] = entry["value"]
                files[code, key] = path
    return tables, files


def check(tables):
    errors = []
    if not (PACKS / "systemfont1.ttf").is_file():
        errors.append("Missing systemfont1.ttf")
    english = tables["en"]
    for code, entries in tables.items():
        for key in english.keys() - entries.keys():
            errors.append(f"Missing: {code}/{key}")
        for key in entries.keys() - english.keys():
            errors.append(f"Unknown: {code}/{key}")
        for key in english.keys() & entries.keys():
            if not entries[key].strip():
                errors.append(f"Empty: {code}/{key}")
            args = lambda value: sorted(re.findall(r"\{\d+[^}]*\}", value))
            if args(english[key]) != args(entries[key]):
                errors.append(f"Format arguments differ: {code}/{key}")
        for role in ("default", "bold"):
            if not (PACKS / code / "Fonts" / f"{role}.ttf").is_file():
                errors.append(f"Missing font: {code}/{role}.ttf")
    for path in (ROOT / "Assets/Scripts").rglob("*.cs"):
        for key in re.findall(r'Localization\.(?:Get|Format)\("([^"\n]+)"', path.read_text(encoding="utf-8-sig")):
            if not key.endswith(".") and key not in english:
                errors.append(f"Unknown code key: {path.name}: {key}")
    for folder in ("NPC", "Events"):
        for path in (ROOT / "Assets/Resources" / folder).rglob("*.json"):
            for key in re.findall(r'"@([^"\n]+)"', path.read_text(encoding="utf-8")):
                if key not in english:
                    errors.append(f"Unknown dialogue key: {path.name}: {key}")
    if errors:
        raise ValueError("\n".join(errors))
    print(f"PASS: {len(english)} keys, {len(tables)} languages, fonts and placeholders.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("check", "export", "import"))
    parser.add_argument("file", nargs="?", type=Path)
    args = parser.parse_args()
    tables, files = read_tables()
    if args.action == "check":
        check(tables)
    elif args.action == "export":
        if args.file is None:
            parser.error("CSV output path is required")
        args.file.parent.mkdir(parents=True, exist_ok=True)
        with args.file.open("w", encoding="utf-8-sig", newline="") as handle:
            writer = csv.DictWriter(handle, fieldnames=["key", *tables.keys()])
            writer.writeheader()
            for key in sorted(tables["en"]):
                writer.writerow({"key": key, **{code: values.get(key, "") for code, values in tables.items()}})
        print(args.file.resolve())
    else:
        if args.file is None:
            parser.error("CSV input path is required")
        with args.file.open(encoding="utf-8-sig", newline="") as handle:
            reader = csv.DictReader(handle)
            if not set(["key", *tables]).issubset(reader.fieldnames or []):
                raise ValueError("CSV must include key and every language column")
            seen = set()
            for row in reader:
                key = row["key"]
                if not key or key in seen:
                    raise ValueError(f"Empty or duplicate CSV key: {key}")
                seen.add(key)
                for code in tables:
                    tables[code][key] = row[code]
        check(tables)  # Validate all changes before writing any file.
        outputs = {}
        for code, entries in tables.items():
            for key, value in entries.items():
                path = files.get((code, key), PACKS / code / "Strings" / "additional.json")
                outputs.setdefault(path, []).append({"key": key, "value": value})
        for path, entries in outputs.items():
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(json.dumps({"entries": entries}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print("Imported translations. Omitted CSV keys are preserved.")


if __name__ == "__main__":
    main()
