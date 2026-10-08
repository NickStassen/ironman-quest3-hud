#!/usr/bin/env bash
# Downloads the small YOLOv9-t model (about 2.3 MB, 80 COCO classes) that Meta ships in
# oculus-samples/Unity-PassthroughCameraApiSamples, into the Unity project's Resources folder as "yolo.sentis".
# Outputs: boxes [N,4] x1,y1,x2,y2 in input pixels, class ids [N], scores [N]. NMS is not in the model; it runs on the CPU.
# Pinned to a commit because older versions of this file had a different (2-output) layout.
# The model is not committed to this repo; check the sample repo for its license terms.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
dest="$here/../unity/IronManHUD/Assets/Resources"
mkdir -p "$dest"
commit="9792e1e162c22a5e14a60b92fa3fce2258803251"
sha256="cc25e14d60a90efeddaeedcc5a666c342345e722b281a3f6e73c692adb796b21"
base="https://raw.githubusercontent.com/oculus-samples/Unity-PassthroughCameraApiSamples/$commit/Assets/PassthroughCameraApiSamples/MultiObjectDetection/SentisInference/Model"
curl -fL "$base/yolov9sentis.sentis" -o "$dest/yolo.sentis"
echo "$sha256  $dest/yolo.sentis" | sha256sum -c -
echo "Saved $dest/yolo.sentis ($(wc -c < "$dest/yolo.sentis") bytes)"
