"""Generate a reference from an unmodified, pinned official Python jieba checkout.

Usage: python scripts/generate-python-parity.py artifacts/python-jieba
No package installation is needed. The reference is not generated from C# output.
"""
import ast
import hashlib
import json
import logging
from pathlib import Path
import random
import subprocess
import sys

repo = Path(__file__).resolve().parents[1]
upstream = Path(sys.argv[1]).resolve()
commit = subprocess.check_output(["git", "-C", str(upstream), "rev-parse", "HEAD"], text=True).strip()
expected_commit = "67fa2e36e72f69d9134b8a1037b83fbb070b9775"
if commit != expected_commit:
    raise SystemExit("Unexpected upstream revision: " + commit)
if subprocess.check_output(["git", "-C", str(upstream), "status", "--porcelain"], text=True).strip():
    raise SystemExit("Upstream checkout must be unmodified.")
sys.path.insert(0, str(upstream))
import jieba
import jieba.posseg
import jieba.analyse

jieba.setLogLevel(logging.WARNING)
texts = []
sources = ["test.py", "test_cutall.py", "test_cut_for_search.py", "test_no_hmm.py", "test_pos.py", "test_pos_no_hmm.py"]
source_hashes = {}
for source in sources:
    path = upstream / "test" / source
    source_hashes[source] = hashlib.sha256(path.read_bytes()).hexdigest()
    tree = ast.parse(path.read_text(encoding="utf-8-sig"))
    for node in ast.walk(tree):
        if (isinstance(node, ast.Call) and isinstance(node.func, ast.Name)
                and node.func.id == "cuttest" and node.args and isinstance(node.args[0], ast.Constant)
                and isinstance(node.args[0].value, str)):
            texts.append(node.args[0].value)
texts.extend((repo / "test/Segmenter.Tests/Cases/jieba_test.txt").read_text(encoding="utf-8-sig").splitlines())
texts.extend([
    "", " ", "\t\r\n", "北京颐和园春游合影20260418.jpg", "京都和服樱花旅行记录.mp4",
    "IMG_20260418_123456.jpg", "DSC_012345.JPG", "family-beach-holiday-2026.mp4",
    "重庆火锅美食探店1080p.mp4", "成都大熊猫繁育研究基地.jpg", "东京旅行と桜の写真.jpg",
    "海边日落_朋友聚会_4k.mp4", "微信图片_20261024123000.jpg", "北京旅行🌸合影.jpg",
    "𠀀𠮷野家北京", "カメラ사진北京", "hello\u00a0world", "艾尔肯·吐尼亚孜新疆阿克苏人",
    "1.5% 99.99% 3.14 720p", ".NET平台 U.S.A. cet-4", "结过婚的和尚未结过婚的",
])
rng = random.Random(20261024)
words = [line.split()[0] for line in (upstream / "jieba/dict.txt").read_text(encoding="utf-8").splitlines()]
for i in range(256):
    texts.append("".join(rng.choices(words, k=3)) + f"_{i:04}.jpg")
for i in range(128):
    texts.append("".join(chr(rng.randint(0x4e00, 0x9fa5)) for _ in range(rng.randint(2, 12))))
texts = list(dict.fromkeys(texts))

# Match data and documented configuration, then compare algorithms. Native defaults are discussed in the report.
stopwords = set((repo / "src/Segmenter/Resources/stopwords.txt").read_text(encoding="utf-8-sig").splitlines())
tfidf = jieba.analyse.TFIDF()
tfidf.stop_words = stopwords.copy()
textrank = jieba.analyse.TextRank()
textrank.stop_words = stopwords.copy()
cases = []
for text in texts:
    cases.append({
        "Text": text,
        "Cut": list(jieba.cut(text)), "NoHmm": list(jieba.cut(text, HMM=False)),
        "All": list(jieba.cut(text, cut_all=True)),
        "Search": list(jieba.cut_for_search(text)), "SearchNoHmm": list(jieba.cut_for_search(text, HMM=False)),
        "Pos": [{"Word": pair.word, "Flag": pair.flag} for pair in jieba.posseg.cut(text)],
        "PosNoHmm": [{"Word": pair.word, "Flag": pair.flag} for pair in jieba.posseg.cut(text, HMM=False)],
        "Tfidf": [{"Word": word, "Weight": weight} for word, weight in tfidf.extract_tags(text, withWeight=True)],
        "TextRank": [{"Word": word, "Weight": weight} for word, weight in textrank.textrank(text, withWeight=True, allowPOS=("ns", "n", "vn", "v"))],
    })
resources = {}
for name, relative in [("dict.txt", "dict.txt"), ("idf.txt", "analyse/idf.txt")]:
    official_lines = (upstream / "jieba" / relative).read_text(encoding="utf-8-sig").splitlines()
    local_lines = (repo / "src/Segmenter/Resources" / name).read_text(encoding="utf-8-sig").splitlines()
    if official_lines != local_lines:
        raise SystemExit("Resource mismatch: " + name)
    resources[name] = hashlib.sha256("\n".join(official_lines).encode()).hexdigest()
# Validate every converted HMM/POS JSON model, not just the text dictionaries.
import jieba.finalseg as finalseg
import jieba.posseg as posseg
state_name = lambda state: "-".join(state)
models = {
    "prob_trans.json": finalseg.trans_P,
    "prob_emit.json": finalseg.emit_P,
    "pos_prob_start.json": {state_name(key): value for key, value in posseg.start_P.items()},
    "pos_prob_trans.json": {state_name(key): {state_name(k): v for k, v in value.items()} for key, value in posseg.trans_P.items()},
    "pos_prob_emit.json": {state_name(key): value for key, value in posseg.emit_P.items()},
    "char_state_tab.json": {key: sorted(state_name(state) for state in value) for key, value in posseg.char_state_tab_P.items()},
}
for name, expected in models.items():
    path = repo / "src/Segmenter/Resources" / name
    actual = json.loads(path.read_text(encoding="utf-8-sig"))
    if name == "char_state_tab.json": actual = {key: sorted(value) for key, value in actual.items()}
    if actual != expected: raise SystemExit("Model mismatch: " + name)
    resources[name] = hashlib.sha256(path.read_bytes()).hexdigest()
output = repo / "test/Segmenter.Tests/Cases/python-parity.json"
temporary_output = output.with_suffix(".json.tmp")
temporary_output.write_text(json.dumps({
    "Metadata": {"Repository": "https://github.com/fxsjy/jieba", "Commit": commit, "Version": jieba.__version__,
                 "Python": sys.version, "Sources": source_hashes, "Resources": resources,
                 "StopWords": "C# bundled stopwords.txt", "TextRankAllowPos": ["ns", "n", "vn", "v"], "Seed": 20261024},
    "Cases": cases,
}, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
temporary_output.replace(output)
print(f"Generated {len(cases)} cases x 9 operations: {output}")
