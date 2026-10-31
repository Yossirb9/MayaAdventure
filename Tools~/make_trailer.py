"""Turn Builds/trailer (frames + sound log from the TrailerDirector) into an MP4 with the game's own music and effects.

python make_trailer.py <Builds/trailer dir> <out.mp4>
Needs numpy and imageio-ffmpeg (pip install imageio-ffmpeg).
"""
import os
import subprocess
import sys
import wave

import imageio_ffmpeg
import numpy as np

src, out = sys.argv[1], sys.argv[2]
RATE = 44100
info = dict(line.split('=', 1) for line in open(os.path.join(src, 'info.txt'), encoding='utf-8').read().split('\n') if '=' in line)
frames, fps, music_name = int(info['frames']), int(info['fps']), info['music'].strip()
duration = frames / fps
total = int(duration * RATE) + RATE


def load(name):
    path = os.path.join(src, 'audio', name + '.wav')
    if not os.path.exists(path):
        return None
    with wave.open(path) as w:
        ch, rate, n = w.getnchannels(), w.getframerate(), w.getnframes()
        data = np.frombuffer(w.readframes(n), dtype=np.int16).astype(np.float32) / 32768.0
    data = data.reshape(-1, ch).mean(axis=1)
    if rate != RATE:  # linear resample
        t = np.linspace(0, len(data) - 1, int(len(data) * RATE / rate))
        data = np.interp(t, np.arange(len(data)), data)
    return data


mix = np.zeros(total, dtype=np.float32)

# Music bed, looped, with a fade in and out.
music = load(music_name) if music_name else None
if music is not None and len(music) > 0:
    reps = int(np.ceil(total / len(music)))
    bed = np.tile(music, reps)[:total] * 0.55
    fade_in = int(0.6 * RATE)
    bed[:fade_in] *= np.linspace(0, 1, fade_in)
    end = int(duration * RATE)
    fade_out = int(1.8 * RATE)
    bed[end - fade_out:end] *= np.linspace(1, 0, fade_out)
    bed[end:] = 0
    mix += bed

# Effects at the moment they played in the game.
cache = {}
count = 0
for line in open(os.path.join(src, 'events.tsv'), encoding='utf-8'):
    parts = line.rstrip('\n').split('\t')
    if len(parts) < 3:
        continue
    t, name, vol = float(parts[0]), parts[1], float(parts[2])
    if name not in cache:
        cache[name] = load(name)
    clip = cache[name]
    if clip is None:
        continue
    i = int(t * RATE)
    n = min(len(clip), total - i)
    if n > 0:
        mix[i:i + n] += clip[:n] * vol * 0.8
        count += 1

mix = np.tanh(mix * 1.1) * 0.92  # soft limiter
pcm = (mix * 32767).astype(np.int16)
wav_path = os.path.join(src, 'mix.wav')
with wave.open(wav_path, 'w') as w:
    w.setnchannels(1)
    w.setsampwidth(2)
    w.setframerate(RATE)
    w.writeframes(pcm.tobytes())
print(f'audio: {count} effects over {duration:.1f}s')

ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()
fade_out_start = max(0.0, duration - 0.9)
cmd = [ffmpeg, '-y', '-loglevel', 'error', '-framerate', str(fps), '-i', os.path.join(src, 'frames', 'f%05d.jpg'),
       '-i', wav_path,
       '-vf', f'fade=t=in:st=0:d=0.5,fade=t=out:st={fade_out_start:.2f}:d=0.9',
       '-af', f'afade=t=out:st={fade_out_start:.2f}:d=0.9',
       '-c:v', 'libx264', '-preset', 'slow', '-crf', '19', '-pix_fmt', 'yuv420p',
       '-c:a', 'aac', '-b:a', '192k', '-shortest', '-movflags', '+faststart', out]
subprocess.run(cmd, check=True)
print('wrote', out, os.path.getsize(out) // 1024, 'KB')
