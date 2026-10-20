#!/usr/bin/env python3
"""
Checks what's about to be pushed for secrets and personal information, and
stops the push if it finds any. The repo's pre-push hook (.githooks/pre-push)
runs it before every `git push`, and its pre-commit hook (.githooks/pre-commit)
runs it on the working tree before every `git commit`. Turn both hooks on once
per clone, from the root of the repo:

    git config core.hooksPath .githooks

Run it by hand from the root of the repo to check the working tree:

    python scripts/pre_push_check.py

The three checks:
1. gitleaks looks for secrets: tokens, API keys, passwords, private keys.
2. The identifier search looks for your own identifiers, listed one fixed
   string per line in ~/.claude/public-repo-identifiers.txt (copy
   scripts/public-repo-identifiers.example.txt to start it), matched without
   regard to case. A few kinds of hit are allowed (ALLOWED below lists them),
   such as links to this repo on GitHub, the copyright line in LICENSE,
   GitHub Actions pinned to a commit SHA, and noreply addresses.
3. Every commit's author and committer email is a GitHub noreply address
   (ending in @users.noreply.github.com).

What it scans:
- As the hook: only what the push sends. For each branch or tag, that's the
  commits the remote doesn't have yet: <remote sha>..<local sha>, or for a
  new branch or tag, the commits not on any of that remote's tracking
  branches. gitleaks scans those commits. The identifier search reads the
  lines they add, the names of the files they touch, their messages, the
  names of the pushed branches and tags, and annotated tags' messages.
  Deleting a remote branch or tag sends nothing, so it isn't checked.
- By hand, or as the pre-commit hook: every file in the working tree,
  tracked or untracked (ignored files are left out), the commits not on any
  remote yet, the current branch's name, and the email this clone commits
  with.

Ends with one "passed" line, or lists every finding with where it is and which
check found it, then exits with status 1. Exits with status 2 if gitleaks or
the identifier list is missing, so the push stops rather than going through
unchecked.
"""
import argparse
import glob
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
IDENTIFIERS = Path.home() / ".claude" / "public-repo-identifiers.txt"
EXAMPLE = "scripts/public-repo-identifiers.example.txt"
ZERO_SHA = re.compile(r"^0+$")
AS_HOOK = False  # set when git runs the script as the pre-push hook
# The at sign is written as \x40 in this file, so it passes its own search.
NOREPLY = re.compile(r"\x40users\.noreply\.github\.com$", re.I)

# Text an identifier may appear inside: (only in this file, or None for any
# file, commit message or ref name; pattern). A hit is allowed when it lies
# wholly inside a match of one of these.
ALLOWED = [
    # Links to this repo on GitHub, which hold its owner's username.
    (None, re.compile(r"github\.com/[\w.-]+/ai-toolkit\b[\w./#?=&%-]*", re.I)),
    # The copyright holder in the license.
    ("LICENSE", re.compile(r"^Copyright \(c\) \d{4}.*$")),
    # GitHub Actions pinned to a full commit SHA, as in the workflows.
    (None, re.compile(r"uses:\s*[\w.-]+/[\w./-]+\x40[0-9a-f]{40}\b")),
    # GitHub noreply addresses, and noreply addresses in commit trailers.
    (None, re.compile(r"[\w.+-]*\x40users\.noreply\.github\.com", re.I)),
    (None, re.compile(r"\bnoreply\x40[\w-]+(?:\.[\w-]+)+", re.I)),
    # Made-up addresses at the domains reserved for examples.
    (None, re.compile(r"[\w.+-]*\x40example\.(?:com|org|net)\b", re.I)),
    # A shell script passing on its arguments.
    (None, re.compile(r'"\$\x40"')),
]


def setup_error(msg):
    stopped = "\nThe push was stopped, since it couldn't be checked." if AS_HOOK else ""
    print(f"\nPre-push check couldn't run: {msg}{stopped}", file=sys.stderr)
    sys.exit(2)


def git(*args, ok=(0,)):
    r = subprocess.run(["git", "-c", "core.quotePath=false", *args], cwd=REPO, capture_output=True)
    if r.returncode not in ok:
        setup_error(f"`git {' '.join(args)}` failed: {r.stderr.decode('utf-8', 'replace').strip()}")
    return r.returncode, r.stdout.decode("utf-8", "replace")


def git_out(*args):
    return git(*args)[1]


# --- Setup -----------------------------------------------------------------

def find_gitleaks():
    found = shutil.which("gitleaks")
    if found:
        return found
    local = os.environ.get("LOCALAPPDATA")
    if local:
        # winget installs it here. A terminal opened before the install, or a
        # hook started from one, doesn't have it on PATH yet.
        for pattern in (r"Microsoft\WinGet\Links\gitleaks.exe",
                        r"Microsoft\WinGet\Packages\Gitleaks.Gitleaks_*\gitleaks.exe"):
            matches = glob.glob(os.path.join(local, pattern))
            if matches:
                return matches[0]
    setup_error("gitleaks isn't installed, or isn't on PATH. Install it (Windows: "
                "`winget install Gitleaks.Gitleaks`, macOS: `brew install gitleaks`, or a release "
                "from https://github.com/gitleaks/gitleaks/releases), then open a new terminal.")


def load_identifiers():
    if not IDENTIFIERS.is_file():
        setup_error(f"the identifier list {IDENTIFIERS} is missing. Create it from {EXAMPLE}, "
                    "with one of your own identifiers per line: your username, email addresses, "
                    "private project names and anything else that mustn't be published.")
    terms = []
    for line in IDENTIFIERS.read_text(encoding="utf-8-sig").splitlines():
        line = line.strip()
        if line and not line.startswith("#"):
            terms.append((line, re.compile(re.escape(line), re.I)))
    if not terms:
        setup_error(f"the identifier list {IDENTIFIERS} has no identifiers in it. Add one per line.")
    return terms


# --- Checks ----------------------------------------------------------------

class Report:
    def __init__(self, terms):
        self.terms = terms
        self.findings = []

    def add(self, check, where, detail):
        self.findings.append(f"[{check}] {where}: {detail}")

    def search(self, where, text, path=None):
        """Reports each identifier in one line of text that no allow rule covers."""
        allowed = [m.span() for only, rule in ALLOWED if only in (None, path) for m in rule.finditer(text)]
        for term, pattern in self.terms:
            for m in pattern.finditer(text):
                if not any(a <= m.start() and m.end() <= b for a, b in allowed):
                    excerpt = text.strip()
                    if len(excerpt) > 160:
                        excerpt = excerpt[:157] + "..."
                    self.add("identifiers", where, f'"{term}" in: {excerpt}')
                    break

    def search_lines(self, where, text, path=None):
        for n, line in enumerate(text.splitlines(), 1):
            self.search(f"{where}:{n}", line, path)


def run_gitleaks(report, exe, *args):
    r = subprocess.run([exe, *args, "--redact", "--no-banner", "--no-color", "--verbose",
                        "--exit-code", "1", "--log-level", "warn"],
                       cwd=REPO, capture_output=True)
    out = (r.stdout + r.stderr).decode("utf-8", "replace").strip()
    if r.returncode == 0:
        return
    if "leaks found" not in out:
        setup_error(f"gitleaks {args[0]} failed:\n{out}")
    out = "\n".join(f"      {line}" for line in out.splitlines() if line.strip())
    report.add("gitleaks", f"gitleaks {args[0]}", "found secrets (shown redacted):\n" + out)


def check_commits(report, revs):
    """Messages, added lines, file names and emails of the commits `git rev-list <revs>` lists."""
    commits = 0
    for entry in git_out("log", "--format=%x1e%h%x1f%ae%x1f%ce%x1f%B", *revs).split("\x1e")[1:]:
        sha, author, committer, message = entry.split("\x1f", 3)
        commits += 1
        for role, email in (("author", author), ("committer", committer)):
            if not NOREPLY.search(email):
                report.add("noreply", f"commit {sha}", f"the {role} email isn't a GitHub noreply address")
        report.search_lines(f"commit {sha} message", message)

    sha = path = None
    in_header = False
    line_no = 0
    diff = git_out("log", "-p", "-U0", "--no-color", "--no-ext-diff", "--no-renames",
                   "--diff-merges=first-parent", "--src-prefix=a/", "--dst-prefix=b/",
                   "--format=%x01%h", *revs)
    for line in diff.splitlines():
        if line.startswith("\x01"):
            sha, path, in_header = line[1:], None, False
        elif line.startswith("diff --git "):
            in_header, path = True, None
        elif in_header and line.startswith("+++ "):
            path = None if line[4:] == "/dev/null" else line[6:]
            if path:
                report.search(f"commit {sha} file name {path}", path)
        elif line.startswith("\x40\x40"):  # a hunk header, which gives the new line number
            in_header = False
            m = re.match(r"\x40\x40 -\S+ \+(\d+)", line)
            line_no = int(m.group(1)) if m else 0
        elif not in_header and path and line.startswith("+"):
            report.search(f"commit {sha} {path}:{line_no}", line[1:], path)
            line_no += 1
    return commits


def check_tag_object(report, sha, ref):
    """An annotated tag's message and tagger email."""
    if git_out("cat-file", "-t", sha).strip() != "tag":
        return
    header, _, message = git_out("cat-file", "-p", sha).partition("\n\n")
    tagger = re.search(r"^tagger .*<([^>]*)>", header, re.M)
    if tagger and not NOREPLY.search(tagger.group(1)):
        report.add("noreply", ref, "the tagger email isn't a GitHub noreply address")
    report.search_lines(f"{ref} message", message)


def check_push(report, exe, remote, stdin):
    commits = refs = 0
    for line in stdin.splitlines():
        parts = line.split()
        if len(parts) != 4:
            continue
        local_ref, local_sha, remote_ref, remote_sha = parts
        if ZERO_SHA.match(local_sha):
            continue  # deleting a remote branch or tag sends nothing
        refs += 1
        report.search(f"pushed ref {local_ref}", local_ref)
        if remote_ref != local_ref:
            report.search(f"pushed ref {remote_ref}", remote_ref)
        check_tag_object(report, local_sha, remote_ref)

        known = not ZERO_SHA.match(remote_sha) and git("cat-file", "-e", f"{remote_sha}^{{commit}}", ok=(0, 1, 128))[0] == 0
        revs = [f"{remote_sha}..{local_sha}"] if known else [local_sha, "--not", f"--remotes={remote}"]
        if git_out("rev-list", "--max-count=1", *revs).strip():
            run_gitleaks(report, exe, "git", "--log-opts", " ".join(revs), str(REPO))
            commits += check_commits(report, revs)
    return f"{commits} new commit{'s' * (commits != 1)} on {refs} branch{'es' * (refs != 1)} or tag{'s' * (refs != 1)}"


def check_working_tree(report, exe):
    run_gitleaks(report, exe, "dir", str(REPO))
    run_gitleaks(report, exe, "git", str(REPO))

    files = [f for f in git_out("ls-files", "-z", "-co", "--exclude-standard").split("\0") if f]
    for rel in files:
        report.search(f"file name {rel}", rel)
        try:
            data = (REPO / rel).read_bytes()
        except OSError:
            continue  # deleted but not yet staged
        if b"\0" in data:
            continue  # binary
        report.search_lines(rel, data.decode("utf-8", "replace"), rel)

    branch = git_out("rev-parse", "--abbrev-ref", "HEAD").strip()
    report.search("current branch", branch)
    email = git_out("config", "user.email").strip() if git("config", "user.email", ok=(0, 1))[0] == 0 else ""
    if not NOREPLY.search(email):
        report.add("noreply", "git config user.email", "this clone doesn't commit with a GitHub noreply address")
    commits = check_commits(report, ["HEAD", "--not", "--remotes"])
    return f"{len(files)} files and {commits} unpushed commit{'s' * (commits != 1)}"


def main():
    p = argparse.ArgumentParser(description="Check a push, or the working tree, for secrets and personal information.")
    p.add_argument("remote", nargs="?", help="the remote's name (git passes it to the pre-push hook)")
    p.add_argument("url", nargs="?", help="the remote's URL (git passes it to the pre-push hook)")
    args = p.parse_args()
    global AS_HOOK
    AS_HOOK = bool(args.remote)
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(errors="replace")

    exe = find_gitleaks()
    report = Report(load_identifiers())
    if args.remote:
        scanned = check_push(report, exe, args.remote, sys.stdin.read())
    else:
        scanned = check_working_tree(report, exe)

    if not report.findings:
        print(f"Pre-push check passed: no secrets, identifiers or non-noreply emails in {scanned}.")
        return
    print(f"Pre-push check FAILED: {len(report.findings)} finding{'s' * (len(report.findings) != 1)} "
          f"in {scanned}.\n", file=sys.stderr)
    for finding in report.findings:
        print(f"  {finding}", file=sys.stderr)
    print("\nRemove each one (rewriting the commits that hold it) and try again. Nothing was pushed."
          if args.remote else "\nRemove each one before pushing.", file=sys.stderr)
    sys.exit(1)


if __name__ == "__main__":
    main()
