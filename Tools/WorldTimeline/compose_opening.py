"""Render the original score and river effects for the opening Timeline."""

import math
from pathlib import Path
import wave

import numpy as np
from PIL import Image, ImageFilter


RATE = 44100
LENGTH = 32
ROOT = Path(__file__).resolve().parents[2] / "Client/Assets/GameMain/Art/Opening"
RNG = np.random.default_rng(417)
TIME = np.arange(RATE * LENGTH) / RATE


def stereo():
    return np.zeros((len(TIME), 2))


def add(bus, at, samples, gain, pan=0):
    start = round(at * RATE)
    samples = samples[: len(bus) - start] * gain
    bus[start : start + len(samples), 0] += samples * math.sqrt((1 - pan) / 2)
    bus[start : start + len(samples), 1] += samples * math.sqrt((1 + pan) / 2)


def note(midi, duration, kind):
    t = np.arange(round(RATE * duration)) / RATE
    hz = 440 * 2 ** ((midi - 69) / 12)
    phase = 2 * np.pi * hz * t
    if kind == "string":
        phase += .45 * (1 - np.exp(-18 * t))
        sound = sum(
            np.sin(phase * harmonic * (1 + .0002 * harmonic**2) + .15 * harmonic)
            * level * np.exp(-t * (1.2 + .52 * harmonic))
            for harmonic, level in enumerate([1, .52, .28, .24, .11, .07, .04], 1)
        )
        sound += RNG.normal(0, .075, len(t)) * np.exp(-65 * t)
        attack = .009
    else:
        phase += .34 * np.sin(2 * np.pi * 4.5 * t) * np.minimum(1, t)
        breath = np.convolve(RNG.normal(0, 1, len(t)), np.hanning(17) / 8, "same")
        sound = (.8 * np.sin(phase) + .22 * np.sin(2 * phase) +
                 .12 * np.sin(3 * phase) + .075 * breath)
        sound *= .93 + .07 * np.sin(2 * np.pi * 2.1 * t)
        attack = .27
    return sound * np.minimum(1, t / attack) * np.minimum(1, (duration - t) / .55)


def bell(duration, hz):
    t = np.arange(round(RATE * duration)) / RATE
    partials = [(1, 1), (1.47, .34), (2.08, .21), (3.11, .14), (4.9, .045)]
    return sum(gain * np.sin(2 * np.pi * hz * ratio * t) * np.exp(-t * (.75 + ratio))
               for ratio, gain in partials) * np.minimum(1, t / .015)


def write(name, signal, peak):
    signal *= np.minimum(1, (LENGTH - TIME) / .36)[:, None]
    signal *= peak / np.max(np.abs(signal))
    with wave.open(str(ROOT / name), "wb") as output:
        output.setparams((2, 2, RATE, len(TIME), "NONE", "not compressed"))
        output.writeframes((signal * 32767).astype("<i2").tobytes())


def compose():
    score = stereo()
    motif = [
        (1.9, 50, .32), (3.5, 57, .23), (5.7, 60, .20), (7.25, 57, .18),
        (9.2, 53, .28), (11.45, 55, .16), (12.8, 50, .20), (14.65, 57, .24),
        (16.2, 48, .18), (19.85, 45, .21), (23.8, 57, .26), (25.0, 60, .27),
        (26.6, 62, .30), (28.45, 57, .24), (30.0, 50, .31), (30.15, 62, .16),
    ]
    for i, (at, pitch, gain) in enumerate(motif):
        add(score, at, note(pitch, 3, "string"), gain, -.32 if i % 2 else .28)
    for at, pitch, duration, gain in [
        (10.5, 62, 2.1, .058), (12.4, 60, 2.5, .060), (14.3, 57, 2.8, .047),
        (24.9, 65, 1.9, .049), (26.6, 67, 1.55, .051), (28.05, 69, 3.2, .055),
    ]:
        add(score, at, note(pitch, duration, "flute"), gain, .20)
    tremor = np.exp(-((TIME - 22.0) / 3.2) ** 4)
    score += (.040 * tremor * (np.sin(2 * np.pi * 54.9 * TIME) +
                              .38 * np.sin(2 * np.pi * 109.4 * TIME)))[:, None]
    add(score, 19.2, bell(5, 73), .18, -.06)
    add(score, 28.0, bell(3.9, 148), .086, .10)
    dry = score.copy()
    for delay, gain in [(0.083, .21), (.173, .13), (.349, .09), (.53, .045)]:
        shift = round(RATE * delay)
        score[shift:] += dry[:-shift, ::-1] * gain
    write("TheTwelfthName.wav", score, .66)

    effects = stereo()
    for channel in range(2):
        noise = RNG.normal(0, 1, len(TIME))
        taps = np.hanning(129)
        river = np.convolve(noise, taps / taps.sum(), "same")
        river *= .27 + .17 * np.sin(2 * np.pi * .41 * TIME + .3 * channel) ** 2
        river *= 1 - .85 * np.exp(-((TIME - 22.3) / 3.4) ** 6)
        effects[:, channel] = river
    for at, pan, gain in [(1.15, -.45, .07), (5.3, .35, .13), (13.05, -.32, .085), (17.1, -.48, .06)]:
        t = np.arange(RATE) / RATE
        splash = np.convolve(RNG.normal(0, 1, len(t)), np.hanning(39) / 20, "same")
        add(effects, at, splash * np.minimum(1, t * 39) * np.exp(-t * 7), gain, pan)
    t = np.arange(round(1.2 * RATE)) / RATE
    scrape = np.convolve(RNG.normal(0, 1, len(t)), np.hanning(15) / 8, "same")
    add(effects, 10.55, scrape * np.sin(np.pi * t / 1.2) ** 2 *
        (.5 + .5 * np.sin(2 * np.pi * 8 * t) ** 2), .08, -.27)
    write("RiverAndPaper.wav", effects, .31)


def paint_mist():
    opacity = np.zeros((256, 512))
    for cols, rows, weight in [(6, 3, .59), (17, 8, .28), (46, 21, .13)]:
        noise = Image.fromarray(RNG.integers(0, 256, (rows, cols), dtype=np.uint8))
        opacity += np.asarray(noise.resize((512, 256), Image.Resampling.BICUBIC)) * weight / 255
    y, x = np.mgrid[0:1:256j, 0:1:512j]
    opacity = np.maximum(0, (opacity - .29) / .71) * np.exp(-((y - .64 + .09 * np.sin(6 * x)) / .24) ** 2)
    rgba = Image.new("RGBA", (512, 256), "white")
    rgba.putalpha(Image.fromarray((opacity * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(2)))
    rgba.save(ROOT / "DriftingMist.png")

    shade = Image.new("RGBA", (4, 256), "black")
    shade.putalpha(Image.fromarray(np.broadcast_to(
        (np.maximum(0, np.linspace(0, 1, 256) - .60) / .40 * 192).astype(np.uint8)[:, None],
        (256, 4)).copy()))
    shade.save(ROOT / "SubtitleShade.png")


if __name__ == "__main__":
    compose()
    paint_mist()
