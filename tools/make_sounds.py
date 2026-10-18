#!/usr/bin/env python3
"""Synthesises AVAKarateka's sound effects and tunes into ../src/AVAKarateka/Sounds as 16-bit mono WAV.

Nothing is sampled from any game: every effect is built here from square, triangle and noise voices. The title plays
Sakura Sakura, the traditional Japanese song, on a plucked koto voice; the march in play, over taiko drums, is a new
composition. The kiai is spoken by the formant voice in speech.py. Run it from anywhere:
    python3 tools/make_sounds.py
"""
import math
import os
import random
import struct
import wave

from speech import say


RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "AVAKarateka", "Sounds")
RNG = random.Random(1984)


def write(name, samples, volume=0.7):
    peak = max(1e-9, max(abs(s) for s in samples))
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s / peak * volume * 32767)) for s in samples))


def voice(kind, phase):
    p = phase % 1.0
    if kind == "square":
        return 1.0 if p < 0.5 else -1.0
    if kind == "pulse":
        return 1.0 if p < 0.25 else -1.0
    if kind == "tri":
        return 4 * p - 1 if p < 0.5 else 3 - 4 * p
    if kind == "saw":
        return 2 * p - 1
    if kind == "sine":
        return math.sin(2 * math.pi * p)
    return RNG.uniform(-1, 1)


def tone(seconds, f0, f1=None, kind="square", attack=0.004, release=1.0, vibrato=0.0, vib_rate=8.0, gain=1.0):
    """A note gliding from f0 to f1 (exponentially), with a short attack and a shaped decay."""
    n = int(RATE * seconds)
    f1 = f1 or f0
    out, phase, held = [], 0.0, 0.0
    for i in range(n):
        t = i / RATE
        f = f0 * (f1 / f0) ** (i / max(1, n - 1))
        if vibrato:
            f *= 1 + vibrato * math.sin(2 * math.pi * vib_rate * t)
        phase += f / RATE
        if kind == "noise":
            # Noise re-sampled at the voice's rate: a lower 'pitch' sounds rougher.
            if int(phase) != int(phase - f / RATE):
                held = RNG.uniform(-1, 1)
            s = held
        else:
            s = voice(kind, phase)
        env = min(1.0, t / attack) * (1 - i / n) ** release
        out.append(s * env * gain)
    return out


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def cat(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, v in enumerate(p):
            out[i] += v
    return out


NOTES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def hz(note):
    n = NOTES[note[0]]
    rest = note[1:]
    while rest and rest[0] in "#b":
        n += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    return 440.0 * 2 ** ((12 * (int(rest) + 1) + n - 69) / 12)


def tune(bpm, lead, bass, kind="pulse"):
    """Two voices: a lead and a bass line, as (note, beats) lists; '-' is a rest."""
    beat = 60.0 / bpm

    def line(notes, k, gain, release):
        out = []
        for note, beats in notes:
            d = beats * beat
            out.extend(silence(d) if note == "-" else tone(d, hz(note), kind=k, release=release, gain=gain))
        return out

    return mix(line(lead, kind, 0.55, 0.6), line(bass, "tri", 0.6, 0.3))


def seq(bpm, notes, kind="pulse", gain=0.55, release=0.6):
    beat = 60.0 / bpm
    out = []
    for note, beats in notes:
        d = beats * beat
        out.extend(silence(d) if note == "-" else tone(d, hz(note), kind=kind, release=release, gain=gain))
    return out




def koto(seconds, note, gain=0.6):
    """A plucked string: bright at first, dying away."""
    f = hz(note)
    return mix(tone(seconds, f, kind="tri", attack=0.002, release=1.0, gain=gain), tone(min(seconds, 0.12), f * 2, kind="pulse", attack=0.001, release=1.0, gain=gain * 0.25))


def taiko(seconds, gain=1.0):
    return cat(mix(tone(0.22, 110, 45, "sine", attack=0.001, release=1.0, gain=gain), tone(0.04, 700, 200, "noise", attack=0.001, release=1.0, gain=gain * 0.4)), silence(max(0, seconds - 0.22)))


def sakura():
    """Sakura Sakura (traditional): the melody in the old miyako-bushi scale."""
    q, h, e = 1, 2, 0.5
    return [("A4", q), ("A4", q), ("B4", h), ("A4", q), ("A4", q), ("B4", h),
            ("A4", q), ("B4", q), ("C5", q), ("B4", q), ("A4", q), ("B4", e), ("A4", e), ("F4", h),
            ("E4", q), ("C4", q), ("E4", q), ("F4", q), ("E4", q), ("E4", e), ("C4", e), ("B3", h),
            ("A4", q), ("B4", q), ("C5", q), ("B4", q), ("A4", q), ("B4", e), ("A4", e), ("F4", h),
            ("E4", q), ("C4", q), ("E4", q), ("F4", q), ("E4", q), ("E4", e), ("C4", e), ("B3", h),
            ("A4", q), ("A4", q), ("B4", h), ("A4", q), ("A4", q), ("B4", h),
            ("E4", q), ("F4", q), ("B4", e), ("A4", e), ("F4", q), ("E4", 4)]


def play_koto(bpm, notes):
    beat = 60.0 / bpm
    out = []
    for note, beats in notes:
        out.extend(silence(beats * beat) if note == "-" else koto(beats * beat, note))
    return out


def drums(bpm, bars):
    beat = 60.0 / bpm
    out = []
    for i in range(bars):
        out.extend(cat(taiko(beat), taiko(beat * 0.5, 0.5), taiko(beat * 0.5, 0.5), taiko(beat), taiko(beat, 0.7)))
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    for f in os.listdir(OUT):
        if f.endswith(".wav"):
            os.remove(os.path.join(OUT, f))
    # The fight: a strike through the air, a blow landing, a blow met by a guard.
    write("Swing.wav", tone(0.12, 1800, 500, "noise", attack=0.01, release=1.0), 0.18)
    write("Hit.wav", mix(tone(0.1, 180, 70, "square", attack=0.001, release=1.0, gain=0.7), tone(0.06, 2400, 600, "noise", attack=0.001, release=1.0, gain=0.6)), 0.4)
    write("Block.wav", mix(tone(0.05, 900, 700, "square", attack=0.001, release=1.0, gain=0.4), tone(0.04, 3000, 1500, "noise", attack=0.001, release=1.0, gain=0.4)), 0.25)
    write("Hurt.wav", cat(tone(0.06, 500, 300, "square", release=0.8), tone(0.1, 300, 160, "square", release=1.0)), 0.3)
    write("Fall.wav", cat(tone(0.25, 400, 90, "square", release=0.6, gain=0.6), mix(tone(0.3, 70, 35, "sine", release=1.0), tone(0.12, 500, 100, "noise", release=1.0, gain=0.5))), 0.42)
    write("Kick.wav", mix(tone(0.12, 220, 60, "square", attack=0.001, release=1.0), tone(0.08, 1800, 400, "noise", attack=0.001, release=1.0, gain=0.6)), 0.42)
    write("Stance.wav", cat(tone(0.03, hz("E5"), kind="pulse", release=0.5), tone(0.05, hz("A5"), kind="pulse", release=0.8)), 0.16)
    write("Step.wav", tone(0.04, 300, 150, "noise", attack=0.001, release=1.0), 0.1)
    # A guard bowing before the fight: a gong.
    write("Bow.wav", mix(tone(1.4, 98, 96, "sine", attack=0.005, release=1.0), tone(1.0, 233, 230, "sine", attack=0.005, release=1.0, gain=0.4),
                         tone(0.6, 341, kind="tri", attack=0.005, release=1.0, gain=0.25)), 0.4)
    write("Screech.wav", tone(0.5, 2600, 1500, "saw", attack=0.02, release=0.7, vibrato=0.06, vib_rate=30), 0.24)
    write("Rescue.wav", play_koto(140, [("E5", 0.5), ("F5", 0.5), ("A5", 0.5), ("B5", 0.5), ("C6", 0.5), ("E6", 2)]), 0.4)
    # The kiai, when a guard goes down.
    write("SayHai.wav", mix(say("HH AA1 IY !", rate=RATE, pitch=150, speed=0.9, size=1.0), [s * 0.3 for s in say("HH AA1 IY !", rate=RATE, pitch=225, speed=0.9, size=1.0, seed=11)]), 0.5)

    # In play: a march of its own over taiko drums.
    q = 1
    march = [("A4", q), ("-", q), ("E4", q), ("F4", q), ("A4", q), ("B4", q), ("C5", 2),
             ("B4", q), ("A4", q), ("F4", q), ("E4", q), ("F4", 2), ("E4", 2),
             ("A4", q), ("B4", q), ("C5", q), ("E5", q), ("F5", q), ("E5", q), ("C5", 2),
             ("B4", q), ("C5", q), ("B4", q), ("F4", q), ("A4", 4)]
    bpm = 112
    write("Music.wav", mix(play_koto(bpm, march + march), drums(bpm, 16)), 0.26)
    # The title: Sakura Sakura.
    write("TitleTune.wav", cat(play_koto(76, sakura()), silence(2.0)), 0.3)
    write("Victory.wav", cat(play_koto(100, [("A4", 0.5), ("B4", 0.5), ("C5", 0.5), ("E5", 0.5), ("F5", 1), ("E5", 1), ("B5", 1), ("A5", 3)])), 0.4)
    write("GameOver.wav", mix(play_koto(60, [("E5", 1), ("C5", 1), ("B4", 1), ("F4", 1), ("E4", 3)]), cat(taiko(1), taiko(1), taiko(1), taiko(1), taiko(3))), 0.42)


if __name__ == "__main__":
    main()
