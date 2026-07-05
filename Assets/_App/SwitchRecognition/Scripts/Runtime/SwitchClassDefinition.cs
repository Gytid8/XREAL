using UnityEngine;

namespace Unity.XR.XREAL.SwitchRecognition
{
    [CreateAssetMenu(fileName = "SwitchClassDefinition", menuName = "Switch Recognition/Switch Class Definition")]
    public class SwitchClassDefinition : ScriptableObject
    {
        [System.Serializable]
        public struct SwitchClassEntry
        {
            public int classId;
            public string displayName;
            public Color labelColor;
        }

        [SerializeField] private SwitchClassEntry[] _classes =
        {
            new SwitchClassEntry { classId = 0, displayName = "断路器", labelColor = new Color(0.2f, 0.75f, 1f, 1f) },
            new SwitchClassEntry { classId = 1, displayName = "隔离开关", labelColor = new Color(0.45f, 0.95f, 0.55f, 1f) },
            new SwitchClassEntry { classId = 2, displayName = "负荷开关", labelColor = new Color(1f, 0.78f, 0.35f, 1f) }
        };

        public SwitchClassEntry[] Classes => _classes;

        public SwitchClassEntry GetClass(int classId)
        {
            if (_classes == null || _classes.Length == 0)
                return default;

            foreach (var entry in _classes)
            {
                if (entry.classId == classId)
                    return entry;
            }

            return _classes[Mathf.Clamp(classId, 0, _classes.Length - 1)];
        }
    }
}
