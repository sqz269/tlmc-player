#!/usr/bin/env python3
"""Measure the CJK segmentation trap on a live Meilisearch (SCHEMA-V6.md section 7).

Meilisearch picks its segmenter per field via whatlang detection, which classifies
text as Mandarin unless >=5% of characters are hiragana/katakana. Pure-kanji
Japanese titles — a large share of Touhou track and game names — then get Chinese
(jieba) segmentation silently, and recall quietly degrades on exactly the titles
most likely to be searched in their original script. `localizedAttributes` (1.10+)
overrides detection per attribute; this probe measures how much that matters on
this corpus before trusting it.

It indexes the same pure-kanji titles into two throwaway indexes — one with default
settings, one declaring `*_default` as jpn exactly like the backend's
SearchIndexContract — and runs kanji sub-term queries against both, each with and
without the query-side `locales` hint. Ground truth is substring containment.

Usage:
    # against the local cluster (values from K8s/local/.generated-credentials):
    python3 probe.py --url "$MEILI_URL" --key "$MEILI_MASTER_KEY"

    # against a dev-mode engine (docker compose, no key):
    python3 probe.py

    --titles-file FILE   one title per line, replaces the built-in corpus
    --keep               leave the probe indexes behind for inspection
"""

import argparse
import json
import os
import sys
import time
import urllib.error
import urllib.request

# Real pure-kanji titles: official game names, original themes, and doujin track
# names. Nothing here contains kana, so whatlang has nothing to hang a Japanese
# classification on — every one of these is at risk of jieba segmentation.
TITLES = [
    "東方紅魔郷",
    "東方妖々夢",
    "東方永夜抄",
    "東方花映塚",
    "東方文花帖",
    "東方風神録",
    "東方地霊殿",
    "東方星蓮船",
    "東方神霊廟",
    "東方輝針城",
    "東方紺珠伝",
    "東方天空璋",
    "東方鬼形獣",
    "東方虹龍洞",
    "東方獣王園",
    "東方怪綺談",
    "東方夢時空",
    "東方幻想郷",
    "東方靈異伝",
    "東方封魔録",
    "上海紅茶館",
    "少女綺想曲",
    "幽霊楽団",
    "千年幻想郷",
    "竹取飛翔",
    "妖魔夜行",
    "紅楼",
    "懐郷",
    "妖精大戦争",
    "蓬莱伝説",
    "二色蓮花蝶",
    "桜花之恋塚",
    "夢消失",
    "幻想機械",
    "夢幻遊戯",
    "死体旅行",
    "幻想浄瑠璃",
    "月見草",
    "蠢々秋月",
    "砕月",
    "広有射怪鳥事",
    "風神少女",
    "少女秘封倶楽部",
    "博麗神社例大祭",
    "幻想万華鏡",
    "蓬莱人形",
    "蓮台野夜行",
    "夢違科学世紀",
    "卯酉東海道",
    "大空魔術",
    "鳥船遺跡",
    "伊弉諾物質",
    "燕石博物誌",
    "旧約酒場",
]

# Sub-terms someone would actually type. Ground truth per query is computed as
# substring containment over the corpus, so adding titles never breaks the probe.
QUERIES = [
    "東方",
    "紅魔郷",
    "幻想郷",
    "妖々夢",
    "永夜抄",
    "紅茶館",
    "上海",
    "綺想曲",
    "幽霊楽団",
    "竹取",
    "妖精大戦争",
    "蓬莱",
    "蓮花蝶",
    "秘封倶楽部",
    "少女",
    "秋月",
    "神霊廟",
    "紺珠伝",
    "科学世紀",
    "東海道",
    "夢",
]

PLAIN_UID = "cjk_probe_plain"
LOCALIZED_UID = "cjk_probe_localized"

# Mirrors SearchIndexContract.BuildSettings() for the field this probe uses.
LOCALIZED_SETTINGS = {
    "localizedAttributes": [
        {"attributePatterns": ["*_default"], "locales": ["jpn"]},
    ],
}


class Meili:
    def __init__(self, url, key):
        self.url = url.rstrip("/")
        self.key = key

    def request(self, method, path, body=None):
        req = urllib.request.Request(self.url + path, method=method)
        req.add_header("Content-Type", "application/json")
        if self.key:
            req.add_header("Authorization", f"Bearer {self.key}")
        data = json.dumps(body).encode() if body is not None else None
        try:
            with urllib.request.urlopen(req, data=data, timeout=30) as resp:
                return json.loads(resp.read() or b"null")
        except urllib.error.HTTPError as e:
            payload = e.read().decode(errors="replace")
            raise SystemExit(f"{method} {path} -> HTTP {e.code}: {payload}") from e
        except urllib.error.URLError as e:
            raise SystemExit(f"cannot reach Meilisearch at {self.url}: {e.reason}") from e

    def wait(self, task):
        uid = task["taskUid"]
        deadline = time.time() + 120
        while time.time() < deadline:
            status = self.request("GET", f"/tasks/{uid}")
            if status["status"] in ("succeeded", "failed", "canceled"):
                if status["status"] != "succeeded":
                    raise SystemExit(f"task {uid} {status['status']}: {status.get('error')}")
                return
            time.sleep(0.2)
        raise SystemExit(f"task {uid} did not finish in 120s")

    def search(self, uid, query, locales=None):
        body = {"q": query, "limit": len(TITLES) + len(QUERIES)}
        if locales:
            body["locales"] = locales
        return self.request("POST", f"/indexes/{uid}/search", body)


def build_index(meili, uid, titles, settings):
    meili.request("DELETE", f"/indexes/{uid}")  # returns a task even for missing
    meili.wait(meili.request("POST", "/indexes", {"uid": uid, "primaryKey": "id"}))
    if settings:
        meili.wait(meili.request("PATCH", f"/indexes/{uid}/settings", settings))
    docs = [{"id": str(i), "title_default": t} for i, t in enumerate(titles)]
    meili.wait(meili.request("PUT", f"/indexes/{uid}/documents", docs))


def run_queries(meili, uid, titles, locales):
    total_expected = 0
    total_found = 0
    misses = []
    for query in QUERIES:
        expected = {t for t in titles if query in t}
        if not expected:
            continue
        hits = meili.search(uid, query, locales)["hits"]
        found = {h["title_default"] for h in hits} & expected
        total_expected += len(expected)
        total_found += len(found)
        for title in sorted(expected - found):
            misses.append((query, title))
    return total_found, total_expected, misses


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--url", default=os.environ.get("MEILI_URL", "http://localhost:7700"))
    parser.add_argument("--key", default=os.environ.get("MEILI_MASTER_KEY", ""))
    parser.add_argument("--titles-file", help="one title per line, replaces the built-in corpus")
    parser.add_argument("--keep", action="store_true", help="leave the probe indexes behind")
    args = parser.parse_args()

    titles = TITLES
    if args.titles_file:
        with open(args.titles_file, encoding="utf-8") as f:
            titles = [line.strip() for line in f if line.strip()]

    meili = Meili(args.url, args.key)
    version = meili.request("GET", "/version")["pkgVersion"]
    major, minor = (int(x) for x in version.split(".")[:2])
    if (major, minor) < (1, 10):
        raise SystemExit(f"Meilisearch {version} predates localizedAttributes (needs >= 1.10)")
    print(f"Meilisearch {version} at {args.url}, {len(titles)} titles, {len(QUERIES)} queries\n")

    build_index(meili, PLAIN_UID, titles, settings=None)
    build_index(meili, LOCALIZED_UID, titles, settings=LOCALIZED_SETTINGS)

    worst_misses = []
    print(f"{'index settings':<22} {'query locales':<15} {'recall':>12}")
    for uid, label in ((PLAIN_UID, "plain"), (LOCALIZED_UID, "localizedAttributes")):
        for locales in (None, ["jpn"]):
            found, expected, misses = run_queries(meili, uid, titles, locales)
            locale_label = ",".join(locales) if locales else "(detected)"
            print(f"{label:<22} {locale_label:<15} {found:>5}/{expected} ({found / expected:.0%})")
            if label == "plain" and locales is None:
                worst_misses = misses

    if worst_misses:
        print("\nmissed by the unconfigured index (query -> title):")
        for query, title in worst_misses:
            print(f"  {query} -> {title}")

    if not args.keep:
        meili.request("DELETE", f"/indexes/{PLAIN_UID}")
        meili.request("DELETE", f"/indexes/{LOCALIZED_UID}")
        print("\nprobe indexes deleted (--keep to retain them)")


if __name__ == "__main__":
    sys.exit(main())
