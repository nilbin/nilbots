import importlib.util
import math
import stat
import struct
import tempfile
import unittest
import wave
import zipfile
from pathlib import Path


SCRIPT = Path(__file__).resolve().parents[1] / "compile_soundtrack.py"
SPEC = importlib.util.spec_from_file_location("compile_soundtrack", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
PIPELINE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(PIPELINE)


class CompileSoundtrackTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)

    def tearDown(self):
        self.temporary.cleanup()

    def write_wav(
        self,
        path,
        frames=32000,
        sample_rate=8000,
        channels=1,
        amplitude=1000,
    ):
        with wave.open(str(path), "wb") as writer:
            writer.setnchannels(channels)
            writer.setsampwidth(2)
            writer.setframerate(sample_rate)
            samples = bytearray()
            for index in range(frames):
                value = int(
                    amplitude * math.sin(2.0 * math.pi * 220.0 * index / sample_rate)
                )
                samples.extend(value.to_bytes(2, "little", signed=True) * channels)
            writer.writeframes(bytes(samples))

    def stem_config(self):
        return [
            {
                "id": "bed",
                "source": "Bed.wav",
                "label": "Bed",
                "role": "atmosphere",
                "gainDb": 0.0,
                "response": {"minimum": 0.0, "full": 0.5},
            },
            {
                "id": "drive",
                "source": "Drive.wav",
                "label": "Drive",
                "role": "rhythm",
                "gainDb": 0.0,
                "response": {"minimum": 0.4, "full": 0.8},
            },
        ]

    def make_archive(self, members):
        path = self.root / "stems.zip"
        with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as archive:
            for name, source in members:
                if isinstance(source, zipfile.ZipInfo):
                    archive.writestr(source, b"target")
                else:
                    archive.write(source, name)
        return path

    def route_section(
        self,
        section_id,
        classification,
        role,
        bar_count=1,
        minimum_bars=None,
    ):
        output = {
            "id": section_id,
            "classification": classification,
            "role": role,
            "barCount": bar_count,
        }
        if minimum_bars is not None:
            output["repeat"] = {"minimumBars": minimum_bars}
        return {"output": output}

    def route_edge(
        self,
        source,
        target,
        timing="next-quantum",
        quantize_bars=1,
        weight=1,
    ):
        return {
            "from": source,
            "to": target,
            "timing": timing,
            "quantizeBars": quantize_bars,
            "weight": weight,
        }

    def adaptive_validation_fixture(self):
        sections = {
            "hold-" + state: self.route_section(
                "hold-" + state,
                state,
                "hold",
                bar_count=4,
                minimum_bars=8,
            )
            for state in PIPELINE.GAMEPLAY_CLASSIFICATIONS
        }
        sections["resolve"] = self.route_section(
            "resolve", "resolve", "resolve", bar_count=1
        )
        transitions = []
        for source in PIPELINE.GAMEPLAY_CLASSIFICATIONS:
            source_id = "hold-" + source
            for target in PIPELINE.GAMEPLAY_CLASSIFICATIONS:
                if target != source:
                    transitions.append(
                        self.route_edge(source_id, "hold-" + target)
                    )
            transitions.append(self.route_edge(source_id, "resolve"))
        config = {
            "adaptiveLatencyBudgetBars": {
                "gameplay": 1,
                "resolve": 1,
            }
        }
        return config, sections, transitions

    def test_secure_ingestion_accepts_exact_regular_file_mappings(self):
        bed = self.root / "bed.wav"
        drive = self.root / "drive.wav"
        self.write_wav(bed)
        self.write_wav(drive)
        archive = self.make_archive([("Bed.wav", bed), ("Drive.wav", drive)])
        destination = self.root / "extracted"
        destination.mkdir()

        paths, summary = PIPELINE.extract_stems_safely(
            archive, self.stem_config(), destination
        )

        self.assertEqual({"bed", "drive"}, set(paths))
        self.assertEqual(2, summary["memberCount"])
        pcm = PIPELINE.validate_wav_alignment(paths, self.stem_config())
        self.assertEqual(32000, pcm["frames"])
        self.assertEqual(8000, pcm["sampleRate"])

    def test_secure_ingestion_rejects_traversal(self):
        bed = self.root / "bed.wav"
        self.write_wav(bed)
        archive = self.root / "traversal.zip"
        with zipfile.ZipFile(archive, "w") as writer:
            writer.write(bed, "Bed.wav")
            writer.writestr("../Drive.wav", b"not safe")

        with self.assertRaisesRegex(PIPELINE.PipelineError, "unsafe path"):
            PIPELINE.inspect_archive(archive, self.stem_config())

    def test_secure_ingestion_rejects_symlinks(self):
        bed = self.root / "bed.wav"
        self.write_wav(bed)
        link = zipfile.ZipInfo("Drive.wav")
        link.create_system = 3
        link.external_attr = (stat.S_IFLNK | 0o777) << 16
        archive = self.make_archive([("Bed.wav", bed), ("Drive.wav", link)])

        with self.assertRaisesRegex(PIPELINE.PipelineError, "symlink"):
            PIPELINE.inspect_archive(archive, self.stem_config())

    def test_secure_ingestion_rejects_portable_name_collisions(self):
        bed = self.root / "bed.wav"
        drive = self.root / "drive.wav"
        self.write_wav(bed)
        self.write_wav(drive)
        archive = self.make_archive([("Bed.wav", bed), ("bed.WAV", drive)])

        with self.assertRaisesRegex(PIPELINE.PipelineError, "duplicate/colliding"):
            PIPELINE.inspect_archive(archive, self.stem_config())

    def test_secure_ingestion_rejects_missing_and_unmapped_files(self):
        bed = self.root / "bed.wav"
        self.write_wav(bed)
        archive = self.make_archive([("Bed.wav", bed)])

        with self.assertRaisesRegex(PIPELINE.PipelineError, "missing configured"):
            PIPELINE.inspect_archive(archive, self.stem_config())

    def test_alignment_rejects_different_frame_counts(self):
        bed = self.root / "bed.wav"
        drive = self.root / "drive.wav"
        self.write_wav(bed, frames=32000)
        self.write_wav(drive, frames=31999)

        with self.assertRaisesRegex(PIPELINE.PipelineError, "not sample-aligned"):
            PIPELINE.validate_wav_alignment(
                {"bed": bed, "drive": drive}, self.stem_config()
            )

    def test_adaptive_route_prefers_bounded_bridge_over_slow_direct_edge(self):
        sections = {
            "source": self.route_section(
                "source", "sparse", "hold", bar_count=4, minimum_bars=8
            ),
            "bridge": self.route_section(
                "bridge", "tension", "bridge", bar_count=1
            ),
            "target": self.route_section(
                "target", "tension", "hold", bar_count=4, minimum_bars=8
            ),
        }
        transitions = [
            self.route_edge("source", "target", timing="section-end"),
            self.route_edge("source", "bridge"),
            self.route_edge("bridge", "target", timing="section-end"),
        ]

        direct = PIPELINE.lowest_latency_adaptive_route(
            "source", "tension", sections, transitions[:1]
        )
        route = PIPELINE.lowest_latency_adaptive_route(
            "source", "tension", sections, transitions
        )

        self.assertEqual(4.0, direct["worstCaseBars"])
        self.assertEqual(2.0, route["worstCaseBars"])
        self.assertEqual(["source", "bridge", "target"], route["path"])

    def test_adaptive_route_cycles_do_not_satisfy_reachability(self):
        sections = {
            "source": self.route_section(
                "source", "sparse", "hold", bar_count=4, minimum_bars=8
            ),
            "bridge": self.route_section(
                "bridge", "sparse", "bridge", bar_count=1
            ),
            "target": self.route_section(
                "target", "tension", "hold", bar_count=4, minimum_bars=8
            ),
        }
        transitions = [
            self.route_edge("source", "bridge"),
            self.route_edge("bridge", "source", timing="section-end"),
        ]

        with self.assertRaisesRegex(PIPELINE.PipelineError, "unreachable"):
            PIPELINE.lowest_latency_adaptive_route(
                "source", "tension", sections, transitions
            )

    def test_loopable_holds_need_no_authored_same_state_rotation(self):
        config, sections, transitions = self.adaptive_validation_fixture()

        report = PIPELINE.validate_adaptive_routes(
            config, sections, transitions
        )

        self.assertEqual(20, len(report["gameplay"]))
        self.assertEqual(5, len(report["resolve"]))

    def test_stinger_cannot_satisfy_finite_same_state_continuation(self):
        config, sections, transitions = self.adaptive_validation_fixture()
        sections["bridge"] = self.route_section(
            "bridge", "tension", "bridge", bar_count=1
        )
        sections["sting"] = self.route_section(
            "sting", "tension", "stinger", bar_count=1
        )
        transitions.extend(
            [
                self.route_edge("bridge", "sting", timing="section-end"),
                self.route_edge(
                    "sting", "hold-tension", timing="section-end"
                ),
            ]
        )

        with self.assertRaisesRegex(
            PIPELINE.PipelineError,
            "bridge has no executable non-stinger same-state continuation",
        ):
            PIPELINE.validate_adaptive_routes(config, sections, transitions)

    def test_rendered_head_crossfade_preserves_grid_and_uses_continuation(self):
        source = self.root / "source.wav"
        rendered = self.root / "rendered.wav"
        self.write_wav(source, frames=40000)

        verification = PIPELINE.write_wav_section(
            source,
            rendered,
            0,
            32000,
            head_crossfade_seconds=0.0625,
            continuation_end_frame=40000,
        )

        self.assertEqual(1.0, verification["boundarySimilarity"])
        self.assertEqual(500, verification["continuationFrames"])
        with wave.open(str(rendered), "rb") as reader, wave.open(
            str(source), "rb"
        ) as original:
            self.assertEqual(32000, reader.getnframes())
            first = int.from_bytes(reader.readframes(1), "little", signed=True)
            reader.setpos(reader.getnframes() - 1)
            last = int.from_bytes(reader.readframes(1), "little", signed=True)
            original.setpos(32000)
            expected_first = int.from_bytes(
                original.readframes(1), "little", signed=True
            )
            original.setpos(31999)
            expected_last = int.from_bytes(
                original.readframes(1), "little", signed=True
            )
        self.assertEqual(expected_first, first)
        self.assertEqual(expected_last, last)

    def test_section_headroom_applies_authored_section_stem_gain(self):
        source = self.root / "source.wav"
        self.write_wav(source, frames=8000, amplitude=1000)
        config = {
            "stems": [
                {
                    "id": "bed",
                    "gainDb": 0.0,
                }
            ],
            "masterGainDb": 0.0,
            "analysis": {"targetPeakDbfs": -10.0},
        }
        internal = {
            "startFrame": 0,
            "endFrame": 8000,
            "includedStems": ["bed"],
            "config": {
                "id": "boosted",
                "stemGainsDb": {"bed": 24.0},
            },
            "output": {
                "id": "boosted",
                "loopable": False,
            },
        }

        with self.assertRaisesRegex(
            PIPELINE.PipelineError,
            "section boosted mix peak .* above target",
        ):
            PIPELINE.prepare_section_mix_headroom(
                {"bed": source},
                config,
                {"boosted": internal},
                [],
            )

    def test_equal_power_transition_overlap_must_retain_pack_headroom(self):
        source = self.root / "source.wav"
        self.write_wav(source, frames=16000, amplitude=16000)
        config = {
            "stems": [
                {
                    "id": "bed",
                    "gainDb": 0.0,
                }
            ],
            "masterGainDb": 0.0,
            "analysis": {"targetPeakDbfs": -4.0},
        }

        def section(section_id, start_frame):
            return {
                "startFrame": start_frame,
                "endFrame": start_frame + 8000,
                "includedStems": ["bed"],
                "config": {"id": section_id},
                "output": {
                    "id": section_id,
                    "loopable": False,
                },
            }

        first = section("first", 0)
        second = section("second", 8000)
        PIPELINE.prepare_section_mix_headroom(
            {"bed": source},
            config,
            {"first": first, "second": second},
            [],
        )

        with self.assertRaisesRegex(
            PIPELINE.PipelineError,
            "transition first -> second equal-power overlap .* above target",
        ):
            PIPELINE.validate_transition_mix_headroom(
                {"bed": source},
                config,
                {
                    "barFrames": 8000,
                    "sampleRate": 8000,
                },
                first,
                second,
                {
                    "from": "first",
                    "to": "second",
                    "timing": "section-end",
                    "quantizeBars": 1,
                    "crossfadeBars": 0.25,
                },
            )

    def test_mp4_timestamp_canonicalization_is_reproducible(self):
        def box(kind, payload):
            return struct.pack(">I4s", len(payload) + 8, kind) + payload

        def timestamp_box(kind, timestamp):
            return box(
                kind,
                b"\x00\x00\x00\x00"
                + struct.pack(">II", timestamp, timestamp)
                + b"\x00" * 24,
            )

        def container(timestamp):
            return box(
                b"moov",
                timestamp_box(b"mvhd", timestamp)
                + box(
                    b"trak",
                    timestamp_box(b"tkhd", timestamp)
                    + box(b"mdia", timestamp_box(b"mdhd", timestamp)),
                ),
            )

        first = self.root / "first.m4a"
        second = self.root / "second.m4a"
        first.write_bytes(container(100))
        second.write_bytes(container(200))

        PIPELINE.canonicalize_mp4_timestamps(first)
        PIPELINE.canonicalize_mp4_timestamps(second)

        self.assertEqual(first.read_bytes(), second.read_bytes())


if __name__ == "__main__":
    unittest.main()
