using System.IO;
using UnityEngine;
using YARG.Helpers;
using YARG.Menu.Persistent;
using YARG.Settings.Metadata;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static UnityEngine.Rendering.DebugUI;

namespace YARG.Menu.Settings
{
    public class PresetActions : MonoBehaviour
    {
        private PresetsTab _tab;

        public void Initialize(PresetsTab tab)
        {
            _tab = tab;
        }

        public void RenamePreset()
        {
            _tab.Rename();
        }

        public void CopyPreset()
        {
            _tab.CopyCurrentPreset();
        }

        public void DeletePreset()
        {
            _tab.DeleteCurrentPreset();
        }

        public void ImportPreset()
        {
            _tab.ImportPreset();
        }

        public void ExportPreset()
        {
            _tab.ExportPreset();
        }
    }
}