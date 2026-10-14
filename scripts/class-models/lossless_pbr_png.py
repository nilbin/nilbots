#!/usr/bin/env python3
"""Strict, dependency-free PNG validation and glTF MR channel packing.

The texture-donor pipeline runs in pinned Blender's bundled Python, where a
third-party image library is not guaranteed.  This module deliberately
supports only the provider format we archive for game PBR maps: non-interlaced
8-bit grayscale, grayscale-alpha, RGB, or RGBA PNGs.  It validates every PNG
chunk and scanline before returning samples, and never color-converts them.
"""

from __future__ import annotations

import hashlib
import struct
import zlib
from typing import Any


PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
MAX_PNG_BYTES = 128 * 1024 * 1024
MAX_PNG_DIMENSION = 8192
MAX_PNG_PIXELS = 4096 * 4096
COLOR_CHANNELS = {
    0: 1,  # grayscale
    2: 3,  # RGB
    4: 2,  # grayscale + alpha
    6: 4,  # RGBA
}
COLOR_TYPE_NAMES = {
    0: "grayscale",
    2: "rgb",
    4: "grayscale-alpha",
    6: "rgba",
}


def sha256_bytes(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def _paeth(left: int, above: int, upper_left: int) -> int:
    estimate = left + above - upper_left
    left_distance = abs(estimate - left)
    above_distance = abs(estimate - above)
    upper_left_distance = abs(estimate - upper_left)
    if left_distance <= above_distance and left_distance <= upper_left_distance:
        return left
    if above_distance <= upper_left_distance:
        return above
    return upper_left


def _unfilter_scanlines(
    filtered: bytes,
    *,
    width: int,
    height: int,
    channels: int,
    label: str,
) -> bytes:
    row_bytes = width * channels
    expected = height * (row_bytes + 1)
    if len(filtered) != expected:
        raise ValueError(
            f"{label} decompressed PNG length is {len(filtered)}, expected "
            f"{expected}"
        )
    result = bytearray(width * height * channels)
    previous = bytearray(row_bytes)
    source_offset = 0
    target_offset = 0
    for row_index in range(height):
        filter_type = filtered[source_offset]
        source_offset += 1
        encoded = filtered[source_offset : source_offset + row_bytes]
        source_offset += row_bytes
        decoded = bytearray(row_bytes)
        for index, value in enumerate(encoded):
            left = decoded[index - channels] if index >= channels else 0
            above = previous[index]
            upper_left = previous[index - channels] if index >= channels else 0
            if filter_type == 0:
                predictor = 0
            elif filter_type == 1:
                predictor = left
            elif filter_type == 2:
                predictor = above
            elif filter_type == 3:
                predictor = (left + above) // 2
            elif filter_type == 4:
                predictor = _paeth(left, above, upper_left)
            else:
                raise ValueError(
                    f"{label} row {row_index} uses invalid PNG filter "
                    f"{filter_type}"
                )
            decoded[index] = (value + predictor) & 0xFF
        result[target_offset : target_offset + row_bytes] = decoded
        target_offset += row_bytes
        previous = decoded
    return bytes(result)


def inspect_png(
    payload: bytes,
    *,
    label: str,
    include_samples: bool = False,
) -> dict[str, Any]:
    """Validate a provider PNG and return stable metadata and optional samples."""
    if len(payload) > MAX_PNG_BYTES:
        raise ValueError(
            f"{label} exceeds the {MAX_PNG_BYTES}-byte PNG safety limit"
        )
    if not payload.startswith(PNG_SIGNATURE):
        raise ValueError(f"{label} is not a PNG")

    offset = len(PNG_SIGNATURE)
    ihdr: tuple[int, int, int, int, int, int, int] | None = None
    idat_parts: list[bytes] = []
    saw_idat = False
    ended_idat = False
    saw_iend = False
    while offset < len(payload):
        if offset + 12 > len(payload):
            raise ValueError(f"{label} has a truncated PNG chunk")
        length = struct.unpack_from(">I", payload, offset)[0]
        chunk_type = payload[offset + 4 : offset + 8]
        chunk_end = offset + 12 + length
        if chunk_end > len(payload):
            raise ValueError(f"{label} has an out-of-bounds PNG chunk")
        data = payload[offset + 8 : offset + 8 + length]
        expected_crc = struct.unpack_from(">I", payload, offset + 8 + length)[0]
        observed_crc = zlib.crc32(data, zlib.crc32(chunk_type)) & 0xFFFFFFFF
        if observed_crc != expected_crc:
            rendered = chunk_type.decode("ascii", errors="replace")
            raise ValueError(f"{label} PNG {rendered} CRC does not match")
        offset = chunk_end

        if ihdr is None and chunk_type != b"IHDR":
            raise ValueError(f"{label} PNG IHDR is not first")
        if chunk_type == b"IHDR":
            if ihdr is not None or length != 13:
                raise ValueError(f"{label} has an invalid PNG IHDR")
            ihdr = struct.unpack(">IIBBBBB", data)
        elif chunk_type == b"IDAT":
            if ended_idat:
                raise ValueError(f"{label} has non-consecutive PNG IDAT chunks")
            saw_idat = True
            idat_parts.append(data)
        elif chunk_type == b"IEND":
            if length != 0 or not saw_idat or saw_iend:
                raise ValueError(f"{label} has an invalid PNG IEND")
            saw_iend = True
            if offset != len(payload):
                raise ValueError(f"{label} has bytes after PNG IEND")
            break
        else:
            if saw_idat:
                ended_idat = True
            # PNG critical chunks have an uppercase first byte. PLTE and every
            # indexed format are intentionally unsupported in this bounded
            # provider-ingest path.
            if chunk_type and 65 <= chunk_type[0] <= 90:
                rendered = chunk_type.decode("ascii", errors="replace")
                raise ValueError(f"{label} uses unsupported PNG chunk {rendered}")

    if ihdr is None or not saw_iend:
        raise ValueError(f"{label} is missing required PNG chunks")
    width, height, bit_depth, color_type, compression, filtering, interlace = ihdr
    if width <= 0 or height <= 0:
        raise ValueError(f"{label} has invalid PNG dimensions")
    if width > MAX_PNG_DIMENSION or height > MAX_PNG_DIMENSION:
        raise ValueError(
            f"{label} exceeds the {MAX_PNG_DIMENSION}px dimension safety limit"
        )
    if width * height > MAX_PNG_PIXELS:
        raise ValueError(
            f"{label} exceeds the {MAX_PNG_PIXELS}-pixel safety limit"
        )
    if bit_depth != 8 or color_type not in COLOR_CHANNELS:
        raise ValueError(
            f"{label} must be a non-indexed 8-bit grayscale/RGB PNG"
        )
    if compression != 0 or filtering != 0 or interlace != 0:
        raise ValueError(
            f"{label} must use standard compression/filtering and no interlace"
        )

    channels = COLOR_CHANNELS[color_type]
    expected_decoded = height * (width * channels + 1)
    inflater = zlib.decompressobj()
    filtered = inflater.decompress(b"".join(idat_parts), expected_decoded + 1)
    if inflater.unconsumed_tail or len(filtered) > expected_decoded:
        raise ValueError(f"{label} PNG expands beyond its declared dimensions")
    filtered += inflater.flush()
    if (
        not inflater.eof
        or inflater.unused_data
        or len(filtered) != expected_decoded
    ):
        raise ValueError(f"{label} has an invalid PNG deflate stream")
    samples = _unfilter_scanlines(
        filtered,
        width=width,
        height=height,
        channels=channels,
        label=label,
    )
    result: dict[str, Any] = {
        "bytes": len(payload),
        "sha256": sha256_bytes(payload),
        "mimeType": "image/png",
        "width": width,
        "height": height,
        "bitDepth": bit_depth,
        "colorType": color_type,
        "colorTypeName": COLOR_TYPE_NAMES[color_type],
        "channels": channels,
        "interlaced": False,
        "pixelSamplesSha256": sha256_bytes(samples),
    }
    if include_samples:
        result["samples"] = samples
    return result


def scalar_plane(payload: bytes, *, label: str) -> tuple[bytes, dict[str, Any]]:
    """Return exact 8-bit scalar samples from a grayscale provider map."""
    record = inspect_png(payload, label=label, include_samples=True)
    samples = record.pop("samples")
    channels = record["channels"]
    color_type = record["colorType"]
    scalar = bytearray(record["width"] * record["height"])
    for pixel_index in range(len(scalar)):
        offset = pixel_index * channels
        if color_type in {0, 4}:
            scalar[pixel_index] = samples[offset]
            continue
        red, green, blue = samples[offset : offset + 3]
        if red != green or red != blue:
            raise ValueError(
                f"{label} RGB samples are not grayscale at pixel {pixel_index}"
            )
        scalar[pixel_index] = red
    result = bytes(scalar)
    record["scalarSamplesSha256"] = sha256_bytes(result)
    return result, record


def _png_chunk(chunk_type: bytes, data: bytes) -> bytes:
    crc = zlib.crc32(data, zlib.crc32(chunk_type)) & 0xFFFFFFFF
    return (
        struct.pack(">I", len(data))
        + chunk_type
        + data
        + struct.pack(">I", crc)
    )


def encode_rgb8_png(*, width: int, height: int, samples: bytes) -> bytes:
    """Encode deterministic filter-none RGB8 PNG bytes without conversion."""
    if width <= 0 or height <= 0 or len(samples) != width * height * 3:
        raise ValueError("RGB8 PNG samples do not match declared dimensions")
    row_bytes = width * 3
    filtered = b"".join(
        b"\x00" + samples[offset : offset + row_bytes]
        for offset in range(0, len(samples), row_bytes)
    )
    ihdr = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)
    return (
        PNG_SIGNATURE
        + _png_chunk(b"IHDR", ihdr)
        + _png_chunk(b"IDAT", zlib.compress(filtered, level=9))
        + _png_chunk(b"IEND", b"")
    )


def compose_metallic_roughness_png(
    *,
    metallic_payload: bytes,
    roughness_payload: bytes,
) -> tuple[bytes, dict[str, Any]]:
    """Pack scalar maps into glTF RGB8 channels: R=1, G=roughness, B=metal."""
    metallic, metallic_record = scalar_plane(
        metallic_payload,
        label="Standalone metallic PNG",
    )
    roughness, roughness_record = scalar_plane(
        roughness_payload,
        label="Standalone roughness PNG",
    )
    metallic_size = (metallic_record["width"], metallic_record["height"])
    roughness_size = (roughness_record["width"], roughness_record["height"])
    if metallic_size != roughness_size:
        raise ValueError(
            "Standalone metallic and roughness PNG dimensions do not match: "
            f"{metallic_size} != {roughness_size}"
        )
    samples = bytearray(len(metallic) * 3)
    for index, (metallic_value, roughness_value) in enumerate(
        zip(metallic, roughness)
    ):
        offset = index * 3
        samples[offset] = 255
        samples[offset + 1] = roughness_value
        samples[offset + 2] = metallic_value
    payload = encode_rgb8_png(
        width=metallic_size[0],
        height=metallic_size[1],
        samples=bytes(samples),
    )
    output_record = inspect_png(
        payload,
        label="Composed metallic/roughness PNG",
        include_samples=True,
    )
    output_samples = output_record.pop("samples")
    output_red = output_samples[0::3]
    output_green = output_samples[1::3]
    output_blue = output_samples[2::3]
    if (
        output_red != b"\xff" * len(metallic)
        or output_green != roughness
        or output_blue != metallic
    ):
        raise ValueError("Composed metallic/roughness PNG channel proof failed")
    return payload, {
        "schema": "nilbots-lossless-metallic-roughness-compose-1",
        "algorithm": "RGB8 filter-none PNG: R=255, G=roughness, B=metallic",
        "metallic": metallic_record,
        "roughness": roughness_record,
        "output": output_record,
        "channelProof": {
            "outputRedSamplesSha256": sha256_bytes(output_red),
            "outputGreenSamplesSha256": sha256_bytes(output_green),
            "outputBlueSamplesSha256": sha256_bytes(output_blue),
            "greenRoughnessSamplesSha256": sha256_bytes(roughness),
            "blueMetallicSamplesSha256": sha256_bytes(metallic),
            "redConstant": 255,
        },
    }
