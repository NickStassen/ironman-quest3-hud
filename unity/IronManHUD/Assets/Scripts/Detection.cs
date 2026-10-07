using UnityEngine;

namespace IronManHud
{
    /// <summary>One detection in model-input pixel space (x1, y1 = top-left; x2, y2 = bottom-right; y grows downward).</summary>
    public struct Detection
    {
        public int ClassId;
        public float Score;
        public float X1, Y1, X2, Y2;

        public float Width => X2 - X1;
        public float Height => Y2 - Y1;
        public float Area => Mathf.Max(0f, Width) * Mathf.Max(0f, Height);

        public static float IoU(in Detection a, in Detection b)
        {
            float x1 = Mathf.Max(a.X1, b.X1);
            float y1 = Mathf.Max(a.Y1, b.Y1);
            float x2 = Mathf.Min(a.X2, b.X2);
            float y2 = Mathf.Min(a.Y2, b.Y2);
            float inter = Mathf.Max(0f, x2 - x1) * Mathf.Max(0f, y2 - y1);
            float union = a.Area + b.Area - inter;
            return union <= 0f ? 0f : inter / union;
        }
    }

    /// <summary>The 80 COCO class names, used when no labels file is assigned.</summary>
    public static class CocoLabels
    {
        public static readonly string[] Names =
        {
            "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat", "traffic light",
            "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat", "dog", "horse", "sheep", "cow",
            "elephant", "bear", "zebra", "giraffe", "backpack", "umbrella", "handbag", "tie", "suitcase", "frisbee",
            "skis", "snowboard", "sports ball", "kite", "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket", "bottle",
            "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple", "sandwich", "orange",
            "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair", "couch", "potted plant", "bed",
            "dining table", "toilet", "tv", "laptop", "mouse", "remote", "keyboard", "cell phone", "microwave", "oven",
            "toaster", "sink", "refrigerator", "book", "clock", "vase", "scissors", "teddy bear", "hair drier", "toothbrush"
        };

        public static string[] Parse(TextAsset labels)
        {
            if (labels == null || string.IsNullOrWhiteSpace(labels.text))
            {
                return Names;
            }
            var lines = labels.text.Replace("\r", "").Split('\n');
            var result = new System.Collections.Generic.List<string>(lines.Length);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0)
                {
                    result.Add(trimmed);
                }
            }
            return result.Count > 0 ? result.ToArray() : Names;
        }

        public static string Get(string[] labels, int classId)
        {
            if (labels != null && classId >= 0 && classId < labels.Length)
            {
                return labels[classId];
            }
            return "class " + classId;
        }
    }
}
