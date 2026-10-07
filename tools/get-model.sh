#!/usr/bin/env bash
# Downloads the small YOLOv9-t model (about 2.3 MB, 80 COCO classes, NMS-ready outputs) that Meta ships in
# oculus-samples/Unity-PassthroughCameraApiSamples, into the Unity project's Resources folder as "yolo.sentis".
# The model is not committed to this repo; check the sample repo for its license terms.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
dest="$here/../unity/IronManHUD/Assets/Resources"
mkdir -p "$dest"
base="https://raw.githubusercontent.com/oculus-samples/Unity-PassthroughCameraApiSamples/main/Assets/PassthroughCameraApiSamples/MultiObjectDetection/SentisInference/Model"
curl -fL "$base/yolov9sentis.sentis" -o "$dest/yolo.sentis"
echo "Saved $dest/yolo.sentis ($(wc -c < "$dest/yolo.sentis") bytes)"
