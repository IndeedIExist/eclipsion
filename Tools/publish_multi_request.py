#!/usr/bin/env python3

import requests
import os
import subprocess
from typing import Iterable
import urllib3
import sys
from datetime import datetime

urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

# Console colours
class Colors:
    HEADER = '\033[95m'
    OKBLUE = '\033[94m'
    OKCYAN = '\033[96m'
    OKGREEN = '\033[92m'
    WARNING = '\033[93m'
    FAIL = '\033[91m'
    ENDC = '\033[0m'
    BOLD = '\033[1m'

def log(message, level="INFO"):
    """Log with a timestamp and colour"""
    timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    colors = {
        "INFO": Colors.OKBLUE,
        "SUCCESS": Colors.OKGREEN,
        "WARNING": Colors.WARNING,
        "ERROR": Colors.FAIL,
        "DEBUG": Colors.OKCYAN
    }
    color = colors.get(level, "")
    print(f"{color}[{timestamp}] [{level}]{Colors.ENDC} {message}")
    sys.stdout.flush()

# Environment variables
PUBLISH_TOKEN = os.environ.get("PUBLISH_TOKEN")
VERSION = os.environ.get("GITHUB_SHA")
RELEASE_DIR = "release"

# Fork configuration
# TODO: ROBUST_CDN_URL still points at third-party (Corvax) infrastructure.
# Replace it with this project's own Robust CDN before publishing builds.
ROBUST_CDN_URL = "https://cdn.corvaxforge.ru/"
FORK_ID = "eclipsion"

def main():
    log("=" * 80, "INFO")
    log("STARTING BUILD PUBLISH", "INFO")
    log("=" * 80, "INFO")

    # Check environment variables
    log("Checking environment variables...", "INFO")
    if not PUBLISH_TOKEN:
        log("ERROR: PUBLISH_TOKEN is not set!", "ERROR")
        sys.exit(1)
    log(f"✓ PUBLISH_TOKEN is set (length: {len(PUBLISH_TOKEN)} characters)", "SUCCESS")

    if not VERSION:
        log("ERROR: GITHUB_SHA (VERSION) is not set!", "ERROR")
        sys.exit(1)
    log(f"✓ VERSION: {VERSION}", "SUCCESS")

    log(f"✓ CDN URL: {ROBUST_CDN_URL}", "INFO")
    log(f"✓ FORK ID: {FORK_ID}", "INFO")
    log(f"✓ RELEASE DIR: {RELEASE_DIR}", "INFO")

    # Check the release directory
    log("", "INFO")
    log("Checking the release directory...", "INFO")
    if not os.path.exists(RELEASE_DIR):
        log(f"ERROR: Directory {RELEASE_DIR} not found!", "ERROR")
        sys.exit(1)

    files = list(get_files_to_publish())
    if not files:
        log(f"ERROR: No .zip files found in directory {RELEASE_DIR}!", "ERROR")
        sys.exit(1)

    log(f"✓ Files found to publish: {len(files)}", "SUCCESS")
    total_size = sum(os.path.getsize(f) for f in files)
    log(f"✓ Total size: {total_size / (1024*1024):.2f} MB", "SUCCESS")
    for f in files:
        size_mb = os.path.getsize(f) / (1024*1024)
        log(f"  - {os.path.basename(f)}: {size_mb:.2f} MB", "DEBUG")

    # Get the engine version
    log("", "INFO")
    log("Getting the engine version...", "INFO")
    engine_version = get_engine_version()
    log(f"✓ Engine Version: {engine_version}", "SUCCESS")

    # Create the session
    log("", "INFO")
    log("Creating HTTP session...", "INFO")
    session = requests.Session()
    session.headers = {
        "Authorization": f"Bearer {PUBLISH_TOKEN}",
    }
    session.verify = False
    log("✓ Session created", "SUCCESS")

    # Connection test
    log("", "INFO")
    log("=" * 80, "INFO")
    log("STEP 1: CDN CONNECTION TEST", "INFO")
    log("=" * 80, "INFO")
    test_url = f"{ROBUST_CDN_URL}fork/{FORK_ID}/"
    log(f"Sending GET request: {test_url}", "INFO")

    try:
        test_resp = session.get(test_url, timeout=10)
        log(f"✓ Response received", "SUCCESS")
        log(f"  Status code: {test_resp.status_code}", "DEBUG")
        log(f"  Content-Type: {test_resp.headers.get('Content-Type', 'N/A')}", "DEBUG")
        log(f"  Content-Length: {test_resp.headers.get('Content-Length', 'N/A')}", "DEBUG")

        if test_resp.status_code == 200:
            log("✓ Connection to the CDN established successfully", "SUCCESS")
        else:
            log(f"⚠ Unexpected status code: {test_resp.status_code}", "WARNING")
            log(f"  Response: {test_resp.text[:200]}", "DEBUG")
    except Exception as e:
        log(f"✗ CONNECTION ERROR: {e}", "ERROR")
        log("Continuing despite the error...", "WARNING")

    # Start the publish
    log("", "INFO")
    log("=" * 80, "INFO")
    log("STEP 2: STARTING THE PUBLISH", "INFO")
    log("=" * 80, "INFO")

    start_url = f"{ROBUST_CDN_URL}fork/{FORK_ID}/publish/start"
    data = {
        "version": VERSION,
        "engineVersion": engine_version,
    }

    log(f"Sending POST request: {start_url}", "INFO")
    log(f"Request data:", "DEBUG")
    log(f"  version: {data['version']}", "DEBUG")
    log(f"  engineVersion: {data['engineVersion']}", "DEBUG")

    try:
        resp = session.post(start_url, json=data, timeout=30)
        log(f"✓ Response received", "SUCCESS")
        log(f"  Status code: {resp.status_code}", "DEBUG")

        if resp.status_code == 200:
            log("✓ Publish started successfully!", "SUCCESS")
            try:
                response_data = resp.json()
                log(f"  Server response: {response_data}", "DEBUG")
            except:
                log(f"  Response (text): {resp.text[:200]}", "DEBUG")
        else:
            log(f"✗ ERROR: Status code {resp.status_code}", "ERROR")
            log(f"  Server response: {resp.text[:500]}", "ERROR")
            resp.raise_for_status()
    except requests.exceptions.HTTPError as e:
        log(f"✗ HTTP ERROR: {e}", "ERROR")
        sys.exit(1)
    except Exception as e:
        log(f"✗ UNEXPECTED ERROR: {e}", "ERROR")
        sys.exit(1)

    # Upload the files
    log("", "INFO")
    log("=" * 80, "INFO")
    log("STEP 3: UPLOADING FILES", "INFO")
    log("=" * 80, "INFO")

    file_url = f"{ROBUST_CDN_URL}fork/{FORK_ID}/publish/file"

    for idx, file in enumerate(files, 1):
        file_name = os.path.basename(file)
        file_size = os.path.getsize(file)
        file_size_mb = file_size / (1024*1024)

        log("", "INFO")
        log(f"File {idx}/{len(files)}: {file_name}", "INFO")
        log(f"  Size: {file_size_mb:.2f} MB ({file_size:,} bytes)", "DEBUG")
        log(f"  Path: {file}", "DEBUG")

        try:
            with open(file, "rb") as f:
                headers = {
                    "Content-Type": "application/octet-stream",
                    "Robust-Cdn-Publish-File": file_name,
                    "Robust-Cdn-Publish-Version": VERSION
                }

                log(f"  Sending POST request: {file_url}", "DEBUG")
                log(f"  Headers:", "DEBUG")
                log(f"    Robust-Cdn-Publish-File: {file_name}", "DEBUG")
                log(f"    Robust-Cdn-Publish-Version: {VERSION}", "DEBUG")
                log(f"  Starting upload...", "INFO")

                resp = session.post(file_url, data=f, headers=headers, timeout=300)

                log(f"  ✓ Response received", "SUCCESS")
                log(f"    Status code: {resp.status_code}", "DEBUG")

                if resp.status_code == 200:
                    log(f"  ✓ File {file_name} uploaded successfully!", "SUCCESS")
                else:
                    log(f"  ✗ ERROR: Status code {resp.status_code}", "ERROR")
                    log(f"    Server response: {resp.text[:500]}", "ERROR")
                    resp.raise_for_status()

        except requests.exceptions.ConnectionError as e:
            log(f"  ✗ CONNECTION ERROR: {e}", "ERROR")
            log(f"  Possible cause: timeout or an nginx configuration problem", "ERROR")
            log(f"  Check the client_max_body_size and proxy_read_timeout settings", "ERROR")
            sys.exit(1)
        except requests.exceptions.Timeout as e:
            log(f"  ✗ TIMEOUT: {e}", "ERROR")
            log(f"  The file took too long to upload (>300 seconds)", "ERROR")
            sys.exit(1)
        except Exception as e:
            log(f"  ✗ UNEXPECTED ERROR: {e}", "ERROR")
            sys.exit(1)

    # Finish the publish
    log("", "INFO")
    log("=" * 80, "INFO")
    log("STEP 4: FINISHING THE PUBLISH", "INFO")
    log("=" * 80, "INFO")

    finish_url = f"{ROBUST_CDN_URL}fork/{FORK_ID}/publish/finish"
    data = {
        "version": VERSION
    }

    log(f"Sending POST request: {finish_url}", "INFO")
    log(f"Request data:", "DEBUG")
    log(f"  version: {data['version']}", "DEBUG")

    try:
        resp = session.post(finish_url, json=data, timeout=30)
        log(f"✓ Response received", "SUCCESS")
        log(f"  Status code: {resp.status_code}", "DEBUG")

        if resp.status_code == 200:
            log("✓ Publish finished successfully!", "SUCCESS")
            try:
                response_data = resp.json()
                log(f"  Server response: {response_data}", "DEBUG")
            except:
                log(f"  Response (text): {resp.text[:200]}", "DEBUG")
        else:
            log(f"✗ ERROR: Status code {resp.status_code}", "ERROR")
            log(f"  Server response: {resp.text[:500]}", "ERROR")
            resp.raise_for_status()
    except Exception as e:
        log(f"✗ ERROR WHILE FINISHING: {e}", "ERROR")
        sys.exit(1)

    # Summary
    log("", "INFO")
    log("=" * 80, "INFO")
    log("PUBLISH COMPLETED SUCCESSFULLY! 🎉", "SUCCESS")
    log("=" * 80, "INFO")
    log(f"Version: {VERSION}", "INFO")
    log(f"Engine version: {engine_version}", "INFO")
    log(f"Files uploaded: {len(files)}", "INFO")
    log(f"Total size: {total_size / (1024*1024):.2f} MB", "INFO")
    log("=" * 80, "INFO")


def get_files_to_publish() -> Iterable[str]:
    """Get the list of files to publish"""
    for file in os.listdir(RELEASE_DIR):
        if file.endswith('.zip'):
            yield os.path.join(RELEASE_DIR, file)


def get_engine_version() -> str:
    """Get the engine version from RobustToolbox"""
    try:
        proc = subprocess.run(
            ["git", "describe", "--tags", "--abbrev=0"],
            stdout=subprocess.PIPE,
            cwd="RobustToolbox",
            check=True,
            encoding="UTF-8"
        )
        tag = proc.stdout.strip()
        if tag.startswith("v"):
            return tag[1:]  # Strip the v prefix
        return tag
    except subprocess.CalledProcessError as e:
        log(f"⚠ Could not get the engine version via git: {e}", "WARNING")
        log(f"  Falling back to the default version: 'unknown'", "WARNING")
        return "unknown"
    except Exception as e:
        log(f"⚠ Unexpected error while getting the engine version: {e}", "WARNING")
        log(f"  Falling back to the default version: 'unknown'", "WARNING")
        return "unknown"


if __name__ == '__main__':
    try:
        main()
    except KeyboardInterrupt:
        log("", "INFO")
        log("⚠ Publish interrupted by the user", "WARNING")
        sys.exit(1)
    except Exception as e:
        log("", "INFO")
        log(f"✗ FATAL ERROR: {e}", "ERROR")
        import traceback
        log(traceback.format_exc(), "ERROR")
        sys.exit(1)
