using UnityEngine;
using YARG.Core.Game;

namespace YARG.Gameplay.Visuals
{
    public class IndicatorStripes : MonoBehaviour
    {
        [SerializeField]
        private GameObject _stripPrefab;
        [SerializeField]
        private float _spacing = 0.3f;

        [Space]
        [SerializeField]
        private Transform _leftContainer;
        [SerializeField]
        private Transform _rightContainer;

        private int _stripeCount;
        private bool _isCustomPreset;

        public void Initialize(in PresetContainer<EnginePreset> enginePreset)
        {
            _isCustomPreset = false;

            if (enginePreset.Id == EnginePreset.Casual.Id)
            {
                SpawnStripe(new Color(0.9f, 0.3f, 0.9f));
            }
            else if (enginePreset.Id == EnginePreset.Precision.Id)
            {
                SpawnStripe(new Color(1.0f, 0.9f, 0.0f));
            }
            else if (enginePreset.Id != EnginePreset.Default.Id)
            {
                // Otherwise, it must be a custom preset
                SpawnStripe(new Color(1.0f, 0.25f, 0.25f));
                _isCustomPreset = true;
            }
        }

        public void Initialize(EnginePreset.FiveFretGuitarPreset guitarPreset)
        {
            if (!_isCustomPreset) return;

            if (!guitarPreset.AntiGhosting)
            {
                SpawnStripe(new Color(1f, 0.5f, 0f));
            }

            if (guitarPreset.InfiniteFrontEnd)
            {
                SpawnStripe(new Color(0.3f, 0.75f, 0.3f));
            }
        }

        private void SpawnStripe(Color c)
        {
            SpawnStripe(_leftContainer, c);
            SpawnStripe(_rightContainer, c);

            _stripeCount++;
        }

        private void SpawnStripe(Transform container, Color c)
        {
            var stripe = Instantiate(_stripPrefab, container);
            stripe.transform.localPosition = Vector3.zero.AddZ(-_spacing * _stripeCount);

            foreach (var meshRenderer in stripe.GetComponentsInChildren<MeshRenderer>())
            {
                foreach (var material in meshRenderer.materials)
                {
                    material.color = c;
                }
            }
        }
    }
}