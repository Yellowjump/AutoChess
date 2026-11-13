"""Assemble the opening picture master from the Blender renders and paper inserts."""

import argparse
from pathlib import Path
import subprocess

import av
import cv2
import numpy as np


FPS = 24
SIZE = (1280, 604)
DURATION = 6.4
DISSOLVE = .40
ART = Path(__file__).resolve().parents[2] / "Client/Assets/GameMain/Art/Opening"


def still(path, t, center_start, center_end, zoom_start, zoom_end):
    height, width = path.shape[:2]
    t = min(1, max(0, t / DURATION))
    t = t * t * (3 - 2 * t)
    zoom = zoom_start + (zoom_end - zoom_start) * t
    crop_width = min(width, height * SIZE[0] / SIZE[1]) / zoom
    crop_height = crop_width * SIZE[1] / SIZE[0]
    cx, cy = [a + (b - a) * t for a, b in zip(center_start, center_end)]
    left = np.clip(cx * width - crop_width / 2, 0, width - crop_width)
    top = np.clip(cy * height - crop_height / 2, 0, height - crop_height)
    transform = np.float32([[SIZE[0] / crop_width, 0, -left * SIZE[0] / crop_width],
                            [0, SIZE[1] / crop_height, -top * SIZE[1] / crop_height]])
    return cv2.warpAffine(path, transform, SIZE, flags=cv2.INTER_CUBIC, borderMode=cv2.BORDER_REPLICATE)


class Motion:
    def __init__(self, path):
        self.clip = av.open(str(path))
        stream = self.clip.streams.video[0]
        if stream.average_rate != FPS or (stream.width, stream.height) != SIZE:
            raise ValueError(f"{path} must be rendered at {SIZE} and {FPS} fps")
        self.frames = self.clip.decode(stream)
        self.index = -1
        self.image = None

    def frame(self, time):
        index = round(max(0, min(DURATION, time)) * FPS)
        while self.index < index:
            frame = next(self.frames, None)
            if frame is None:
                if self.image is None:
                    raise ValueError("Missing rendered frames")
                break
            self.index += 1
            self.image = frame.to_ndarray(format="bgr24")
        return self.image

    def close(self):
        self.clip.close()


def insert(name, start, end, zoom):
    image = cv2.imdecode(np.fromfile(ART / name, dtype=np.uint8), cv2.IMREAD_COLOR)
    if image is None:
        raise ValueError(f"Cannot decode {name}")
    return lambda t: still(image, t, start, end, 1, zoom)


def edit(ffmpeg, render_dir, output):
    motion = [Motion(render_dir / f"cut-{name}.mp4") for name in ("ferry", "child", "gate")]
    shots = [
        motion[0].frame,
        insert("FerryLedger.png", (.49, .52), (.39, .51), 1.12),
        motion[1].frame,
        insert("GuixuFormation.png", (.5, .48), (.55, .51), 1.10),
        motion[2].frame,
    ]
    command = [
        str(ffmpeg), "-hide_banner", "-loglevel", "error", "-y",
        "-f", "rawvideo", "-pix_fmt", "bgr24", "-s", f"{SIZE[0]}x{SIZE[1]}", "-r", str(FPS),
        "-i", "-", "-an", "-c:v", "libx264", "-profile:v", "baseline", "-g", str(FPS), "-crf", "17",
        "-pix_fmt", "yuv420p", "-vf", "scale=out_color_matrix=bt709",
        "-bsf:v", "h264_metadata=colour_primaries=1:transfer_characteristics=1:matrix_coefficients=1",
        "-movflags", "+faststart+write_colr", str(output),
    ]
    try:
        with subprocess.Popen(command, stdin=subprocess.PIPE) as encoder:
            with encoder.stdin as stdin:
                for frame in range(round(DURATION * len(shots) * FPS)):
                    time = frame / FPS
                    shot = int(time / DURATION)
                    cut = round(time / DURATION)
                    boundary = cut * DURATION
                    if 0 < cut < len(shots) and abs(time - boundary) < DISSOLVE / 2:
                        alpha = (time - boundary) / DISSOLVE + .5
                        alpha = alpha * alpha * (3 - 2 * alpha)
                        prior = shots[cut - 1](time - boundary + DURATION)
                        after = shots[cut](time - boundary)
                        image = cv2.addWeighted(prior, 1 - alpha, after, alpha, 0)
                    else:
                        image = shots[shot](time - shot * DURATION)
                    stdin.write(image.tobytes())
            if encoder.wait() != 0:
                raise RuntimeError("Could not encode the opening picture")
    finally:
        for segment in motion:
            segment.close()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("ffmpeg", type=Path)
    parser.add_argument("render_dir", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    edit(args.ffmpeg, args.render_dir, args.output)
