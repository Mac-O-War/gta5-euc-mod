"""Minimal DXT1 (BC1) DDS writer with mipmaps, numpy only.

Good enough for opaque game textures; quality is a bounding-box endpoint fit with an inset,
which is what most fast BC1 encoders do.
"""
import struct

import numpy as np


def _to565(c):
    """c: (..., 3) float 0..1 -> uint16 RGB565 and the colour it actually decodes to."""
    r = np.clip(np.rint(c[..., 0] * 31), 0, 31).astype(np.uint16)
    g = np.clip(np.rint(c[..., 1] * 63), 0, 63).astype(np.uint16)
    b = np.clip(np.rint(c[..., 2] * 31), 0, 31).astype(np.uint16)
    packed = (r << 11) | (g << 5) | b
    decoded = np.stack([r / 31.0, g / 63.0, b / 31.0], axis=-1)
    return packed, decoded


def encode_bc1(rgb: np.ndarray) -> bytes:
    """rgb: (H, W, 3) float 0..1, top row first. H and W multiples of 4."""
    h, w, _ = rgb.shape
    blocks = rgb.reshape(h // 4, 4, w // 4, 4, 3).transpose(0, 2, 1, 3, 4).reshape(-1, 16, 3)

    lo = blocks.min(axis=1)
    hi = blocks.max(axis=1)
    inset = (hi - lo) / 16.0
    c0, d0 = _to565(hi - inset)
    c1, d1 = _to565(lo + inset)

    # 4-colour mode needs c0 > c1; swap where needed.
    swap = c0 < c1
    c0[swap], c1[swap] = c1[swap], c0[swap].copy()
    d0[swap], d1[swap] = d1[swap], d0[swap].copy()

    palette = np.stack([d0, d1, (2 * d0 + d1) / 3.0, (d0 + 2 * d1) / 3.0], axis=1)  # (N, 4, 3)
    dist = ((blocks[:, :, None, :] - palette[:, None, :, :]) ** 2).sum(axis=-1)    # (N, 16, 4)
    idx = dist.argmin(axis=-1).astype(np.uint32)
    idx[c0 == c1] = 0

    shifts = (np.arange(16, dtype=np.uint32) * 2)
    bits = (idx << shifts).sum(axis=1).astype(np.uint32)

    out = np.zeros(len(blocks), dtype=[("c0", "<u2"), ("c1", "<u2"), ("bits", "<u4")])
    out["c0"], out["c1"], out["bits"] = c0, c1, bits
    return out.tobytes()


def _downsample(img: np.ndarray) -> np.ndarray:
    h, w, c = img.shape
    return img.reshape(h // 2, 2, w // 2, 2, c).mean(axis=(1, 3))


def write_dds_bc1(path: str, rgb: np.ndarray, min_mip: int = 4) -> int:
    """Write a DXT1 DDS with a full mip chain down to min_mip px. Returns the mip count."""
    h, w, _ = rgb.shape
    assert h % 4 == 0 and w % 4 == 0 and (h & (h - 1)) == 0 and (w & (w - 1)) == 0, "power-of-two size required"

    mips = []
    level = rgb
    while True:
        mips.append(encode_bc1(level))
        if min(level.shape[:2]) // 2 < min_mip:
            break
        level = _downsample(level)

    linear_size = (w // 4) * (h // 4) * 8
    flags = 0x1 | 0x2 | 0x4 | 0x1000 | 0x20000 | 0x80000  # CAPS|HEIGHT|WIDTH|PIXELFORMAT|MIPMAPCOUNT|LINEARSIZE
    pixel_format = struct.pack("<II4sIIIII", 32, 0x4, b"DXT1", 0, 0, 0, 0, 0)
    caps = 0x1000 | 0x400000 | 0x8  # TEXTURE|MIPMAP|COMPLEX
    header = struct.pack("<IIIIIII", 124, flags, h, w, linear_size, 0, len(mips))
    header += b"\0" * 44 + pixel_format + struct.pack("<IIIII", caps, 0, 0, 0, 0)
    assert len(header) == 124

    with open(path, "wb") as f:
        f.write(b"DDS " + header)
        for m in mips:
            f.write(m)
    return len(mips)
