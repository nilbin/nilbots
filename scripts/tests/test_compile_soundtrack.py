import copy
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

    def normalization_config(self):
        return {
            "schemaVersion": 1,
            "id": "test-score",
            "title": "Test Score",
            "default": False,
            "provenance": {
                "sourceTool": "Test",
                "rightsStatus": "user-supplied-unverified",
                "shipApproval": "pending",
            },
            "sourceArchive": "stems.zip",
            "bpm": 120,
            "beatsPerBar": 4,
            "segmentBars": 4,
            "gridOriginFrame": 0,
            "barFrames": 8000,
            "sourceEndFrame": 64000,
            "masterGainDb": -3,
            "stems": self.stem_config(),
        }

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

    def test_optional_retrospective_cue_and_adaptive_seam_are_normalized(self):
        raw = self.normalization_config()
        raw["retrospectiveCue"] = {
            "id": "final-runway",
            "startBar": 2,
            "barCount": 4,
            "anchorBar": 2,
            "stems": ["bed", "drive"],
        }
        raw["adaptiveSeam"] = {
            "strategy": "staged",
            "retreatBars": 1,
            "overlapBars": 0.25,
            "riseBars": 1,
            "curve": "linear",
        }

        normalized = PIPELINE.normalize_config(
            raw, self.root / "soundtrack.json"
        )

        self.assertEqual(raw["retrospectiveCue"], normalized["retrospectiveCue"])
        self.assertEqual(
            {
                "strategy": "staged",
                "retreatBars": 1.0,
                "overlapBars": 0.25,
                "riseBars": 1.0,
                "curve": "linear",
            },
            normalized["adaptiveSeam"],
        )

    def test_retrospective_cue_rejects_unknown_fields_ranges_and_stems(self):
        cases = [
            (
                "unknown field",
                {"extra": True},
                "contains unknown field",
            ),
            (
                "range past source",
                {"startBar": 6, "barCount": 3},
                "range ends at bar 9",
            ),
            (
                "anchor outside cue",
                {"anchorBar": 4},
                "anchorBar must be at most 3",
            ),
            (
                "unknown stem",
                {"stems": ["bed", "missing"]},
                "names unknown stem",
            ),
            (
                "duplicate stem",
                {"stems": ["bed", "bed"]},
                "contains duplicate stem",
            ),
        ]
        base_cue = {
            "id": "final-runway",
            "startBar": 2,
            "barCount": 4,
            "anchorBar": 2,
            "stems": ["bed", "drive"],
        }
        for label, changes, message in cases:
            with self.subTest(label):
                raw = self.normalization_config()
                raw["retrospectiveCue"] = {
                    **copy.deepcopy(base_cue),
                    **changes,
                }
                with self.assertRaisesRegex(PIPELINE.PipelineError, message):
                    PIPELINE.normalize_config(
                        raw, self.root / "soundtrack.json"
                    )

    def test_optional_straight_through_cue_is_normalized(self):
        raw = self.normalization_config()
        raw["straightThroughCue"] = {
            "id": "opening-passage",
            "startBar": 0,
            "barCount": 6,
            "stems": ["bed", "drive"],
        }

        normalized = PIPELINE.normalize_config(
            raw, self.root / "soundtrack.json"
        )

        self.assertEqual(
            raw["straightThroughCue"],
            normalized["straightThroughCue"],
        )

    def test_straight_through_cue_rejects_unknown_fields_ranges_and_stems(self):
        cases = [
            (
                "unknown field",
                {"anchorBar": 2},
                "contains unknown field",
            ),
            (
                "range past source",
                {"startBar": 6, "barCount": 3},
                "range ends at bar 9",
            ),
            (
                "unknown stem",
                {"stems": ["bed", "missing"]},
                "names unknown stem",
            ),
            (
                "duplicate stem",
                {"stems": ["drive", "drive"]},
                "contains duplicate stem",
            ),
        ]
        base_cue = {
            "id": "opening-passage",
            "startBar": 0,
            "barCount": 6,
            "stems": ["bed", "drive"],
        }
        for label, changes, message in cases:
            with self.subTest(label):
                raw = self.normalization_config()
                raw["straightThroughCue"] = {
                    **copy.deepcopy(base_cue),
                    **changes,
                }
                with self.assertRaisesRegex(PIPELINE.PipelineError, message):
                    PIPELINE.normalize_config(
                        raw, self.root / "soundtrack.json"
                    )

    def test_adaptive_seam_rejects_unknown_policy_and_invalid_ranges(self):
        cases = [
            ("unknown field", {"extra": True}, "contains unknown field"),
            ("strategy", {"strategy": "crossfade"}, "strategy must be staged"),
            ("curve", {"curve": "equal-power"}, "curve must be linear"),
            (
                "overlap exceeds retreat",
                {"retreatBars": 0.25, "overlapBars": 0.5},
                "must not exceed retreatBars",
            ),
            (
                "nonpositive rise",
                {"riseBars": 0},
                "riseBars must be at least 0.25",
            ),
        ]
        base_seam = {
            "strategy": "staged",
            "retreatBars": 1,
            "overlapBars": 0.25,
            "riseBars": 1,
            "curve": "linear",
        }
        for label, changes, message in cases:
            with self.subTest(label):
                raw = self.normalization_config()
                raw["adaptiveSeam"] = {
                    **copy.deepcopy(base_seam),
                    **changes,
                }
                with self.assertRaisesRegex(PIPELINE.PipelineError, message):
                    PIPELINE.normalize_config(
                        raw, self.root / "soundtrack.json"
                    )

    def test_retrospective_cue_descriptor_and_optional_manifest_metadata(self):
        config = {
            "retrospectiveCue": {
                "id": "final-runway",
                "startBar": 2,
                "barCount": 4,
                "anchorBar": 2,
                "stems": ["bed", "drive"],
            },
            "adaptiveSeam": {
                "strategy": "staged",
                "retreatBars": 1.0,
                "overlapBars": 0.25,
                "riseBars": 1.0,
                "curve": "linear",
            },
        }
        analysis = {
            "gridOriginFrame": 100,
            "barFrames": 8000,
            "sampleRate": 8000,
            "trimStartFrame": 100,
            "trimEndFrame": 64100,
        }

        cue, internal = PIPELINE.prepare_retrospective_cue(config, analysis)
        metadata = PIPELINE.public_optional_adaptive_metadata(config, cue)

        self.assertEqual(
            {
                "id": "final-runway",
                "startBar": 2,
                "barCount": 4,
                "anchorBar": 2,
                "durationSeconds": 4.0,
                "files": {},
            },
            cue,
        )
        self.assertEqual(16100, internal["startFrame"])
        self.assertEqual(48100, internal["endFrame"])
        self.assertEqual(["bed", "drive"], internal["stems"])
        self.assertIs(metadata["retrospectiveCue"], cue)
        self.assertEqual(config["adaptiveSeam"], metadata["adaptiveSeam"])

    def test_straight_through_cue_descriptor_and_manifest_metadata(self):
        config = {
            "straightThroughCue": {
                "id": "opening-passage",
                "startBar": 0,
                "barCount": 6,
                "stems": ["bed", "drive"],
            },
            "adaptiveSeam": None,
        }
        analysis = {
            "gridOriginFrame": 100,
            "barFrames": 8000,
            "sampleRate": 8000,
            "trimStartFrame": 100,
            "trimEndFrame": 64100,
        }

        cue, internal = PIPELINE.prepare_straight_through_cue(
            config, analysis
        )
        metadata = PIPELINE.public_optional_adaptive_metadata(
            config, None, cue
        )

        self.assertEqual(
            {
                "id": "opening-passage",
                "startBar": 0,
                "barCount": 6,
                "durationSeconds": 6.0,
                "file": "",
            },
            cue,
        )
        self.assertEqual(100, internal["startFrame"])
        self.assertEqual(48100, internal["endFrame"])
        self.assertEqual(["bed", "drive"], internal["stems"])
        self.assertIs(metadata["straightThroughCue"], cue)
        self.assertNotIn("retrospectiveCue", metadata)

    def test_straight_through_mix_bakes_stem_gains_but_not_master_gain(self):
        bed = self.root / "bed.wav"
        drive = self.root / "drive.wav"
        mixed = self.root / "mix.wav"
        self.write_wav(bed, frames=8000, amplitude=1000)
        self.write_wav(drive, frames=8000, amplitude=1000)
        stems = self.stem_config()
        stems[0]["gainDb"] = -6.0
        master_gain_db = -12.0

        metrics = PIPELINE.render_straight_through_mix(
            {"bed": bed, "drive": drive},
            stems,
            ["bed", "drive"],
            0,
            8000,
            master_gain_db,
            destination=mixed,
        )

        with wave.open(str(bed), "rb") as source, wave.open(
            str(mixed), "rb"
        ) as rendered:
            source.setpos(1)
            sample = int.from_bytes(
                source.readframes(1), "little", signed=True
            )
            rendered.setpos(1)
            actual = int.from_bytes(
                rendered.readframes(1), "little", signed=True
            )
        expected = int(round(sample * (10.0 ** (-6.0 / 20.0)) + sample))
        self.assertEqual(expected, actual)
        self.assertAlmostEqual(
            master_gain_db,
            metrics["postMasterPeakDbfs"] - metrics["rawPeakDbfs"],
            places=3,
        )

    def test_straight_through_mix_rejects_pcm_clipping(self):
        bed = self.root / "bed.wav"
        drive = self.root / "drive.wav"
        self.write_wav(bed, frames=8000, amplitude=20000)
        self.write_wav(drive, frames=8000, amplitude=20000)

        with self.assertRaisesRegex(
            PIPELINE.PipelineError,
            "mix clips before the runtime master gain",
        ):
            PIPELINE.render_straight_through_mix(
                {"bed": bed, "drive": drive},
                self.stem_config(),
                ["bed", "drive"],
                0,
                8000,
                -3.0,
            )

    def test_straight_through_mix_rejects_post_master_headroom(self):
        bed = self.root / "bed.wav"
        self.write_wav(bed, frames=8000, amplitude=8000)
        config = {
            "stems": self.stem_config(),
            "masterGainDb": 6.0,
            "analysis": {"targetPeakDbfs": -9.0},
        }

        with self.assertRaisesRegex(
            PIPELINE.PipelineError,
            "after the runtime pack master",
        ):
            PIPELINE.validate_straight_through_cue_mix(
                {"bed": bed},
                config,
                {"id": "opening-passage"},
                {
                    "startFrame": 0,
                    "endFrame": 8000,
                    "stems": ["bed"],
                },
            )

    def test_raw_retrospective_cue_extraction_preserves_source_samples(self):
        source = self.root / "source.wav"
        rendered = self.root / "continuous.wav"
        self.write_wav(source, frames=40000)
        start_frame = 123
        end_frame = 32123

        verification = PIPELINE.write_wav_section(
            source,
            rendered,
            start_frame,
            end_frame,
        )

        self.assertEqual(0.0, verification["boundarySimilarity"])
        self.assertEqual(0, verification["continuationFrames"])
        with wave.open(str(source), "rb") as original, wave.open(
            str(rendered), "rb"
        ) as cue:
            original.setpos(start_frame)
            expected_first = original.readframes(1)
            original.setpos(end_frame - 1)
            expected_last = original.readframes(1)
            actual_first = cue.readframes(1)
            cue.setpos(cue.getnframes() - 1)
            actual_last = cue.readframes(1)
            self.assertEqual(end_frame - start_frame, cue.getnframes())
        self.assertEqual(expected_first, actual_first)
        self.assertEqual(expected_last, actual_last)

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
