"""Apply the source-only runtime fixes to the exact tested SDK revision.

No game content is read. Conflicting SDK edits are rejected, not overwritten.
Use --skip-dependencies for patch validation without network/dependency setup.
"""
import argparse
from pathlib import Path
import subprocess
import sys

SDK_PIN = "babc769a94be5618010abfd075ed84f3c2bc09f5"
MSPACK_PIN = "305907723a4e7ab2018e58040059ffb5e77db837"


def git(source, *args, check=True):
    return subprocess.run(["git", "-C", str(source), *args], check=check,
                          capture_output=True, text=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("--skip-dependencies", action="store_true")
    args = parser.parse_args()
    source = args.source.resolve()
    patch = Path(__file__).resolve().parents[1] / "thirdparty/rexglue-sdk-runtime-fixes.patch"
    if git(source, "rev-parse", "HEAD").stdout.strip() != SDK_PIN:
        raise SystemExit(f"Expected SDK commit {SDK_PIN}; refusing to patch another revision.")
    if git(source, "apply", "--reverse", "--check", str(patch), check=False).returncode == 0:
        print("Runtime patch already applied.")
    else:
        result = git(source, "apply", "--check", str(patch), check=False)
        if result.returncode:
            raise SystemExit("SDK patch conflicts with local edits:\n" + result.stderr)
        git(source, "apply", str(patch))
        print("Applied audio, frame pacing, FPS readout and Windows export fixes.")
    if not args.skip_dependencies:
        # git submodule update reads the index, not the patched worktree gitlink.
        # Keep the tested public libmspack pin; 3059077 is reachable on the
        # public remote (ancestor of master), so fresh clones fetch it fine.
        git(source, "update-index", "--cacheinfo", f"160000,{MSPACK_PIN},thirdparty/libmspack")
        subprocess.run(["git", "-C", str(source), "submodule", "update", "--init", "--recursive"], check=True)
        subprocess.run([sys.executable, str(Path(__file__).with_name("prepare_renderer_mspack.py")),
                        str(source / "thirdparty/libmspack")], check=True)


if __name__ == "__main__":
    main()
