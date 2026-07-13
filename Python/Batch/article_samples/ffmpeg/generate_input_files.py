#!/usr/bin/env python3
"""Generate sample MP4 input files for the ffmpeg Batch tutorial.

The Batch sample processes local MP4 files found in the ``InputFiles``
directory. To avoid committing binary media to the repository, this helper
synthesizes a few short, silent-to-tone sample clips locally with ffmpeg.

Requires ffmpeg on your PATH: https://ffmpeg.org/download.html

Usage:
    python generate_input_files.py
"""
from __future__ import print_function
import os
import shutil
import subprocess
import sys

# Number of sample clips to generate (matches the tutorial's InputFiles set).
FILE_COUNT = 5


def main():
    if shutil.which("ffmpeg") is None:
        sys.exit(
            "ffmpeg was not found on your PATH. Install it from "
            "https://ffmpeg.org/download.html and run this script again.")

    output_dir = os.path.join(
        os.path.dirname(os.path.abspath(__file__)), "InputFiles")
    os.makedirs(output_dir, exist_ok=True)

    for i in range(1, FILE_COUNT + 1):
        # Vary the length slightly so the clips are not identical.
        duration = 5 + i
        output_path = os.path.join(
            output_dir, "LowPriVMs-{}.mp4".format(i))
        print("Generating {}...".format(output_path))
        subprocess.check_call([
            "ffmpeg", "-y", "-loglevel", "error",
            "-f", "lavfi",
            "-i", "testsrc=duration={}:size=640x480:rate=30".format(
                duration),
            "-f", "lavfi",
            "-i", "sine=frequency=1000:duration={}".format(duration),
            "-c:v", "libx264", "-pix_fmt", "yuv420p",
            "-c:a", "aac", "-shortest",
            output_path,
        ])

    print("Done. Created {} sample files in {}.".format(
        FILE_COUNT, output_dir))


if __name__ == "__main__":
    main()
