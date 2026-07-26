#!/usr/bin/env python3

"""
Sends updates to a Discord webhook for new changelog entries since the last GitHub Actions publish run.
If there are no previous successful runs, it sends the entire changelog.
"""

import itertools
import os
from pathlib import Path
from typing import Any, Iterable

import requests
import yaml
import time

DEBUG = False
DEBUG_CHANGELOG_FILE_OLD = Path("Resources/Changelog/Old.yml")
GITHUB_API_URL = os.environ.get("GITHUB_API_URL", "https://api.github.com")

# Discord webhook settings
DISCORD_SPLIT_LIMIT = 2000
DISCORD_WEBHOOK_URL = os.environ.get("DISCORD_WEBHOOK_URL")

CHANGELOG_FILE = "Resources/Changelog/Changelog.yml"

TYPES_TO_EMOJI = {"Fix": "🐛", "Add": "🆕", "Remove": "❌", "Tweak": "⚒️"}

ChangelogEntry = dict[str, Any]

def main():
    if not DISCORD_WEBHOOK_URL:
        print("No Discord webhook URL found, skipping send")
        return

    if DEBUG:
        last_changelog_stream = DEBUG_CHANGELOG_FILE_OLD.read_text()
    else:
        last_changelog_stream = get_last_changelog()

    last_changelog = yaml.safe_load(last_changelog_stream)
    with open(CHANGELOG_FILE, "r") as f:
        cur_changelog = yaml.safe_load(f)

    diff = diff_changelog(last_changelog, cur_changelog)
    message_lines = changelog_entries_to_message_lines(diff)
    send_message_lines(message_lines)

def get_most_recent_workflow(
    sess: requests.Session, github_repository: str, github_run: str
) -> Any:
    workflow_run = get_current_run(sess, github_repository, github_run)
    past_runs = get_past_runs(sess, workflow_run)
    for run in past_runs.get("workflow_runs", []):
        if run["id"] != workflow_run["id"] and run["conclusion"] == "success":
            return run
    return None

def get_current_run(
    sess: requests.Session, github_repository: str, github_run: str
) -> Any:
    resp = sess.get(
        f"{GITHUB_API_URL}/repos/{github_repository}/actions/runs/{github_run}"
    )
    resp.raise_for_status()
    return resp.json()

def get_past_runs(sess: requests.Session, current_run: Any) -> Any:
    params = {"status": "success", "created": f"<={current_run['created_at']}"}
    resp = sess.get(f"{current_run['workflow_url']}/runs", params=params)
    resp.raise_for_status()
    return resp.json()

def get_last_changelog() -> str:
    github_repository = os.environ["GITHUB_REPOSITORY"]
    github_run = os.environ["GITHUB_RUN_ID"]
    github_token = os.environ["GITHUB_TOKEN"]

    session = requests.Session()
    session.headers["Authorization"] = f"Bearer {github_token}"
    session.headers["Accept"] = "application/vnd.github+json"
    session.headers["X-GitHub-Api-Version"] = "2022-11-28"

    try:
        # First try to get the last successful run from the workflow
        most_recent = get_most_recent_workflow(session, github_repository, github_run)
        if most_recent is None:
            print("::warning ::No previous successful runs. Falling back to an empty changelog.")
            return yaml.safe_dump({"Entries": []})

        last_sha = most_recent["head_commit"]["id"]
        print(f"Last successful publish job was {most_recent['id']}: {last_sha}")
        last_changelog_stream = get_last_changelog_by_sha(session, last_sha, github_repository)
        return last_changelog_stream
    except requests.exceptions.HTTPError as e:
        if e.response.status_code == 401:
            # If the token has no API access, fall back to the alternative method
            print("::warning ::Token has no access to the GitHub API. Trying the fallback method...")
            return get_last_changelog_fallback()
        else:
            print(f"::warning ::Could not fetch the previous changelog: {e}. Falling back to an empty changelog.")
            return yaml.safe_dump({"Entries": []})
    except Exception as e:
        print(f"::warning ::Could not fetch the previous changelog: {e}. Falling back to an empty changelog.")
        return yaml.safe_dump({"Entries": []})

def get_last_changelog_fallback() -> str:
    """Fallback method for fetching the previous changelog via git diff"""
    try:
        # Get the last commit from the current branch
        import subprocess
        result = subprocess.run(
            ["git", "log", "--oneline", "-n", "1", "--skip=1"],
            capture_output=True,
            text=True,
            check=True
        )
        if result.stdout:
            # Get the previous version of the file
            result = subprocess.run(
                ["git", "show", f"HEAD~1:{CHANGELOG_FILE}"],
                capture_output=True,
                text=True,
                check=True
            )
            return result.stdout
    except Exception as e:
        print(f"::warning ::Could not fetch the previous changelog via git: {e}")
    
    return yaml.safe_dump({"Entries": []})

def get_last_changelog_by_sha(
    sess: requests.Session, sha: str, github_repository: str
) -> str:
    params = {"ref": sha}
    headers = {"Accept": "application/vnd.github.raw"}

    resp = sess.get(
        f"{GITHUB_API_URL}/repos/{github_repository}/contents/{CHANGELOG_FILE}",
        headers=headers,
        params=params,
    )
    resp.raise_for_status()
    return resp.text

def diff_changelog(
    old: dict[str, Any], cur: dict[str, Any]
) -> Iterable[ChangelogEntry]:
    old_entries = old.get("Entries", [])
    old_entry_ids = {e["id"] for e in old_entries}
    return (e for e in cur["Entries"] if e["id"] not in old_entry_ids)

def get_discord_body(content: str):
    return {
        "content": content,
        "allowed_mentions": {"parse": ["roles"]},
        "flags": 1 << 2,
    }

def send_discord_webhook(lines: list[str], ping_role: bool = False):
    content = "".join(lines)
    
    # Prepend the role ping if needed
    if ping_role:
        content = f"<@&1318776836599320663>\n{content}"
    
    body = get_discord_body(content)
    retry_attempt = 0

    try:
        response = requests.post(DISCORD_WEBHOOK_URL, json=body, timeout=10)
        while response.status_code == 429:
            retry_attempt += 1
            if retry_attempt > 20:
                print("Too many retries despite honouring the retry_after header... giving up")
                exit(1)
            retry_after = response.json().get("retry_after", 5)
            print(f"Rate limited, retrying in {retry_after} seconds")
            time.sleep(retry_after)
            response = requests.post(DISCORD_WEBHOOK_URL, json=body, timeout=10)
        response.raise_for_status()
        print(f"Successfully sent to Discord: {len(content)} characters")
    except requests.exceptions.RequestException as e:
        print(f"Failed to send message: {e}")
        exit(1)

def changelog_entries_to_message_lines(entries: Iterable[ChangelogEntry]) -> list[str]:
    message_lines = []
    
    # Collect all entries into a list so they can be sorted by author
    entries_list = list(entries)
    if not entries_list:
        return message_lines

    # Sort by author for grouping
    entries_list.sort(key=lambda x: x["author"])

    for contributor_name, group in itertools.groupby(entries_list, lambda x: x["author"]):
        message_lines.append("\n")
        message_lines.append(f"**{contributor_name}** updated:\n")

        for entry in group:
            url = entry.get("url")
            if url and not url.strip():
                url = None

            for change in entry["changes"]:
                emoji = TYPES_TO_EMOJI.get(change["type"], "❓")
                message = change["message"]

                if len(message) > DISCORD_SPLIT_LIMIT:
                    message = message[: DISCORD_SPLIT_LIMIT - 100].rstrip() + " [...]"

                if url is not None:
                    pr_number = url.split("/")[-1]
                    line = f"{emoji} - {message} ([#{pr_number}]({url}))\n"
                else:
                    line = f"{emoji} - {message}\n"

                message_lines.append(line)

    return message_lines

def send_message_lines(message_lines: list[str]):
    if not message_lines:
        print("No new changes to send")
        return

    # Check whether the changelog actually contains entries
    has_entries = any(line.strip() and not line.startswith("**") and "updated:" not in line
                     for line in message_lines if line.strip())

    if not has_entries:
        print("Changelog contains no entries (author headings only)")
        return
    
    chunk_lines = []
    chunk_length = 0
    chunks = []
    
    # Split into chunks for Discord
    for line in message_lines:
        line_length = len(line)
        new_chunk_length = chunk_length + line_length

        if new_chunk_length > DISCORD_SPLIT_LIMIT - 50:  # Leave room for the ping
            if chunk_lines:
                chunks.append(chunk_lines.copy())
            chunk_lines = [line]
            chunk_length = line_length
        else:
            chunk_lines.append(line)
            chunk_length = new_chunk_length

    if chunk_lines:
        chunks.append(chunk_lines)
    
    # Send the chunks, pinging only in the first one
    for i, chunk in enumerate(chunks):
        if i == 0:
            send_discord_webhook(chunk, ping_role=True)
        else:
            send_discord_webhook(chunk, ping_role=False)

if __name__ == "__main__":
    main()
