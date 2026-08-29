#!/usr/bin/env python3
import hashlib
import json
import os
import subprocess
import sys

ROOT = subprocess.run(["git", "rev-parse", "--show-toplevel"],
                      capture_output=True, text=True, check=True).stdout.strip()
LEDGER = os.path.join(ROOT, ".review", "ledger.json")

FOUNDATION = (
    "src/shared/",
    "src/backend/Cartex.Domain/",
    "src/backend/Cartex.Application/Common/Finance/",
    "src/backend/Cartex.Application/Common/Inventory/",
    "src/backend/Cartex.Persistence/ApplicationDbContext.cs",
    "src/backend/Cartex.Persistence/TransactionScoped.cs",
    "src/backend/Cartex.Application/Common/Behaviors/",
    "docs/domain-rules.md",
    "docs/catalog/",
)

REVIEWABLE = (".cs", ".ts", ".html", ".axaml", ".xaml", ".sql", ".razor", ".md")
SKIP = ("/obj/", "/bin/", "/node_modules/", "/dist/", "/.angular/", "/TestResults/")


def worktree_hashes():
    deleted = set(subprocess.run(["git", "-C", ROOT, "ls-files", "-d"],
                  capture_output=True, text=True, check=True).stdout.splitlines())
    paths = [p for p in subprocess.run(["git", "-C", ROOT, "ls-files"],
             capture_output=True, text=True, check=True).stdout.splitlines()
             if p.endswith(REVIEWABLE) and p not in deleted
             and not any(s in "/" + p for s in SKIP)]
    result = {}
    for i in range(0, len(paths), 200):
        batch = paths[i:i + 200]
        proc = subprocess.run(["git", "-C", ROOT, "hash-object", "--"] + batch,
                              capture_output=True, text=True, check=True)
        for path, h in zip(batch, proc.stdout.split()):
            result[path] = h
    return result


def load():
    if not os.path.exists(LEDGER):
        return {}
    with open(LEDGER, encoding="utf-8") as handle:
        return json.load(handle)


def save(ledger):
    os.makedirs(os.path.dirname(LEDGER), exist_ok=True)
    with open(LEDGER, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(ledger, handle, ensure_ascii=False, indent=1, sort_keys=True)
        handle.write("\n")


def is_foundation(path):
    return any(path.startswith(f) or path == f.rstrip("/") for f in FOUNDATION)


def status():
    ledger = load()
    hashes = worktree_hashes()
    new, changed, sealed, foundation_dirty = [], [], 0, []
    for path, h in hashes.items():
        rec = ledger.get(path)
        if rec is None:
            new.append(path)
        elif rec["hash"] != h:
            changed.append(path)
            if is_foundation(path):
                foundation_dirty.append(path)
        else:
            sealed += 1
    total = len(new) + len(changed) + sealed
    print(f"MUHRLANGAN : {sealed}/{total}")
    print(f"YANGI      : {len(new)}")
    print(f"O'ZGARGAN  : {len(changed)}")
    if foundation_dirty:
        print("\n!! POYDEVOR fayllar o'zgargan — ularga bog'liq muhrlar shubhali:")
        for p in foundation_dirty:
            print("   " + p)
    if new or changed:
        print("\nReview kutayotgan (delta):")
        for p in sorted(new + changed):
            tag = "yangi" if p in new else "o'zgargan"
            print(f"   [{tag:9}] {p}")
    return 1 if (new or changed) else 0


def seal(paths, reviewer, note):
    import datetime
    ledger = load()
    date = datetime.date.today().isoformat()
    hashes = worktree_hashes()
    targets = paths if paths else list(hashes.keys())
    n = 0
    for path in targets:
        if path not in hashes:
            print("o'tkazildi (kuzatilmagan/fayl yo'q):", path)
            continue
        ledger[path] = {"hash": hashes[path], "date": date,
                        "reviewer": reviewer, "note": note}
        n += 1
    save(ledger)
    print(f"{n} fayl muhrlandi ({reviewer}, {date}).")
    return 0


def main():
    args = sys.argv[1:]
    if not args or args[0] == "status":
        return status()
    if args[0] == "seal":
        reviewer = "review"
        note = ""
        paths = []
        i = 1
        while i < len(args):
            if args[i] == "--by":
                reviewer = args[i + 1]; i += 2
            elif args[i] == "--note":
                note = args[i + 1]; i += 2
            else:
                paths.append(args[i]); i += 1
        return seal(paths, reviewer, note)
    print("foydalanish: review_ledger.py [status | seal [paths...] [--by NAME] [--note TEXT]]")
    return 2


if __name__ == "__main__":
    sys.exit(main())
