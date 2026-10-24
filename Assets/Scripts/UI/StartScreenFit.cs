using UnityEngine;

namespace FarmMVP
{
    [ExecuteAlways]
    public class StartScreenFit : MonoBehaviour
    {
        public Vector2 referenceSize = new Vector2(1920, 1080);
        private void OnEnable() => Fit();
        private void OnRectTransformDimensionsChange() => Fit();
        private void LateUpdate() => Fit();
        private void Fit()
        {
            if (!(transform.parent is RectTransform parent)) return;
            float scale = Mathf.Min(parent.rect.width / referenceSize.x, parent.rect.height / referenceSize.y);
            if (scale <= 0) return;
            var desired = new Vector3(scale, scale, 1);
            if (transform.localScale != desired) transform.localScale = desired;
        }
    }
}
