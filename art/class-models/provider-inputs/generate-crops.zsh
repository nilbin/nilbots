#!/bin/zsh

set -euo pipefail

script_dir=${0:A:h}
repo_root=$(git -C "$script_dir" rev-parse --show-toplevel)
cd "$repo_root"

crop() {
  local source=$1
  local output=$2
  local height=$3
  local width=$4
  local offset_y=$5
  local offset_x=$6

  mkdir -p "${output:h}"
  sips \
    --cropToHeightWidth "$height" "$width" \
    --cropOffset "$offset_y" "$offset_x" \
    "$source" \
    --out "$output" >/dev/null
}

# Aegis Tortoise mobile: V1 is retained for leg/depth reference; V2 is the
# selected material/geometry review source.
for version in v1 v2; do
  source="art/class-models/concept-targets/aegis-tortoise-3d-concept-${version}/approval-sheet.png"
  output_root="art/class-models/provider-inputs/meshy/aegis-tortoise-mobile/reference-${version}"
  [[ "$version" == v2 ]] && output_root="art/class-models/provider-inputs/meshy/aegis-tortoise-mobile/selected-v2"

  crop "$source" "$output_root/01-top.png" 610 700 20 60
  crop "$source" "$output_root/02-side.png" 320 700 650 35
  crop "$source" "$output_root/03-front-three-quarter.png" 510 740 90 760
  crop "$source" "$output_root/04-rear-three-quarter.png" 375 710 610 775
done

# Lattice Loom: V1 is the retained grounded alternative, V2 is the selected
# low-hover geometry, and V3 is an accent-placement review reference only.
for version in v1 v2 v3; do
  source="art/class-models/concept-targets/lattice-loom-3d-concept-${version}/approval-sheet.png"
  output_root="art/class-models/provider-inputs/meshy/lattice-loom/reference-${version}"
  [[ "$version" == v2 ]] && output_root="art/class-models/provider-inputs/meshy/lattice-loom/selected-v2"
  [[ "$version" == v3 ]] && output_root="art/class-models/provider-inputs/meshy/lattice-loom/accent-review-v3"

  crop "$source" "$output_root/01-top.png" 660 745 20 35
  crop "$source" "$output_root/02-side.png" 260 770 690 30
  crop "$source" "$output_root/03-front-three-quarter.png" 545 756 95 780
  crop "$source" "$output_root/04-rear-three-quarter.png" 310 680 650 815
done

# Distinct Bulwark forms. The gameplay-camera panel is the nearest available
# front three-quarter view on each approved board.
source="art/class-models/concept-targets/aegis-tortoise-shell-3d-concept-v1/approval-sheet.png"
output_root="art/class-models/provider-inputs/meshy/aegis-tortoise-shell/candidate-v1"
crop "$source" "$output_root/01-top.png" 500 580 50 120
crop "$source" "$output_root/02-side.png" 280 650 670 70
crop "$source" "$output_root/03-front-three-quarter.png" 500 690 50 790
crop "$source" "$output_root/04-rear-three-quarter.png" 380 700 600 780

source="art/class-models/concept-targets/aegis-tortoise-turret-3d-concept-v1/approval-sheet.png"
output_root="art/class-models/provider-inputs/meshy/aegis-tortoise-turret/candidate-v1"
crop "$source" "$output_root/01-top.png" 650 680 15 50
crop "$source" "$output_root/02-side.png" 260 710 700 40
crop "$source" "$output_root/03-front-three-quarter.png" 420 690 90 820
crop "$source" "$output_root/04-rear-three-quarter.png" 360 700 620 790

# Projectile heads remain three independent future tasks. These boards are
# 1254×1254 rather than the 1536×1024 chassis/form boards.
source="art/class-models/concept-targets/projectiles/trident-spark-3d-concept-v1/approval-sheet.png"
output_root="art/class-models/provider-inputs/meshy/trident-spark/candidate-v1"
crop "$source" "$output_root/01-top.png" 550 1050 75 100
crop "$source" "$output_root/02-side.png" 220 1050 630 100
crop "$source" "$output_root/03-front-three-quarter.png" 360 620 845 20
crop "$source" "$output_root/04-rear-three-quarter.png" 350 620 865 620

source="art/class-models/concept-targets/projectiles/rebound-diamond-3d-concept-v1/approval-sheet.png"
output_root="art/class-models/provider-inputs/meshy/rebound-diamond/candidate-v1"
crop "$source" "$output_root/01-top.png" 550 900 100 40
crop "$source" "$output_root/02-end-on-not-side.png" 550 250 100 950
crop "$source" "$output_root/03-front-three-quarter.png" 500 600 675 40
crop "$source" "$output_root/04-rear-three-quarter.png" 470 620 700 610

source="art/class-models/concept-targets/projectiles/lattice-rivet-3d-concept-v1/approval-sheet.png"
output_root="art/class-models/provider-inputs/meshy/lattice-rivet/candidate-v1"
crop "$source" "$output_root/01-top.png" 470 850 90 80
crop "$source" "$output_root/02-side.png" 230 760 560 450
crop "$source" "$output_root/03-front-three-quarter.png" 350 570 820 45
crop "$source" "$output_root/04-rear-three-quarter.png" 350 600 830 630
