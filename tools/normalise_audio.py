# Levels the game's own songs and sounds so they're equally loud across the game (EBU R128, measured with ffmpeg):
#   music (assets/music, also christmas/ and halloween/)   -17 LUFS (the loudest every song reaches without its peaks
#                                                                     going over -1 dBTP: Seth's are already high)
#   the voice line (assets/soundeffects/voice)              -15 LUFS (a little above the music it leads into)
#   sound effects (buzzers, correct/wrong, podium jingles)  -18 LUFS
#   button sounds (assets/soundeffects/ui)                  -24 LUFS (quiet: they play all the time)
# Long files are measured by their integrated loudness, short ones (under 3 s: a buzzer, a click) by their loudest
# moment (max momentary loudness), as integrated loudness needs a few seconds; one too short even for that (a click) by
# its average level (RMS). The gain never pushes a peak over
# -1 dBTP. Files already within 0.5 dB of their target are left alone, so running it again changes nothing; new
# songs and sounds just get levelled. Re-encodes Ogg Vorbis at 128 kbps like the songs (MP3/WAV become .ogg).
# Run: py -I tools/normalise_audio.py [--dry-run]   (needs ffmpeg on PATH; then reimport: Godot --headless --editor)
import os
import re
import subprocess
import sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
TOLERANCE = 0.5
PEAK = -1.0

def target(path):
    rel = os.path.relpath(path, ROOT).replace("\\", "/")
    if rel.startswith("assets/music/"):
        return -17.0
    if rel.startswith("assets/soundeffects/voice/"):
        return -15.0
    if rel.startswith("assets/soundeffects/ui/"):
        return -24.0
    if rel.startswith("assets/soundeffects/"):
        return -18.0
    return None

def measure(path):
    """(loudness, true peak, seconds): integrated loudness, or the loudest moment for short files."""
    out = subprocess.run(["ffmpeg", "-nostats", "-hide_banner", "-i", path, "-af", "ebur128=peak=true", "-f", "null", "-"],
                         capture_output=True, text=True, encoding="utf-8", errors="replace").stderr
    seconds = 0.0
    m = re.search(r"Duration: (\d+):(\d+):([\d.]+)", out)
    if m:
        seconds = int(m.group(1)) * 3600 + int(m.group(2)) * 60 + float(m.group(3))
    summary = out[out.rfind("Summary:"):]
    integrated = float(re.search(r"I:\s+(-?[\d.]+) LUFS", summary).group(1))
    peak = float(re.search(r"Peak:\s+(-?[\d.inf]+) dBFS", summary).group(1)) if re.search(r"Peak:\s+(-?[\d.]+) dBFS", summary) else 0.0
    momentary = [float(v) for v in re.findall(r"\bM:\s*(-?[\d.]+)", out[:out.rfind("Summary:")])]
    loud = integrated if seconds >= 3 and integrated > -69 else max(momentary or [integrated])
    if loud < -69:
        rms = subprocess.run(["ffmpeg", "-nostats", "-hide_banner", "-i", path, "-af", "volumedetect", "-f", "null", "-"],
                             capture_output=True, text=True, encoding="utf-8", errors="replace").stderr
        loud = float(re.search(r"mean_volume: (-?[\d.]+) dB", rms).group(1))
    return loud, peak, seconds

def main():
    dry = "--dry-run" in sys.argv
    files = []
    for folder in ("assets/music", "assets/soundeffects"):
        for base, _, names in os.walk(os.path.join(ROOT, folder)):
            files += [os.path.join(base, n) for n in names if n.lower().endswith((".ogg", ".mp3", ".wav"))]
    for path in sorted(files):
        want = target(path)
        if want is None:
            continue
        loud, peak, seconds = measure(path)
        gain = min(want - loud, PEAK - peak)
        rel = os.path.relpath(path, ROOT)
        if abs(gain) < TOLERANCE:
            print(f"ok      {loud:6.1f} -> {want:5.1f}  {rel}")
            continue
        print(f"{'would' if dry else 'level'} {loud:6.1f} -> {loud + gain:5.1f} ({gain:+.1f} dB, peak {peak:.1f})  {rel}")
        if dry:
            continue
        out = os.path.splitext(path)[0] + ".ogg"
        tmp = out + ".tmp.ogg"
        # (Vorbis can't encode above 48 kHz: a 96 kHz click comes down to 48)
        rate = int(subprocess.run(["ffprobe", "-v", "error", "-show_entries", "stream=sample_rate", "-of", "csv=p=0", path],
                                  capture_output=True, text=True).stdout.split()[0])
        resample = ["-ar", "48000"] if rate > 48000 else []
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", path, "-af", f"volume={gain:.2f}dB", *resample, "-c:a", "libvorbis", "-b:a", "128k", tmp], check=True)
        if out != path:
            os.remove(path)
        os.replace(tmp, out)

if __name__ == "__main__":
    main()
