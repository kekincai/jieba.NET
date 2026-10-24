"""Compare an exporter result with the pinned Python reference, without normalizing tokens."""
from collections import Counter
import json
import math
from pathlib import Path
import sys

repo = Path(__file__).resolve().parents[1]
reference = json.loads((repo / "test/Segmenter.Tests/Cases/python-parity.json").read_text(encoding="utf-8"))["Cases"]
actual = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8-sig"))
assert len(reference) == len(actual)
differences = []
for expected, result in zip(reference, actual):
    assert expected["Text"] == result["Text"]
    for mode in expected.keys() - {"Text"}:
        left, right = expected[mode], result[mode]
        if mode in ("Tfidf", "TextRank"):
            equal = len(left) == len(right) and all(a["Word"] == b["Word"] and math.isclose(a["Weight"], b["Weight"], rel_tol=1e-10, abs_tol=1e-12) for a, b in zip(left, right))
        else:
            equal = left == right
        if not equal:
            differences.append({"Text": expected["Text"], "Mode": mode, "Python": left, "CSharp": right})
output = Path(sys.argv[2]) if len(sys.argv) > 2 else repo / "artifacts/python-parity-differences.json"
output.write_text(json.dumps(differences, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps({"Cases": len(reference), "Operations": len(reference) * 9, "Differences": len(differences), "ByMode": Counter(item["Mode"] for item in differences)}, ensure_ascii=False))
for item in differences[:8]:
    print(json.dumps(item, ensure_ascii=False))
print("Saved:", output)

raise SystemExit(1 if differences else 0)
