#!/usr/bin/env python3
"""Generate randomized reading lists against a local Kavita instance (dev tool)."""

import json
import random
import sys
import requests

BASE_URL = "http://localhost:5000"

ADJECTIVES = [
    "Amber", "Azure", "Blazing", "Cobalt", "Crimson", "Dusk", "Ember", "Feral",
    "Gilded", "Hollow", "Indigo", "Jade", "Lunar", "Muted", "Neon", "Obsidian",
    "Pale", "Radiant", "Scarlet", "Silent", "Solar", "Twilight", "Verdant",
    "Violet", "Woven",
]

NOUNS = [
    "Anchor", "Archive", "Bastion", "Canopy", "Chronicle", "Citadel", "Comet",
    "Compass", "Dagger", "Elephant", "Falcon", "Glacier", "Harbor", "Lantern",
    "Mirage", "Nexus", "Otter", "Phantom", "Relic", "Ridgeline", "Sparrow",
    "Summit", "Tempest", "Walrus", "Zenith",
]

LOREM_PARAGRAPHS = [
    (
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor "
        "incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud "
        "exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat."
    ),
    (
        "Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu "
        "fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa "
        "qui officia deserunt mollit anim id est laborum."
    ),
    (
        "Curabitur pretium tincidunt lacus. Nulla gravida orci a odio. Nullam varius, turpis "
        "molestie pretium placerat, arcu purus rhoncus libero, vel fringilla nunc massa eget "
        "purus. Vestibulum ante ipsum primis in faucibus orci luctus."
    ),
    (
        "Pellentesque habitant morbi tristique senectus et netus et malesuada fames ac turpis "
        "egestas. Proin pharetra nonummy pede. Mauris et orci. Aenean nec lorem. In porttitor "
        "donec laoreet nonummy augue."
    ),
    (
        "Suspendisse dui purus, scelerisque at, vulputate vitae, pretium mattis, nunc. Mauris "
        "eget neque at sem venenatis eleifend. Ut nonummy. Fusce aliquet pede non pede. "
        "Suspendisse dapibus lorem pellentesque magna."
    ),
    (
        "Integer nulla. Donec blandit feugiat ligula. Donec hendrerit, felis et imperdiet "
        "euismod, purus ipsum pretium metus, in lacinia nulla nisl eget sapien. Donec ut est "
        "in lectus consequat consequat."
    ),
]

TAGS = ["Manga", "Marvel", "DC", "Indie"]


def make_session(api_key: str) -> requests.Session:
    s = requests.Session()
    s.headers.update({"x-api-key": api_key, "Content-Type": "application/json"})
    return s


def fetch_all_series(session: requests.Session) -> list[int]:
    series_ids: list[int] = []
    page = 1
    page_size = 500
    filter_body = {"statements": [], "combination": 0, "sortOptions": None, "limitTo": 0}

    while True:
        resp = session.post(
            f"{BASE_URL}/api/series/v2",
            params={"pageNumber": page, "pageSize": page_size},
            json=filter_body,
        )
        resp.raise_for_status()
        data = resp.json()
        series_ids.extend(item["id"] for item in data)

        pagination_header = resp.headers.get("X-Pagination")
        if pagination_header:
            pagination = json.loads(pagination_header)
            if page >= pagination.get("TotalPages", 1):
                break
        else:
            break
        page += 1

    return series_ids


def random_title() -> str:
    return f"{random.choice(ADJECTIVES)} {random.choice(NOUNS)} [GEN]"


def create_list(session: requests.Session, title: str) -> int:
    resp = session.post(f"{BASE_URL}/api/readinglist/create", json={"title": title})
    resp.raise_for_status()
    return resp.json()["id"]


def update_list_metadata(
    session: requests.Session,
    list_id: int,
    title: str,
    tags: list[str],
    starting_year: int,
    ending_year: int,
) -> None:
    payload = {
        "readingListId": list_id,
        "title": title,
        "summary": random.choice(LOREM_PARAGRAPHS),
        "promoted": False,
        "coverImageLocked": False,
        "startingMonth": 0,
        "startingYear": starting_year,
        "endingMonth": 0,
        "endingYear": ending_year,
        "tags": tags,
    }
    resp = session.post(f"{BASE_URL}/api/readinglist/update", json=payload)
    resp.raise_for_status()


def add_series_to_list(session: requests.Session, list_id: int, series_id: int) -> bool:
    resp = session.post(
        f"{BASE_URL}/api/readinglist/update-by-series",
        json={"seriesId": series_id, "readingListId": list_id},
    )
    if not resp.ok:
        print(f"  [warn] Failed to add series {series_id} to list {list_id}: {resp.status_code}")
        return False
    return True


def main() -> None:
    api_key = input("Enter Kavita API key: ").strip()
    if not api_key:
        print("API key is required.")
        sys.exit(1)

    session = make_session(api_key)

    print("Fetching series…")
    try:
        all_series = fetch_all_series(session)
    except requests.HTTPError as e:
        print(f"Failed to fetch series: {e}")
        sys.exit(1)

    if not all_series:
        print("No series found. Make sure the API key has access to at least one library.")
        sys.exit(1)

    print(f"Found {len(all_series)} series. Generating 10 reading lists…\n")

    for i in range(10):
        title = random_title()
        chosen_tags = random.sample(TAGS, k=random.randint(1, 2))
        starting_year = random.randint(2000, 2022)
        ending_year = random.randint(starting_year, 2024)
        count = random.randint(3, min(8, len(all_series)))
        chosen_series = random.sample(all_series, k=count)

        try:
            list_id = create_list(session, title)
            update_list_metadata(session, list_id, title, chosen_tags, starting_year, ending_year)
        except requests.HTTPError as e:
            print(f"[{i+1}/10] Failed to create list '{title}': {e}")
            continue

        added = sum(add_series_to_list(session, list_id, sid) for sid in chosen_series)
        print(
            f"[{i+1}/10] Created: {title}  "
            f"(id={list_id}, {added}/{count} series, tags: {'/'.join(chosen_tags)})"
        )

    print("\nDone.")


if __name__ == "__main__":
    main()
