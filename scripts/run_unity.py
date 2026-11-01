"""Run only this task's Unity process group with a deadline and cleanup audit.

Usage: python3 scripts/run_unity.py --timeout 600 -- <command> [arguments...]
Normal batch commands own graceful shutdown. A deadline sends TERM, then KILL
only to the newly created process group. No claim of cleanup after host failure.
"""

import argparse
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import time


def group_members(group):
    output = subprocess.check_output(["ps", "-ax", "-o", "pid=,pgid=,comm="], text=True)
    members = []
    for line in output.splitlines():
        parts = line.strip().split(None, 2)
        if len(parts) == 3 and int(parts[1]) == group:
            members.append({"pid": int(parts[0]), "executable": parts[2]})
    return members


def stop_group(group):
    if not group_members(group):
        return
    os.killpg(group, signal.SIGTERM)
    for _ in range(100):
        if not group_members(group):
            return
        time.sleep(0.1)
    if group_members(group):
        os.killpg(group, signal.SIGKILL)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--timeout", type=int, default=600)
    parser.add_argument("--record", type=Path, default=Path("artifacts/process-audit.json"))
    parser.add_argument("command", nargs=argparse.REMAINDER)
    args = parser.parse_args()
    command = args.command[1:] if args.command[:1] == ["--"] else args.command
    if not command:
        parser.error("a command is required")
    child = subprocess.Popen(command, start_new_session=True)
    print(f"Started task process PID/PGID {child.pid}", flush=True)
    seen = {}
    started = time.monotonic()
    code = 1
    try:
        while child.poll() is None:
            for member in group_members(child.pid):
                seen[member["pid"]] = member
            if time.monotonic() - started > args.timeout:
                raise TimeoutError("Unity validation deadline exceeded")
            time.sleep(0.5)
        code = child.returncode
    except (TimeoutError, KeyboardInterrupt) as error:
        print(str(error), file=sys.stderr)
        code = 124
    finally:
        stop_group(child.pid)
        child.wait()
        time.sleep(0.2)
        remaining = group_members(child.pid)
        args.record.parent.mkdir(parents=True, exist_ok=True)
        args.record.write_text(json.dumps({"launcher_pid": child.pid, "observed": list(seen.values()),
                                           "remaining": remaining, "exit_code": code}, indent=2) + "\n")
        print(f"Task process cleanup verified: {not remaining}", flush=True)
        if remaining:
            code = 1
    return code


if __name__ == "__main__":
    sys.exit(main())
